using PDFiumCore;

namespace PDFPageComposer.App.Services;

public sealed class PdfiumLibrary : IDisposable
{
    private static readonly object SyncRoot = new();
    private static int referenceCount;
    private bool disposed;

    public PdfiumLibrary()
    {
        lock (SyncRoot)
        {
            if (referenceCount == 0)
            {
                fpdfview.FPDF_InitLibrary();
            }

            referenceCount++;
        }
    }

    public void Dispose()
    {
        lock (SyncRoot)
        {
            if (disposed)
            {
                return;
            }

            referenceCount--;
            if (referenceCount == 0)
            {
                fpdfview.FPDF_DestroyLibrary();
            }

            disposed = true;
        }
    }
}
