using Microsoft.Data.Sqlite;
using RPGCharacterManager.Ai.Tools;
using RPGCharacterManager.Content;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Tests.Characters;

namespace RPGCharacterManager.Tests.Ai;

/// <summary>
/// Проверка инструментов помощника.
///
/// Главное свойство инструментов — универсальность: ни один из них не знает
/// заранее ни одного вида объектов. Всё, что они умеют, они узнают из описания
/// вида контента, поэтому те же проверки годятся для любой игровой системы.
/// </summary>
public sealed class AiToolTests
{
    private static async Task<int> CountAsync(ContentService content, string typeId)
    {
        var page = await content.SearchAsync(typeId, null, 0, 1);

        return page.TotalCount;
    }

    // ---------- Что помощник знает о базе ----------

    [Fact]
    public async Task ВидыОбъектов_Перечисляются_СКоличеством()
    {
        await using var context = await AiTestContext.CreateAsync();
        await context.Characters.AddAsync(CharacterContent.Spell("Огненный шар", "ognennyi_shar"));

        var result = await context.InvokeAsync(ListTypesTool.ToolName, "{}");

        Assert.Contains(ContentTypeIds.Spells, result.Text, StringComparison.Ordinal);
        Assert.Contains("объектов: 1", result.Text, StringComparison.Ordinal);
        Assert.Empty(result.Proposals);
    }

