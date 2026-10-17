using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.Tests.TikTok.Import;

/// <summary>In-memory ITikTokRepository that mirrors the SQLite ApplyImportAsync semantics.</summary>
internal sealed class InMemoryTikTokRepository : ITikTokRepository
{
    private readonly Dictionary<string, TikTokOrder> _orders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SkuConfig> _skus = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IdolAlias> _aliases = new(StringComparer.Ordinal);
    private readonly List<ImportBatchRecord> _imports = [];
    private readonly List<PrintBatchRecord> _printBatches = [];
    private readonly List<PrintBatchItemRecord> _printItems = [];
    private readonly List<TikTokShop> _shops = [];
    private List<PdfAsset> _assets = [];

    public List<ImportChangeSet> AppliedChangeSets { get; } = [];

    public IReadOnlyDictionary<string, TikTokOrder> Orders => _orders;

    public IReadOnlyDictionary<string, SkuConfig> Skus => _skus;

    public void SeedOrder(TikTokOrder order) => _orders[order.OrderId] = order;

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<TikTokShop>> GetShopsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TikTokShop>>(_shops.ToArray());

    public Task<TikTokShop> AddShopAsync(string name, CancellationToken cancellationToken)
    {
        var shop = new TikTokShop(_shops.Count + 1, name);
        _shops.Add(shop);
        return Task.FromResult(shop);
    }

    public Task RenameShopAsync(long shopId, string name, CancellationToken cancellationToken)
    {
        var index = _shops.FindIndex(shop => shop.ShopId == shopId);
        _shops[index] = _shops[index] with { Name = name };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SkuConfig>> GetSkusAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SkuConfig>>(_skus.Values.ToArray());

    public Task SaveSkusAsync(IReadOnlyCollection<SkuConfig> skus, CancellationToken cancellationToken)
    {
        foreach (var sku in skus)
        {
            _skus[sku.SkuId] = sku;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IdolAlias>> GetIdolAliasesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<IdolAlias>>(_aliases.Values.ToArray());

    public Task SaveIdolAliasAsync(IdolAlias alias, CancellationToken cancellationToken)
    {
        _aliases[alias.Alias] = alias;
        return Task.CompletedTask;
    }

    public Task DeleteIdolAliasAsync(string alias, CancellationToken cancellationToken)
    {
        _aliases.Remove(alias);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PdfAsset>> GetPdfAssetsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PdfAsset>>(_assets.ToArray());

    public Task ReplacePdfAssetsAsync(IReadOnlyCollection<PdfAsset> assets, CancellationToken cancellationToken)
    {
        _assets = [.. assets];
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TikTokOrder>> GetOrdersAsync(bool includeInactive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TikTokOrder>>(_orders.Values
            .Where(order => includeInactive || order.IsActive)
            .OrderBy(order => order.CreatedAt)
            .ThenBy(order => order.OrderId, StringComparer.Ordinal)
            .ToArray());

    public Task<IReadOnlyList<TikTokOrder>> GetOrdersByIdsAsync(
        IReadOnlyCollection<string> orderIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TikTokOrder>>(orderIds
            .Where(_orders.ContainsKey)
            .Select(orderId => _orders[orderId])
            .ToArray());

    public Task<IReadOnlyList<TikTokOrder>> GetActiveOrdersForShopAsync(long shopId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TikTokOrder>>(_orders.Values
            .Where(order => order.ShopId == shopId && order.IsActive)
            .ToArray());

    public Task<IReadOnlyDictionary<string, IReadOnlyList<PrintBatchItemRecord>>> GetPrintedItemsForOrdersAsync(
        IReadOnlyCollection<string> orderIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, IReadOnlyList<PrintBatchItemRecord>>(StringComparer.Ordinal);
        foreach (var orderId in orderIds)
        {
            if (_orders.TryGetValue(orderId, out var order) && order.PrintedBatchId is { } batchId)
            {
                result[orderId] = _printItems
                    .Where(item => item.BatchId == batchId && item.OrderId == orderId)
                    .ToArray();
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<PrintBatchItemRecord>>>(result);
    }

    public Task<ImportBatchRecord> ApplyImportAsync(ImportChangeSet changeSet, CancellationToken cancellationToken)
    {
        AppliedChangeSets.Add(changeSet);
        var importId = _imports.Count + 1L;

        foreach (var order in changeSet.OrdersToInsert)
        {
            if (_orders.ContainsKey(order.OrderId))
            {
                throw new InvalidOperationException($"Duplicate primary key {order.OrderId}.");
            }

            _orders.Add(order.OrderId, order with { FirstImportId = importId, LastSeenImportId = importId });
        }

        foreach (var order in changeSet.OrdersToUpdate)
        {
            var stored = _orders[order.OrderId];
            _orders[order.OrderId] = order with { FirstImportId = stored.FirstImportId, LastSeenImportId = importId };
        }

        foreach (var orderId in changeSet.OrderIdsToDeactivate)
        {
            _orders[orderId] = _orders[orderId] with { IsActive = false };
        }

        foreach (var sku in changeSet.NewSkus)
        {
            _skus.TryAdd(sku.SkuId, sku);
        }

        var batch = new ImportBatchRecord(
            importId,
            changeSet.ShopId,
            changeSet.FileName,
            changeSet.Mode,
            changeSet.ImportedAt,
            changeSet.OrdersToInsert.Count,
            changeSet.OrdersToUpdate.Count,
            changeSet.AlreadyPrinted,
            changeSet.Skipped,
            changeSet.OrderIdsToDeactivate.Count);
        _imports.Add(batch);
        return Task.FromResult(batch);
    }

    public Task<IReadOnlyList<ImportBatchRecord>> GetImportBatchesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ImportBatchRecord>>(_imports.ToArray());

    public Task SetNoteReviewedAsync(string orderId, string? noteHash, CancellationToken cancellationToken)
    {
        _orders[orderId] = _orders[orderId] with { NoteReviewedHash = noteHash };
        return Task.CompletedTask;
    }

    public Task<int> DeleteImportAsync(long importId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<int> DeleteAllOrdersAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<FileOverride>> GetFileOverridesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FileOverride>>([]);

    public Task SaveFileOverridesAsync(string orderId, IReadOnlyCollection<FileOverride> overrides, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task UnmarkPrintedAsync(string orderId, CancellationToken cancellationToken)
    {
        _orders[orderId] = _orders[orderId] with
        {
            PrintedBatchId = null,
            PrintedAt = null,
            PrintedSource = null,
            ChangedAfterPrint = false,
        };
        return Task.CompletedTask;
    }

    public Task CommitPrintBatchAsync(
        PrintBatchRecord batch,
        IReadOnlyList<PrintBatchItemRecord> items,
        IReadOnlyCollection<string> orderIds,
        CancellationToken cancellationToken)
    {
        _printBatches.Add(batch);
        _printItems.AddRange(items);
        foreach (var orderId in orderIds)
        {
            _orders[orderId] = _orders[orderId] with
            {
                PrintedBatchId = batch.BatchId,
                PrintedAt = batch.CreatedAt,
                PrintedSource = PrintedSource.Batch,
                ChangedAfterPrint = false,
            };
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PrintBatchRecord>> GetPrintBatchesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PrintBatchRecord>>(_printBatches.ToArray());

    public Task<IReadOnlyList<PrintBatchItemRecord>> GetPrintBatchItemsAsync(string batchId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PrintBatchItemRecord>>(_printItems.Where(item => item.BatchId == batchId).ToArray());
}
