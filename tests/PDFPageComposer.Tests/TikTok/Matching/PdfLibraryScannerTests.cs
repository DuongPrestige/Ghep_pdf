using System.IO;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;
using PDFPageComposer.App.TikTok.Matching;
using PDFPageComposer.App.TikTok.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PDFPageComposer.Tests.TikTok.Matching;

public sealed class PdfLibraryScannerTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 16);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"gpdf-tiktok-library-{Guid.NewGuid():N}");

    public PdfLibraryScannerTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task ScanAsync_reads_real_pdfs_and_flags_invalid_files()
    {
        var valid = CreatePdf("IVAN - 3 PAGE - 15.10.pdf", 3);
        var wrongCount = CreatePdf("CODY - 4 PAGE - 14.10.pdf", 2);
        var badName = CreatePdf("random file.pdf", 1);
        var nested = CreatePdf(Path.Combine("sub", "deep", "JACK - 2 pages - 1.10.PDF"), 2);
        await File.WriteAllTextAsync(Path.Combine(_directory, "notes.txt"), "x");

        using var pdfium = new PdfiumLibrary();
        var scanner = new PdfLibraryScanner(new PdfMetadataService(pdfium));

        var result = await scanner.ScanAsync(_directory, [], Today, CancellationToken.None);

        Assert.Equal(4, result.Count);
        Assert.Equal(result.Select(asset => asset.FilePath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase), result.Select(asset => asset.FilePath));

        var ivan = Single(result, valid);
        Assert.True(ivan.IsValid);
        Assert.Equal("IVAN", ivan.Idol);
        Assert.Equal(3, ivan.PageCount);
        Assert.Equal(3, ivan.ActualPageCount);
        Assert.Equal(new DateOnly(2026, 10, 15), ivan.FileDate);
        Assert.Null(ivan.Error);

        var cody = Single(result, wrongCount);
        Assert.False(cody.IsValid);
        Assert.Equal(2, cody.ActualPageCount);
        Assert.Equal("Số trang thực tế (2) khác số trang trên tên file (4)", cody.Error);

        var bad = Single(result, badName);
        Assert.False(bad.IsValid);
        Assert.Equal(string.Empty, bad.Idol);
        Assert.Equal(0, bad.PageCount);
        Assert.Equal(default, bad.FileDate);
        Assert.Equal("Tên file không đúng quy ước <IDOL> - <N> PAGE - <dd.MM>.pdf", bad.Error);

        var jack = Single(result, nested);
        Assert.True(jack.IsValid);
        Assert.Equal("JACK", jack.Idol);
    }

    [Fact]
    public async Task ScanAsync_reuses_previous_entry_when_timestamp_matches()
    {
        var first = CreatePdf("IVAN - 3 PAGE - 15.10.pdf", 3);
        var second = CreatePdf("IVAN - 3 PAGE - 10.10.pdf", 3);
        var metadata = new CountingMetadataService();
        var scanner = new PdfLibraryScanner(metadata);

        var initial = await scanner.ScanAsync(_directory, [], Today, CancellationToken.None);
        Assert.Equal(2, metadata.Calls);
        Assert.All(initial, asset => Assert.True(asset.IsValid));

        var cached = await scanner.ScanAsync(_directory, initial, Today, CancellationToken.None);
        Assert.Equal(2, metadata.Calls);
        Assert.Equal(initial, cached);

        File.SetLastWriteTimeUtc(second, File.GetLastWriteTimeUtc(second).AddMinutes(5));
        var afterTouch = await scanner.ScanAsync(_directory, cached, Today, CancellationToken.None);
        Assert.Equal(3, metadata.Calls);
        Assert.Equal(metadata.LastPath, Path.GetFullPath(second), StringComparer.OrdinalIgnoreCase);
        Assert.Contains(afterTouch, asset => string.Equals(asset.FilePath, Path.GetFullPath(first), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ScanAsync_marks_unreadable_pdf_invalid_without_throwing()
    {
        CreatePdf("IVAN - 3 PAGE - 15.10.pdf", 3);
        var scanner = new PdfLibraryScanner(new CountingMetadataService(throwError: PdfMetadataError.InvalidPdf));

        var result = await scanner.ScanAsync(_directory, [], Today, CancellationToken.None);

        var asset = Assert.Single(result);
        Assert.False(asset.IsValid);
        Assert.Equal("IVAN", asset.Idol);
        Assert.Null(asset.ActualPageCount);
        Assert.False(string.IsNullOrWhiteSpace(asset.Error));
    }

    [Fact]
    public async Task ScanAsync_throws_for_missing_folder()
    {
        var scanner = new PdfLibraryScanner(new CountingMetadataService());

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => scanner.ScanAsync(Path.Combine(_directory, "missing"), [], Today, CancellationToken.None));
    }

    [Fact]
    public async Task ScanAsync_honors_cancellation()
    {
        CreatePdf("IVAN - 3 PAGE - 15.10.pdf", 3);
        var scanner = new PdfLibraryScanner(new CountingMetadataService());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scanner.ScanAsync(_directory, [], Today, cancellation.Token));
    }

    private static PdfAsset Single(IReadOnlyList<PdfAsset> assets, string path) =>
        Assert.Single(assets, asset => string.Equals(asset.FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));

    private string CreatePdf(string relativePath, int pageCount)
    {
        var path = Path.Combine(_directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var document = new PdfDocument();
        for (var index = 0; index < pageCount; index++)
        {
            document.AddPage();
        }

        document.Save(path);
        return path;
    }

    private sealed class CountingMetadataService(PdfMetadataError? throwError = null) : IPdfMetadataService
    {
        public int Calls { get; private set; }

        public string? LastPath { get; private set; }

        public async Task<SourcePdfFile> ReadAsync(string filePath, CancellationToken cancellationToken)
        {
            Calls++;
            LastPath = filePath;
            if (throwError is { } error)
            {
                throw new PdfMetadataException(filePath, error, "test");
            }

            await Task.Yield();
            using var document = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
            return new SourcePdfFile(Guid.NewGuid(), filePath, Path.GetFileName(filePath), document.PageCount, 0, "test");
        }
    }
}
