using Microsoft.Data.Sqlite;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Data;

/// <summary>Order reads and the transactional import write.</summary>
public sealed partial class SqliteTikTokRepository
{
    private const string OrderColumns =
        "OrderId, ShopId, StatusText, CreatedAt, BuyerUsername, BuyerMessage, SellerNote, IsActive, FirstImportId, " +
        "LastSeenImportId, PrintedBatchId, PrintedAt, PrintedSource, ChangedAfterPrint, NoteReviewedHash, SourceRow";

    public async Task<IReadOnlyList<TikTokOrder>> GetOrdersAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var filter = includeInactive ? "1 = 1" : "IsActive = 1";
        return await LoadOrdersAsync(connection, filter, [], cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TikTokOrder>> GetOrdersByIdsAsync(
        IReadOnlyCollection<string> orderIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderIds);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var orders = new List<TikTokOrder>();
        foreach (var chunk in SqliteValues.DistinctChunks(orderIds))
        {
            var (inList, parameters) = SqliteValues.BuildInList(chunk);
            var filter = "OrderId IN " + inList;
            orders.AddRange(await LoadOrdersAsync(connection, filter, parameters, cancellationToken).ConfigureAwait(false));
        }

        return SortOrders(orders);
    }

    public async Task<IReadOnlyList<TikTokOrder>> GetActiveOrdersForShopAsync(long shopId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await LoadOrdersAsync(
            connection,
            "IsActive = 1 AND ShopId = @shopId",
            [("@shopId", shopId)],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ImportBatchRecord> ApplyImportAsync(ImportChangeSet changeSet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changeSet);

        var batch = new ImportBatchRecord(
            0,
            changeSet.ShopId,
            changeSet.FileName,
            changeSet.Mode,
            changeSet.ImportedAt,
            changeSet.OrdersToInsert.Count,
            changeSet.OrdersToUpdate.Count,
            changeSet.AlreadyPrinted,
            changeSet.Skipped,
            changeSet.OrderIdsToDeactivate.Count);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await BeginTransactionAsync(connection, cancellationToken).ConfigureAwait(false);

        var importId = await InsertImportBatchAsync(connection, transaction, batch, cancellationToken).ConfigureAwait(false);

        foreach (var order in changeSet.OrdersToInsert)
        {
            await InsertOrderAsync(connection, transaction, order, importId, cancellationToken).ConfigureAwait(false);
        }

        foreach (var order in changeSet.OrdersToUpdate)
        {
            await UpdateOrderAsync(connection, transaction, order, importId, cancellationToken).ConfigureAwait(false);
        }

        foreach (var orderId in changeSet.OrderIdsToDeactivate)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE \"Order\" SET IsActive = 0 WHERE OrderId = @orderId;",
                cancellationToken,
                ("@orderId", orderId)).ConfigureAwait(false);
        }

