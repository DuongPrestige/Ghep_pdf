namespace PDFPageComposer.App.Models;

public enum PdfExportError
{
    EmptyOutput,
    OutputMatchesSource,
    MissingSource,
    SourceChanged,
    InvalidPage,
    DestinationUnavailable,
    Unknown
}
