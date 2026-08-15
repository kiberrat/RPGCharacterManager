using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace RPGCharacterManager.Import.Readers;

/// <summary>
/// Чтение чужих баз данных SQLite.
///
/// Приложение не знает, как устроена чужая база, и не пытается угадать: оно
/// перечисляет таблицы и выкладывает их строки перечнем «столбец: значение».
/// Что из этого — заклинание, а что — предмет, определяет распознавание,
/// ровно как и для книги правил.
/// </summary>
internal sealed class SqliteReader : DocumentReaderBase
{
    /// <summary>Сколько строк читается из одной таблицы.</summary>
    public const int RowLimit = 500;

    /// <inheritdoc />
    public override string Format => "SQLite";

    /// <inheritdoc />
    public override IReadOnlyList<string> Extensions { get; } = [".db", ".sqlite", ".sqlite3", ".db3"];

    /// <inheritdoc />
    protected override async Task<string> ExtractAsync(
        string path,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        // База открывается только для чтения: чужой файл приложение не изменяет
        // ни при каких обстоятельствах, в том числе не создаёт рядом с ним журнал.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var tables = await ReadTablesAsync(connection, cancellationToken).ConfigureAwait(false);

        if (tables.Count == 0)
        {
            notes.Add("таблиц нет");

            return string.Empty;
        }

        var builder = new StringBuilder();
        var names = new List<string>();

        foreach (var table in tables)
        {
            var rows = await WriteTableAsync(connection, table, builder, cancellationToken)
                .ConfigureAwait(false);

            names.Add($"{table} ({rows})");
        }

        notes.Add($"таблиц: {tables.Count}");
        notes.Add(string.Join(", ", names));

        return builder.ToString();
    }

    /// <summary>
    /// Возвращает имена таблиц базы, кроме служебных.
    /// </summary>
    /// <param name="connection">Открытое соединение.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Имена таблиц.</returns>
    private static async Task<List<string>> ReadTablesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";

        var tables = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    /// <summary>
    /// Выкладывает строки одной таблицы перечнем «столбец: значение».
    /// </summary>
    /// <param name="connection">Открытое соединение.</param>
    /// <param name="table">Имя таблицы.</param>
    /// <param name="builder">Собираемый текст.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество прочитанных строк.</returns>
    private static async Task<int> WriteTableAsync(
        SqliteConnection connection,
        string table,
        StringBuilder builder,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        // Имя таблицы приходит из самой базы и подставляется в кавычках:
        // параметром имя объекта задать нельзя, а кавычки не дают ему
        // превратиться в часть запроса.
        command.CommandText = $"SELECT * FROM \"{table.Replace("\"", "\"\"", StringComparison.Ordinal)}\" LIMIT {RowLimit}";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        builder.Append("Таблица ").AppendLine(table);

        var rows = 0;

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows++;

            builder.Append("Запись ").Append(rows.ToString(CultureInfo.InvariantCulture)).AppendLine(":");

            for (var index = 0; index < reader.FieldCount; index++)
            {
                if (await reader.IsDBNullAsync(index, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                builder.Append("  ").Append(reader.GetName(index)).Append(": ")
                    .AppendLine(reader.GetValue(index).ToString());
            }
        }

        builder.AppendLine();

        return rows;
    }
}