        foreach (var sku in changeSet.NewSkus)
        {
            await WriteSkuAsync(connection, transaction, sku, "INSERT OR IGNORE", cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return batch with { ImportId = importId };
    }

    public async Task<IReadOnlyList<ImportBatchRecord>> GetImportBatchesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = SqliteValues.CreateCommand(
            connection,
            null,
            """
            SELECT ImportId, ShopId, FileName, Mode, ImportedAt, Inserted, Updated, AlreadyPrinted, Skipped, Deactivated
            FROM ImportBatch ORDER BY ImportId DESC;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var batches = new List<ImportBatchRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            batches.Add(new ImportBatchRecord(
                reader.GetInt64("ImportId"),
                reader.GetInt64("ShopId"),
                reader.GetString("FileName"),
                reader.GetEnum<ImportMode>("Mode"),
                reader.GetDateTimeOffset("ImportedAt"),
                reader.GetInt32("Inserted"),
                reader.GetInt32("Updated"),
                reader.GetInt32("AlreadyPrinted"),
                reader.GetInt32("Skipped"),
                reader.GetInt32("Deactivated")));
        }

        return batches;
    }

    public async Task<int> DeleteImportAsync(long importId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await BeginTransactionAsync(connection, cancellationToken).ConfigureAwait(false);

        var parameter = ("@importId", (object?)importId);
        await ExecuteAsync(
            connection,
            transaction,
            """
            DELETE FROM OrderLine WHERE OrderId IN
                (SELECT OrderId FROM "Order" WHERE FirstImportId = @importId AND LastSeenImportId = @importId);
            DELETE FROM OrderFileOverride WHERE OrderId IN
                (SELECT OrderId FROM "Order" WHERE FirstImportId = @importId AND LastSeenImportId = @importId);
            """,
            cancellationToken,
            parameter).ConfigureAwait(false);
        var deleted = await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM \"Order\" WHERE FirstImportId = @importId AND LastSeenImportId = @importId;",
            cancellationToken,
            parameter).ConfigureAwait(false);

        // Kept orders were also seen in another import; point them at it instead of the deleted batch.
        await ExecuteAsync(
            connection,
            transaction,
            """
            UPDATE "Order" SET FirstImportId = LastSeenImportId WHERE FirstImportId = @importId;
            UPDATE "Order" SET LastSeenImportId = FirstImportId WHERE LastSeenImportId = @importId;
            """,
            cancellationToken,
            parameter).ConfigureAwait(false);
        var batches = await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM ImportBatch WHERE ImportId = @importId;",
            cancellationToken,
            parameter).ConfigureAwait(false);
        if (batches == 0)
        {
            throw new InvalidOperationException($"Không tìm thấy lần import #{importId}.");
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    public async Task<int> DeleteAllOrdersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await BeginTransactionAsync(connection, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM OrderLine;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM OrderFileOverride;", cancellationToken).ConfigureAwait(false);
        var deleted = await ExecuteAsync(connection, transaction, "DELETE FROM \"Order\";", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "DELETE FROM ImportBatch;", cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    private static async Task<long> InsertImportBatchAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ImportBatchRecord batch,
        CancellationToken cancellationToken)
    {
        using var command = SqliteValues.CreateCommand(
            connection,
            transaction,
            """
            INSERT INTO ImportBatch(ShopId, FileName, Mode, ImportedAt, Inserted, Updated, AlreadyPrinted, Skipped, Deactivated)
            VALUES(@shopId, @fileName, @mode, @importedAt, @inserted, @updated, @alreadyPrinted, @skipped, @deactivated)
            RETURNING ImportId;
            """,
            ("@shopId", batch.ShopId),
            ("@fileName", batch.FileName),
            ("@mode", SqliteValues.ToDb(batch.Mode)),
            ("@importedAt", SqliteValues.ToDb(batch.ImportedAt)),
            ("@inserted", batch.Inserted),
            ("@updated", batch.Updated),
            ("@alreadyPrinted", batch.AlreadyPrinted),
            ("@skipped", batch.Skipped),
            ("@deactivated", batch.Deactivated));
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task InsertOrderAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TikTokOrder order,
        long importId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        await ExecuteAsync(
            connection,
            transaction,
            $"""
            INSERT INTO "Order"({OrderColumns})
            VALUES(@orderId, @shopId, @statusText, @createdAt, @buyerUsername, @buyerMessage, @sellerNote, @isActive,
                   @importId, @importId, @printedBatchId, @printedAt, @printedSource, @changedAfterPrint, @noteReviewedHash, @sourceRow);
            """,
            cancellationToken,
            OrderParameters(order, importId)).ConfigureAwait(false);
        await InsertOrderLinesAsync(connection, transaction, order, cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpdateOrderAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TikTokOrder order,
        long importId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        var affected = await ExecuteAsync(
            connection,
            transaction,
            // The order was read before this transaction. Print and review columns may have changed since then
            // (print batch commit, "Đã xem", unmark, another app instance), so they are never written back from
            // that snapshot: PrintedBatchId and NoteReviewedHash are left alone (the merger never changes them),
            // the Seller Note print mark applies only while the stored row has no batch, and ChangedAfterPrint
            // is only ever raised for the batch the change was computed against.
            """
            UPDATE "Order" SET
                ShopId = @shopId, StatusText = @statusText, CreatedAt = @createdAt, BuyerUsername = @buyerUsername,
                BuyerMessage = @buyerMessage, SellerNote = @sellerNote, IsActive = @isActive, LastSeenImportId = @importId,
                SourceRow = @sourceRow,
                PrintedAt = CASE WHEN PrintedBatchId IS NULL AND @printedBatchId IS NULL THEN @printedAt ELSE PrintedAt END,
                PrintedSource = CASE WHEN PrintedBatchId IS NULL AND @printedBatchId IS NULL THEN @printedSource ELSE PrintedSource END,
                ChangedAfterPrint = CASE
                    WHEN PrintedBatchId IS NOT NULL AND PrintedBatchId = @printedBatchId
                        THEN MAX(ChangedAfterPrint, @changedAfterPrint)
                    ELSE ChangedAfterPrint END
            WHERE OrderId = @orderId;
            """,
            cancellationToken,
            OrderParameters(order, importId)).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new InvalidOperationException($"Không tìm thấy đơn {order.OrderId} để cập nhật.");
        }

        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM OrderLine WHERE OrderId = @orderId;",
            cancellationToken,
            ("@orderId", order.OrderId)).ConfigureAwait(false);
        await InsertOrderLinesAsync(connection, transaction, order, cancellationToken).ConfigureAwait(false);
    }

    private static (string Name, object? Value)[] OrderParameters(TikTokOrder order, long importId) =>
    [
        ("@orderId", order.OrderId),
        ("@shopId", order.ShopId),
        ("@statusText", order.StatusText),
        ("@createdAt", SqliteValues.ToDb(order.CreatedAt)),
        ("@buyerUsername", order.BuyerUsername),
        ("@buyerMessage", order.BuyerMessage),
        ("@sellerNote", order.SellerNote),
        ("@isActive", SqliteValues.ToDb(order.IsActive)),
        ("@importId", importId),
        ("@printedBatchId", order.PrintedBatchId),
        ("@printedAt", order.PrintedAt is { } printedAt ? SqliteValues.ToDb(printedAt) : null),
        ("@printedSource", order.PrintedSource is { } source ? SqliteValues.ToDb(source) : null),
        ("@changedAfterPrint", SqliteValues.ToDb(order.ChangedAfterPrint)),
        ("@noteReviewedHash", order.NoteReviewedHash),
        ("@sourceRow", order.SourceRow),
    ];

    private static async Task InsertOrderLinesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TikTokOrder order,
        CancellationToken cancellationToken)
    {
        foreach (var line in order.Lines)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO OrderLine(OrderId, SkuId, LineNo, ProductName, VariationName, Quantity)
                VALUES(@orderId, @skuId, @lineNo, @productName, @variationName, @quantity);
                """,
                cancellationToken,
                ("@orderId", order.OrderId),
                ("@skuId", line.SkuId),
                ("@lineNo", line.LineNo),
                ("@productName", line.ProductName),
                ("@variationName", line.VariationName),
                ("@quantity", line.Quantity)).ConfigureAwait(false);
        }
    }

    /// <summary>Loads orders matching filter (a WHERE clause on "Order") with their lines.</summary>
    private static async Task<IReadOnlyList<TikTokOrder>> LoadOrdersAsync(
        SqliteConnection connection,
        string filter,
        (string Name, object? Value)[] parameters,
        CancellationToken cancellationToken)
    {
        var lines = await LoadOrderLinesAsync(connection, filter, parameters, cancellationToken).ConfigureAwait(false);

        using var command = SqliteValues.CreateCommand(
            connection,
            null,
            $"SELECT {OrderColumns} FROM \"Order\" WHERE {filter};",
            parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var orders = new List<TikTokOrder>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var orderId = reader.GetString("OrderId");
            orders.Add(new TikTokOrder(
                orderId,
                reader.GetInt64("ShopId"),
                reader.GetString("StatusText"),
                reader.GetDateTimeOffset("CreatedAt"),
                reader.GetNullableString("BuyerUsername"),
                reader.GetNullableString("BuyerMessage"),
                reader.GetNullableString("SellerNote"),
                reader.GetBoolean("IsActive"),
                reader.GetInt64("FirstImportId"),
                reader.GetInt64("LastSeenImportId"),
                reader.GetNullableString("PrintedBatchId"),
                reader.GetNullableDateTimeOffset("PrintedAt"),
                reader.GetNullableEnum<PrintedSource>("PrintedSource"),
                reader.GetBoolean("ChangedAfterPrint"),
                reader.GetNullableString("NoteReviewedHash"),
                lines.TryGetValue(orderId, out var orderLines) ? orderLines : [],
                reader.GetInt32("SourceRow")));
        }

        return SortOrders(orders);
    }

    private static async Task<Dictionary<string, List<TikTokOrderLine>>> LoadOrderLinesAsync(
        SqliteConnection connection,
        string orderFilter,
        (string Name, object? Value)[] parameters,
        CancellationToken cancellationToken)
    {
        using var command = SqliteValues.CreateCommand(
            connection,
            null,
            $"""
            SELECT OrderId, SkuId, LineNo, ProductName, VariationName, Quantity FROM OrderLine
            WHERE OrderId IN (SELECT OrderId FROM "Order" WHERE {orderFilter})
            ORDER BY OrderId, LineNo;
            """,
            parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var lines = new Dictionary<string, List<TikTokOrderLine>>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var line = new TikTokOrderLine(
                reader.GetString("OrderId"),
                reader.GetString("SkuId"),
                reader.GetInt32("LineNo"),
                reader.GetString("ProductName"),
                reader.GetString("VariationName"),
                reader.GetInt32("Quantity"));
            if (!lines.TryGetValue(line.OrderId, out var list))
            {
                list = [];
                lines.Add(line.OrderId, list);
            }

            list.Add(line);
        }

        return lines;
    }

    /// <summary>
    /// CSV file order (TikTokOrderOrdering). Sorted in memory because CreatedAt, the tie-break, is stored as
    /// round-trip text with its offset; text order is not instant order when offsets differ.
    /// </summary>
    private static List<TikTokOrder> SortOrders(IEnumerable<TikTokOrder> orders) =>
        TikTokOrderOrdering.InFileOrder(orders).ToList();
}
