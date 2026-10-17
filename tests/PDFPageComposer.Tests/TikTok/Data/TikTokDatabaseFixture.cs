using System.IO;
using Microsoft.Data.Sqlite;
using PDFPageComposer.App.TikTok.Data;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.Tests.TikTok.Data;

/// <summary>A repository on a temporary database file, deleted on dispose.</summary>
internal sealed class TikTokDatabaseFixture : IDisposable
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private readonly string _directory;

    private TikTokDatabaseFixture(string directory, SqliteTikTokRepository repository)
    {
        _directory = directory;
        Repository = repository;
    }

    public SqliteTikTokRepository Repository { get; }

    public string DatabasePath => Repository.DatabasePath;

    public static async Task<TikTokDatabaseFixture> CreateAsync(bool initialize = true)
    {
        var directory = Path.Combine(Path.GetTempPath(), "gpdf-tiktok-data-" + Guid.NewGuid().ToString("N"));
        var repository = new SqliteTikTokRepository(Path.Combine(directory, "nested", "tiktok.db"));
        var fixture = new TikTokDatabaseFixture(directory, repository);
        if (initialize)
        {
            await repository.InitializeAsync(CancellationToken.None);
        }

        return fixture;
    }

    public static DateTimeOffset VnTime(int day, int hour, int minute = 0) =>
        new(2026, 10, day, hour, minute, 0, VietnamOffset);

    public static TikTokOrder Order(
        string orderId,
        long shopId,
        DateTimeOffset createdAt,
        params TikTokOrderLine[] lines) =>
        new(
            orderId,
            shopId,
            "Chờ vận chuyển",
            createdAt,
            "b***n",
            null,
            null,
            true,
            0,
            0,
            null,
            null,
            null,
            false,
            null,
            lines);

    public static TikTokOrderLine Line(string orderId, string skuId, int lineNo, int quantity = 1) =>
        new(orderId, skuId, lineNo, "Card bo góc " + skuId, "IVAN - 4 thẻ", quantity);

    public static SkuConfig Sku(string skuId, SkuProcessType type = SkuProcessType.Auto, string? idol = "IVAN") =>
        new(
            skuId,
            "Card bo góc " + skuId,
            "IVAN - 4 thẻ",
            type,
            SkuConfigStatus.Unconfigured,
            idol,
            4,
            VnTime(1, 8),
            VnTime(1, 9));

    public static ImportChangeSet ChangeSet(
        long shopId,
        IReadOnlyList<TikTokOrder>? insert = null,
        IReadOnlyList<TikTokOrder>? update = null,
        IReadOnlyList<string>? deactivate = null,
        IReadOnlyList<SkuConfig>? newSkus = null,
        ImportMode mode = ImportMode.Full) =>
        new(
            shopId,
            "Chờ vận chuyển đơn hàng.csv",
            mode,
            VnTime(16, 8, 38),
            insert ?? [],
            update ?? [],
            deactivate ?? [],
            newSkus ?? [],
            AlreadyPrinted: 2,
            Skipped: 3);

    public async Task<long> CountAsync(string table)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString());
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public async Task<IReadOnlyList<string>> GetColumnsAsync(string table)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString());
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT name FROM pragma_table_info('{table}');";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
