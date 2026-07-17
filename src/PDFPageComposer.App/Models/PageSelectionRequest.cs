namespace PDFPageComposer.App.Models;

public sealed record PageSelectionRequest(SourcePdfPage Page, SelectionGesture Gesture);
