namespace RPGCharacterManager.Items;

/// <summary>
/// Свойства оружия: «острое, тяжёлое, пробивающее».
///
/// Свойство — это просто название, поэтому пользователь придумывает собственные
/// свойства, не изменяя ни структуру базы данных, ни код приложения. Каждое свойство
/// становится признаком объекта правил, и условие правила боя может на него опираться.
/// </summary>
internal static class WeaponProperties
{
    private static readonly char[] Separators = [',', ';', '\n', '\r'];

    /// <summary>
    /// Разбирает список свойств оружия.
    /// </summary>
    /// <param name="properties">Список свойств, разделённых запятыми или переводами строк.</param>
    /// <returns>Названия свойств без повторов и пустых значений.</returns>
    public static IReadOnlyList<string> Parse(string? properties)
    {
        if (string.IsNullOrWhiteSpace(properties))
        {
            return [];
        }

        return properties
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
