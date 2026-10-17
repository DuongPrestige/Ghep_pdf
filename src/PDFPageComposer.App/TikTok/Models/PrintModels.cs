namespace PDFPageComposer.App.TikTok.Models;

/// <summary>
/// BlockOrdersWithNotes: orders with a Buyer Message or Seller Note go to HasNote (TT-10); can be turned off in settings.
/// IdolAliases: also applied when matching a confirmed SKU idol to library files, so adding an alias fixes existing SKUs.
/// AlwaysUseNewestFiles: every set is composed from the idol's newest files, whatever their page count, so customers get
/// the newest images; when off, only files with the exact page count are used (TT-04).
/// FileOverrides: files chosen by hand per printed set; they replace the automatic choice for that set.
/// </summary>
public sealed record ClassificationOptions(
    bool SellerNoteMarksPrinted = true,
    bool AllowReprint = false,
    bool BlockOrdersWithNotes = true,
    IReadOnlyList<IdolAlias>? IdolAliases = null,
    bool AlwaysUseNewestFiles = false,
    IReadOnlyList<FileOverride>? FileOverrides = null);

public sealed record OrderClassification(string OrderId, OrderCategory Category, IReadOnlyList<string> Reasons);

/// <summary>
/// Pages FirstPage..LastPage of Asset, printed for copy CopyIndex (1-based) of an order line. A set composed from
/// several files (TT-04, or chosen by hand) is several consecutive PrintPlanCopy entries with the same line and CopyIndex.
/// </summary>
public sealed record PrintPlanCopy(
    string OrderId,
    string SkuId,
    int LineNo,
    int CopyIndex,
    int Quantity,
    string Idol,
    int PageCount,
    PdfAsset Asset,
    bool DuplicateImage,
    bool IsManualChoice = false,
    int FirstPage = 1)
{
    public int LastPage => FirstPage + PageCount - 1;

    /// <summary>True when only some pages of the library file are printed.</summary>
    public bool UsesPartOfFile => FirstPage > 1 || Asset.PageCount > PageCount;
}

public sealed record PrintPlanOrder(TikTokOrder Order, bool IncludeSeparator, IReadOnlyList<PrintPlanCopy> Copies)
{
    public int SheetCount => Copies.Sum(copy => copy.PageCount);
}

public sealed record PrintPlan(
    IReadOnlyList<PrintPlanOrder> Orders,
    IReadOnlyList<OrderClassification> Rejected,
    SeparatorMode SeparatorMode)
{
    public int TotalPages => Orders.Sum(order => order.SheetCount + (order.IncludeSeparator ? 1 : 0));
}

public sealed record SeparatorPageInfo(
    string OrderId,
    string ShopName,
    string? BuyerUsername,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> ItemLines,
    int SheetCount);

public sealed record PrintBatchRecord(
    string BatchId,
    DateTimeOffset CreatedAt,
    string OutputPath,
    SeparatorMode SeparatorMode,
    int OrderCount,
    int PageCount,
    string Status);

/// <summary>
/// Quantity is a snapshot of the line quantity when printed (used for ChangedAfterPrint).
/// PagesUsed is set when only some pages of the file were printed (null = the whole file); FirstPage is set when the
/// printed pages do not start at page 1 (null = page 1).
/// </summary>
public sealed record PrintBatchItemRecord(
    string BatchId,
    int Seq,
    string OrderId,
    string SkuId,
    int CopyIndex,
    int Quantity,
    string PdfAssetPath,
    string? Warning,
    int? PagesUsed = null,
    int? FirstPage = null);

public sealed record PrintBatchResult(string BatchId, string OutputPath, int PageCount, int OrderCount);
