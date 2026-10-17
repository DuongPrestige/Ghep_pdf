using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok;

/// <summary>A source of order lines: Seller Center CSV today, a browser extension later.</summary>
public interface IOrderSource
{
    string Name { get; }

    Task<OrderSourceResult> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>Imports order lines with de-duplication (PRD_TIKTOK TT-09, TT-10). Idempotent.</summary>
public interface IOrderImportService
{
    Task<ImportReport> ImportAsync(
        long shopId,
        string sourceName,
        ImportMode mode,
        IReadOnlyList<ImportedOrderLine> lines,
        CancellationToken cancellationToken);

    /// <summary>
    /// Same as the overload above; protectedOrderIds (orders whose rows the source rejected) are never
    /// deactivated by a Full import, because they are still in the export.
    /// </summary>
    Task<ImportReport> ImportAsync(
        long shopId,
        string sourceName,
        ImportMode mode,
        IReadOnlyList<ImportedOrderLine> lines,
        IReadOnlyCollection<string> protectedOrderIds,
        CancellationToken cancellationToken);
}

/// <summary>SQLite persistence for the TikTok module. All multi-row writes are transactional.</summary>
public interface ITikTokRepository
{
    /// <summary>Creates the database file if needed and runs schema migrations.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TikTokShop>> GetShopsAsync(CancellationToken cancellationToken);

    Task<TikTokShop> AddShopAsync(string name, CancellationToken cancellationToken);

    Task RenameShopAsync(long shopId, string name, CancellationToken cancellationToken);

    Task<IReadOnlyList<SkuConfig>> GetSkusAsync(CancellationToken cancellationToken);

    /// <summary>Inserts or replaces the given SKU configurations.</summary>
    Task SaveSkusAsync(IReadOnlyCollection<SkuConfig> skus, CancellationToken cancellationToken);

    Task<IReadOnlyList<IdolAlias>> GetIdolAliasesAsync(CancellationToken cancellationToken);

    Task SaveIdolAliasAsync(IdolAlias alias, CancellationToken cancellationToken);

    Task DeleteIdolAliasAsync(string alias, CancellationToken cancellationToken);

    Task<IReadOnlyList<PdfAsset>> GetPdfAssetsAsync(CancellationToken cancellationToken);

    /// <summary>Replaces the whole library table with a fresh scan result.</summary>
    Task ReplacePdfAssetsAsync(IReadOnlyCollection<PdfAsset> assets, CancellationToken cancellationToken);

    /// <summary>Orders with their lines, ordered by CreatedAt then OrderId.</summary>
    Task<IReadOnlyList<TikTokOrder>> GetOrdersAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<IReadOnlyList<TikTokOrder>> GetOrdersByIdsAsync(IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken);

    /// <summary>Active, not deactivated orders of one shop (used by Full-mode deactivation).</summary>
    Task<IReadOnlyList<TikTokOrder>> GetActiveOrdersForShopAsync(long shopId, CancellationToken cancellationToken);

    /// <summary>Print items of each order's current PrintedBatchId (snapshot used for ChangedAfterPrint).</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<PrintBatchItemRecord>>> GetPrintedItemsForOrdersAsync(
        IReadOnlyCollection<string> orderIds,
        CancellationToken cancellationToken);

    /// <summary>Applies a whole import in one transaction and returns the stored batch with its new ImportId.</summary>
    Task<ImportBatchRecord> ApplyImportAsync(ImportChangeSet changeSet, CancellationToken cancellationToken);

    Task<IReadOnlyList<ImportBatchRecord>> GetImportBatchesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one import batch and the orders seen only in that import. Orders also seen in another import are kept.
    /// SKU configurations, aliases, the library and print history are kept. Returns the number of deleted orders.
    /// </summary>
    Task<int> DeleteImportAsync(long importId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes every order and import batch. SKU configurations, aliases, the library and print history are kept.
    /// Returns the number of deleted orders.
    /// </summary>
    Task<int> DeleteAllOrdersAsync(CancellationToken cancellationToken);

    /// <summary>Every file chosen by hand for a printed set, ordered by order, line, copy and Seq.</summary>
    Task<IReadOnlyList<FileOverride>> GetFileOverridesAsync(CancellationToken cancellationToken);

    /// <summary>Replaces the chosen files of one order; an empty list returns the order to the automatic choice.</summary>
    Task SaveFileOverridesAsync(string orderId, IReadOnlyCollection<FileOverride> overrides, CancellationToken cancellationToken);

    /// <summary>Stores (or clears with null) the note hash approved by "Đã xem – cho phép ghép".</summary>
    Task SetNoteReviewedAsync(string orderId, string? noteHash, CancellationToken cancellationToken);

    /// <summary>Clears PrintedBatchId/PrintedAt/PrintedSource/ChangedAfterPrint. History rows are kept.</summary>
    Task UnmarkPrintedAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>Writes the batch, its items and marks the orders printed (PrintedSource.Batch) in one transaction.</summary>
    Task CommitPrintBatchAsync(
        PrintBatchRecord batch,
        IReadOnlyList<PrintBatchItemRecord> items,
        IReadOnlyCollection<string> orderIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PrintBatchRecord>> GetPrintBatchesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PrintBatchItemRecord>> GetPrintBatchItemsAsync(string batchId, CancellationToken cancellationToken);
}

public interface ISkuConfigSuggester
{
    SkuSuggestion Suggest(string productName, string variationName, IReadOnlyList<IdolAlias> aliases);
}

public interface IPdfLibraryScanner
{
    /// <summary>
    /// Scans folder (including subfolders) for *.pdf. Files whose path and LastWriteTimeUtc match an entry in
    /// previous are reused without reopening the PDF. today resolves the missing year of "dd.MM".
    /// </summary>
    Task<IReadOnlyList<PdfAsset>> ScanAsync(
        string folder,
        IReadOnlyList<PdfAsset> previous,
        DateOnly today,
        CancellationToken cancellationToken);
}

public interface IOrderClassifier
{
    OrderClassification Classify(
        TikTokOrder order,
        IReadOnlyDictionary<string, SkuConfig> skus,
        IReadOnlyList<PdfAsset> assets,
        ClassificationOptions options);
}

public interface IPrintPlanBuilder
{
    /// <summary>Classifies, sorts by CreatedAt then OrderId and assigns library files (TT-04 → TT-07).</summary>
    PrintPlan Build(
        IReadOnlyList<TikTokOrder> orders,
        IReadOnlyDictionary<string, SkuConfig> skus,
        IReadOnlyList<PdfAsset> assets,
        SeparatorMode separatorMode,
        ClassificationOptions options);
}

public interface ISeparatorPageRenderer
{
    /// <summary>Renders a one-page A4 PDF into tempDirectory and returns its path.</summary>
    Task<string> RenderAsync(SeparatorPageInfo info, string tempDirectory, CancellationToken cancellationToken);
}

public interface IPrintBatchExporter
{
    /// <summary>
    /// Exports plan to outputPath through IPdfExportService, then commits history and marks orders printed.
    /// On failure or cancellation nothing is committed. Does not open the viewer.
    /// </summary>
    Task<PrintBatchResult> ExportAsync(
        PrintPlan plan,
        IReadOnlyDictionary<long, string> shopNames,
        string outputPath,
        IProgress<int>? progress,
        CancellationToken cancellationToken);
}
