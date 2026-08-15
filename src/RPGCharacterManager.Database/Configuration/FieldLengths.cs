namespace RPGCharacterManager.Database.Configuration;

/// <summary>
/// Ограничения длины текстовых полей базы данных.
/// Значения собраны в одном месте, чтобы схема оставалась согласованной,
/// а в конфигурациях сущностей отсутствовали произвольные числа.
/// </summary>
internal static class FieldLengths
{
    /// <summary>Короткое имя: название объекта, категория, тип.</summary>
    public const int Name = 200;

    /// <summary>Внутреннее имя, используемое в формулах и правилах.</summary>
    public const int SystemName = 120;

    /// <summary>Краткая строка: версия, цвет, единица измерения.</summary>
    public const int ShortText = 64;

    /// <summary>Строка среднего размера: путь к файлу, источник, требования.</summary>
    public const int MediumText = 512;

    /// <summary>Текст выражения формулы или условия.</summary>
    public const int Expression = 2000;

    /// <summary>Описание объекта.</summary>
    public const int Description = 8000;
}
