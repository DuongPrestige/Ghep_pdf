using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Export;

/// <summary>"Xuất danh sách CSV" (PRD_TIKTOK TT-12): loads the orders of a scope and writes them as CSV.</summary>
public interface IOrderListExportService
{
    /// <summary>batchId is required for <see cref="OrderListExportScope.PrintBatch"/> and ignored otherwise.</summary>
    Task<IReadOnlyList<OrderListExportRow>> BuildRowsAsync(
        OrderListExportScope scope,
        string? batchId,
        OrderListExportContext context,
        CancellationToken cancellationToken);

    Task WriteAsync(string path, IReadOnlyList<OrderListExportRow> rows, CancellationToken cancellationToken);
}

public sealed class OrderListExportService : IOrderListExportService
{
    private readonly ITikTokRepository repository;
    private readonly OrderListExportBuilder builder;

    public OrderListExportService(ITikTokRepository repository, IOrderClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(classifier);
        this.repository = repository;
        builder = new OrderListExportBuilder(classifier);
    }

    public async Task<IReadOnlyList<OrderListExportRow>> BuildRowsAsync(
        OrderListExportScope scope,
        string? batchId,
        OrderListExportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<TikTokOrder> orders;
        long? latestImportId = null;
        switch (scope)
        {
            case OrderListExportScope.PrintBatch:
                ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
                return await BuildBatchRowsAsync(batchId, context, cancellationToken).ConfigureAwait(false);
            case OrderListExportScope.AllImported:
            case OrderListExportScope.NotPrinted:
                orders = await repository.GetOrdersAsync(includeInactive: false, cancellationToken).ConfigureAwait(false);
                break;
            case OrderListExportScope.LatestImport:
                var imports = await repository.GetImportBatchesAsync(cancellationToken).ConfigureAwait(false);
                latestImportId = imports.Count == 0 ? null : imports.Max(batch => batch.ImportId);
                orders = latestImportId is null
                    ? []
                    : await repository.GetOrdersAsync(includeInactive: true, cancellationToken).ConfigureAwait(false);
                break;
            case OrderListExportScope.PrintedOnly:
                orders = await repository.GetOrdersAsync(includeInactive: true, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
        }

        var printedIds = orders
            .Where(order => order.PrintedBatchId is not null)
            .Select(order => order.OrderId)
            .ToList();
        var printedItems = printedIds.Count == 0
            ? new Dictionary<string, IReadOnlyList<PrintBatchItemRecord>>(StringComparer.Ordinal)
            : await repository.GetPrintedItemsForOrdersAsync(printedIds, cancellationToken).ConfigureAwait(false);

        return builder.Build(
            new OrderListExportInput(scope, orders, printedItems, latestImportId),
            context);
    }

    /// <summary>
    /// Rows of one print batch: the files and print time come from that batch's own items, even when an order was
    /// later unmarked or reprinted in another batch.
    /// </summary>
    private async Task<IReadOnlyList<OrderListExportRow>> BuildBatchRowsAsync(
        string batchId,
        OrderListExportContext context,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetPrintBatchItemsAsync(batchId, cancellationToken).ConfigureAwait(false);
        var itemsByOrder = items
            .GroupBy(item => item.OrderId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PrintBatchItemRecord>)group.OrderBy(item => item.Seq).ToList(),
                StringComparer.Ordinal);
        var batchOrderIds = itemsByOrder.Keys.ToList();
        var orders = await repository.GetOrdersByIdsAsync(batchOrderIds, cancellationToken).ConfigureAwait(false);
        var batches = await repository.GetPrintBatchesAsync(cancellationToken).ConfigureAwait(false);
        var batch = batches.FirstOrDefault(record => string.Equals(record.BatchId, batchId, StringComparison.Ordinal));

        return builder.Build(
            new OrderListExportInput(
                OrderListExportScope.PrintBatch,
                orders,
                itemsByOrder,
                BatchOrderIds: batchOrderIds,
                Batch: new OrderListExportBatch(batchId, batch?.CreatedAt)),
            context);
    }

    public Task WriteAsync(string path, IReadOnlyList<OrderListExportRow> rows, CancellationToken cancellationToken) =>
        OrderListCsvWriter.WriteFileAsync(path, rows, cancellationToken);
}
