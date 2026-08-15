using System.Globalization;
using System.Text;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.Ai.Tools;

/// <summary>
/// Перечень созданных персонажей.
/// </summary>
internal sealed class ListCharactersTool : IAiTool
{
    /// <summary>Имя инструмента.</summary>
    public const string ToolName = "list_characters";

    private const int Limit = 50;

    private readonly ICharacterService _characters;

    /// <summary>
    /// Создаёт инструмент перечня персонажей.
    /// </summary>
    /// <param name="characters">Служба персонажей.</param>
    public ListCharactersTool(ICharacterService characters) => _characters = Guard.NotNull(characters);

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Title => "Персонажи";

    /// <inheritdoc />
    public string Description =>
        "Возвращает созданных персонажей: имя, уровень, игровую систему, расу и класс.";

    /// <inheritdoc />
    public IReadOnlyList<AiToolParameter> Parameters =>
    [
        new(AiToolParameters.Query, "Часть имени персонажа.", AiParameterKind.Text),
    ];

    /// <inheritdoc />
    public async Task<AiToolResult> InvokeAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(arguments);

        var page = await _characters
            .SearchAsync(arguments.Text(AiToolParameters.Query), 0, Limit, cancellationToken)
            .ConfigureAwait(false);

        if (page.Items.Count == 0)
        {
            return AiToolResult.Answer("Персонажей пока нет.");
        }

        var builder = new StringBuilder("Персонажи:").AppendLine();

        foreach (var item in page.Items)
        {
            builder.Append("- ").Append(item.Id).Append(" — ").Append(item.Name)
                .Append(", уровень ").Append(item.Level.ToString(CultureInfo.CurrentCulture));

            var parts = new[] { item.GameSystemName, item.RaceName, item.ClassName }
                .Where(part => !string.IsNullOrWhiteSpace(part));

            builder.Append(AiContentText.Separator).AppendLine(string.Join(AiContentText.Separator, parts));
        }

        return AiToolResult.Answer(builder.ToString());
    }
}

/// <summary>
/// Чтение листа персонажа со всеми вычисленными значениями.
/// </summary>
internal sealed class ReadCharacterTool : IAiTool
{
    /// <summary>Имя инструмента.</summary>
    public const string ToolName = "read_character";

    private const int NameLimit = 40;

    private readonly ICharacterService _characters;
    private readonly ICharacterSheetService _sheets;

    /// <summary>
    /// Создаёт инструмент чтения листа персонажа.
    /// </summary>
    /// <param name="characters">Служба персонажей.</param>
    /// <param name="sheets">Служба листа персонажа.</param>
    public ReadCharacterTool(ICharacterService characters, ICharacterSheetService sheets)
    {
        _characters = Guard.NotNull(characters);
        _sheets = Guard.NotNull(sheets);
    }

    /// <inheritdoc />
    public string Name => ToolName;

    /// <inheritdoc />
    public string Title => "Лист персонажа";

    /// <inheritdoc />
    public string Description =>
        "Возвращает лист персонажа: характеристики, навыки, ресурсы, черты и способности " +
        "с уже вычисленными значениями. Вызывай, прежде чем что-либо советовать персонажу.";

    /// <inheritdoc />
    public IReadOnlyList<AiToolParameter> Parameters =>
    [
        new(
            AiToolParameters.Id,
            "Идентификатор персонажа либо его имя.",
            AiParameterKind.Text,
            IsRequired: true),
    ];

    /// <inheritdoc />
    public async Task<AiToolResult> InvokeAsync(
        AiToolArguments arguments,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(arguments);

        var identifier = await ResolveAsync(arguments.Text(AiToolParameters.Id), cancellationToken)
            .ConfigureAwait(false);

        if (identifier is null)
        {
            return AiToolResult.Answer(
                $"Персонаж не найден. Перечень персонажей возвращает {ListCharactersTool.ToolName}.");
        }

        var sheet = await _sheets.LoadAsync(identifier.Value, cancellationToken).ConfigureAwait(false);

        if (sheet.IsFailure)
        {
            return AiToolResult.Answer($"Прочитать лист персонажа не удалось: {sheet.Error}");
        }

        return AiToolResult.Answer(Describe(sheet.Value));
    }

    /// <summary>
    /// Составляет краткое описание листа персонажа.
    /// </summary>
    /// <param name="sheet">Лист персонажа.</param>
    /// <returns>Текст описания.</returns>
    private static string Describe(CharacterSheet sheet)
    {
        var builder = new StringBuilder();
        var character = sheet.Character;

        builder.Append("Персонаж «").Append(character.Name).Append("», уровень ")
            .Append(character.Level.ToString(CultureInfo.CurrentCulture)).AppendLine(".");

        Append(builder, "Характеристики", sheet.Attributes
            .Where(item => !item.IsHidden)
            .Select(item => $"{item.Name}: {Number(item.Value)} (модификатор {Number(item.Modifier)})"));

        Append(builder, "Ресурсы", sheet.Resources.Select(item =>
            $"{item.Name}: {Number(item.Current)} из {Number(item.Maximum)}"));

        Append(builder, "Навыки", sheet.Skills
            .Where(item => item.ProficiencyLevel > 0)
            .Select(item => $"{item.Name}: {Number(item.Value)}"));

        Append(builder, "Черты", sheet.Traits.Select(item => item.Name));
        Append(builder, "Способности", sheet.Abilities.Select(item => item.Name));

        if (sheet.Issues.Count > 0)
        {
            Append(builder, "Замечания расчёта", sheet.Issues.Select(issue => issue.Message));
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string title, IEnumerable<string> values)
    {
        var items = values.Take(NameLimit).ToList();

        if (items.Count == 0)
        {
            return;
        }

        builder.Append(title).Append(": ").Append(string.Join(AiContentText.Separator, items)).AppendLine(".");
    }

    private static string Number(double value) =>
        value.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>
    /// Находит персонажа по идентификатору либо по имени.
    /// </summary>
    /// <param name="text">Идентификатор или имя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор персонажа либо <see langword="null"/>.</returns>
    private async Task<Guid?> ResolveAsync(string? text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (Guid.TryParse(text, out var identifier))
        {
            return identifier;
        }

        var page = await _characters.SearchAsync(text, 0, NameLimit, cancellationToken).ConfigureAwait(false);

        return page.Items
            .FirstOrDefault(item => item.Name.Equals(text, StringComparison.OrdinalIgnoreCase))?
            .Id;
    }
}
