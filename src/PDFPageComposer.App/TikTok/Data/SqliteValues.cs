using System.Globalization;
using Microsoft.Data.Sqlite;

namespace PDFPageComposer.App.TikTok.Data;

/// <summary>Storage formats and small ADO.NET helpers shared by the TikTok repository.</summary>
internal static class SqliteValues
{
    /// <summary>SQLite keeps the bound-parameter limit high, but IN lists are chunked to stay well below it.</summary>
    public const int MaxInListSize = 500;

    private const string DateOnlyFormat = "yyyy-MM-dd";

    public static string ToDb(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    public static string ToDb(DateOnly value) => value.ToString(DateOnlyFormat, CultureInfo.InvariantCulture);

    public static long ToDb(DateTime utcValue) => utcValue.ToUniversalTime().Ticks;

    public static long ToDb(bool value) => value ? 1L : 0L;

    public static string ToDb<TEnum>(TEnum value)
        where TEnum : struct, Enum => value.ToString();

    public static DateTimeOffset ParseDateTimeOffset(string value) =>
        DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static DateOnly ParseDateOnly(string value) =>
        DateOnly.ParseExact(value, DateOnlyFormat, CultureInfo.InvariantCulture);

    public static DateTime ParseUtcTicks(long ticks) => new(ticks, DateTimeKind.Utc);

    public static TEnum ParseEnum<TEnum>(string value)
        where TEnum : struct, Enum => Enum.Parse<TEnum>(value, ignoreCase: false);

    public static string GetString(this SqliteDataReader reader, string column) =>
        reader.GetString(reader.GetOrdinal(column));

    public static string? GetNullableString(this SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static long GetInt64(this SqliteDataReader reader, string column) =>
        reader.GetInt64(reader.GetOrdinal(column));

    public static int GetInt32(this SqliteDataReader reader, string column) =>
        reader.GetInt32(reader.GetOrdinal(column));

    public static int? GetNullableInt32(this SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    public static bool GetBoolean(this SqliteDataReader reader, string column) =>
        reader.GetInt64(reader.GetOrdinal(column)) != 0;

    public static DateTimeOffset GetDateTimeOffset(this SqliteDataReader reader, string column) =>
        ParseDateTimeOffset(reader.GetString(column));

    public static DateTimeOffset? GetNullableDateTimeOffset(this SqliteDataReader reader, string column)
    {
        var value = reader.GetNullableString(column);
        return value is null ? null : ParseDateTimeOffset(value);
    }

    public static TEnum GetEnum<TEnum>(this SqliteDataReader reader, string column)
        where TEnum : struct, Enum => ParseEnum<TEnum>(reader.GetString(column));

    public static TEnum? GetNullableEnum<TEnum>(this SqliteDataReader reader, string column)
        where TEnum : struct, Enum
    {
        var value = reader.GetNullableString(column);
        return value is null ? null : ParseEnum<TEnum>(value);
    }

    public static SqliteCommand CreateCommand(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }

    /// <summary>Builds "(@p0, @p1, ...)" and the matching parameters for an IN list.</summary>
    public static (string InList, (string Name, object? Value)[] Parameters) BuildInList(IReadOnlyList<string> values)
    {
        var parameters = new (string Name, object? Value)[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            parameters[index] = ("@p" + index.ToString(CultureInfo.InvariantCulture), values[index]);
        }

        return ("(" + string.Join(", ", parameters.Select(parameter => parameter.Name)) + ")", parameters);
    }

    public static IEnumerable<IReadOnlyList<string>> DistinctChunks(IEnumerable<string> values) =>
        values.Distinct(StringComparer.Ordinal).Chunk(MaxInListSize);

    public static bool IsUniqueViolation(SqliteException exception) =>
        exception.SqliteErrorCode == 19 && exception.SqliteExtendedErrorCode is 2067 or 1555;
}
