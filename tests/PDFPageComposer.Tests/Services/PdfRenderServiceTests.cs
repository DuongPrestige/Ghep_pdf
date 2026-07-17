using System.IO;
using PDFPageComposer.App.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace PDFPageComposer.Tests.Services;

public sealed class PdfRenderServiceTests
{
    [Fact]
    public async Task RenderPageAsync_returns_bgra_pixels_for_requested_page()
    {
        var pdfPath = Path.Combine(Path.GetTempPath(), $"pdf-page-composer-render-{Guid.NewGuid():N}.pdf");
        CreateFixturePdf(pdfPath);

        try
        {
            using var pdfium = new PdfiumLibrary();
            var service = new PdfRenderService(pdfium);

            var result = await service.RenderPageAsync(pdfPath, pageNumber: 1, pixelWidth: 120, CancellationToken.None);

            Assert.Equal(120, result.PixelWidth);
            Assert.True(result.PixelHeight > 0);
            Assert.True(result.Stride >= result.PixelWidth * 4);
            Assert.Equal(result.Stride * result.PixelHeight, result.Bgra32Pixels.Length);
        }
        finally
        {
            if (File.Exists(pdfPath))
            {
                File.Delete(pdfPath);
            }
        }
    }

    [Fact]
    public async Task RenderPageAsync_renders_requested_page_beyond_first_page()
    {
        var pdfPath = Path.Combine(Path.GetTempPath(), $"pdf-page-composer-render-pages-{Guid.NewGuid():N}.pdf");
        CreateColoredFixturePdf(pdfPath);

        try
        {
            using var pdfium = new PdfiumLibrary();
            var service = new PdfRenderService(pdfium);

            var first = await service.RenderPageAsync(pdfPath, pageNumber: 1, pixelWidth: 80, CancellationToken.None);
            var second = await service.RenderPageAsync(pdfPath, pageNumber: 2, pixelWidth: 80, CancellationToken.None);
            var third = await service.RenderPageAsync(pdfPath, pageNumber: 3, pixelWidth: 80, CancellationToken.None);

            Assert.NotEqual(first.Bgra32Pixels, second.Bgra32Pixels);
            Assert.NotEqual(second.Bgra32Pixels, third.Bgra32Pixels);
        }
        finally
        {
            if (File.Exists(pdfPath))
            {
                File.Delete(pdfPath);
            }
        }
    }

    [Fact]
    public async Task RenderPageAsync_observes_pre_canceled_token()
    {
        using var pdfium = new PdfiumLibrary();
        var service = new PdfRenderService(pdfium);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RenderPageAsync(@"D:\Docs\a.pdf", pageNumber: 1, pixelWidth: 120, cancellation.Token));
    }

    private static void CreateFixturePdf(string pdfPath)
    {
        using var document = new PdfDocument();
        document.AddPage();
        document.Save(pdfPath);
    }

    private static void CreateColoredFixturePdf(string pdfPath)
    {
        using var document = new PdfDocument();
        AddColoredPage(document, XColors.Red);
        AddColoredPage(document, XColors.Green);
        AddColoredPage(document, XColors.Blue);
        document.Save(pdfPath);
    }

    private static void AddColoredPage(PdfDocument document, XColor color)
    {
        var page = document.AddPage();
        using var graphics = XGraphics.FromPdfPage(page);
        graphics.DrawRectangle(new XSolidBrush(color), 0, 0, page.Width.Point, page.Height.Point);
    }
}
