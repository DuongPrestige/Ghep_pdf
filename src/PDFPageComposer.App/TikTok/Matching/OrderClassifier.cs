using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Matching;

/// <summary>
/// Classifies one order (PRD_TIKTOK TT-08, TT-10). Categories are evaluated in priority order:
/// Printed → NoCardNeeded → HasNote → NeedsConfig → Manual → MissingFile → Ready.
/// </summary>
public sealed class OrderClassifier : IOrderClassifier
{
    public OrderClassification Classify(
        TikTokOrder order,
        IReadOnlyDictionary<string, SkuConfig> skus,
        IReadOnlyList<PdfAsset> assets,
        ClassificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(skus);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(options);

        // A printed order is only planned again when the user chose "In lại"; that choice also counts as having
        // read its notes (TT-11 asks the user to write e.g. "ĐÃ IN" into the Seller Note after printing).
        var printedReasons = GetPrintedReasons(order, options);
        var isPrinted = printedReasons.Count > 0;
        if (isPrinted && !options.AllowReprint)
        {
            return Result(order, OrderCategory.Printed, printedReasons);
        }

        var cardLines = order.Lines
            .OrderBy(line => line.LineNo)
            .Select(line => (Line: line, Sku: skus.TryGetValue(line.SkuId, out var sku) ? sku : null))
            .Where(entry => IsCardLine(entry.Sku))
            .ToList();
        if (cardLines.Count == 0)
        {
            return Result(order, OrderCategory.NoCardNeeded, ["Đơn chỉ gồm món không cần in card."]);
        }

        if (!isPrinted && options.BlockOrdersWithNotes)
        {
            var noteReasons = GetNoteReasons(order);
            if (noteReasons.Count > 0)
            {
                return Result(order, OrderCategory.HasNote, noteReasons);
            }
        }

        var configReasons = cardLines
            .Select(entry => DescribeConfigProblem(entry.Line, entry.Sku))
            .OfType<string>()
            .ToList();
        if (configReasons.Count > 0)
        {
            return Result(order, OrderCategory.NeedsConfig, configReasons);
        }

        var manualReasons = cardLines
            .Where(entry => entry.Sku!.ProcessType == SkuProcessType.Manual)
            .Select(entry => $"Card thủ công: {DescribeLine(entry.Line)}")
            .ToList();
        if (manualReasons.Count > 0)
        {
            return Result(order, OrderCategory.Manual, manualReasons);
        }

        var missingReasons = new List<string>();
        foreach (var (line, sku) in cardLines)
        {
            if (Enumerable.Range(1, line.Quantity)
                .All(copy => FileOverrides.For(options.FileOverrides, order.OrderId, line.SkuId, copy).Count > 0))
            {
                continue;
            }

            var pages = PdfAssetMatcher.PagesForCards(sku!.CardCount!.Value);
            if (!PdfAssetMatcher.CanPrint(assets, sku.Idol!, pages, options.IdolAliases, options.AlwaysUseNewestFiles))
            {
                var reason = $"Thiếu file kho {PdfAssetMatcher.ResolveIdol(sku.Idol!, options.IdolAliases)} - {pages} PAGE";
                if (!missingReasons.Contains(reason, StringComparer.Ordinal))
                {
                    missingReasons.Add(reason);
                }
            }
        }

        return missingReasons.Count > 0
            ? Result(order, OrderCategory.MissingFile, missingReasons)
            : Result(order, OrderCategory.Ready, []);
    }

    /// <summary>
    /// A line counts unless its SKU is a confirmed Ignore SKU.
    /// Unknown and unconfirmed SKUs (even when suggested as Ignore) still need configuration.
    /// </summary>
    private static bool IsCardLine(SkuConfig? sku) =>
        sku is null || sku.ConfigStatus != SkuConfigStatus.Confirmed || sku.ProcessType != SkuProcessType.Ignore;

    private static List<string> GetPrintedReasons(TikTokOrder order, ClassificationOptions options)
    {
        var reasons = new List<string>();
        if (order.PrintedBatchId is not null)
        {
            reasons.Add($"Đã in (lô {order.PrintedBatchId})");
        }
        else if (order.PrintedSource == PrintedSource.SellerNote)
        {
            // A stored Seller Note mark only counts while the option is on (TT-10 toggle).
            if (options.SellerNoteMarksPrinted)
            {
                reasons.Add("Đã in theo Seller Note");
            }
        }
        else if (order.PrintedSource is not null)
        {
            reasons.Add("Đã in");
        }
        else if (options.SellerNoteMarksPrinted && OrderNotes.IsMarkedPrinted(order.SellerNote))
        {
            reasons.Add("Đã in theo Seller Note");
        }

        return reasons;
    }

    private static List<string> GetNoteReasons(TikTokOrder order)
    {
        var reasons = new List<string>();
        if (!OrderNotes.HasNote(order.BuyerMessage, order.SellerNote))
        {
            return reasons;
        }

        var currentHash = OrderNotes.ComputeHash(order.BuyerMessage, order.SellerNote);
        if (string.Equals(order.NoteReviewedHash, currentHash, StringComparison.Ordinal))
        {
            return reasons;
        }

        if (TextNormalizer.TrimCell(order.BuyerMessage) is { } buyerMessage)
        {
            reasons.Add($"Có ghi chú của khách: {buyerMessage}");
        }

        if (TextNormalizer.TrimCell(order.SellerNote) is { } sellerNote)
        {
            reasons.Add($"Có ghi chú Seller Note: {sellerNote}");
        }

        return reasons;
    }

    private static string? DescribeConfigProblem(TikTokOrderLine line, SkuConfig? sku)
    {
        if (sku is null)
        {
            return $"SKU {line.SkuId} chưa có trong danh sách cấu hình ({DescribeLine(line)})";
        }

        if (sku.ConfigStatus != SkuConfigStatus.Confirmed)
        {
            return $"SKU {line.SkuId} chưa được cấu hình ({DescribeLine(line)})";
        }

        if (sku.ProcessType == SkuProcessType.Auto && !PdfAssetMatcher.IsUsableAuto(sku))
        {
            return $"SKU {line.SkuId} thiếu mã idol hoặc số card ({DescribeLine(line)})";
        }

        return null;
    }

    private static string DescribeLine(TikTokOrderLine line) =>
        TextNormalizer.TrimCell(line.VariationName) ?? TextNormalizer.TrimCell(line.ProductName) ?? line.SkuId;

    private static OrderClassification Result(TikTokOrder order, OrderCategory category, IReadOnlyList<string> reasons) =>
        new(order.OrderId, category, reasons);
}
