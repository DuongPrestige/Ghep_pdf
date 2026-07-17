using System.IO;
using System.Security.Cryptography;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFiumCore;

namespace PDFPageComposer.App.Services;

public sealed class PdfMetadataService : IPdfMetadataService
{
    public PdfMetadataService(PdfiumLibrary pdfiumLibrary)
    {
        ArgumentNullException.ThrowIfNull(pdfiumLibrary);
    }

    public Task<SourcePdfFile> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return Task.Run(() => Read(filePath, cancellationToken), cancellationToken);
    }

    private static SourcePdfFile Read(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new PdfMetadataException(fullPath, PdfMetadataError.NotFound, "File does not exist.");
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new PdfMetadataException(fullPath, PdfMetadataError.NotPdf, "File is not a PDF.");
        }

        var fileInfo = new FileInfo(fullPath);
        var fingerprint = ComputeFingerprint(fullPath, fileInfo);
        var document = fpdfview.FPDF_LoadDocument(fullPath, null);
        if (document == null)
        {
            throw CreateLoadException(fullPath);
        }

        try
        {
            var sourceFileId = Guid.NewGuid();
            var pageCount = fpdfview.FPDF_GetPageCount(document);
            var pages = new List<SourcePdfPage>(pageCount);

            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double width = 0;
                double height = 0;
                _ = fpdfview.FPDF_GetPageSizeByIndex(document, pageIndex, ref width, ref height);
                pages.Add(new SourcePdfPage(Guid.NewGuid(), sourceFileId, pageIndex + 1)
                {
                    Width = width,
                    Height = height
                });
            }

            return new SourcePdfFile(
                sourceFileId,
                fullPath,
                Path.GetFileName(fullPath),
                pageCount,
                fileInfo.Length,
                fingerprint,
                pages: pages);
        }
        finally
        {
            fpdfview.FPDF_CloseDocument(document);
        }
    }

    private static string ComputeFingerprint(string filePath, FileInfo fileInfo)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return $"{Convert.ToHexString(hash)}:{fileInfo.Length}:{fileInfo.LastWriteTimeUtc.Ticks}";
    }

    private static PdfMetadataException CreateLoadException(string filePath)
    {
        var error = (int)fpdfview.FPDF_GetLastError();
        return error switch
        {
            3 => new PdfMetadataException(filePath, PdfMetadataError.InvalidPdf, "PDF file is invalid or corrupted."),
            4 => new PdfMetadataException(filePath, PdfMetadataError.PasswordRequired, "PDF file requires a password."),
            5 => new PdfMetadataException(filePath, PdfMetadataError.PermissionDenied, "PDF file access is denied."),
            _ => new PdfMetadataException(filePath, PdfMetadataError.Unknown, $"PDFium could not open the file. Error code: {error}.")
        };
    }
}
