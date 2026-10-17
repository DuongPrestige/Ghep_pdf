using System.IO;
using PDFPageComposer.App.TikTok.Models;
using PdfSharp;
using PdfSharp.Pdf;

namespace PDFPageComposer.App.TikTok.Export;

/// <summary>
/// Writes the A4 separator page printed in front of an order (PRD_TIKTOK TT-07). Since 2026-10-17 the page is left
/// blank to save ink; the order details stay in the print history and the order list export.
/// </summary>
public sealed class SeparatorPageRenderer : ISeparatorPageRenderer
{
    public Task<string> RenderAsync(SeparatorPageInfo info, string tempDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentException.ThrowIfNullOrWhiteSpace(tempDirectory);

        return Task.Run(() => Render(tempDirectory, cancellationToken), cancellationToken);
    }

    private static string Render(string tempDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(tempDirectory);

        var filePath = Path.Combine(tempDirectory, $"separator-{Guid.NewGuid():N}.pdf");
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PageSize.A4;
        page.Orientation = PageOrientation.Portrait;

        document.Save(filePath);
        return filePath;
    }
}
