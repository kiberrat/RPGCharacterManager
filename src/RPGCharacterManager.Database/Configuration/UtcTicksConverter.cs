using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RPGCharacterManager.Database.Configuration;

/// <summary>
/// Преобразование <see cref="DateTimeOffset"/> в число тактов всемирного времени.
///
/// SQLite не поддерживает сортировку по значениям <see cref="DateTimeOffset"/>:
/// они хранятся текстом вместе со смещением часового пояса, поэтому лексикографический
/// порядок не совпадает с хронологическим. Хранение в виде целого числа тактов UTC
/// делает сортировку корректной и позволяет использовать индексы при выборке
/// журналов бросков и истории действий, рассчитанных на миллионы записей.
///
/// Все отметки времени приложение создаёт в UTC, поэтому сведения о смещении
/// не теряются: при чтении восстанавливается нулевое смещение.
/// </summary>
internal sealed class UtcTicksConverter : ValueConverter<DateTimeOffset, long>
{
    /// <summary>
    /// Создаёт преобразователь значений.
    /// </summary>
    public UtcTicksConverter()
        : base(
            value => value.ToUniversalTime().Ticks,
            value => new DateTimeOffset(value, TimeSpan.Zero))
    {
    }
}
