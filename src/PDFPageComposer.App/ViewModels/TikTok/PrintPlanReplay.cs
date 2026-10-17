using System.IO;
using PDFPageComposer.App.TikTok.Export;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>Rebuilds the print plan of an earlier batch from its history rows ("Tạo lại", TT-11).</summary>
public static class PrintPlanReplay
{
    /// <summary>
    /// Splits the orders of a batch into those that can be replayed and those that must not be re-marked printed
    /// with the old content: inactive, un-marked ("Bỏ đánh dấu") or changed after printing (TT-09).
    /// Orders listed in items but missing from orders are excluded as well.
    /// </summary>
    public static (IReadOnlyList<TikTokOrder> Replayable, IReadOnlyList<(string OrderId, string Reason)> Excluded) SplitStaleOrders(
        IReadOnlyList<PrintBatchItemRecord> items,
        IReadOnlyList<TikTokOrder> orders)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(orders);

        var orderMap = orders.ToDictionary(order => order.OrderId, StringComparer.Ordinal);
        var replayable = new List<TikTokOrder>();
        var excluded = new List<(string OrderId, string Reason)>();
        foreach (var orderId in items.OrderBy(item => item.Seq).Select(item => item.OrderId).Distinct(StringComparer.Ordinal))
        {
            var reason = !orderMap.TryGetValue(orderId, out var order) ? "không còn trong dữ liệu"
                : !order.IsActive ? "không còn chờ vận chuyển"
                : order.PrintedBatchId is null ? "đã bỏ đánh dấu đã in"
                : order.ChangedAfterPrint ? "đã thay đổi sau khi in"
                : null;
            if (reason is null)
            {
                replayable.Add(order!);
            }
            else
            {
                excluded.Add((orderId, reason));
            }
        }

        return (replayable, excluded);
    }

    public static string DescribeExcluded(IReadOnlyList<(string OrderId, string Reason)> excluded)
    {
        ArgumentNullException.ThrowIfNull(excluded);
        return string.Join("; ", excluded.Select(entry => $"{entry.OrderId} ({entry.Reason})"));
    }

    /// <exception cref="InvalidOperationException">An order or a library file of the batch no longer exists.</exception>
    public static PrintPlan Build(
        PrintBatchRecord batch,
        IReadOnlyList<PrintBatchItemRecord> items,
        IReadOnlyList<TikTokOrder> orders,
        IReadOnlyList<PdfAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(assets);

        if (items.Count == 0)
        {
            throw new InvalidOperationException($"Lô in {batch.BatchId} không có dữ liệu chi tiết để tạo lại.");
        }

        var orderMap = orders.ToDictionary(order => order.OrderId, StringComparer.Ordinal);
        var assetMap = assets
            .Where(asset => asset.IsValid)
            .GroupBy(asset => asset.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var planOrders = new List<PrintPlanOrder>();
        foreach (var group in items.OrderBy(item => item.Seq).GroupBy(item => item.OrderId, StringComparer.Ordinal))
        {
            if (!orderMap.TryGetValue(group.Key, out var order))
            {
                throw new InvalidOperationException($"Không tìm thấy đơn {group.Key} trong dữ liệu.");
            }

            var copies = group.Select(item => ToCopy(order, item, assetMap)).ToList();
            var isComplex = copies.Select(copy => copy.SkuId).Distinct(StringComparer.Ordinal).Count() >= 2
                || copies.Any(copy => copy.Quantity > 1);
            var includeSeparator = batch.SeparatorMode switch
            {
                SeparatorMode.All => true,
                SeparatorMode.ComplexOnly => isComplex,
                _ => false,
            };

            planOrders.Add(new PrintPlanOrder(order, includeSeparator, copies));
        }

        return new PrintPlan(planOrders, [], batch.SeparatorMode);
    }

    private static PrintPlanCopy ToCopy(
        TikTokOrder order,
        PrintBatchItemRecord item,
        IReadOnlyDictionary<string, PdfAsset> assetMap)
    {
        if (!assetMap.TryGetValue(item.PdfAssetPath, out var asset))
        {
            throw new InvalidOperationException(
                $"File kho {Path.GetFileName(item.PdfAssetPath)} không còn trong kho. Hãy quét lại kho rồi thử lại.");
        }

        var lineNo = order.Lines.FirstOrDefault(line => string.Equals(line.SkuId, item.SkuId, StringComparison.Ordinal))?.LineNo ?? 0;
        return new PrintPlanCopy(
            order.OrderId,
            item.SkuId,
            lineNo,
            item.CopyIndex,
            item.Quantity,
            asset.Idol,
            item.PagesUsed is int used && used < asset.PageCount ? used : asset.PageCount,
            asset,
            string.Equals(item.Warning, PrintBatchExporter.DuplicateImageWarning, StringComparison.Ordinal),
            FirstPage: item.FirstPage ?? 1);
    }
}
