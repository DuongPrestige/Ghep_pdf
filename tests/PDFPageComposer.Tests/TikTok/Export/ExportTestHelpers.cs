using System.IO;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.TikTok.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace PDFPageComposer.Tests.TikTok.Export;

internal static class ExportTestHelpers
{
    public static readonly DateTimeOffset OrderTime = new(2026, 10, 15, 9, 5, 0, TimeSpan.FromHours(7));

    /// <summary>Creates a PDF whose page widths identify each page in the output.</summary>
    public static void CreatePdf(string path, params int[] pageWidths)
    {
        using var document = new PdfDocument();
        foreach (var width in pageWidths)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(width);
            page.Height = XUnit.FromPoint(500);
            using var graphics = XGraphics.FromPdfPage(page);
            graphics.DrawRectangle(XBrushes.LightGray, 10, 10, 50, 50);
        }

        document.Save(path);
    }

    public static PdfAsset Asset(string path, string idol, int pageCount) =>
        new(path, idol, pageCount, new DateOnly(2026, 10, 15), pageCount, File.GetLastWriteTimeUtc(path), true, null);

    public static TikTokOrder Order(string orderId, long shopId, params TikTokOrderLine[] lines) =>
        new(
            orderId,
            shopId,
            "Chờ vận chuyển",
            OrderTime,
            "ng***ễn",
            null,
            null,
            true,
            1,
            1,
            null,
            null,
            null,
            false,
            null,
            lines);

    public static PrintPlanCopy Copy(
        string orderId,
        string skuId,
        int copyIndex,
        int quantity,
        PdfAsset asset,
        bool duplicateImage = false) =>
        new(orderId, skuId, 1, copyIndex, quantity, asset.Idol, asset.PageCount, asset, duplicateImage);
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

internal sealed class CountingMetadataService(IPdfMetadataService inner) : IPdfMetadataService
{
    public Dictionary<string, int> Calls { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<SourcePdfFile> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        Calls[filePath] = Calls.GetValueOrDefault(filePath) + 1;
        return inner.ReadAsync(filePath, cancellationToken);
    }
}

internal sealed class ThrowingExportService(Exception exception) : IPdfExportService
{
    public Task ExportAsync(
        IReadOnlyCollection<SourcePdfFile> sourceFiles,
        IReadOnlyCollection<OutputGroup> groups,
        string outputPath,
        IProgress<int>? progress,
        CancellationToken cancellationToken) => Task.FromException(exception);
}

internal sealed class CancelAfterFirstProgress(CancellationTokenSource cancellation) : IProgress<int>
{
    public void Report(int value) => cancellation.Cancel();
}
