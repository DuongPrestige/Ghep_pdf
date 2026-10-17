namespace PDFPageComposer.App.TikTok.Models;

/// <summary>
/// A persisted TikTok order. Never contains recipient name, phone or address (AC-T07).
/// SourceRow is the CSV row of the order's first line in the last import that contained it (0 = unknown);
/// with LastSeenImportId it keeps the Seller Center file order (see TikTokOrderOrdering).
/// </summary>
public sealed record TikTokOrder(
    string OrderId,
    long ShopId,
    string StatusText,
    DateTimeOffset CreatedAt,
    string? BuyerUsername,
    string? BuyerMessage,
    string? SellerNote,
    bool IsActive,
    long FirstImportId,
    long LastSeenImportId,
    string? PrintedBatchId,
    DateTimeOffset? PrintedAt,
    PrintedSource? PrintedSource,
    bool ChangedAfterPrint,
    string? NoteReviewedHash,
    IReadOnlyList<TikTokOrderLine> Lines,
    int SourceRow = 0);

/// <summary>Order of the CSV files: import by import, then CSV row; CreatedAt and OrderId break ties.</summary>
public static class TikTokOrderOrdering
{
    public static IOrderedEnumerable<TikTokOrder> InFileOrder(IEnumerable<TikTokOrder> orders) =>
        orders
            .OrderBy(order => order.LastSeenImportId)
            .ThenBy(order => order.SourceRow)
            .ThenBy(order => order.CreatedAt)
            .ThenBy(order => order.OrderId, StringComparer.Ordinal);
}

/// <summary>One SKU inside an order. Key: (OrderId, SkuId). LineNo keeps file order, starting at 1.</summary>
public sealed record TikTokOrderLine(
    string OrderId,
    string SkuId,
    int LineNo,
    string ProductName,
    string VariationName,
    int Quantity);

public sealed record TikTokShop(long ShopId, string Name);
