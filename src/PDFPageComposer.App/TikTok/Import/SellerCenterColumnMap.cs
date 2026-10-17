namespace PDFPageComposer.App.TikTok.Import;

/// <summary>
/// Column indexes of the Seller Center export, resolved by trimmed, case-insensitive header name.
/// Only the whitelisted columns below are ever read; personal data columns are ignored on purpose (AC-T07).
/// </summary>
internal sealed class SellerCenterColumnMap
{
    public const string OrderIdHeader = "Order ID";
    public const string OrderStatusHeader = "Order Status";
    public const string OrderSubstatusHeader = "Order Substatus";
    public const string SkuIdHeader = "SKU ID";
    public const string ProductNameHeader = "Product Name";
    public const string VariationHeader = "Variation";
    public const string QuantityHeader = "Quantity";
    public const string CreatedTimeHeader = "Created Time";
    public const string BuyerUsernameHeader = "Buyer Username";
    public const string BuyerMessageHeader = "Buyer Message";
    public const string SellerNoteHeader = "Seller Note";
    public const string PackageIdHeader = "Package ID";

    private static readonly string[] RequiredHeaders =
    [
        OrderIdHeader,
        OrderStatusHeader,
        SkuIdHeader,
        ProductNameHeader,
        VariationHeader,
        QuantityHeader,
        CreatedTimeHeader,
    ];

    private SellerCenterColumnMap(IReadOnlyDictionary<string, int> indexes)
    {
        OrderId = indexes[OrderIdHeader];
        OrderStatus = indexes[OrderStatusHeader];
        SkuId = indexes[SkuIdHeader];
        ProductName = indexes[ProductNameHeader];
        Variation = indexes[VariationHeader];
        Quantity = indexes[QuantityHeader];
        CreatedTime = indexes[CreatedTimeHeader];
        OrderSubstatus = Optional(indexes, OrderSubstatusHeader);
        BuyerUsername = Optional(indexes, BuyerUsernameHeader);
        BuyerMessage = Optional(indexes, BuyerMessageHeader);
        SellerNote = Optional(indexes, SellerNoteHeader);
        PackageId = Optional(indexes, PackageIdHeader);
    }

    public int OrderId { get; }

    public int OrderStatus { get; }

    public int SkuId { get; }

    public int ProductName { get; }

    public int Variation { get; }

    public int Quantity { get; }

    public int CreatedTime { get; }

    public int? OrderSubstatus { get; }

    public int? BuyerUsername { get; }

    public int? BuyerMessage { get; }

    public int? SellerNote { get; }

    public int? PackageId { get; }

    /// <exception cref="OrderSourceFormatException">A required column is missing.</exception>
    public static SellerCenterColumnMap Create(IReadOnlyList<string> header)
    {
        ArgumentNullException.ThrowIfNull(header);

        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < header.Count; index++)
        {
            var name = TextNormalizer.TrimCell(header[index].TrimStart('﻿'));
            if (name is not null)
            {
                indexes.TryAdd(name, index);
            }
        }

        var missing = RequiredHeaders.Where(required => !indexes.ContainsKey(required)).ToArray();
        if (missing.Length > 0)
        {
            throw OrderSourceFormatException.ForMissingColumns(missing);
        }

        return new SellerCenterColumnMap(indexes);
    }

    /// <summary>Trimmed cell value (trailing TAB removed); null when the cell is empty or absent.</summary>
    public static string? Get(IReadOnlyList<string> record, int? columnIndex)
    {
        if (columnIndex is not { } index || index >= record.Count)
        {
            return null;
        }

        return TextNormalizer.TrimCell(record[index]);
    }

    private static int? Optional(IReadOnlyDictionary<string, int> indexes, string header) =>
        indexes.TryGetValue(header, out var index) ? index : null;
}
