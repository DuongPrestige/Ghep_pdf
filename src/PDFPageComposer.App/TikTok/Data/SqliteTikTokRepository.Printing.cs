using Microsoft.Data.Sqlite;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Data;

/// <summary>Print history, printed-state changes and the "Đã xem – cho phép ghép" flag.</summary>
public sealed partial class SqliteTikTokRepository
{
    private const string PrintBatchItemColumns = "BatchId, Seq, OrderId, SkuId, CopyIndex, Quantity, PdfAssetPath, Warning, PagesUsed, FirstPage";

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<PrintBatchItemRecord>>> GetPrintedItemsForOrdersAsync(
        IReadOnlyCollection<string> orderIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderIds);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<string, IReadOnlyList<PrintBatchItemRecord>>(StringComparer.Ordinal);
        foreach (var chunk in SqliteValues.DistinctChunks(orderIds))
        {
            var (inList, parameters) = SqliteValues.BuildInList(chunk);

            var printedOrders = new List<string>();
            using (var orders = SqliteValues.CreateCommand(
                connection,
                null,
                $"SELECT OrderId FROM \"Order\" WHERE PrintedBatchId IS NOT NULL AND OrderId IN {inList};",
                parameters))
            {
                await using var reader = await orders.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    printedOrders.Add(reader.GetString("OrderId"));
                }
            }

