namespace PDFPageComposer.App.Models;

public sealed class PdfMetadataException : Exception
{
    public PdfMetadataException(string filePath, PdfMetadataError error, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        FilePath = filePath;
        Error = error;
    }

    public string FilePath { get; }

    public PdfMetadataError Error { get; }
}
