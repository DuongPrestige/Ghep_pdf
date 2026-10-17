using Microsoft.Data.Sqlite;

namespace PDFPageComposer.App.TikTok.Data;

/// <summary>
/// Versioned schema migrations for tiktok.db. Each migration runs once, in its own transaction, and is
/// recorded in SchemaVersion. Never edit a released migration; append a new one instead.
/// The schema intentionally has no recipient name, phone or address columns (AC-T07).
/// </summary>
internal static class TikTokSchemaMigrator
{
    private static readonly IReadOnlyList<(int Version, string Sql)> Migrations =
    [
        (1, """
            CREATE TABLE Shop(
                ShopId INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE COLLATE NOCASE);

            CREATE TABLE Sku(
                SkuId TEXT PRIMARY KEY NOT NULL,
                ProductName TEXT NOT NULL,
                VariationName TEXT NOT NULL,
                ProcessType TEXT NOT NULL,
                ConfigStatus TEXT NOT NULL,
                Idol TEXT NULL,
                CardCount INTEGER NULL,
                FirstSeenAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL);

            CREATE TABLE IdolAlias(
                Alias TEXT PRIMARY KEY NOT NULL,
                Idol TEXT NOT NULL);

            CREATE TABLE PdfAsset(
                FilePath TEXT PRIMARY KEY NOT NULL,
                Idol TEXT NOT NULL,
                PageCount INTEGER NOT NULL,
                FileDate TEXT NOT NULL,
                ActualPageCount INTEGER NULL,
                LastWriteTime INTEGER NOT NULL,
                IsValid INTEGER NOT NULL,
                Error TEXT NULL);

            CREATE TABLE ImportBatch(
                ImportId INTEGER PRIMARY KEY AUTOINCREMENT,
                ShopId INTEGER NOT NULL REFERENCES Shop(ShopId),
                FileName TEXT NOT NULL,
                Mode TEXT NOT NULL,
                ImportedAt TEXT NOT NULL,
                Inserted INTEGER NOT NULL,
                Updated INTEGER NOT NULL,
                AlreadyPrinted INTEGER NOT NULL,
                Skipped INTEGER NOT NULL,
                Deactivated INTEGER NOT NULL);

            CREATE TABLE "Order"(
                OrderId TEXT PRIMARY KEY NOT NULL,
                ShopId INTEGER NOT NULL REFERENCES Shop(ShopId),
                StatusText TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                BuyerUsername TEXT NULL,
                BuyerMessage TEXT NULL,
                SellerNote TEXT NULL,
                IsActive INTEGER NOT NULL,
                FirstImportId INTEGER NOT NULL,
                LastSeenImportId INTEGER NOT NULL,
                PrintedBatchId TEXT NULL,
                PrintedAt TEXT NULL,
                PrintedSource TEXT NULL,
                ChangedAfterPrint INTEGER NOT NULL DEFAULT 0,
                NoteReviewedHash TEXT NULL);

            CREATE INDEX IX_Order_Shop_Active ON "Order"(ShopId, IsActive);

            CREATE TABLE OrderLine(
                OrderId TEXT NOT NULL REFERENCES "Order"(OrderId) ON DELETE CASCADE,
                SkuId TEXT NOT NULL,
                LineNo INTEGER NOT NULL,
                ProductName TEXT NOT NULL,
                VariationName TEXT NOT NULL,
                Quantity INTEGER NOT NULL,
                PRIMARY KEY(OrderId, SkuId));

            CREATE TABLE PrintBatch(
                BatchId TEXT PRIMARY KEY NOT NULL,
                CreatedAt TEXT NOT NULL,
                OutputPath TEXT NOT NULL,
                SeparatorMode TEXT NOT NULL,
                OrderCount INTEGER NOT NULL,
                PageCount INTEGER NOT NULL,
                Status TEXT NOT NULL);

            CREATE TABLE PrintBatchItem(
                BatchId TEXT NOT NULL REFERENCES PrintBatch(BatchId) ON DELETE CASCADE,
                Seq INTEGER NOT NULL,
                OrderId TEXT NOT NULL,
                SkuId TEXT NOT NULL,
                CopyIndex INTEGER NOT NULL,
                Quantity INTEGER NOT NULL,
                PdfAssetPath TEXT NOT NULL,
                Warning TEXT NULL,
                PRIMARY KEY(BatchId, Seq));

            CREATE INDEX IX_PrintBatchItem_Order ON PrintBatchItem(OrderId);
            """),
        (2, """
            ALTER TABLE PrintBatchItem ADD COLUMN PagesUsed INTEGER NULL;
            """),
        (3, """
            ALTER TABLE "Order" ADD COLUMN SourceRow INTEGER NOT NULL DEFAULT 0;
            """),
        (4, """
            CREATE TABLE OrderFileOverride(
                OrderId TEXT NOT NULL,
                SkuId TEXT NOT NULL,
                CopyIndex INTEGER NOT NULL,
                Seq INTEGER NOT NULL,
                PdfAssetPath TEXT NOT NULL,
                PagesUsed INTEGER NOT NULL,
                PRIMARY KEY(OrderId, SkuId, CopyIndex, Seq));
            """),
        (5, """
            ALTER TABLE OrderFileOverride ADD COLUMN FirstPage INTEGER NOT NULL DEFAULT 1;
            ALTER TABLE PrintBatchItem ADD COLUMN FirstPage INTEGER NULL;
            """),
        (6, """
            ALTER TABLE OrderFileOverride ADD COLUMN FilePageCount INTEGER NULL;
            """),
    ];

    public static int LatestVersion => Migrations[^1].Version;

    /// <summary>Applies every migration newer than the stored version. Safe to call repeatedly.</summary>
    public static async Task MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using (var create = SqliteValues.CreateCommand(
            connection,
            null,
            "CREATE TABLE IF NOT EXISTS SchemaVersion(Version INTEGER PRIMARY KEY NOT NULL, AppliedAt TEXT NOT NULL);"))
        {
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var currentVersion = await GetCurrentVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        if (currentVersion > LatestVersion)
        {
            throw new InvalidOperationException(
                $"Database TikTok có phiên bản {currentVersion}, mới hơn phiên bản ứng dụng hỗ trợ ({LatestVersion}). Vui lòng cập nhật ứng dụng.");
        }

        foreach (var (version, sql) in Migrations.Where(migration => migration.Version > currentVersion))
        {
            using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            using (var migrate = SqliteValues.CreateCommand(connection, transaction, sql))
            {
                await migrate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            using (var record = SqliteValues.CreateCommand(
                connection,
                transaction,
                "INSERT INTO SchemaVersion(Version, AppliedAt) VALUES(@version, @appliedAt);",
                ("@version", version),
                ("@appliedAt", SqliteValues.ToDb(DateTimeOffset.Now))))
            {
                await record.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task<int> GetCurrentVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var query = SqliteValues.CreateCommand(connection, null, "SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion;");
        var result = await query.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
