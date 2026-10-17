using System.IO;
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

    public IReadOnlyList<string> PickCsvFiles()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "File xuất đơn (*.csv)|*.csv",
            Multiselect = true,
            Title = "Chọn file xuất đơn TikTok Seller Center"
        };

        return dialog.ShowDialog() == true
            ? dialog.FileNames
            : [];
    }

    public string? PickFolder(string? initialFolder)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        return dialog.ShowDialog() == true
            ? dialog.FolderName
            : null;
    }

    public string? PickOutputPdfFile(string suggestedFileName, string? initialFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);

        var dialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Title = "Chọn nơi lưu file in",
            DefaultExt = ".pdf",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = suggestedFileName
        };

        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }

    public string? PickSaveCsvFile(string suggestedFileName, string? initialFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);

        var dialog = new SaveFileDialog
        {
            Filter = "File CSV (*.csv)|*.csv",
            Title = "Chọn nơi lưu danh sách đơn",
            DefaultExt = ".csv",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = suggestedFileName
        };

        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }

    public bool Confirm(string message, string title) =>
        System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No) == System.Windows.MessageBoxResult.Yes;
}
