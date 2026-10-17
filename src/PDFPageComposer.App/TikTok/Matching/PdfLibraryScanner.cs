using System.IO;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Matching;

/// <summary>Scans the print library folder (TT-01). Never throws for a single bad file; marks it invalid instead.</summary>
public sealed class PdfLibraryScanner : IPdfLibraryScanner
{
    private readonly IPdfMetadataService _metadataService;

    public PdfLibraryScanner(IPdfMetadataService metadataService)
    {
        ArgumentNullException.ThrowIfNull(metadataService);
        _metadataService = metadataService;
    }

    public Task<IReadOnlyList<PdfAsset>> ScanAsync(
        string folder,
        IReadOnlyList<PdfAsset> previous,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(previous);

        return Task.Run(() => ScanCoreAsync(folder, previous, today, cancellationToken), cancellationToken);
    }

    private async Task<IReadOnlyList<PdfAsset>> ScanCoreAsync(
        string folder,
        IReadOnlyList<PdfAsset> previous,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var fullFolder = Path.GetFullPath(folder);
        if (!Directory.Exists(fullFolder))
        {
            throw new DirectoryNotFoundException($"Không tìm thấy thư mục kho PDF: {fullFolder}");
        }

        var cache = new Dictionary<string, PdfAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in previous)
        {
            if (!string.IsNullOrWhiteSpace(asset.FilePath))
            {
                cache[Path.GetFullPath(asset.FilePath)] = asset;
            }
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MatchCasing = MatchCasing.CaseInsensitive,
            IgnoreInaccessible = true,
        };

        var files = Directory.EnumerateFiles(fullFolder, "*.pdf", options)
            .Where(path => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<PdfAsset>(files.Count);
        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(await ScanFileAsync(filePath, cache, today, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    private async Task<PdfAsset> ScanFileAsync(
        string filePath,
        IReadOnlyDictionary<string, PdfAsset> cache,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        DateTime lastWriteTimeUtc;
        try
        {
            lastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Invalid(filePath, default, "Không đọc được thông tin file.");
        }

        if (!PdfAssetNameParser.TryParse(Path.GetFileName(filePath), today, out var idol, out var pageCount, out var fileDate))
        {
            return Invalid(filePath, lastWriteTimeUtc, PdfAssetNameParser.InvalidNameError);
        }

        int? actualPageCount = null;
        if (cache.TryGetValue(filePath, out var cached)
            && cached.LastWriteTimeUtc == lastWriteTimeUtc
            && cached.ActualPageCount is not null)
        {
            actualPageCount = cached.ActualPageCount;
        }

        if (actualPageCount is null)
        {
            var readResult = await ReadPageCountAsync(filePath, cancellationToken).ConfigureAwait(false);
            if (readResult.Error is not null)
            {
                return new PdfAsset(filePath, idol, pageCount, fileDate, null, lastWriteTimeUtc, false, readResult.Error);
            }

            actualPageCount = readResult.PageCount;
        }

        if (actualPageCount != pageCount)
        {
            return new PdfAsset(
                filePath,
                idol,
                pageCount,
                fileDate,
                actualPageCount,
                lastWriteTimeUtc,
                false,
                $"Số trang thực tế ({actualPageCount}) khác số trang trên tên file ({pageCount})");
        }

        return new PdfAsset(filePath, idol, pageCount, fileDate, actualPageCount, lastWriteTimeUtc, true, null);
    }

    private async Task<(int? PageCount, string? Error)> ReadPageCountAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await _metadataService.ReadAsync(filePath, cancellationToken).ConfigureAwait(false);
            return (metadata.PageCount, null);
        }
        catch (PdfMetadataException exception)
        {
            return (null, DescribeMetadataError(exception.Error));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, "Không mở được file PDF.");
        }
    }

    private static string DescribeMetadataError(PdfMetadataError error) => error switch
    {
        PdfMetadataError.NotFound => "File không còn tồn tại.",
        PdfMetadataError.NotPdf => "File không phải PDF.",
        PdfMetadataError.PermissionDenied => "Không có quyền đọc file.",
        PdfMetadataError.PasswordRequired => "File PDF có mật khẩu.",
        PdfMetadataError.InvalidPdf => "File PDF bị lỗi hoặc không hợp lệ.",
        _ => "Không đọc được file PDF.",
    };

    private static PdfAsset Invalid(string filePath, DateTime lastWriteTimeUtc, string error) =>
        new(filePath, string.Empty, 0, default, null, lastWriteTimeUtc, false, error);
}