            var items = new Dictionary<string, List<PrintBatchItemRecord>>(StringComparer.Ordinal);
            using (var query = SqliteValues.CreateCommand(
                connection,
                null,
                $"""
                SELECT i.BatchId, i.Seq, i.OrderId, i.SkuId, i.CopyIndex, i.Quantity, i.PdfAssetPath, i.Warning, i.PagesUsed, i.FirstPage
                FROM PrintBatchItem i
                JOIN "Order" o ON o.OrderId = i.OrderId AND o.PrintedBatchId = i.BatchId
                WHERE o.OrderId IN {inList}
                ORDER BY i.OrderId, i.Seq;
                """,
                parameters))
            {
                await using var reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var item = ReadPrintBatchItem(reader);
                    if (!items.TryGetValue(item.OrderId, out var list))
                    {
                        list = [];
                        items.Add(item.OrderId, list);
                    }

                    list.Add(item);
                }
            }

            foreach (var orderId in printedOrders)
            {
                result[orderId] = items.TryGetValue(orderId, out var list) ? list : [];
            }
        }

        return result;
    }

    public async Task SetNoteReviewedAsync(string orderId, string? noteHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderId);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await ExecuteAsync(
            connection,
            null,
            "UPDATE \"Order\" SET NoteReviewedHash = @hash WHERE OrderId = @orderId;",
            cancellationToken,
            ("@hash", noteHash),
            ("@orderId", orderId)).ConfigureAwait(false);
        EnsureOrderFound(affected, orderId);
    }

    public async Task UnmarkPrintedAsync(string orderId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderId);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await ExecuteAsync(
            connection,
            null,
            """
            UPDATE "Order"
            SET PrintedBatchId = NULL, PrintedAt = NULL, PrintedSource = NULL, ChangedAfterPrint = 0
            WHERE OrderId = @orderId;
            """,
            cancellationToken,
            ("@orderId", orderId)).ConfigureAwait(false);
        EnsureOrderFound(affected, orderId);
    }

    public async Task CommitPrintBatchAsync(
        PrintBatchRecord batch,
        IReadOnlyList<PrintBatchItemRecord> items,
        IReadOnlyCollection<string> orderIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(orderIds);
        if (items.Any(item => !string.Equals(item.BatchId, batch.BatchId, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Mọi dòng của lô in phải có cùng BatchId với lô.", nameof(items));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await BeginTransactionAsync(connection, cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO PrintBatch(BatchId, CreatedAt, OutputPath, SeparatorMode, OrderCount, PageCount, Status)
            VALUES(@batchId, @createdAt, @outputPath, @separatorMode, @orderCount, @pageCount, @status);
            """,
            cancellationToken,
            ("@batchId", batch.BatchId),
            ("@createdAt", SqliteValues.ToDb(batch.CreatedAt)),
            ("@outputPath", batch.OutputPath),
            ("@separatorMode", SqliteValues.ToDb(batch.SeparatorMode)),
            ("@orderCount", batch.OrderCount),
            ("@pageCount", batch.PageCount),
            ("@status", batch.Status)).ConfigureAwait(false);

        foreach (var item in items)
        {
            await ExecuteAsync(
                connection,
                transaction,
                $"""
                INSERT INTO PrintBatchItem({PrintBatchItemColumns})
                VALUES(@batchId, @seq, @orderId, @skuId, @copyIndex, @quantity, @pdfAssetPath, @warning, @pagesUsed, @firstPage);
                """,
                cancellationToken,
                ("@batchId", item.BatchId),
                ("@seq", item.Seq),
                ("@orderId", item.OrderId),
                ("@skuId", item.SkuId),
                ("@copyIndex", item.CopyIndex),
                ("@quantity", item.Quantity),
                ("@pdfAssetPath", item.PdfAssetPath),
                ("@warning", item.Warning),
                ("@pagesUsed", item.PagesUsed),
                ("@firstPage", item.FirstPage)).ConfigureAwait(false);
        }

        // ChangedAfterPrint is recomputed against the current lines (same rule as OrderImportMerger): a normal batch
        // built from the current lines gives 0, a "Tạo lại" replay of outdated SKUs or quantities keeps the warning.
        foreach (var orderId in orderIds.Distinct(StringComparer.Ordinal))
        {
            var affected = await ExecuteAsync(
                connection,
                transaction,
                """
                UPDATE "Order"
                SET PrintedBatchId = @batchId, PrintedAt = @printedAt, PrintedSource = @printedSource,
                    ChangedAfterPrint = EXISTS (
                        SELECT 1 FROM PrintBatchItem p
                        WHERE p.BatchId = @batchId AND p.OrderId = @orderId
                          AND NOT EXISTS (
                              SELECT 1 FROM OrderLine l
                              WHERE l.OrderId = p.OrderId AND l.SkuId = p.SkuId AND l.Quantity = p.Quantity))
                WHERE OrderId = @orderId;
                """,
                cancellationToken,
                ("@batchId", batch.BatchId),
                ("@printedAt", SqliteValues.ToDb(batch.CreatedAt)),
                ("@printedSource", SqliteValues.ToDb(PrintedSource.Batch)),
                ("@orderId", orderId)).ConfigureAwait(false);
            EnsureOrderFound(affected, orderId);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PrintBatchRecord>> GetPrintBatchesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = SqliteValues.CreateCommand(
            connection,
            null,
            "SELECT BatchId, CreatedAt, OutputPath, SeparatorMode, OrderCount, PageCount, Status FROM PrintBatch;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var batches = new List<PrintBatchRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            batches.Add(new PrintBatchRecord(
                reader.GetString("BatchId"),
                reader.GetDateTimeOffset("CreatedAt"),
                reader.GetString("OutputPath"),
                reader.GetEnum<SeparatorMode>("SeparatorMode"),
                reader.GetInt32("OrderCount"),
                reader.GetInt32("PageCount"),
                reader.GetString("Status")));
        }

        // Newest first; sorted in memory because CreatedAt text keeps its offset.
        return batches
            .OrderByDescending(batch => batch.CreatedAt)
            .ThenByDescending(batch => batch.BatchId, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<PrintBatchItemRecord>> GetPrintBatchItemsAsync(string batchId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batchId);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = SqliteValues.CreateCommand(
            connection,
            null,
            $"SELECT {PrintBatchItemColumns} FROM PrintBatchItem WHERE BatchId = @batchId ORDER BY Seq;",
            ("@batchId", batchId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<PrintBatchItemRecord>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(ReadPrintBatchItem(reader));
        }

        return items;
    }

    private static PrintBatchItemRecord ReadPrintBatchItem(SqliteDataReader reader) =>
        new(
            reader.GetString("BatchId"),
            reader.GetInt32("Seq"),
            reader.GetString("OrderId"),
            reader.GetString("SkuId"),
            reader.GetInt32("CopyIndex"),
            reader.GetInt32("Quantity"),
            reader.GetString("PdfAssetPath"),
            reader.GetNullableString("Warning"),
            reader.IsDBNull(reader.GetOrdinal("PagesUsed")) ? null : reader.GetInt32(reader.GetOrdinal("PagesUsed")),
            reader.IsDBNull(reader.GetOrdinal("FirstPage")) ? null : reader.GetInt32(reader.GetOrdinal("FirstPage")));

    private static void EnsureOrderFound(int affectedRows, string orderId)
    {
        if (affectedRows == 0)
        {
            throw new InvalidOperationException($"Không tìm thấy đơn {orderId}.");
        }
    }
}
