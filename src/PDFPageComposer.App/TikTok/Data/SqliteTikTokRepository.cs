using System.IO;
using Microsoft.Data.Sqlite;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Data;

/// <summary>
/// SQLite implementation of <see cref="ITikTokRepository"/> (TECH_SPEC_TIKTOK section 4).
/// Every call opens its own pooled connection; all multi-row writes run in one transaction.
/// Only parameterized SQL is used. Buyer data is never logged.
/// </summary>
public sealed partial class SqliteTikTokRepository : ITikTokRepository
{
    private readonly string _connectionString;

    public SqliteTikTokRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        DatabasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = true,
        }.ToString();
    }

    public string DatabasePath { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, "PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);
        await TikTokSchemaMigrator.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TikTokShop>> GetShopsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = SqliteValues.CreateCommand(
            connection,
            null,
            "SELECT ShopId, Name FROM Shop ORDER BY Name COLLATE NOCASE, ShopId;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var shops = new List<TikTokShop>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            shops.Add(new TikTokShop(reader.GetInt64("ShopId"), reader.GetString("Name")));
        }

        return shops;
    }

    public async Task<TikTokShop> AddShopAsync(string name, CancellationToken cancellationToken)
    {
        var trimmed = NormalizeShopName(name);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = SqliteValues.CreateCommand(
            connection,
            null,
            "INSERT INTO Shop(Name) VALUES(@name) RETURNING ShopId;",
            ("@name", trimmed));
        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return new TikTokShop(Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture), trimmed);
        }
        catch (SqliteException exception) when (SqliteValues.IsUniqueViolation(exception))
        {
            throw new InvalidOperationException($"Shop \"{trimmed}\" đã tồn tại.", exception);
        }
    }

    public async Task RenameShopAsync(long shopId, string name, CancellationToken cancellationToken)
    {
        var trimmed = NormalizeShopName(name);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = SqliteValues.CreateCommand(
            connection,
            null,
            "UPDATE Shop SET Name = @name WHERE ShopId = @shopId;",
            ("@name", trimmed),
            ("@shopId", shopId));
        int affected;
        try
        {
            affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (SqliteValues.IsUniqueViolation(exception))
        {
            throw new InvalidOperationException($"Shop \"{trimmed}\" đã tồn tại.", exception);
        }

        if (affected == 0)
        {
            throw new InvalidOperationException("Không tìm thấy shop cần đổi tên.");
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<SqliteTransaction> BeginTransactionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken) =>
        (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    private static async Task<int> ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        using var command = SqliteValues.CreateCommand(connection, transaction, sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeShopName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Tên shop không được để trống.", nameof(name));
        }

        return trimmed;
    }
}
