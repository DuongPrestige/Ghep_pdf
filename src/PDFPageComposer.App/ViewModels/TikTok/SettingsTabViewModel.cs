using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>Tab "Cài đặt": folders, Seller Note rule, default separator mode and shops (TASK-066).</summary>
public sealed partial class SettingsTabViewModel : ObservableObject
{
    private readonly TikTokWorkspace workspace;

    public SettingsTabViewModel(TikTokWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        this.workspace = workspace;
        defaultSeparatorMode = TikTokLabels.SeparatorModeOptions[1];
    }

    public TikTokWorkspace Workspace => workspace;

    public IReadOnlyList<TikTokOption<SeparatorMode>> SeparatorModes => TikTokLabels.SeparatorModeOptions;

    [ObservableProperty]
    private string? pdfLibraryFolder;

    [ObservableProperty]
    private string? outputFolder;

    [ObservableProperty]
    private bool sellerNoteMarksPrinted = true;

    [ObservableProperty]
    private bool blockOrdersWithNotes = true;

    [ObservableProperty]
    private bool alwaysUseNewestFiles = true;

    [ObservableProperty]
    private TikTokOption<SeparatorMode> defaultSeparatorMode;

    [ObservableProperty]
    private string? newShopName;

    [ObservableProperty]
    private TikTokShop? selectedShop;

    [ObservableProperty]
    private string? renameShopName;

    partial void OnSelectedShopChanged(TikTokShop? value) => RenameShopName = value?.Name;

    /// <summary>Copies the stored settings into the editable fields.</summary>
    public void Refresh()
    {
        var settings = workspace.Settings;
        PdfLibraryFolder = settings.PdfLibraryFolder;
        OutputFolder = settings.OutputFolder;
        SellerNoteMarksPrinted = settings.SellerNoteMarksPrinted;
        BlockOrdersWithNotes = settings.BlockOrdersWithNotes;
        AlwaysUseNewestFiles = settings.AlwaysUseNewestFiles;
        DefaultSeparatorMode = SeparatorModes.FirstOrDefault(option => option.Value == settings.DefaultSeparatorMode)
            ?? SeparatorModes[1];
    }

    [RelayCommand]
    private void PickLibraryFolder()
    {
        var picked = workspace.Services.FileDialogs.PickFolder(PdfLibraryFolder);
        if (picked is not null)
        {
            PdfLibraryFolder = picked;
        }
    }

    [RelayCommand]
    private void PickOutputFolder()
    {
        var picked = workspace.Services.FileDialogs.PickFolder(OutputFolder);
        if (picked is not null)
        {
            OutputFolder = picked;
        }
    }

    [RelayCommand]
    private void ClearOutputFolder()
    {
        OutputFolder = null;
    }

    [RelayCommand]
    private Task SaveAsync() =>
        workspace.RunAsync(
            "Đang lưu cài đặt...",
            async cancellationToken =>
            {
                // Edit a copy: the live settings change only after the file was saved.
                var previousDefault = workspace.Settings.DefaultSeparatorMode;
                var candidate = workspace.CopySettings();
                candidate.PdfLibraryFolder = string.IsNullOrWhiteSpace(PdfLibraryFolder) ? null : PdfLibraryFolder.Trim();
                candidate.OutputFolder = string.IsNullOrWhiteSpace(OutputFolder) ? null : OutputFolder.Trim();
                candidate.SellerNoteMarksPrinted = SellerNoteMarksPrinted;
                candidate.BlockOrdersWithNotes = BlockOrdersWithNotes;
                candidate.AlwaysUseNewestFiles = AlwaysUseNewestFiles;
                candidate.DefaultSeparatorMode = DefaultSeparatorMode.Value;
                await workspace.SaveSettingsAsync(candidate, cancellationToken);
                if (previousDefault != candidate.DefaultSeparatorMode)
                {
                    // Before the reload, so the Orders preview is planned with the new mode.
                    workspace.NotifyDefaultSeparatorModeChanged();
                }

                await workspace.ReloadAllAsync(cancellationToken);
                workspace.StatusText = "Đã lưu cài đặt.";
            },
            "SaveTikTokSettings");

    [RelayCommand]
    private Task AddShopAsync()
    {
        var name = NewShopName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            workspace.ShowError("Nhập tên shop.");
            return Task.CompletedTask;
        }

        return workspace.RunAsync(
            "Đang thêm shop...",
            async cancellationToken =>
            {
                var repository = workspace.Services.Repository;
                await Task.Run(() => repository.AddShopAsync(name, cancellationToken), cancellationToken);
                await workspace.ReloadShopsAsync(cancellationToken);
                NewShopName = null;
                workspace.StatusText = $"Đã thêm shop \"{name}\".";
            },
            "AddShop");
    }

    [RelayCommand]
    private Task RenameShopAsync()
    {
        var shop = SelectedShop;
        var name = RenameShopName?.Trim();
        if (shop is null || string.IsNullOrEmpty(name))
        {
            workspace.ShowError("Chọn shop và nhập tên mới.");
            return Task.CompletedTask;
        }

        return workspace.RunAsync(
            "Đang đổi tên shop...",
            async cancellationToken =>
            {
                var repository = workspace.Services.Repository;
                await Task.Run(() => repository.RenameShopAsync(shop.ShopId, name, cancellationToken), cancellationToken);
                await workspace.ReloadAllAsync(cancellationToken);
                workspace.StatusText = $"Đã đổi tên shop thành \"{name}\".";
            },
            "RenameShop");
    }
}
