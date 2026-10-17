using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.Tests.TikTok.Import;

internal sealed class RecordingSuggester : ISkuConfigSuggester
{
    public List<(string ProductName, string VariationName)> Calls { get; } = [];

    public SkuSuggestion Suggest(string productName, string variationName, IReadOnlyList<IdolAlias> aliases)
    {
        Calls.Add((productName, variationName));
        return new SkuSuggestion(SkuProcessType.Auto, "IVAN", 20, 1);
    }
}

internal sealed class StubSettingsService(AppSettings settings) : IAppSettingsService
{
    public AppSettings Settings { get; } = settings;

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Settings);

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class OrderLines
{
    public static readonly DateTimeOffset Created = new(2026, 10, 15, 9, 0, 0, TimeSpan.FromHours(7));

    public static ImportedOrderLine Line(
        string orderId,
        string skuId,
        int quantity = 1,
        string status = "Cần vận chuyển",
        string? buyerMessage = null,
        string? sellerNote = null,
        int row = 1) =>
        new(
            orderId,
            status,
            null,
            skuId,
            $"[CARD BO GÓC] product {skuId}",
            "20 CARD, 1 Mặt",
            quantity,
            Created,
            "b***r",
            buyerMessage,
            sellerNote,
            null,
            row);
}
