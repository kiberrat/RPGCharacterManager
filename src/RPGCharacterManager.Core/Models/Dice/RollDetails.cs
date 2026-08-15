using System.Text.Json;
using System.Text.Json.Serialization;
using RPGCharacterManager.Core.Abstractions.Dice;

namespace RPGCharacterManager.Core.Models.Dice;

/// <summary>
/// Подробности броска, сохраняемые в журнале.
///
/// В журнале лежит не только итог: без выпавших костей повтор броска показал бы
/// другое число, а разобрать, откуда взялся результат, стало бы невозможно.
///
/// Описание формата вынесено в контракты, потому что записывает подробности
/// подсистема бросков, а читает их не только она: статистика считает по тем же
/// костям, сколько раз выпал максимум и какова средняя грань. Два описания
/// одного формата рано или поздно разошлись бы.
/// </summary>
/// <param name="Attempts">
/// Выполненные попытки в порядке броска. Значение отсутствует, если запись создана
/// другой подсистемой и содержит собственный состав подробностей.
/// </param>
public sealed record RollDetails(
    [property: JsonPropertyName("попытки")] IReadOnlyList<RollAttemptDetails>? Attempts)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        // Русские имена полей и подписи попадают в файл базы данных как есть:
        // журнал бросков читаем без словаря соответствий.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Записывает подробности броска в строку.
    /// </summary>
    /// <param name="attempts">Выполненные попытки.</param>
    /// <returns>Текст в формате JSON.</returns>
    public static string Write(IReadOnlyList<RollAttempt> attempts)
    {
        ArgumentNullException.ThrowIfNull(attempts);

        return JsonSerializer.Serialize(
            new RollDetails(attempts
                .Select(attempt => new RollAttemptDetails(
                    attempt.Total,
                    attempt.IsChosen,
                    attempt.Dice.Select(cast => new DieCastDetails(cast.Sides, cast.Value)).ToList()))
                .ToList()),
            Options);
    }

    /// <summary>
    /// Восстанавливает попытки броска из строки журнала.
    ///
    /// Записи, созданные другими подсистемами — например, атакой оружия, — имеют
    /// собственный состав подробностей. Для них возвращается одна попытка с итогом
    /// записи и без костей: журнал показывает такую запись наравне с остальными.
    /// </summary>
    /// <param name="json">Текст подробностей.</param>
    /// <param name="total">Итог записи журнала.</param>
    /// <returns>Попытки броска.</returns>
    public static IReadOnlyList<RollAttempt> Read(string? json, double total)
    {
        var fallback = new[] { new RollAttempt(total, [], true) };

        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            // Записи других подсистем разбираются без ошибки, но поля попыток
            // в них нет: тогда объект создан, а список остаётся пустым.
            if (JsonSerializer.Deserialize<RollDetails>(json, Options) is not { Attempts.Count: > 0 } details)
            {
                return fallback;
            }

            return details.Attempts
                .Select(attempt => new RollAttempt(
                    attempt.Total,
                    attempt.Dice.Select(cast => new DieCast(cast.Sides, cast.Value)).ToList(),
                    attempt.IsChosen))
                .ToList();
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    /// <summary>
    /// Возвращает кости принятой попытки записи журнала.
    ///
    /// Считать нужно только принятый итог: при преимуществе выражение вычисляется
    /// дважды, но в игре произошёл один бросок, и вторая попытка не должна
    /// удваивать статистику.
    /// </summary>
    /// <param name="json">Текст подробностей.</param>
    /// <param name="total">Итог записи журнала.</param>
    /// <returns>Выпавшие кости принятой попытки.</returns>
    public static IReadOnlyList<DieCast> ChosenDice(string? json, double total)
    {
        var attempts = Read(json, total);

        return attempts.FirstOrDefault(attempt => attempt.IsChosen)?.Dice ?? [];
    }
}

/// <summary>
/// Одна попытка броска в журнале.
/// </summary>
/// <param name="Total">Итог попытки.</param>
/// <param name="IsChosen">Итог принят за результат броска.</param>
/// <param name="Dice">Выпавшие кости.</param>
public sealed record RollAttemptDetails(
    [property: JsonPropertyName("итог")] double Total,
    [property: JsonPropertyName("принята")] bool IsChosen,
    [property: JsonPropertyName("кости")] IReadOnlyList<DieCastDetails> Dice);

/// <summary>
/// Одна выпавшая кость в журнале.
/// </summary>
/// <param name="Sides">Количество граней.</param>
/// <param name="Value">Выпавшее значение.</param>
public sealed record DieCastDetails(
    [property: JsonPropertyName("граней")] int Sides,
    [property: JsonPropertyName("выпало")] int Value);