    [Fact]
    public async Task ПоляВида_Заклинания_СодержатИменаИВидыЗначений()
    {
        await using var context = await AiTestContext.CreateAsync();

        var result = await context.InvokeAsync(
            DescribeTypeTool.ToolName,
            """{"type":"spells"}""");

        Assert.Contains("name", result.Text, StringComparison.Ordinal);
        Assert.Contains("обязательное", result.Text, StringComparison.Ordinal);
        Assert.Contains("формула движка вычислений", result.Text, StringComparison.Ordinal);
        Assert.Contains("ссылка на объект вида", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ПоляВида_НазваниеВместоИмени_Распознаётся()
    {
        await using var context = await AiTestContext.CreateAsync();

        var result = await context.InvokeAsync(
            DescribeTypeTool.ToolName,
            """{"type":"Заклинания"}""");

        Assert.Contains("Вид «spells»", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ПоляВида_НеизвестныйВид_ПеречисляетДоступные()
    {
        await using var context = await AiTestContext.CreateAsync();

        var result = await context.InvokeAsync(
            DescribeTypeTool.ToolName,
            """{"type":"кибер-импланты"}""");

        Assert.Contains("Такого вида объектов нет", result.Text, StringComparison.Ordinal);
        Assert.Contains(ContentTypeIds.Spells, result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Поиск_ПоНазванию_ВозвращаетИдентификатор()
    {
        await using var context = await AiTestContext.CreateAsync();
        var spell = CharacterContent.Spell("Ледяная стрела", "ledyanaya_strela");

        await context.Characters.AddAsync(spell);

        var result = await context.InvokeAsync(
            FindObjectsTool.ToolName,
            """{"type":"spells","query":"Ледяная"}""");

        Assert.Contains(spell.Id.ToString(), result.Text, StringComparison.Ordinal);
    }

    // ---------- Создание объектов ----------

    [Fact]
    public async Task Создание_Заклинания_ГотовитПредложениеИНичегоНеПишет()
    {
        await using var context = await AiTestContext.CreateAsync();

        var result = await context.InvokeAsync(
            CreateObjectTool.ToolName,
            """{"type":"spells","values":{"name":"Ледяная стрела","level":"2","formula":"2d6"}}""");

        var proposal = Assert.Single(result.Proposals);

        Assert.Equal(AiProposalKind.Create, proposal.Kind);
        Assert.Equal("Ледяная стрела", proposal.Title);
        Assert.Contains(proposal.Changes, change => change.Field == "Уровень" && change.NewValue == "2");

        // Ничего не изменилось: предложение ждёт подтверждения пользователя.
        Assert.Equal(0, await CountAsync(context.Content, ContentTypeIds.Spells));
    }

    [Fact]
    public async Task Создание_БезНазвания_ПроситЗаполнитьПоле()
    {
        await using var context = await AiTestContext.CreateAsync();

        var result = await context.InvokeAsync(
            CreateObjectTool.ToolName,
            """{"type":"spells","values":{"level":"2"}}""");

        Assert.Empty(result.Proposals);
        Assert.Contains("название", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Создание_НесуществующееПоле_ПопадаетВЗамечания()
    {
        await using var context = await AiTestContext.CreateAsync();

        var result = await context.InvokeAsync(
            CreateObjectTool.ToolName,
            """{"type":"spells","values":{"name":"Молния","мощность":"высокая"}}""");

        Assert.Single(result.Proposals);
        Assert.Contains("мощность", result.Text, StringComparison.Ordinal);
        Assert.Contains("Не записано", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Создание_СсылкаПоНазванию_НаходитСвязанныйОбъект()
    {
        await using var context = await AiTestContext.CreateAsync();
        var mana = CharacterContent.Resource("Мана", "mana", "10");

        await context.Characters.AddAsync(mana);

        var result = await context.InvokeAsync(
            CreateObjectTool.ToolName,
            """{"type":"spells","values":{"name":"Искра","resource":"Мана"}}""");

        var proposal = Assert.Single(result.Proposals);

        // Ссылка показана названием, а не идентификатором: пользователь читает
        // предложение, а не сверяет шестнадцатеричные числа.
        Assert.Contains(
            proposal.Changes,
            change => change.Field == "Расходуемый ресурс" && change.NewValue == "Мана");
    }

    [Fact]
    public async Task Создание_НесуществующаяСсылка_ПопадаетВЗамечания()
    {
        await using var context = await AiTestContext.CreateAsync();

        var result = await context.InvokeAsync(
            CreateObjectTool.ToolName,
            """{"type":"spells","values":{"name":"Искра","resource":"Эфир"}}""");

        Assert.Contains("Эфир", result.Text, StringComparison.Ordinal);
        Assert.Contains("не найден", result.Text, StringComparison.Ordinal);
    }

    // ---------- Изменение объектов ----------

    [Fact]
    public async Task Изменение_СуществующегоОбъекта_ПоказываетБылоИСтало()
    {
        await using var context = await AiTestContext.CreateAsync();
        var spell = CharacterContent.Spell("Огненный шар", "ognennyi_shar", level: 3);

        await context.Characters.AddAsync(spell);

        var result = await context.InvokeAsync(
            UpdateObjectTool.ToolName,
            $$$"""{"type":"spells","id":"{{{spell.Id}}}","values":{"level":"5"}}""");

        var proposal = Assert.Single(result.Proposals);
        var change = Assert.Single(proposal.Changes);

        Assert.Equal(AiProposalKind.Update, proposal.Kind);
        Assert.Equal("3", change.OldValue);
        Assert.Equal("5", change.NewValue);
    }

    [Fact]
    public async Task Изменение_СистемногоОбъекта_ПредлагаетСоздатьКопию()
    {
        await using var context = await AiTestContext.CreateAsync();
        var spell = CharacterContent.Spell("Огненный шар", "ognennyi_shar", level: 3);

        spell.IsSystem = true;
        await context.Characters.AddAsync(spell);

        var result = await context.InvokeAsync(
            UpdateObjectTool.ToolName,
            $$$"""{"type":"spells","id":"{{{spell.Id}}}","values":{"level":"5"}}""");

        Assert.Empty(result.Proposals);
        Assert.Contains(CopyObjectTool.ToolName, result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Копия_СистемногоОбъекта_ГотовитПредложениеСоздать()
    {
        await using var context = await AiTestContext.CreateAsync();
        var spell = CharacterContent.Spell("Огненный шар", "ognennyi_shar", level: 3);

        spell.IsSystem = true;
        await context.Characters.AddAsync(spell);

        var result = await context.InvokeAsync(
            CopyObjectTool.ToolName,
            """{"type":"spells","id":"Огненный шар","values":{"level":"5"}}""");

        var proposal = Assert.Single(result.Proposals);

        Assert.Equal(AiProposalKind.Create, proposal.Kind);
        Assert.Equal(spell.Id, proposal.TargetId);
        Assert.Contains("копия", proposal.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Изменение_БезИзменений_НеСоздаётПредложения()
    {
        await using var context = await AiTestContext.CreateAsync();
        var spell = CharacterContent.Spell("Огненный шар", "ognennyi_shar", level: 3);

        await context.Characters.AddAsync(spell);

        var result = await context.InvokeAsync(
            UpdateObjectTool.ToolName,
            $$$"""{"type":"spells","id":"{{{spell.Id}}}","values":{"level":"3"}}""");

        Assert.Empty(result.Proposals);
    }

    [Fact]
    public async Task ЗаписьСписка_БонусЭффекта_ГотовитПредложение()
    {
        await using var context = await AiTestContext.CreateAsync();
        var effect = CharacterContent.Effect("Благословение", "blagoslovenie");

        await context.Characters.AddAsync(effect);

        var result = await context.InvokeAsync(
            AddListItemTool.ToolName,
            $$$"""{"type":"effects","id":"{{{effect.Id}}}","list":"bonuses","values":{"formula":"2"}}""");

        var proposal = Assert.Single(result.Proposals);

        Assert.Equal("bonuses", proposal.ListName);
        Assert.Equal(effect.Id, proposal.TargetId);
    }

    [Fact]
    public async Task ЗаписьСписка_НеизвестныйСписок_ПеречисляетДоступные()
    {
        await using var context = await AiTestContext.CreateAsync();
        var effect = CharacterContent.Effect("Благословение", "blagoslovenie");

        await context.Characters.AddAsync(effect);

        var result = await context.InvokeAsync(
            AddListItemTool.ToolName,
            $$$"""{"type":"effects","id":"{{{effect.Id}}}","list":"чего-то","values":{"formula":"2"}}""");

        Assert.Empty(result.Proposals);
        Assert.Contains("bonuses", result.Text, StringComparison.Ordinal);
    }

    // ---------- Проверка формул и базы ----------

    [Fact]
    public async Task ПроверкаФормулы_ВерноеВыражение_ПоказываетДиапазон()
    {
        await using var context = await AiTestContext.CreateAsync();

        var result = await context.InvokeAsync(
            CheckFormulaTool.ToolName,
            """{"formula":"2d6 + 3"}""");

        Assert.Contains("верно", result.Text, StringComparison.Ordinal);
        Assert.Contains("Диапазон значений", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ПроверкаФормулы_НеверноеВыражение_СообщаетОбОшибке()
    {
        await using var context = await AiTestContext.CreateAsync();

        var result = await context.InvokeAsync(
            CheckFormulaTool.ToolName,
            """{"formula":"2d6 +"}""");

        Assert.Contains("неверно", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ПроверкаБазы_ЧистаяБаза_БезЗамечаний()
    {
        await using var context = await AiTestContext.CreateAsync();

        await context.Characters.AddAsync(
            CharacterContent.Spell("Огненный шар", "ognennyi_shar", formula: "8d6"));

        var result = await context.InvokeAsync(
            CheckDatabaseTool.ToolName,
            """{"type":"spells"}""");

        Assert.Contains("Замечаний не найдено", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ПроверкаБазы_СломаннаяФормула_ПопадаетВОтчёт()
    {
        await using var context = await AiTestContext.CreateAsync();

        await context.Characters.AddAsync(
            CharacterContent.Spell("Кривой шар", "krivoy_shar", formula: "8d6 +"));

        var result = await context.InvokeAsync(CheckDatabaseTool.ToolName, "{}");

        Assert.Contains("Кривой шар", result.Text, StringComparison.Ordinal);
        Assert.Contains("не считается", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ПроверкаБазы_ЦелаяСсылка_НеСчитаетсяЗамечанием()
    {
        await using var context = await AiTestContext.CreateAsync();
        var mana = CharacterContent.Resource("Мана", "mana", "10");

        await context.Characters.AddAsync(mana);
        await context.Characters.AddAsync(CharacterContent.Spell("Искра", "iskra", resourceId: mana.Id));

        var result = await context.InvokeAsync(
            CheckDatabaseTool.ToolName,
            """{"type":"spells"}""");

        Assert.Contains("Замечаний не найдено", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ПроверкаБазы_ПотеряннаяСсылка_ПопадаетВОтчёт()
    {
        await using var context = await AiTestContext.CreateAsync();

        await context.Characters.AddAsync(CharacterContent.Spell("Искра", "iskra"));

        // Сама база потерянных ссылок не допускает: удаление ресурса очищает
        // ссылку на него. Поэтому испорченная запись создаётся в обход базы —
        // так, как она может прийти из содержимого чужого происхождения.
        await PlantMissingReferenceAsync(context.Characters.DatabaseFilePath);

        var result = await context.InvokeAsync(
            CheckDatabaseTool.ToolName,
            """{"type":"spells"}""");

        Assert.Contains("несуществующий объект", result.Text, StringComparison.Ordinal);
    }

    private static async Task PlantMissingReferenceAsync(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = false,
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();

        command.CommandText = "UPDATE Spells SET ResourceId = $resource";
        command.Parameters.AddWithValue("$resource", Guid.NewGuid().ToString());

        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task ПроверкаБазы_ОдинаковыеНазвания_ПопадаютВОтчёт()
    {
        await using var context = await AiTestContext.CreateAsync();

        await context.Characters.AddAsync(
            CharacterContent.Spell("Молния", "molniya_1"),
            CharacterContent.Spell("Молния", "molniya_2"));

        var result = await context.InvokeAsync(
            CheckDatabaseTool.ToolName,
            """{"type":"spells"}""");

        Assert.Contains("повторяется", result.Text, StringComparison.Ordinal);
    }

    // ---------- Персонаж ----------

    [Fact]
    public async Task ЛистПерсонажа_ПоИмени_СодержитУровень()
    {
        await using var context = await AiTestContext.CreateAsync();

        var draft = new Core.Models.Characters.CharacterDraft { Level = 4 };
        draft.Character.Name = "Аргус";

        var created = await context.Characters.Builder.CreateAsync(draft);
        Assert.True(created.IsSuccess, created.Error);

        var result = await context.InvokeAsync(
            ReadCharacterTool.ToolName,
            """{"id":"Аргус"}""");

        Assert.Contains("Аргус", result.Text, StringComparison.Ordinal);
        Assert.Contains("уровень 4", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ЛистПерсонажа_НеизвестноеИмя_СообщаетГдеИскать()
    {
        await using var context = await AiTestContext.CreateAsync();

        var result = await context.InvokeAsync(
            ReadCharacterTool.ToolName,
            """{"id":"Никто"}""");

        Assert.Contains(ListCharactersTool.ToolName, result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Инструменты_ИзменяющиеДанные_ОтмеченыПризнаком()
    {
        await using var context = await AiTestContext.CreateAsync();

        // Признак нужен интерфейсу и указаниям модели: изменяющий инструмент
        // обязан готовить предложение, а не записывать данные.
        string[] changing =
        [
            CreateObjectTool.ToolName,
            CopyObjectTool.ToolName,
            UpdateObjectTool.ToolName,
            AddListItemTool.ToolName,
        ];

        foreach (var tool in context.Assistant.Tools)
        {
            Assert.Equal(changing.Contains(tool.Name), tool.ChangesData);
        }
    }

    [Fact]
    public async Task Инструменты_ОписаныДляМодели()
    {
        await using var context = await AiTestContext.CreateAsync();

        foreach (var tool in context.Assistant.Tools)
        {
            var specification = tool.Describe();

            Assert.False(string.IsNullOrWhiteSpace(specification.Description), tool.Name);
            Assert.False(string.IsNullOrWhiteSpace(tool.Title), tool.Name);

            // Имя инструмента передаётся службе как имя функции: кириллица
            // и пробелы в нём недопустимы.
            Assert.Matches("^[a-z_]+$", tool.Name);

            foreach (var parameter in specification.Parameters)
            {
                Assert.Matches("^[a-z]+$", parameter.Name);
                Assert.False(string.IsNullOrWhiteSpace(parameter.Description), parameter.Name);
            }
        }
    }
}
