using System.IO;
using System.Text;
using PDFiumCore;
using PDFPageComposer.App.Services;
using PDFPageComposer.App.TikTok.Export;
using PDFPageComposer.App.TikTok.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PDFPageComposer.Tests.TikTok.Export;

public sealed class SeparatorPageRendererTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"gpdf-separator-test-{Guid.NewGuid():N}");

    [Fact]
    public async Task RenderAsync_writes_single_a4_portrait_page_into_temp_directory()
    {
        var renderer = new SeparatorPageRenderer();

        var path = await renderer.RenderAsync(CreateInfo(), directory, CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.Equal(Path.GetFullPath(directory), Path.GetDirectoryName(Path.GetFullPath(path)));
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.Equal(1, document.PageCount);
        Assert.Equal(595.3, document.Pages[0].Width.Point, precision: 0);
        Assert.Equal(841.9, document.Pages[0].Height.Point, precision: 0);
    }

    [Fact]
    public async Task RenderAsync_uses_unique_file_names()
    {
        var renderer = new SeparatorPageRenderer();

        var first = await renderer.RenderAsync(CreateInfo(), directory, CancellationToken.None);
        var second = await renderer.RenderAsync(CreateInfo(), directory, CancellationToken.None);

        Assert.NotEqual(first, second);
        Assert.Equal(2, Directory.GetFiles(directory, "*.pdf").Length);
    }

    [Fact]
    public async Task RenderAsync_writes_a_blank_page_to_save_ink()
    {
        var renderer = new SeparatorPageRenderer();
        var info = CreateInfo() with { ItemLines = Enumerable.Range(1, 80).Select(index => $"IVE · bộ {index}/80").ToList() };

        var path = await renderer.RenderAsync(info, directory, CancellationToken.None);

        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        Assert.Equal(1, document.PageCount);
        Assert.Equal(string.Empty, ExtractText(path));
    }

    [Fact]
    public async Task RenderAsync_honors_cancellation()
    {
        var renderer = new SeparatorPageRenderer();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => renderer.RenderAsync(CreateInfo(), directory, cancellation.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static SeparatorPageInfo CreateInfo() =>
        new(
            "583012345678901234",
            "Shop Hàn Quốc Đẹp",
            "ng***ễn",
            ExportTestHelpers.OrderTime,
            ["IVE · bộ 1/2 · IVE - 3 PAGE - 15.10.pdf", "IVE · bộ 2/2 · IVE - 3 PAGE - 15.10.pdf · TRÙNG HÌNH"],
            6);

    private static string ExtractText(string path)
    {
        using var pdfium = new PdfiumLibrary();
        var document = fpdfview.FPDF_LoadDocument(path, null);
        Assert.NotNull(document);
        try
        {
            var page = fpdfview.FPDF_LoadPage(document, 0);
            var textPage = fpdf_text.FPDFTextLoadPage(page);
            try
            {
                var builder = new StringBuilder();
                var count = fpdf_text.FPDFTextCountChars(textPage);
                for (var index = 0; index < count; index++)
                {
                    builder.Append(char.ConvertFromUtf32((int)fpdf_text.FPDFTextGetUnicode(textPage, index)));
                }

                return builder.ToString().Normalize(NormalizationForm.FormC);
            }
            finally
            {
                fpdf_text.FPDFTextClosePage(textPage);
                fpdfview.FPDF_ClosePage(page);
            }
        }
        finally
        {
            fpdfview.FPDF_CloseDocument(document);
        }
    }
}
