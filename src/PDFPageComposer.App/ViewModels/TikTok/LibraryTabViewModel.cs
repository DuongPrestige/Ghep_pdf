using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>One library file in the "Kho PDF" tab.</summary>
public sealed record PdfAssetRow(PdfAsset Asset)
{
    public string FileName => Path.GetFileName(Asset.FilePath);

    public string FilePath => Asset.FilePath;

    public string Idol => Asset.Idol;

    public string PageCountText => Asset.IsValid || Asset.PageCount > 0
        ? Asset.PageCount.ToString(CultureInfo.InvariantCulture)
        : string.Empty;

    public string FileDateText => Asset.IsValid
        ? Asset.FileDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
        : string.Empty;

    public string ActualPageCountText => Asset.ActualPageCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public bool IsValid => Asset.IsValid;

    public string StatusText => Asset.IsValid ? "Hợp lệ" : Asset.Error ?? "Lỗi";
}

/// <summary>Tab "Kho PDF": library folder and scan (TASK-066).</summary>
public sealed partial class LibraryTabViewModel : ObservableObject
{
    private readonly TikTokWorkspace workspace;
    private List<PdfAssetRow> allRows = [];

    public LibraryTabViewModel(TikTokWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        this.workspace = workspace;
    }

    public TikTokWorkspace Workspace => workspace;

    public ObservableCollection<PdfAssetRow> Rows { get; } = [];

    public string Summary
    {
        get
        {
            var valid = allRows.Count(row => row.IsValid);
            return $"{allRows.Count} file: {valid} hợp lệ, {allRows.Count - valid} lỗi";
        }
    }

    [ObservableProperty]
    private string? folder;

    [ObservableProperty]
    private bool showInvalidOnly;

    partial void OnShowInvalidOnlyChanged(bool value) => ApplyFilter();

    /// <summary>Rebuilds the rows from the workspace snapshot.</summary>
    public void Refresh()
    {
        Folder = workspace.Settings.PdfLibraryFolder;
        allRows = workspace.Assets.Select(asset => new PdfAssetRow(asset)).ToList();
        ApplyFilter();
        OnPropertyChanged(nameof(Summary));
    }

    [RelayCommand]
    private Task PickFolderAsync()
    {
        var picked = workspace.Services.FileDialogs.PickFolder(Folder);
        if (picked is null)
        {
            return Task.CompletedTask;
        }

        return workspace.RunAsync(
            "Đang lưu thư mục kho...",
            async cancellationToken =>
            {
                var candidate = workspace.CopySettings();
                candidate.PdfLibraryFolder = picked;
                await workspace.SaveSettingsAsync(candidate, cancellationToken);
                Folder = picked;
                workspace.StatusText = "Đã chọn thư mục kho. Bấm \"Quét kho\" để cập nhật danh sách file.";
            },
            "PickLibraryFolder");
    }

    [RelayCommand]
    private Task ScanAsync()
    {
        var folderToScan = Folder;
        if (string.IsNullOrWhiteSpace(folderToScan))
        {
            workspace.ShowError("Chưa chọn thư mục kho PDF.");
            return Task.CompletedTask;
        }

        return workspace.RunAsync(
            "Đang quét kho PDF...",
            async cancellationToken =>
            {
                var services = workspace.Services;
                var today = workspace.Today;
                var scanned = await Task.Run(
                    async () =>
                    {
                        var previous = await services.Repository.GetPdfAssetsAsync(cancellationToken);
                        var assets = await services.LibraryScanner.ScanAsync(folderToScan, previous, today, cancellationToken);
                        await services.Repository.ReplacePdfAssetsAsync(assets, cancellationToken);
                        return assets;
                    },
                    cancellationToken);

                workspace.SetAssets(scanned);
                Refresh();
                await workspace.RefreshOrdersAsync(cancellationToken);
                workspace.StatusText = $"Đã quét kho: {Summary}.";
            },
            "ScanLibrary");
    }

    private void ApplyFilter()
    {
        Rows.Clear();
        foreach (var row in allRows.Where(row => !ShowInvalidOnly || !row.IsValid))
        {
            Rows.Add(row);
        }
    }
}
