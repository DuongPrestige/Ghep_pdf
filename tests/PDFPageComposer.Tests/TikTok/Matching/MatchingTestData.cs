using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.Tests.TikTok.Matching;

internal static class MatchingTestData
{
    public static readonly DateTimeOffset BaseTime = new(2026, 10, 16, 8, 0, 0, TimeSpan.FromHours(7));

    public static SkuConfig Auto(string skuId, string idol, int cardCount, string variation = "20 CARD, 1 Mặt") =>
        Sku(skuId, SkuProcessType.Auto, SkuConfigStatus.Confirmed, idol, cardCount, variation);

    public static SkuConfig Manual(string skuId, string variation = "20 CARD, 2 MẶT (MẶT ẢNH + CHỮ KÝ)") =>
        Sku(skuId, SkuProcessType.Manual, SkuConfigStatus.Confirmed, null, 20, variation);

    public static SkuConfig Ignore(string skuId, SkuConfigStatus status = SkuConfigStatus.Confirmed) =>
        Sku(skuId, SkuProcessType.Ignore, status, null, null, "Móc khóa");

    public static SkuConfig Sku(
        string skuId,
        SkuProcessType processType,
        SkuConfigStatus status,
        string? idol,
        int? cardCount,
        string variation = "") =>
        new(skuId, "[CARD BO GÓC] - test", variation, processType, status, idol, cardCount, BaseTime, BaseTime);

    public static IReadOnlyDictionary<string, SkuConfig> Skus(params SkuConfig[] skus) =>
        skus.ToDictionary(sku => sku.SkuId, StringComparer.Ordinal);

    public static TikTokOrderLine Line(string orderId, string skuId, int lineNo, int quantity = 1, string variation = "") =>
        new(orderId, skuId, lineNo, "Sản phẩm", variation, quantity);

    public static TikTokOrder Order(
        string orderId,
        int minuteOffset,
        IEnumerable<(string SkuId, int Quantity)> lines,
        string? buyerMessage = null,
        string? sellerNote = null,
        bool isActive = true,
        string? printedBatchId = null,
        PrintedSource? printedSource = null,
        string? noteReviewedHash = null)
    {
        var orderLines = lines
            .Select((line, index) => Line(orderId, line.SkuId, index + 1, line.Quantity))
            .ToList();
        return new TikTokOrder(
            orderId,
            1,
            "Chờ vận chuyển",
            BaseTime.AddMinutes(minuteOffset),
            "buyer",
            buyerMessage,
            sellerNote,
            isActive,
            1,
            1,
            printedBatchId,
            printedBatchId is null ? null : BaseTime,
            printedSource,
            false,
            noteReviewedHash,
            orderLines);
    }

    public static PdfAsset Asset(string idol, int pages, int day, int month = 10, bool isValid = true) =>
        new(
            $@"C:\library\{idol} - {pages} PAGE - {day:00}.{month:00}.pdf",
            idol,
            pages,
            new DateOnly(2026, month, day),
            pages,
            new DateTime(2026, month, day, 0, 0, 0, DateTimeKind.Utc),
            isValid,
            isValid ? null : "invalid");
}
