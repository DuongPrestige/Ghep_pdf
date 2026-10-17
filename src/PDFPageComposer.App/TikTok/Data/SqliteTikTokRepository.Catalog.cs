using Microsoft.Data.Sqlite;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Data;

/// <summary>SKU configuration, idol aliases and the PDF library table.</summary>
public sealed partial class SqliteTikTokRepository
{
    private const string SkuColumns =
        "SkuId, ProductName, VariationName, ProcessType, ConfigStatus, Idol, CardCount, FirstSeenAt, UpdatedAt";

    private const string SkuValues =
        "@skuId, @productName, @variationName, @processType, @configStatus, @idol, @cardCount, @firstSeenAt, @updatedAt";

    public async Task<IReadOnlyList<SkuConfig>> GetSkusAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = SqliteValues.CreateCommand(connection, null, $"SELECT {SkuColumns} FROM Sku ORDER BY SkuId;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var skus = new List<SkuConfig>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            skus.Add(new SkuConfig(
                reader.GetString("SkuId"),
                reader.GetString("ProductName"),
                reader.GetString("VariationName"),
                reader.GetEnum<SkuProcessType>("ProcessType"),
                reader.GetEnum<SkuConfigStatus>("ConfigStatus"),
                reader.GetNullableString("Idol"),
                reader.GetNullableInt32("CardCount"),
                reader.GetDateTimeOffset("FirstSeenAt"),
                reader.GetDateTimeOffset("UpdatedAt")));
        }

        return skus;
    }

    public async Task SaveSkusAsync(IReadOnlyCollection<SkuConfig> skus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skus);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await BeginTransactionAsync(connection, cancellationToken).ConfigureAwait(false);
        foreach (var sku in skus)
        {
            await WriteSkuAsync(connection, transaction, sku, "INSERT OR REPLACE", cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<IdolAlias>> GetIdolAliasesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = SqliteValues.CreateCommand(connection, null, "SELECT Alias, Idol FROM IdolAlias ORDER BY Alias;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var aliases = new List<IdolAlias>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            aliases.Add(new IdolAlias(reader.GetString("Alias"), reader.GetString("Idol")));
        }

        return aliases;
    }

    public async Task SaveIdolAliasAsync(IdolAlias alias, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alias);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            null,
            "INSERT INTO IdolAlias(Alias, Idol) VALUES(@alias, @idol) ON CONFLICT(Alias) DO UPDATE SET Idol = excluded.Idol;",
            cancellationToken,
            ("@alias", alias.Alias),
            ("@idol", alias.Idol)).ConfigureAwait(false);
    }

    public async Task DeleteIdolAliasAsync(string alias, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alias);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, null, "DELETE FROM IdolAlias WHERE Alias = @alias;", cancellationToken, ("@alias", alias))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PdfAsset>> GetPdfAssetsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = SqliteValues.CreateCommand(
            connection,
            null,
            "SELECT FilePath, Idol, PageCount, FileDate, ActualPageCount, LastWriteTime, IsValid, Error FROM PdfAsset ORDER BY FilePath;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var assets = new List<PdfAsset>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            assets.Add(new PdfAsset(
                reader.GetString("FilePath"),
                reader.GetString("Idol"),
                reader.GetInt32("PageCount"),
                SqliteValues.ParseDateOnly(reader.GetString("FileDate")),
                reader.GetNullableInt32("ActualPageCount"),
                SqliteValues.ParseUtcTicks(reader.GetInt64("LastWriteTime")),
                reader.GetBoolean("IsValid"),
                reader.GetNullableString("Error")));
        }

        return assets;
    }

    public async Task ReplacePdfAssetsAsync(IReadOnlyCollection<PdfAsset> assets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assets);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await BeginTransactionAsync(connection, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM PdfAsset;", cancellationToken).ConfigureAwait(false);
        foreach (var asset in assets)
        {
            ArgumentNullException.ThrowIfNull(asset);
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO PdfAsset(FilePath, Idol, PageCount, FileDate, ActualPageCount, LastWriteTime, IsValid, Error)
                VALUES(@filePath, @idol, @pageCount, @fileDate, @actualPageCount, @lastWriteTime, @isValid, @error);
                """,
                cancellationToken,
                ("@filePath", asset.FilePath),
                ("@idol", asset.Idol),
                ("@pageCount", asset.PageCount),
                ("@fileDate", SqliteValues.ToDb(asset.FileDate)),
                ("@actualPageCount", asset.ActualPageCount),
                ("@lastWriteTime", SqliteValues.ToDb(asset.LastWriteTimeUtc)),
                ("@isValid", SqliteValues.ToDb(asset.IsValid)),
                ("@error", asset.Error)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <param name="insertVerb">"INSERT OR REPLACE" for user saves, "INSERT OR IGNORE" for SKUs discovered by an import.</param>
    private static Task<int> WriteSkuAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SkuConfig sku,
        string insertVerb,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sku);
        return ExecuteAsync(
            connection,
            transaction,
            $"{insertVerb} INTO Sku({SkuColumns}) VALUES({SkuValues});",
            cancellationToken,
            ("@skuId", sku.SkuId),
            ("@productName", sku.ProductName),
            ("@variationName", sku.VariationName),
            ("@processType", SqliteValues.ToDb(sku.ProcessType)),
            ("@configStatus", SqliteValues.ToDb(sku.ConfigStatus)),
            ("@idol", sku.Idol),
            ("@cardCount", sku.CardCount),
            ("@firstSeenAt", SqliteValues.ToDb(sku.FirstSeenAt)),
            ("@updatedAt", SqliteValues.ToDb(sku.UpdatedAt)));
    }
}
