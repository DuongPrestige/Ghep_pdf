namespace PDFPageComposer.App.TikTok.Models;

/// <summary>One line item read from an order source (CSV row). Values are already trimmed.</summary>
public sealed record ImportedOrderLine(
    string OrderId,
    string StatusText,
    string? SubstatusText,
    string SkuId,
    string ProductName,
    string VariationName,
    int Quantity,
    DateTimeOffset CreatedAt,
    string? BuyerUsername,
    string? BuyerMessage,
    string? SellerNote,
    string? PackageId,
    int SourceRowNumber);

public sealed record OrderSourceResult(IReadOnlyList<ImportedOrderLine> Lines, IReadOnlyList<string> Warnings)
{
    /// <summary>
    /// Orders that had at least one unreadable row. None of their lines are in <see cref="Lines"/> (no half orders,
    /// TT-08) and a Full import must not hide them (they are still in the export).
    /// </summary>
    public IReadOnlyCollection<string> RejectedOrderIds { get; init; } = [];
}

/// <summary>
/// Everything one import writes, computed by OrderImportService and applied atomically by the repository.
/// The repository assigns the new ImportId and sets FirstImportId (inserts) and LastSeenImportId (inserts + updates).
/// </summary>
public sealed record ImportChangeSet(
    long ShopId,
    string FileName,
    ImportMode Mode,
    DateTimeOffset ImportedAt,
    IReadOnlyList<TikTokOrder> OrdersToInsert,
    IReadOnlyList<TikTokOrder> OrdersToUpdate,
    IReadOnlyList<string> OrderIdsToDeactivate,
    IReadOnlyList<SkuConfig> NewSkus,
    int AlreadyPrinted,
    int Skipped);

public sealed record ImportBatchRecord(
    long ImportId,
    long ShopId,
    string FileName,
    ImportMode Mode,
    DateTimeOffset ImportedAt,
    int Inserted,
    int Updated,
    int AlreadyPrinted,
    int Skipped,
    int Deactivated);

public sealed record ImportReport(
    long ImportId,
    int Inserted,
    int Updated,
    int AlreadyPrinted,
    int Skipped,
    int Deactivated,
    int NewSkus,
    IReadOnlyList<string> Warnings);
