using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Import;

/// <summary>One order of the file after status filtering and (OrderId, SkuId) de-duplication.</summary>
internal sealed record ImportedOrderGroup(string OrderId, ImportedOrderLine FirstLine, IReadOnlyList<ImportedOrderLine> Lines);

internal sealed record ImportedOrderGrouping(
    IReadOnlyList<ImportedOrderGroup> Orders,
    int Skipped,
    IReadOnlyList<string> Warnings);

/// <summary>Steps 1–2 of TECH_SPEC_TIKTOK 3.3: filter by accepted status, group by OrderId, drop duplicate lines.</summary>
internal static class ImportedOrderGrouper
{
    public static ImportedOrderGrouping Group(IReadOnlyList<ImportedOrderLine> lines, IEnumerable<string> acceptedStatuses)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(acceptedStatuses);

        var accepted = acceptedStatuses
            .Select(TextNormalizer.Normalize)
            .Where(status => status.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        var orderIds = new List<string>();
        var linesByOrder = new Dictionary<string, List<ImportedOrderLine>>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var skipped = 0;

        foreach (var line in lines)
        {
            ArgumentNullException.ThrowIfNull(line);

            var orderId = TextNormalizer.TrimCell(line.OrderId);
            var skuId = TextNormalizer.TrimCell(line.SkuId);
            if (orderId is null || skuId is null || !accepted.Contains(TextNormalizer.Normalize(line.StatusText)))
            {
                skipped++;
                continue;
            }

            if (!linesByOrder.TryGetValue(orderId, out var orderLines))
            {
                orderLines = [];
                linesByOrder.Add(orderId, orderLines);
                orderIds.Add(orderId);
            }

            var duplicate = orderLines.Find(existing => string.Equals(existing.SkuId, skuId, StringComparison.Ordinal));
            if (duplicate is not null)
            {
                warnings.Add(
                    $"Dòng trùng mã đơn + SKU: đơn {orderId}, SKU {skuId} (dòng {line.SourceRowNumber} trùng dòng " +
                    $"{duplicate.SourceRowNumber}). Chỉ giữ dòng đầu tiên.");
                continue;
            }

            orderLines.Add(line with { OrderId = orderId, SkuId = skuId });
        }

        var orders = orderIds
            .Select(orderId => new ImportedOrderGroup(orderId, linesByOrder[orderId][0], linesByOrder[orderId]))
            .ToArray();
        return new ImportedOrderGrouping(orders, skipped, warnings);
    }
}
