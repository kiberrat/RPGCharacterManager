using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Ai;
using RPGCharacterManager.Ai.Tools;
using RPGCharacterManager.Content;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Settings;
using RPGCharacterManager.Tests.Characters;
using RPGCharacterManager.Tests.Dice;

namespace RPGCharacterManager.Tests.Ai;

/// <summary>
/// Проверка помощника: переписка с вызовами инструментов, накопление предложений
/// и применение подтверждённых изменений к базе данных.
/// </summary>
public sealed class AiAssistantTests
{
    private static async Task<AiAnswer> AskAsync(AiTestContext context, AiConversation conversation, string question)
    {
        var result = await context.Assistant.AskAsync(conversation, question);
        Assert.True(result.IsSuccess, result.Error);

        return result.Value;
    }

    // ---------- Переписка ----------

    [Fact]
    public async Task Вопрос_БезИнструментов_ВозвращаетОтвет()
    {
        await using var context = await AiTestContext.CreateAsync(
            AiReplies.Text("В этой базе пока ничего нет."));

        var conversation = new AiConversation();
        var answer = await AskAsync(context, conversation, "Что у меня есть?");

        Assert.Equal("В этой базе пока ничего нет.", answer.Text);
        Assert.Empty(answer.Proposals);

        // Первым сообщением запроса всегда идут указания модели: без них она
        // ничего не знает ни о приложении, ни о видах объектов.
        var request = Assert.Single(context.Client.Requests);

        Assert.Equal(AiRole.System, request.Messages[0].Role);
        Assert.Contains("не знает заранее ни одной игры", request.Messages[0].Text, StringComparison.Ordinal);
        Assert.Contains(ContentTypeIds.Spells, request.Messages[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Вопрос_СВызовомИнструмента_ВыполняетЕгоИПродолжает()
    {
        await using var context = await AiTestContext.CreateAsync(
            AiReplies.Call(DescribeTypeTool.ToolName, """{"type":"spells"}"""),
            AiReplies.Text("У заклинания есть уровень, школа и формула."));

        var conversation = new AiConversation();
        var answer = await AskAsync(context, conversation, "Какие поля у заклинания?");

        Assert.Equal("У заклинания есть уровень, школа и формула.", answer.Text);
        Assert.Contains(answer.Steps, step => step.StartsWith("Поля вида", StringComparison.Ordinal));

        // Во втором обращении модель получает результат своего же вызова.
        Assert.Equal(2, context.Client.Requests.Count);
        Assert.Contains(context.Client.Requests[1].Messages, message => message.Role == AiRole.Tool);
    }

    [Fact]
    public async Task Вопрос_НеизвестныйИнструмент_СообщаетМоделиОДоступных()
    {
        await using var context = await AiTestContext.CreateAsync(
            AiReplies.Call("удалить_всё", "{}"),
            AiReplies.Text("Такого я не умею."));

        var conversation = new AiConversation();

        await AskAsync(context, conversation, "Удали всё");

        var tool = context.Client.Requests[1].Messages.Last(message => message.Role == AiRole.Tool);

        Assert.Contains("не существует", tool.Text, StringComparison.Ordinal);
        Assert.Contains(CreateObjectTool.ToolName, tool.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Вопрос_МодельЗамолчалаПослеПодготовки_ПолучаетНапоминаниеИДоводитДоКонца()
    {
        // Найдено в приложении: модель прочитала поля вида, проверила формулу
        // и вернула пустой ответ, так и не создав объект. Напоминание доводит
        // начатое до предложения, вместо ответа, в котором ничего не сделано.
        await using var context = await AiTestContext.CreateAsync(
            AiReplies.Call(DescribeTypeTool.ToolName, """{"type":"spells"}"""),
            AiReplies.Text(string.Empty),
            AiReplies.Call(
                CreateObjectTool.ToolName,
                """{"type":"spells","values":{"name":"Ледяная стрела","level":"1"}}"""),
            AiReplies.Text("Предложил создать заклинание."));

        var conversation = new AiConversation();
        var answer = await AskAsync(context, conversation, "Создай заклинание «Ледяная стрела»");

        var proposal = Assert.Single(answer.Proposals);

        Assert.Equal("Ледяная стрела", proposal.Title);
        Assert.Equal("Предложил создать заклинание.", answer.Text);

        Assert.Contains(
            context.Client.Requests[2].Messages,
            message => message.Role == AiRole.User && message.Text == AiPrompt.Reminder);
    }

    [Fact]
    public async Task Вопрос_МодельМолчитИПослеНапоминания_ОтветВозвращаетсяКакЕсть()
    {
        // Напоминание делается один раз: бесконечно уговаривать модель незачем.
        await using var context = await AiTestContext.CreateAsync(
            AiReplies.Text(string.Empty),
            AiReplies.Text(string.Empty));

        var answer = await AskAsync(context, new AiConversation(), "Создай заклинание");

        Assert.Empty(answer.Text);
        Assert.Empty(answer.Proposals);
        Assert.Equal(2, context.Client.Requests.Count);
    }

    [Fact]
    public async Task Вопрос_БезКлюча_СообщаетГдеЕгоЗадать()
    {
        await using var context = await AiTestContext.CreateAsync();
        context.Client.IsConfigured = false;

        var result = await context.Assistant.AskAsync(new AiConversation(), "Привет");

        Assert.True(result.IsFailure);
        Assert.Contains("Настройки", result.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Вопрос_ВыбранПерсонаж_ПопадаетВУказания()
    {
        await using var context = await AiTestContext.CreateAsync(AiReplies.Text("Понял."));

        var conversation = new AiConversation
        {
            Scope = new AiScope(Guid.NewGuid(), "Аргус"),
        };

        await AskAsync(context, conversation, "Что мне взять?");

        var instructions = context.Client.Requests[0].Messages[0].Text;

        Assert.Contains("Аргус", instructions, StringComparison.Ordinal);
        Assert.Contains(ReadCharacterTool.ToolName, instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Вопрос_СтильТехнический_ПопадаетВУказания()
    {
        await using var characters = await CharacterTestContext.CreateAsync();

        var content = new ContentService(
            StandardContentTypes.Create(),
            characters.ContextFactory,
            NullLogger<ContentService>.Instance);

        var settings = new FixedSettingsService(new AppSettings { AiStyle = AiStyle.Technical });
        var client = new ScriptedAiClient(AiReplies.Text("Готово."));

        var assistant = new AiAssistant(
            client,
            content,
            settings,
            [new ListTypesTool(content)],
            NullLogger<AiAssistant>.Instance);

        var result = await assistant.AskAsync(new AiConversation(), "Опиши базу");
        Assert.True(result.IsSuccess, result.Error);

        Assert.Contains("технически", client.Requests[0].Messages[0].Text, StringComparison.Ordinal);
    }

    // ---------- Предложения ----------

    [Fact]
    public async Task Предложение_Создать_ПопадаетВБеседуИНеПрименяетсяСамо()
    {
        await using var context = await AiTestContext.CreateAsync(
            AiReplies.Call(
                CreateObjectTool.ToolName,
                """{"type":"spells","values":{"name":"Ледяная стрела","level":"2"}}"""),
            AiReplies.Text("Предложил создать заклинание."));

        var conversation = new AiConversation();
        var answer = await AskAsync(context, conversation, "Создай заклинание «Ледяная стрела» второго уровня");

        var proposal = Assert.Single(answer.Proposals);

        Assert.Equal(AiProposalState.Pending, proposal.State);
        Assert.Same(proposal, Assert.Single(conversation.Proposals));

        var page = await context.Content.SearchAsync(ContentTypeIds.Spells, null, 0, 10);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Применение_Предложения_СоздаётОбъектВБазе()
    {
        await using var context = await AiTestContext.CreateAsync(
            AiReplies.Call(
                CreateObjectTool.ToolName,
                """{"type":"spells","values":{"name":"Ледяная стрела","level":"2","formula":"2d6"}}"""),
            AiReplies.Text("Готово."));

        var conversation = new AiConversation();
        var answer = await AskAsync(context, conversation, "Создай заклинание");
        var proposal = Assert.Single(answer.Proposals);

        var applied = await context.Assistant.ApplyAsync(proposal);
        Assert.True(applied.IsSuccess, applied.Error);

        Assert.Equal(AiProposalState.Applied, proposal.State);

        var page = await context.Content.SearchAsync(ContentTypeIds.Spells, null, 0, 10);
        var item = Assert.Single(page.Items);

        Assert.Equal("Ледяная стрела", item.Name);

        var stored = await context.Content.GetAsync(ContentTypeIds.Spells, item.Id);
        var spell = Assert.IsType<Core.Models.Entities.Spell>(stored);

        Assert.Equal(2, spell.Level);
        Assert.Equal("2d6", spell.Formula);

        // Внутреннее имя заполняется службой контента: формулы и правила
        // обращаются к объекту именно по нему.
        Assert.False(string.IsNullOrWhiteSpace(spell.SystemName));
    }

    [Fact]
    public async Task Применение_Дважды_НеПовторяетЗапись()
    {
        await using var context = await AiTestContext.CreateAsync(
            AiReplies.Call(CreateObjectTool.ToolName, """{"type":"spells","values":{"name":"Молния"}}"""),
            AiReplies.Text("Готово."));

        var answer = await AskAsync(context, new AiConversation(), "Создай заклинание");
        var proposal = Assert.Single(answer.Proposals);

        Assert.True((await context.Assistant.ApplyAsync(proposal)).IsSuccess);

        var again = await context.Assistant.ApplyAsync(proposal);

        Assert.True(again.IsFailure);

        var page = await context.Content.SearchAsync(ContentTypeIds.Spells, null, 0, 10);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task Применение_ПредложенияИзменить_МеняетПоле()
    {
        await using var context = await AiTestContext.CreateAsync();

        var spell = CharacterContent.Spell("Огненный шар", "ognennyi_shar", level: 3);
        await context.Characters.AddAsync(spell);

        var result = await context.InvokeAsync(
            UpdateObjectTool.ToolName,
            $$$"""{"type":"spells","id":"{{{spell.Id}}}","values":{"level":"5","school":"Воплощение"}}""");

        var proposal = Assert.Single(result.Proposals);
        var applied = await context.Assistant.ApplyAsync(proposal);

        Assert.True(applied.IsSuccess, applied.Error);

        var stored = await context.Content.GetAsync(ContentTypeIds.Spells, spell.Id);
        var updated = Assert.IsType<Core.Models.Entities.Spell>(stored);

        Assert.Equal(5, updated.Level);
        Assert.Equal("Воплощение", updated.School);
    }

    [Fact]
    public async Task Применение_ПредложенияДополнить_ДобавляетЗаписьСписка()
    {
        await using var context = await AiTestContext.CreateAsync();
        var effect = CharacterContent.Effect("Благословение", "blagoslovenie");

        await context.Characters.AddAsync(effect);

        var result = await context.InvokeAsync(
            AddListItemTool.ToolName,
            $$$"""{"type":"effects","id":"{{{effect.Id}}}","list":"bonuses","values":{"formula":"2","name":"Атака"}}""");

        var proposal = Assert.Single(result.Proposals);
        var applied = await context.Assistant.ApplyAsync(proposal);

        Assert.True(applied.IsSuccess, applied.Error);

        var stored = await context.Content.GetAsync(ContentTypeIds.Effects, effect.Id);
        var updated = Assert.IsType<Core.Models.Entities.Effect>(stored);
        var bonus = Assert.Single(updated.Bonuses);

        Assert.Equal("2", bonus.Formula);
        Assert.Equal("Атака", bonus.Name);
    }

    [Fact]
    public async Task Применение_КопииСистемногоОбъекта_НеТрогаетОригинал()
    {
        await using var context = await AiTestContext.CreateAsync();
        var spell = CharacterContent.Spell("Огненный шар", "ognennyi_shar", level: 3);

        spell.IsSystem = true;
        await context.Characters.AddAsync(spell);

        var result = await context.InvokeAsync(
            CopyObjectTool.ToolName,
            $$$"""{"type":"spells","id":"{{{spell.Id}}}","values":{"level":"5"}}""");

        var proposal = Assert.Single(result.Proposals);
        var applied = await context.Assistant.ApplyAsync(proposal);

        Assert.True(applied.IsSuccess, applied.Error);

        var page = await context.Content.SearchAsync(ContentTypeIds.Spells, null, 0, 10);
        Assert.Equal(2, page.Items.Count);

        var original = Assert.IsType<Core.Models.Entities.Spell>(
            await context.Content.GetAsync(ContentTypeIds.Spells, spell.Id));

        Assert.Equal(3, original.Level);
        Assert.True(original.IsSystem);

        var copyItem = Assert.Single(page.Items, item => item.Id != spell.Id);
        var copy = Assert.IsType<Core.Models.Entities.Spell>(
            await context.Content.GetAsync(ContentTypeIds.Spells, copyItem.Id));

        Assert.Equal(5, copy.Level);
        Assert.False(copy.IsSystem);
    }

    [Fact]
    public async Task Применение_ИсчезнувшегоОбъекта_СообщаетОбОшибке()
    {
        await using var context = await AiTestContext.CreateAsync();

        var proposal = new AiProposal(
            AiProposalKind.Update,
            ContentTypeIds.Spells,
            "Заклинание",
            "Пропавшее",
            [],
            new Dictionary<string, string?> { ["level"] = "2" },
            Guid.NewGuid());

        var applied = await context.Assistant.ApplyAsync(proposal);

        Assert.True(applied.IsFailure);
        Assert.Equal(AiProposalState.Failed, proposal.State);
        Assert.False(string.IsNullOrWhiteSpace(proposal.Error));
    }

    // ---------- Разбор источника ----------

    [Fact]
    public async Task Разбор_Источника_ПредлагаетСоздатьНайденное()
    {
        await using var context = await AiTestContext.CreateAsync(
            AiReplies.Call(
                CreateObjectTool.ToolName,
                """{"type":"weapons","values":{"name":"Моно-катана","damage":"2d6"}}"""),
            AiReplies.Text("Нашёл одно оружие."));

        var conversation = new AiConversation();
        var source = new AiSource("Правила киберпанка", "Моно-катана: лёгкий клинок, урон 2d6.");

        var result = await context.Assistant.AnalyzeAsync(conversation, source);
        Assert.True(result.IsSuccess, result.Error);

        var proposal = Assert.Single(result.Value.Proposals);

        Assert.Equal("Моно-катана", proposal.Title);
        Assert.Same(proposal, Assert.Single(conversation.Proposals));
        Assert.Contains("Разобрано частей: 1", result.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Разбор_ДлинногоИсточника_ИдётПоЧастям()
    {
        await using var context = await AiTestContext.CreateAsync(
            AiReplies.Text("В части объектов нет."),
            AiReplies.Text("И здесь тоже нет."),
            AiReplies.Text("И тут пусто."));

        var text = string.Concat(Enumerable.Repeat("Пустой текст без игровых объектов. ", 1200));
        var steps = new List<int>();
        var progress = new Progress<AiProgress>(step => steps.Add(step.Step));

        var result = await context.Assistant.AnalyzeAsync(
            new AiConversation(),
            new AiSource("Толстая книга", text),
            progress);

        Assert.True(result.IsSuccess, result.Error);

        // Каждая часть — отдельное обращение с собственными указаниями: иначе
        // к концу книги запрос перестал бы помещаться в модель.
        Assert.True(context.Client.Requests.Count > 1);
        Assert.All(context.Client.Requests, request => Assert.Equal(2, request.Messages.Count));
        Assert.Contains("Разобрано частей", result.Value.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Разбор_ПустогоИсточника_СообщаетОбОшибке()
    {
        await using var context = await AiTestContext.CreateAsync();

        var result = await context.Assistant.AnalyzeAsync(
            new AiConversation(),
            new AiSource("Пустая книга", "   "));

        Assert.True(result.IsFailure);
    }

    // ---------- Память беседы ----------

    [Fact]
    public void Беседа_ДлиннаяПереписка_ОбрезаетсяДоПредела()
    {
        var conversation = new AiConversation();

        for (var index = 0; index < AiConversation.DefaultMemoryLimit + 10; index++)
        {
            conversation.Add(AiMessage.User($"Вопрос {index}"));
        }

        var recalled = conversation.Recall();

        Assert.Equal(AiConversation.DefaultMemoryLimit, recalled.Count);
        Assert.Equal("Вопрос 49", recalled[^1].Text);
    }

    [Fact]
    public void Беседа_ОбрезаниеНаОтветеИнструмента_СдвигаетНачало()
    {
        var conversation = new AiConversation();

        conversation.Add(AiMessage.User("Первый"));
        conversation.Add(AiMessage.Tool("вызов-1", "list_types", "Виды объектов: …"));
        conversation.Add(AiMessage.Tool("вызов-2", "list_types", "Виды объектов: …"));
        conversation.Add(AiMessage.Assistant("Готово"));

        // Ответ инструмента без сообщения с вызовом служба отвергает,
        // поэтому отрезок не может начинаться с него.
        var recalled = conversation.Recall(3);

        Assert.Equal(AiRole.Assistant, recalled[0].Role);
        Assert.Single(recalled);
    }

    [Fact]
    public void Беседа_Очистка_УдаляетСообщенияИПредложения()
    {
        var conversation = new AiConversation();

        conversation.Add(AiMessage.User("Вопрос"));
        conversation.Add(new AiProposal(
            AiProposalKind.Create,
            ContentTypeIds.Spells,
            "Заклинание",
            "Молния",
            [],
            new Dictionary<string, string?>()));

        conversation.Clear();

        Assert.Empty(conversation.Messages);
        Assert.Empty(conversation.Proposals);
    }
}
