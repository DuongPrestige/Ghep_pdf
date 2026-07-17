using System.IO;
using System.Security.Cryptography;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PDFPageComposer.App.Services;

public sealed class PdfExportService : IPdfExportService
{
    public Task ExportAsync(
        IReadOnlyCollection<SourcePdfFile> sourceFiles,
        IReadOnlyCollection<OutputGroup> groups,
        string outputPath,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        return Task.Run(() => Export(sourceFiles, groups, outputPath, progress, cancellationToken), cancellationToken);
    }

    private static void Export(
        IReadOnlyCollection<SourcePdfFile> sourceFiles,
        IReadOnlyCollection<OutputGroup> groups,
        string outputPath,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var flattenedItems = groups.SelectMany(group => group.Items).ToList();
        if (flattenedItems.Count == 0)
        {
            throw new PdfExportException(PdfExportError.EmptyOutput, "Output tray is empty.");
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        var sourceById = sourceFiles.ToDictionary(file => file.Id);
        ValidateSources(sourceFiles, flattenedItems, fullOutputPath);

        var outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new PdfExportException(PdfExportError.DestinationUnavailable, "Output directory is invalid.");
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PdfExportException(PdfExportError.DestinationUnavailable, "Output directory is unavailable.", ex);
        }

        var tempPath = Path.Combine(outputDirectory, $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using var outputDocument = new PdfDocument();
            using var inputCache = new PdfDocumentCache();
            var exported = 0;

            foreach (var item in flattenedItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sourceFile = sourceById[item.SourceFileId];
                if (item.SourcePageNumber < 1 || item.SourcePageNumber > sourceFile.PageCount)
                {
                    throw new PdfExportException(PdfExportError.InvalidPage, "Output item references an invalid source page.");
                }

                var sourceDocument = inputCache.Get(sourceFile.FilePath);
                outputDocument.AddPage(sourceDocument.Pages[item.SourcePageNumber - 1]);
                exported++;
                progress?.Report(exported);
            }

            outputDocument.Save(tempPath);

            if (File.Exists(fullOutputPath))
            {
                File.Delete(fullOutputPath);
            }

            File.Move(tempPath, fullOutputPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw new PdfExportException(PdfExportError.DestinationUnavailable, "Could not write output PDF.", ex);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    private static void ValidateSources(
        IReadOnlyCollection<SourcePdfFile> sourceFiles,
        IReadOnlyCollection<OutputPageItem> flattenedItems,
        string fullOutputPath)
    {
        var sourceById = sourceFiles.ToDictionary(file => file.Id);
        foreach (var sourceFile in sourceFiles)
        {
            var fullSourcePath = Path.GetFullPath(sourceFile.FilePath);
            if (string.Equals(fullSourcePath, fullOutputPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new PdfExportException(PdfExportError.OutputMatchesSource, "Output path must not overwrite a source PDF.");
            }

            if (!File.Exists(fullSourcePath))
            {
                throw new PdfExportException(PdfExportError.MissingSource, "A source PDF is missing.");
            }

            if (!string.Equals(ComputeFingerprint(fullSourcePath), sourceFile.Fingerprint, StringComparison.Ordinal))
            {
                throw new PdfExportException(PdfExportError.SourceChanged, "A source PDF has changed since import.");
            }
        }

        foreach (var item in flattenedItems)
        {
            if (!sourceById.ContainsKey(item.SourceFileId))
            {
                throw new PdfExportException(PdfExportError.MissingSource, "Output item references an unknown source PDF.");
            }
        }
    }

    private static string ComputeFingerprint(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return $"{Convert.ToHexString(hash)}:{fileInfo.Length}:{fileInfo.LastWriteTimeUtc.Ticks}";
    }

    private sealed class PdfDocumentCache : IDisposable
    {
        private readonly Dictionary<string, PdfDocument> documents = new(StringComparer.OrdinalIgnoreCase);

        public PdfDocument Get(string filePath)
        {
            var fullPath = Path.GetFullPath(filePath);
            if (!documents.TryGetValue(fullPath, out var document))
            {
                document = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
                documents.Add(fullPath, document);
            }

            return document;
        }

        public void Dispose()
        {
            foreach (var document in documents.Values)
            {
                document.Dispose();
            }
        }
    }
}
