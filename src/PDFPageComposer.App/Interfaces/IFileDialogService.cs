namespace PDFPageComposer.App.Interfaces;

public interface IFileDialogService
{
    IReadOnlyList<string> PickPdfFiles();

    string? PickOutputPdfFile();
}
