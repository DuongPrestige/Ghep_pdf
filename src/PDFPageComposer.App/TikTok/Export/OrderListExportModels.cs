using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Export;

/// <summary>Which orders "Xuất danh sách CSV" writes (PRD_TIKTOK TT-12).</summary>
public enum OrderListExportScope
{
    /// <summary>Every order classified as printed (print batch or Seller Note), still awaiting shipment or not.</summary>
    PrintedOnly,

    /// <summary>Every active (awaiting shipment) order, printed or not.</summary>
    AllImported,

    /// <summary>Active orders not counted as printed yet (ready, manual, missing file, needs config, ...).</summary>
    NotPrinted,

    /// <summary>Orders seen by the newest import batch (LastSeenImportId == newest ImportId).</summary>
    LatestImport,

    /// <summary>Orders of one print batch from the History tab.</summary>
    PrintBatch,
}

/// <summary>
/// Catalog and display settings the export classifies with: the same SKUs, library, options and labels the Orders
/// tab uses. LocalTimeZone converts the stored print time (UTC) for display.
/// </summary>
public sealed record OrderListExportContext(
    IReadOnlyDictionary<long, string> ShopNames,
    IReadOnlyDictionary<string, SkuConfig> Skus,
    IReadOnlyList<PdfAsset> Assets,
    ClassificationOptions Options,
    Func<OrderCategory, string> CategoryLabel,
    TimeZoneInfo LocalTimeZone);

/// <summary>
/// Data the builder filters. Orders are the candidates (all orders, or the orders of the batch). PrintedItems holds
/// the PrintBatchItem rows per order: of each order's current PrintedBatchId, or, for
/// <see cref="OrderListExportScope.PrintBatch"/>, of the selected <see cref="Batch"/> even when the order was later
/// unmarked or reprinted in another batch.
/// </summary>
public sealed record OrderListExportInput(
    OrderListExportScope Scope,
    IReadOnlyList<TikTokOrder> Orders,
    IReadOnlyDictionary<string, IReadOnlyList<PrintBatchItemRecord>> PrintedItems,
    long? LatestImportId = null,
    IReadOnlyCollection<string>? BatchOrderIds = null,
    OrderListExportBatch? Batch = null);

/// <summary>The print batch selected in the History tab; CreatedAt is null when the batch row is missing.</summary>
public sealed record OrderListExportBatch(string BatchId, DateTimeOffset? CreatedAt);

/// <summary>
/// One CSV row: one order line with the order-level values repeated (like the Seller Center export).
/// Values are display text; never contains recipient name, phone or address (they are not stored).
/// </summary>
public sealed record OrderListExportRow(
    string OrderId,
    string ShopName,
    string CreatedAt,
    string Printed,
    string MarkSource,
    string BatchId,
    string PrintedAt,
    string Status,
    string Reasons,
    string SkuId,
    string ProductName,
    string VariationName,
    string Quantity,
    string FilesUsed,
    string ChangedAfterPrint,
    string AwaitingShipment,
    string BuyerUsername,
    string BuyerMessage,
    string SellerNote)
{
    /// <summary>Column headers, in the order of <see cref="ToCells"/>.</summary>
    public static IReadOnlyList<string> Headers { get; } =
    [
        "Order ID",
        "Shop",
        "Thời gian đặt",
        "Đã làm file in",
        "Nguồn đánh dấu",
        "Lô in",
        "Thời gian in",
        "Trạng thái xử lý",
        "Lý do",
        "SKU ID",
        "Sản phẩm",
        "Phân loại",
        "Số lượng",
        "File kho đã dùng",
        "Đơn đã thay đổi sau khi in",
        "Còn chờ vận chuyển",
        "Buyer Username",
        "Ghi chú khách",
        "Seller Note",
    ];

    /// <summary>
    /// Cell values in header order. Order ID and SKU ID end with a TAB (same trick as Seller Center) so Excel keeps
    /// the long numbers as text instead of rounding them. Free-text cells go through <see cref="NeutralizeFormula"/>
    /// so Excel shows notes such as "- giao giờ hành chính" or "+84..." as text instead of a formula result.
    /// </summary>
    public IReadOnlyList<string> ToCells() =>
    [
        OrderId + "\t",
        NeutralizeFormula(ShopName),
        CreatedAt,
        Printed,
        MarkSource,
        BatchId,
        PrintedAt,
        Status,
        NeutralizeFormula(Reasons),
        SkuId.Length == 0 ? string.Empty : SkuId + "\t",
        NeutralizeFormula(ProductName),
        NeutralizeFormula(VariationName),
        Quantity,
        NeutralizeFormula(FilesUsed),
        ChangedAfterPrint,
        AwaitingShipment,
        NeutralizeFormula(BuyerUsername),
        NeutralizeFormula(BuyerMessage),
        NeutralizeFormula(SellerNote),
    ];

    /// <summary>
    /// Excel reads a cell starting with '=', '+', '-', '@', TAB or CR as a formula even when it is quoted; a leading
    /// apostrophe (Excel's text prefix) keeps it as text. Other values are returned unchanged.
    /// </summary>
    public static string NeutralizeFormula(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
    }
}
