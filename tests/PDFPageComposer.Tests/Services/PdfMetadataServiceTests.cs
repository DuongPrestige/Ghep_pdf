using System.IO;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;
using PdfSharp.Pdf;

namespace PDFPageComposer.Tests.Services;

public sealed class PdfMetadataServiceTests
{
    [Fact]
    public async Task ReadAsync_reads_page_count_size_and_fingerprint()
    {
        var pdfPath = Path.Combine(Path.GetTempPath(), $"pdf-page-composer-{Guid.NewGuid():N}.pdf");
        CreateFixturePdf(pdfPath);

        try
        {
            using var pdfium = new PdfiumLibrary();
            var service = new PdfMetadataService(pdfium);

            var sourceFile = await service.ReadAsync(pdfPath, CancellationToken.None);

            Assert.Equal(Path.GetFullPath(pdfPath), sourceFile.FilePath);
            Assert.Equal(2, sourceFile.PageCount);
            Assert.Equal(2, sourceFile.Pages.Count);
            Assert.All(sourceFile.Pages, page => Assert.Equal(sourceFile.Id, page.SourceFileId));
            Assert.All(sourceFile.Pages, page => Assert.True(page.Width > 0));
            Assert.All(sourceFile.Pages, page => Assert.True(page.Height > 0));
            Assert.NotEmpty(sourceFile.Fingerprint);
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
    public async Task ReadAsync_rejects_non_pdf_file()
    {
        var textPath = Path.Combine(Path.GetTempPath(), $"pdf-page-composer-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(textPath, "not a pdf");
        using var pdfium = new PdfiumLibrary();
        var service = new PdfMetadataService(pdfium);

        try
        {
            var exception = await Assert.ThrowsAsync<PdfMetadataException>(
                () => service.ReadAsync(textPath, CancellationToken.None));

            Assert.Equal(PdfMetadataError.NotPdf, exception.Error);
        }
        finally
        {
            File.Delete(textPath);
        }
    }

    [Fact]
    public async Task ReadAsync_reports_missing_file()
    {
        using var pdfium = new PdfiumLibrary();
        var service = new PdfMetadataService(pdfium);
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pdf");

        var exception = await Assert.ThrowsAsync<PDFPageComposer.App.Models.PdfMetadataException>(
            () => service.ReadAsync(missingPath, CancellationToken.None));

        Assert.Equal(PDFPageComposer.App.Models.PdfMetadataError.NotFound, exception.Error);
    }

    [Fact]
    public async Task ReadAsync_reports_corrupt_pdf()
    {
        var pdfPath = Path.Combine(Path.GetTempPath(), $"corrupt-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(pdfPath, "%PDF-1.7 corrupt");
        using var pdfium = new PdfiumLibrary();
        var service = new PdfMetadataService(pdfium);

        try
        {
            var exception = await Assert.ThrowsAsync<PDFPageComposer.App.Models.PdfMetadataException>(
                () => service.ReadAsync(pdfPath, CancellationToken.None));

            Assert.True(
                exception.Error is PDFPageComposer.App.Models.PdfMetadataError.InvalidPdf or PDFPageComposer.App.Models.PdfMetadataError.Unknown,
                $"Unexpected error type: {exception.Error}");
        }
        finally
        {
            File.Delete(pdfPath);
        }
    }

    private static void CreateFixturePdf(string pdfPath)
    {
        using var document = new PdfDocument();
        document.AddPage();
        document.AddPage();
        document.Save(pdfPath);
    }
}
