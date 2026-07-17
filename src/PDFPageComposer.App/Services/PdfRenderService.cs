using System.IO;
using System.Runtime.InteropServices;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFiumCore;

namespace PDFPageComposer.App.Services;

public sealed class PdfRenderService : IPdfRenderService
{
    private static readonly SemaphoreSlim RenderGate = new(1, 1);

    public PdfRenderService(PdfiumLibrary pdfiumLibrary)
    {
        ArgumentNullException.ThrowIfNull(pdfiumLibrary);
    }

    public Task<PdfPageRenderResult> RenderPageAsync(
        string filePath,
        int pageNumber,
        int pixelWidth,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNumber);
        ArgumentOutOfRangeException.ThrowIfLessThan(pixelWidth, 16);

        return Task.Run(async () =>
        {
            await RenderGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return RenderPage(filePath, pageNumber, pixelWidth, cancellationToken);
            }
            finally
            {
                RenderGate.Release();
            }
        }, cancellationToken);
    }

    private static PdfPageRenderResult RenderPage(
        string filePath,
        int pageNumber,
        int pixelWidth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(filePath);
        var document = fpdfview.FPDF_LoadDocument(fullPath, null);
        if (document == null)
        {
            throw new PdfMetadataException(fullPath, PdfMetadataError.Unknown, "PDFium could not open the file for rendering.");
        }

        try
        {
            var pageCount = fpdfview.FPDF_GetPageCount(document);
            if (pageNumber > pageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number exceeds document page count.");
            }

            var page = fpdfview.FPDF_LoadPage(document, pageNumber - 1);
            if (page == null)
            {
                throw new PdfMetadataException(fullPath, PdfMetadataError.InvalidPdf, "PDFium could not load the requested page.");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pageWidth = fpdfview.FPDF_GetPageWidthF(page);
                var pageHeight = fpdfview.FPDF_GetPageHeightF(page);
                var pixelHeight = Math.Max(1, (int)Math.Round(pixelWidth * pageHeight / pageWidth));
                var bitmap = fpdfview.FPDFBitmapCreate(pixelWidth, pixelHeight, 1);
                if (bitmap == null)
                {
                    throw new InvalidOperationException("PDFium could not allocate a render bitmap.");
                }

                try
                {
                    _ = fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, pixelWidth, pixelHeight, 0xFFFFFFFF);
                    fpdfview.FPDF_RenderPageBitmap(bitmap, page, 0, 0, pixelWidth, pixelHeight, 0, 0);
                    cancellationToken.ThrowIfCancellationRequested();

                    var stride = fpdfview.FPDFBitmapGetStride(bitmap);
                    var buffer = fpdfview.FPDFBitmapGetBuffer(bitmap);
                    var pixels = new byte[stride * pixelHeight];
                    Marshal.Copy(buffer, pixels, 0, pixels.Length);
                    return new PdfPageRenderResult(pixelWidth, pixelHeight, stride, pixels);
                }
                finally
                {
                    fpdfview.FPDFBitmapDestroy(bitmap);
                }
            }
            finally
            {
                fpdfview.FPDF_ClosePage(page);
            }
        }
        finally
        {
            fpdfview.FPDF_CloseDocument(document);
        }
    }
}
