namespace PDFPageComposer.App.Models;

public sealed class PdfExportException : Exception
{
    public PdfExportException(PdfExportError error, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public PdfExportError Error { get; }
}
