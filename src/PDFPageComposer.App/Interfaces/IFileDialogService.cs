namespace PDFPageComposer.App.Interfaces;

public interface IFileDialogService
{
    IReadOnlyList<string> PickPdfFiles();

    string? PickOutputPdfFile();

    /// <summary>Lets the user choose one or more Seller Center CSV exports. Returns an empty list when cancelled.</summary>
    IReadOnlyList<string> PickCsvFiles();

    /// <summary>Lets the user choose a folder. Returns null when cancelled.</summary>
    string? PickFolder(string? initialFolder);

    /// <summary>Save dialog for a PDF with a suggested file name. Returns null when cancelled.</summary>
    string? PickOutputPdfFile(string suggestedFileName, string? initialFolder);

    /// <summary>Save dialog for a CSV file with a suggested file name. Returns null when cancelled.</summary>
    string? PickSaveCsvFile(string suggestedFileName, string? initialFolder);

    /// <summary>Yes/No warning box for a destructive action. Returns true when the user chose Yes.</summary>
    bool Confirm(string message, string title);
}
