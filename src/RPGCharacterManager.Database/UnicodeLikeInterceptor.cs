using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RPGCharacterManager.Database;

/// <summary>
/// Замена оператора <c>LIKE</c> на сравнение, не различающее регистр кириллицы.
///
/// Встроенный <c>LIKE</c> в SQLite сводит регистр только у латиницы: «волк»
/// не находил «Волколака», хотя «wolf» находил «Wolfhound». Библиотека ICU
/// в поставку не входит, а заводить отдельный способ отбора для поиска значило бы
/// иметь два разных поиска в одном приложении (решение Р-95).
///
/// SQLite разрешает переопределить <c>LIKE</c> собственной функцией с именем
/// <c>like</c>. Поэтому регистр перестаёт различаться сразу везде, где отбор уже
/// написан, — в контенте, персонажах, журнале и заклинаниях, — и ни одно место
/// отбора для этого не изменялось.
/// </summary>
public sealed class UnicodeLikeInterceptor : DbConnectionInterceptor
{
    /// <summary>Имя переопределяемой функции SQLite.</summary>
    private const string FunctionName = "like";

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Register(connection);

    /// <inheritdoc />
    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Register(connection);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Подключает собственную функцию сравнения к соединению.
    /// </summary>
    /// <param name="connection">Открытое соединение.</param>
    private static void Register(DbConnection connection)
    {
        if (connection is not SqliteConnection sqlite)
        {
            return;
        }

        // Порядок аргументов задан SQLite: выражение «X LIKE Y» вызывает like(Y, X),
        // то есть первым приходит образец, а вторым — проверяемое значение.
        sqlite.CreateFunction<string?, string?, bool>(
            FunctionName,
            (pattern, value) => SqlLike.Matches(pattern, value),
            isDeterministic: true);

        sqlite.CreateFunction<string?, string?, string?, bool>(
            FunctionName,
            (pattern, value, escape) => SqlLike.Matches(pattern, value, Escape(escape)),
            isDeterministic: true);
    }

    /// <summary>
    /// Возвращает знак экранирования из третьего аргумента <c>LIKE</c>.
    /// </summary>
    /// <param name="escape">Значение, переданное в <c>ESCAPE</c>.</param>
    /// <returns>Знак экранирования либо <see langword="null"/>.</returns>
    private static char? Escape(string? escape) =>
        string.IsNullOrEmpty(escape) ? null : escape[0];
}

/// <summary>
/// Сравнение значения с образцом <c>LIKE</c> без различения регистра.
/// </summary>
public static class SqlLike
{
    /// <summary>Любое количество любых знаков.</summary>
    private const char AnySequence = '%';

    /// <summary>Ровно один любой знак.</summary>
    private const char AnyCharacter = '_';

    /// <summary>
    /// Проверяет, подходит ли значение под образец.
    ///
    /// Регистр не различается: сравнение идёт по знакам, приведённым к нижнему
    /// регистру правилами, не зависящими от языка системы. Так «Волк» находит
    /// «волколака», а «ВОЛК» — то же самое.
    /// </summary>
    /// <param name="pattern">Образец со знаками <c>%</c> и <c>_</c>.</param>
    /// <param name="value">Проверяемое значение.</param>
    /// <param name="escape">Знак экранирования; <see langword="null"/> — экранирования нет.</param>
    /// <returns><see langword="true"/>, если значение подходит под образец.</returns>
    public static bool Matches(string? pattern, string? value, char? escape = null)
    {
        // Сравнение с отсутствующим значением в SQL не истинно и не ложно;
        // для отбора это означает «не подходит».
        if (pattern is null || value is null)
        {
            return false;
        }

        return IsMatch(
            pattern.ToLowerInvariant().AsSpan(),
            value.ToLowerInvariant().AsSpan(),
            escape is { } sign ? char.ToLowerInvariant(sign) : null);
    }

    /// <summary>
    /// Сопоставляет значение с образцом, приведённые к нижнему регистру.
    ///
    /// Обычный обход с возвратом: при несовпадении после <c>%</c> сопоставление
    /// продолжается со следующего знака значения. Рекурсии нет, поэтому длинный
    /// образец не переполнит стек.
    /// </summary>
    /// <param name="pattern">Образец.</param>
    /// <param name="value">Значение.</param>
    /// <param name="escape">Знак экранирования.</param>
    /// <returns><see langword="true"/>, если значение подходит под образец.</returns>
    private static bool IsMatch(ReadOnlySpan<char> pattern, ReadOnlySpan<char> value, char? escape)
    {
        var patternIndex = 0;
        var valueIndex = 0;
        var starPattern = -1;
        var starValue = 0;

        while (valueIndex < value.Length)
        {
            var escaped = escape is { } sign
                && patternIndex < pattern.Length
                && pattern[patternIndex] == sign;

            var current = escaped ? patternIndex + 1 : patternIndex;

            if (current < pattern.Length
                && (escaped
                    || (pattern[current] != AnySequence && pattern[current] != AnyCharacter)))
            {
                if (pattern[current] == value[valueIndex] || (!escaped && pattern[current] == AnyCharacter))
                {
                    patternIndex = current + 1;
                    valueIndex++;
                    continue;
                }
            }
            else if (current < pattern.Length && pattern[current] == AnyCharacter)
            {
                patternIndex = current + 1;
                valueIndex++;
                continue;
            }
            else if (current < pattern.Length && pattern[current] == AnySequence)
            {
                starPattern = current;
                starValue = valueIndex;
                patternIndex = current + 1;
                continue;
            }

            if (starPattern < 0)
            {
                return false;
            }

            // Возврат к последнему «%»: оно поглощает ещё один знак значения.
            starValue++;
            valueIndex = starValue;
            patternIndex = starPattern + 1;
        }

        // Образец подходит, если остаток состоит из одних «%».
        while (patternIndex < pattern.Length && pattern[patternIndex] == AnySequence)
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }
}
