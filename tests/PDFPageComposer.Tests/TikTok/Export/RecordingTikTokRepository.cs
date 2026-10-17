using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.Tests.TikTok.Export;

/// <summary>Records CommitPrintBatchAsync calls; every other member is unused by the exporter.</summary>
internal sealed class RecordingTikTokRepository : ITikTokRepository
{
    public List<CommitCall> Commits { get; } = [];

    public Exception? CommitException { get; set; }

    public Task CommitPrintBatchAsync(
        PrintBatchRecord batch,
        IReadOnlyList<PrintBatchItemRecord> items,
        IReadOnlyCollection<string> orderIds,
        CancellationToken cancellationToken)
    {
        if (CommitException is not null)
        {
            throw CommitException;
        }

        Commits.Add(new CommitCall(batch, items.ToList(), orderIds.ToList()));
        return Task.CompletedTask;
    }

    public Task InitializeAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<TikTokShop>> GetShopsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<TikTokShop> AddShopAsync(string name, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task RenameShopAsync(long shopId, string name, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<SkuConfig>> GetSkusAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task SaveSkusAsync(IReadOnlyCollection<SkuConfig> skus, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<IdolAlias>> GetIdolAliasesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task SaveIdolAliasAsync(IdolAlias alias, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task DeleteIdolAliasAsync(string alias, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<PdfAsset>> GetPdfAssetsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task ReplacePdfAssetsAsync(IReadOnlyCollection<PdfAsset> assets, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<TikTokOrder>> GetOrdersAsync(bool includeInactive, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<TikTokOrder>> GetOrdersByIdsAsync(IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<TikTokOrder>> GetActiveOrdersForShopAsync(long shopId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyDictionary<string, IReadOnlyList<PrintBatchItemRecord>>> GetPrintedItemsForOrdersAsync(
        IReadOnlyCollection<string> orderIds,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ImportBatchRecord> ApplyImportAsync(ImportChangeSet changeSet, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<ImportBatchRecord>> GetImportBatchesAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task SetNoteReviewedAsync(string orderId, string? noteHash, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task UnmarkPrintedAsync(string orderId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<int> DeleteImportAsync(long importId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<int> DeleteAllOrdersAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<FileOverride>> GetFileOverridesAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task SaveFileOverridesAsync(string orderId, IReadOnlyCollection<FileOverride> overrides, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<PrintBatchRecord>> GetPrintBatchesAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<PrintBatchItemRecord>> GetPrintBatchItemsAsync(string batchId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public sealed record CommitCall(
        PrintBatchRecord Batch,
        IReadOnlyList<PrintBatchItemRecord> Items,
        IReadOnlyList<string> OrderIds);
}
