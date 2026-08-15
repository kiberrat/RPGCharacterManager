using System.Globalization;

namespace RPGCharacterManager.Core.Abstractions.Extensions;

/// <summary>
/// Формат файла расширения.
///
/// Расширение — это набор объектов самого приложения, а не программа
/// (решение Р-102). Поэтому файл устроен просто: это zip-архив с описанием
/// и содержимым, и прочитать его можно любым архиватором.
/// </summary>
public static class ExtensionPackage
{
    /// <summary>Расширение имени файла пакета.</summary>
    public const string FileExtension = ".rpgpack";

    /// <summary>Имя файла описания внутри пакета.</summary>
    public const string ManifestEntry = "манифест.json";

    /// <summary>Имя файла содержимого внутри пакета.</summary>
    public const string ContentEntry = "содержимое.json";

    /// <summary>Версия формата пакета, создаваемого этим приложением.</summary>
    public const string FormatVersion = "1.0";

    /// <summary>Название формата для диалога выбора файла.</summary>
    public const string FormatTitle = "Расширение RPG Character Manager";

    /// <summary>
    /// Разбирает номер версии.
    ///
    /// Версия «1.0+» из описаний расширений означает «не ниже 1.0», поэтому знак
    /// плюса отбрасывается: сравнение и без него работает как «не ниже».
    /// </summary>
    /// <param name="value">Текст версии.</param>
    /// <returns>Разобранная версия либо <see langword="null"/>.</returns>
    public static Version? ParseVersion(string? value)
    {
        var trimmed = value?.Trim().TrimEnd('+');

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        // Версия «1» разбору не поддаётся, хотя записана людьми постоянно:
        // недостающая часть дописывается нулём.
        if (!trimmed.Contains('.', StringComparison.Ordinal)
            && int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var major))
        {
            return new Version(major, 0);
        }

        return Version.TryParse(trimmed, out var version) ? version : null;
    }

    /// <summary>
    /// Проверяет, удовлетворяет ли имеющаяся версия требованию.
    ///
    /// Нечитаемое требование считается выполненным: отказать в установке из-за
    /// опечатки в описании — худшее, что может сделать приложение с чужим файлом.
    /// </summary>
    /// <param name="available">Имеющаяся версия.</param>
    /// <param name="required">Требуемая наименьшая версия.</param>
    /// <returns><see langword="true"/>, если требование выполнено.</returns>
    public static bool Satisfies(string? available, string? required)
    {
        if (ParseVersion(required) is not { } minimum)
        {
            return true;
        }

        return ParseVersion(available) is not { } current || current >= minimum;
    }
}

/// <summary>
/// Разделы расширения, не являющиеся видами игрового контента.
///
/// Остальные разделы называются идентификаторами видов контента, поэтому
/// перечислять их здесь не нужно: они появляются в расширении сами.
/// </summary>
public static class ExtensionSections
{
    /// <summary>Игровые правила.</summary>
    public const string Rules = "rules";

    /// <summary>Дополнительные имена объектов.</summary>
    public const string Aliases = "aliases";

    /// <summary>Макросы.</summary>
    public const string Macros = "macros";
}
