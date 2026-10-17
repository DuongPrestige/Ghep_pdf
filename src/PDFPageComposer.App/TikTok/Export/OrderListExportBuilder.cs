using System.Globalization;
using System.IO;
using PDFPageComposer.App.TikTok.Matching;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Export;

/// <summary>
/// Pure row builder of "Xuất danh sách CSV" (PRD_TIKTOK TT-12): filters the candidates by scope, classifies them
/// exactly like the Orders tab and emits one row per order line, orders by CreatedAt then OrderId, lines by LineNo.
/// </summary>
public sealed class OrderListExportBuilder
{
    public const string Yes = "Có";
    public const string No = "Không";
    public const string SourceBatch = "Lô in";
    public const string SourceSellerNote = "Seller Note";

    private const string DateTimeFormat = "dd/MM/yyyy HH:mm:ss";

    private static readonly DateOnly NameParseReferenceDate = new(2024, 12, 31);

    private readonly IOrderClassifier classifier;

    public OrderListExportBuilder(IOrderClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        this.classifier = classifier;
    }

    public IReadOnlyList<OrderListExportRow> Build(OrderListExportInput input, OrderListExportContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        // The export only reports the status; "In lại" never applies here.
        var options = context.Options with { AllowReprint = false };
        var batchOrderIds = input.BatchOrderIds is null
            ? null
            : new HashSet<string>(input.BatchOrderIds, StringComparer.Ordinal);
        var assetsByPath = context.Assets
            .GroupBy(asset => asset.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var rows = new List<OrderListExportRow>();
        var orders = TikTokOrderOrdering.InFileOrder(input.Orders);
        foreach (var order in orders)
        {
            if (!IsInScope(order, input, batchOrderIds))
            {
                continue;
            }

            var classification = classifier.Classify(order, context.Skus, context.Assets, options);
            var mark = input.Scope == OrderListExportScope.PrintBatch
                ? BatchMark(input.Batch, context)
                : CurrentMark(order, classification, context);
            if ((input.Scope == OrderListExportScope.PrintedOnly && mark is null)
                || (input.Scope == OrderListExportScope.NotPrinted && mark is not null))
            {
                continue;
            }

            var printedItems = mark is not null && input.PrintedItems.TryGetValue(order.OrderId, out var items) ? items : [];
            rows.AddRange(BuildOrderRows(order, classification, mark, printedItems, assetsByPath, context));
        }

        return rows;
    }

    /// <summary>The print mark of the selected batch: always printed, whatever the order's current mark is.</summary>
    private static PrintMark BatchMark(OrderListExportBatch? batch, OrderListExportContext context) =>
        new(
            SourceBatch,
            batch?.BatchId ?? string.Empty,
            batch?.CreatedAt is { } createdAt ? Format(TimeZoneInfo.ConvertTime(createdAt, context.LocalTimeZone)) : string.Empty);

    /// <summary>The order's current print mark, or null when the classifier does not count it as printed.</summary>
    private static PrintMark? CurrentMark(TikTokOrder order, OrderClassification classification, OrderListExportContext context) =>
        classification.Category != OrderCategory.Printed
            ? null
            : new PrintMark(
                DescribeSource(order),
                order.PrintedBatchId ?? string.Empty,
                order.PrintedAt is { } printedAt ? Format(TimeZoneInfo.ConvertTime(printedAt, context.LocalTimeZone)) : string.Empty);

    private static bool IsInScope(TikTokOrder order, OrderListExportInput input, HashSet<string>? batchOrderIds) =>
        input.Scope switch
        {
            OrderListExportScope.PrintedOnly => true,
            OrderListExportScope.AllImported or OrderListExportScope.NotPrinted => order.IsActive,
            OrderListExportScope.LatestImport => input.LatestImportId is { } latest && order.LastSeenImportId == latest,
            OrderListExportScope.PrintBatch => batchOrderIds is not null && batchOrderIds.Contains(order.OrderId),
            _ => false,
        };

    private static IEnumerable<OrderListExportRow> BuildOrderRows(
        TikTokOrder order,
        OrderClassification classification,
        PrintMark? mark,
        IReadOnlyList<PrintBatchItemRecord> printedItems,
        IReadOnlyDictionary<string, PdfAsset> assetsByPath,
        OrderListExportContext context)
    {
        var template = new OrderListExportRow(
            order.OrderId,
            context.ShopNames.TryGetValue(order.ShopId, out var shopName) ? shopName : $"Shop {order.ShopId}",
            Format(order.CreatedAt),
            mark is null ? No : Yes,
            mark?.Source ?? string.Empty,
            mark?.BatchId ?? string.Empty,
            mark?.PrintedAt ?? string.Empty,
            context.CategoryLabel(classification.Category),
            string.Join(" | ", classification.Reasons),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            order.ChangedAfterPrint ? Yes : string.Empty,
            order.IsActive ? Yes : No,
            order.BuyerUsername ?? string.Empty,
            order.BuyerMessage ?? string.Empty,
            order.SellerNote ?? string.Empty);

        if (order.Lines.Count == 0)
        {
            yield return template;
            yield break;
        }

        foreach (var line in order.Lines.OrderBy(line => line.LineNo))
        {
            yield return template with
            {
                SkuId = line.SkuId,
                ProductName = line.ProductName,
                VariationName = line.VariationName,
                Quantity = line.Quantity.ToString(CultureInfo.InvariantCulture),
                FilesUsed = DescribeFiles(printedItems, line.SkuId, assetsByPath),
            };
        }
    }

    private static string DescribeSource(TikTokOrder order) =>
        order.PrintedBatchId is not null || order.PrintedSource == PrintedSource.Batch ? SourceBatch : SourceSellerNote;

    private static string DescribeFiles(
        IReadOnlyList<PrintBatchItemRecord> printedItems,
        string skuId,
        IReadOnlyDictionary<string, PdfAsset> assetsByPath) =>
        string.Join(
            "; ",
            printedItems
                .Where(item => string.Equals(item.SkuId, skuId, StringComparison.Ordinal))
                .OrderBy(item => item.Seq)
                .Select(item => DescribeFile(item, assetsByPath)));

    private static string DescribeFile(PrintBatchItemRecord item, IReadOnlyDictionary<string, PdfAsset> assetsByPath)
    {
        var name = Path.GetFileName(item.PdfAssetPath);
        if (item.PagesUsed is not { } used)
        {
            return name;
        }

        // The file may have left the library (removed, renamed or folder moved): its name still holds the page total.
        // Only the page count is used, so the reference date is fixed (end of a leap year: "29.02" still parses).
        int? total = assetsByPath.TryGetValue(item.PdfAssetPath, out var asset)
            ? asset.PageCount
            : PdfAssetNameParser.TryParse(name, NameParseReferenceDate, out _, out var parsedPages, out _)
                ? parsedPages
                : null;
        return total is { } pageCount
            ? string.Create(CultureInfo.InvariantCulture, $"{name} (lấy {used}/{pageCount} trang đầu)")
            : string.Create(CultureInfo.InvariantCulture, $"{name} (lấy {used} trang đầu)");
    }

    private static string Format(DateTimeOffset value) => value.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    /// <summary>Display values of the "Đã làm file in" columns for a printed order.</summary>
    private sealed record PrintMark(string Source, string BatchId, string PrintedAt);
}
