using Microsoft.Win32;
using PDFPageComposer.App.Interfaces;

namespace PDFPageComposer.App.Services;

public sealed class FileDialogService : IFileDialogService
{
    public IReadOnlyList<string> PickPdfFiles()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Multiselect = true,
            Title = "Chon file PDF"
        };

        return dialog.ShowDialog() == true
            ? dialog.FileNames
            : [];
    }

    public string? PickOutputPdfFile()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Title = "Chọn nơi lưu PDF",
            DefaultExt = ".pdf",
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }
}
