using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFPageComposer.App.TikTok.Import;
using PDFPageComposer.App.TikTok.Models;
using Serilog;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>"Import đơn" panel: shop, mode, CSV files (picked or dropped) and the import report (TASK-066).</summary>
public sealed partial class ImportPanelViewModel : ObservableObject
{
    private readonly TikTokWorkspace workspace;

    public ImportPanelViewModel(TikTokWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        this.workspace = workspace;
    }

    public TikTokWorkspace Workspace => workspace;

    public ObservableCollection<TikTokShop> Shops => workspace.Shops;

    public ObservableCollection<string> PendingFiles { get; } = [];

    public ObservableCollection<string> Warnings { get; } = [];

    public bool HasShops => Shops.Count > 0;

    [ObservableProperty]
    private bool isOpen;

    [ObservableProperty]
    private TikTokShop? selectedShop;

    [ObservableProperty]
    private string? newShopName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPartialMode))]
    private bool isFullMode = true;

    [ObservableProperty]
    private string? reportText;

    public bool IsPartialMode
    {
        get => !IsFullMode;
        set => IsFullMode = !value;
    }

    public ImportMode Mode => IsFullMode ? ImportMode.Full : ImportMode.Partial;

    /// <summary>Re-selects the remembered shop after the shop list was reloaded.</summary>
    public void Refresh()
    {
        var wantedId = SelectedShop?.ShopId ?? workspace.Settings.LastImportShopId;
        SelectedShop = Shops.FirstOrDefault(shop => shop.ShopId == wantedId) ?? (Shops.Count == 1 ? Shops[0] : null);
        OnPropertyChanged(nameof(HasShops));
    }

    /// <summary>Queues CSV files (from the dialog or drag-drop) and opens the panel. Non-CSV files are ignored.</summary>
    public void QueueFiles(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (workspace.IsBusy)
        {
            // The running import works from PendingFiles; replacing it now would lose the dropped files.
            workspace.ShowError("Đang có thao tác chạy (import/tạo file in), hãy đợi xong rồi thả file tiếp.");
            return;
        }

        PendingFiles.Clear();
        foreach (var path in paths.Where(IsCsv).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            PendingFiles.Add(path);
        }

        IsOpen = true;
        if (PendingFiles.Count == 0)
        {
            workspace.ShowError("Chỉ nhận file .csv xuất từ Seller Center.");
        }
    }

    public static bool IsCsv(string path) =>
        string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void Open()
    {
        ReportText = null;
        Warnings.Clear();
        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
    }

    [RelayCommand]
    private void PickFiles()
    {
        var files = workspace.Services.FileDialogs.PickCsvFiles();
        if (files.Count > 0)
        {
            QueueFiles(files);
        }
    }

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
                var shop = await Task.Run(() => repository.AddShopAsync(name, cancellationToken), cancellationToken);
                await workspace.ReloadShopsAsync(cancellationToken);
                SelectedShop = Shops.FirstOrDefault(candidate => candidate.ShopId == shop.ShopId);
                NewShopName = null;
                OnPropertyChanged(nameof(HasShops));
                workspace.StatusText = $"Đã thêm shop \"{shop.Name}\".";
            },
            "AddShop");
    }

    /// <summary>
    /// Reads every queued file and imports all their lines in one call, so a Full import of several files
    /// does not hide the orders of the files read before.
    /// </summary>
    [RelayCommand]
    private Task ImportAsync()
    {
        var shop = SelectedShop;
        if (shop is null)
        {
            workspace.ShowError("Hãy chọn shop trước khi import.");
            return Task.CompletedTask;
        }

        if (PendingFiles.Count == 0)
        {
            workspace.ShowError("Chưa chọn file xuất đơn (.csv).");
            return Task.CompletedTask;
        }

        var files = PendingFiles.ToList();
        var mode = Mode;
        ReportText = null;
        Warnings.Clear();
        return workspace.RunAsync(
            "Đang import đơn...",
            cancellationToken => ImportFilesAsync(shop, files, mode, cancellationToken),
            "ImportOrders");
    }

    private async Task ImportFilesAsync(
        TikTokShop shop,
        IReadOnlyList<string> files,
        ImportMode mode,
        CancellationToken cancellationToken)
    {
        var services = workspace.Services;
        var lines = new List<ImportedOrderLine>();
        var sourceWarnings = new List<string>();
        var names = new List<string>();
        var rejectedOrderIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var source = services.OrderSourceFactory(file);
            OrderSourceResult result;
            try
            {
                result = await source.ReadAsync(cancellationToken);
            }
            catch (OrderSourceFormatException exception)
            {
                throw new InvalidOperationException($"Không đọc được file {source.Name}: {exception.Message}", exception);
            }

            names.Add(source.Name);
            lines.AddRange(result.Lines);
            rejectedOrderIds.UnionWith(result.RejectedOrderIds);
            sourceWarnings.AddRange(result.Warnings.Select(warning => files.Count > 1 ? $"{source.Name}: {warning}" : warning));
        }

        // An order with an unreadable row in one file must not be imported half from another file either.
        if (rejectedOrderIds.Count > 0)
        {
            lines.RemoveAll(line => rejectedOrderIds.Contains(line.OrderId));
        }

        var sourceName = string.Join(", ", names);
        var report = await Task.Run(
            () => services.ImportService.ImportAsync(shop.ShopId, sourceName, mode, lines, rejectedOrderIds, cancellationToken),
            cancellationToken);

        // The import is committed: from here on nothing may report the operation as failed.
        ReportText = $"{shop.Name} ({TikTokLabels.Of(mode)}): {TikTokLabels.FormatImportReport(report)}";
        foreach (var warning in sourceWarnings.Concat(report.Warnings))
        {
            Warnings.Add(warning);
        }

        RemoveImportedFiles(files);
        await workspace.ReloadAfterCommitAsync($"Đã import {sourceName}");
        workspace.StatusText = $"Đã import {sourceName}: {TikTokLabels.FormatImportReport(report)}.";
        await RememberShopAsync(shop.ShopId);
    }

    private void RemoveImportedFiles(IReadOnlyList<string> files)
    {
        var done = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
        for (var index = PendingFiles.Count - 1; index >= 0; index--)
        {
            if (done.Contains(PendingFiles[index]))
            {
                PendingFiles.RemoveAt(index);
            }
        }
    }

    /// <summary>Best effort: remembering the last shop must not turn a committed import into a failure.</summary>
    private async Task RememberShopAsync(long shopId)
    {
        if (workspace.Settings.LastImportShopId == shopId)
        {
            return;
        }

        var candidate = workspace.CopySettings();
        candidate.LastImportShopId = shopId;
        try
        {
            await workspace.SaveSettingsAsync(candidate, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
                                          or System.Text.Json.JsonException)
        {
            Log.Warning("Saving LastImportShopId failed with {ExceptionType}", exception.GetType().FullName);
        }
    }
}
