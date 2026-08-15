namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Персонаж в списке отбора раздела.
///
/// Отбор «все персонажи или один» одинаков в журнале и в статистике, поэтому
/// описан один раз: два одинаковых списка рано или поздно стали бы вести себя
/// по-разному.
/// </summary>
/// <param name="Id">Идентификатор персонажа; <see langword="null"/> — все персонажи.</param>
/// <param name="Name">Имя персонажа.</param>
public sealed record CharacterFilterOption(Guid? Id, string Name)
{
    /// <summary>Название строки, означающей отбор без ограничения по персонажу.</summary>
    public const string AllCharacters = "Все персонажи";

    /// <summary>Строка отбора «все персонажи».</summary>
    public static CharacterFilterOption All { get; } = new(null, AllCharacters);
}
