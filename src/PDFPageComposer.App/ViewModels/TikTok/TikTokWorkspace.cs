using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Models;
using Serilog;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>
/// State shared by every tab of the TikTok window: services, settings, catalog snapshots and the busy/status line.
/// Long operations go through <see cref="RunAsync"/>, which owns cancellation and Vietnamese error reporting.
/// </summary>
public sealed partial class TikTokWorkspace : ObservableObject
{
    private CancellationTokenSource? operationCancellation;
    private bool initialized;

    public TikTokWorkspace(TikTokServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Services = services;
    }

    public TikTokServices Services { get; }

    public TikTokSettings Settings { get; private set; } = new();

    public ObservableCollection<TikTokShop> Shops { get; } = [];

    public IReadOnlyList<SkuConfig> Skus { get; private set; } = [];

    public IReadOnlyList<PdfAsset> Assets { get; private set; } = [];

    public IReadOnlyList<IdolAlias> Aliases { get; private set; } = [];

    /// <summary>Files chosen by hand per printed set, passed to every classification and plan.</summary>
    public IReadOnlyList<FileOverride> FileOverrides { get; private set; } = [];

    /// <summary>Set by the shell: reloads every tab from the database.</summary>
    public Func<CancellationToken, Task>? ReloadAllHandler { get; set; }

    /// <summary>Set by the shell: re-classifies the orders after the catalog or settings changed.</summary>
    public Func<CancellationToken, Task>? RefreshOrdersHandler { get; set; }

    /// <summary>Set by the shell: applies a newly saved default separator mode to the Orders tab.</summary>
    public Action? DefaultSeparatorModeChangedHandler { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = "Sẵn sàng";

    [ObservableProperty]
    private string? errorText;

    [ObservableProperty]
    private string? progressText;

    public bool IsIdle => !IsBusy;

    public DateTimeOffset Now => Services.TimeProvider.GetLocalNow();

    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    public string ShopName(long shopId) =>
        Shops.FirstOrDefault(shop => shop.ShopId == shopId)?.Name ?? $"Shop {shopId}";

    public IReadOnlyDictionary<long, string> ShopNames() =>
        Shops.ToDictionary(shop => shop.ShopId, shop => shop.Name);

    public IReadOnlyDictionary<string, SkuConfig> SkuMap() =>
        Skus.ToDictionary(sku => sku.SkuId, StringComparer.Ordinal);

    public ClassificationOptions ClassificationOptions(bool allowReprint = false) =>
        new(Settings.SellerNoteMarksPrinted, allowReprint, Settings.BlockOrdersWithNotes, Aliases, Settings.AlwaysUseNewestFiles, FileOverrides);

    /// <summary>Creates the database (first run), runs migrations and loads the settings. Runs off the UI thread.</summary>
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (initialized)
        {
            return;
        }

        var repository = Services.Repository;
        await Task.Run(() => repository.InitializeAsync(cancellationToken), cancellationToken);
        var (settings, wasCorrupt) = await TikTokSettingsLoader.LoadAsync(Services.SettingsService, cancellationToken);
        Settings = settings.TikTok ?? new TikTokSettings();
        initialized = true;
        if (wasCorrupt)
        {
            ShowError(TikTokSettingsLoader.CorruptNotice);
        }
    }

    /// <summary>Reloads shops, SKUs, aliases and library files from the database.</summary>
    public async Task ReloadCatalogAsync(CancellationToken cancellationToken)
    {
        var repository = Services.Repository;
        var (shops, skus, aliases, assets, overrides) = await Task.Run(
            async () => (
                await repository.GetShopsAsync(cancellationToken),
                await repository.GetSkusAsync(cancellationToken),
                await repository.GetIdolAliasesAsync(cancellationToken),
                await repository.GetPdfAssetsAsync(cancellationToken),
                await repository.GetFileOverridesAsync(cancellationToken)),
            cancellationToken);

        ReplaceShops(shops);
        Skus = skus;
        Aliases = aliases;
        Assets = assets;
        FileOverrides = overrides;
    }

    /// <summary>Saves the chosen files of one order (empty = automatic again) and updates the in-memory list.</summary>
    public async Task SaveFileOverridesAsync(string orderId, IReadOnlyList<FileOverride> overrides, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentNullException.ThrowIfNull(overrides);
        var repository = Services.Repository;
        await Task.Run(() => repository.SaveFileOverridesAsync(orderId, overrides, cancellationToken), cancellationToken);
        FileOverrides = FileOverrides
            .Where(choice => !string.Equals(choice.OrderId, orderId, StringComparison.Ordinal))
            .Concat(overrides)
            .ToList();
    }

    public async Task ReloadShopsAsync(CancellationToken cancellationToken)
    {
        var repository = Services.Repository;
        var shops = await Task.Run(() => repository.GetShopsAsync(cancellationToken), cancellationToken);
        ReplaceShops(shops);
    }

    public async Task ReloadAliasesAsync(CancellationToken cancellationToken)
    {
        var repository = Services.Repository;
        Aliases = await Task.Run(() => repository.GetIdolAliasesAsync(cancellationToken), cancellationToken);
    }

    /// <summary>Replaces the given SKUs in the in-memory snapshot after they were saved.</summary>
    public void ApplySavedSkus(IReadOnlyCollection<SkuConfig> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        var map = Skus.ToDictionary(sku => sku.SkuId, StringComparer.Ordinal);
        foreach (var sku in saved)
        {
            map[sku.SkuId] = sku;
        }

        Skus = map.Values.OrderBy(sku => sku.SkuId, StringComparer.Ordinal).ToList();
    }

    public void SetAssets(IReadOnlyList<PdfAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        Assets = assets;
    }

    /// <summary>Saves the TikTok settings without overwriting other settings changed elsewhere in the app.</summary>
    public Task SaveSettingsAsync(CancellationToken cancellationToken) =>
        SaveSettingsAsync(TikTokSettingsLoader.Copy(Settings), cancellationToken);

    /// <summary>
    /// Saves candidate and only then makes it the live settings, so a failed save leaves the running window
    /// consistent with the file on disk.
    /// </summary>
    public async Task SaveSettingsAsync(TikTokSettings candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var latest = await TikTokSettingsLoader.LoadForSaveAsync(Services.SettingsService, cancellationToken);
        latest.TikTok = candidate;
        await Services.SettingsService.SaveAsync(latest, cancellationToken);
        Settings = candidate;
    }

    /// <summary>Copy of the live settings to edit before <see cref="SaveSettingsAsync(TikTokSettings, CancellationToken)"/>.</summary>
    public TikTokSettings CopySettings() => TikTokSettingsLoader.Copy(Settings);

    public void NotifyDefaultSeparatorModeChanged() => DefaultSeparatorModeChangedHandler?.Invoke();

    public Task ReloadAllAsync(CancellationToken cancellationToken) =>
        ReloadAllHandler?.Invoke(cancellationToken) ?? Task.CompletedTask;

    /// <summary>
    /// Reloads every tab after a change was already committed. A failure here must not be reported as a failed
    /// operation (the user would retry and commit twice), so it only shows a hint to reload.
    /// Returns false when the reload failed.
    /// </summary>
    public async Task<bool> ReloadAfterCommitAsync(string committedText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(committedText);
        try
        {
            await ReloadAllAsync(CancellationToken.None);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log.Warning("TikTok reload after commit failed with {ExceptionType}", exception.GetType().FullName);
            ShowError($"{committedText} nhưng không tải lại được danh sách: {exception.Message} Hãy bấm Tải lại.");
            return false;
        }
    }

    /// <summary>Reloads after a commit (best effort) and shows every notice together in the error banner.</summary>
    public async Task ShowCommittedResultAsync(string committedText, IReadOnlyList<string> notices)
    {
        ArgumentNullException.ThrowIfNull(notices);
        var messages = new List<string>();
        if (!await ReloadAfterCommitAsync(committedText) && ErrorText is { } reloadError)
        {
            messages.Add(reloadError);
        }

        messages.AddRange(notices);
        if (messages.Count > 0)
        {
            ShowError(string.Join(Environment.NewLine, messages));
        }
    }

    public Task RefreshOrdersAsync(CancellationToken cancellationToken) =>
        RefreshOrdersHandler?.Invoke(cancellationToken) ?? Task.CompletedTask;

    /// <summary>
    /// Runs one long operation: sets the busy state, owns the cancellation token and turns exceptions into
    /// Vietnamese status/error text. Returns false when the operation failed, was cancelled or another one runs.
    /// </summary>
    public async Task<bool> RunAsync(string busyText, Func<CancellationToken, Task> action, string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(busyText);
        ArgumentNullException.ThrowIfNull(action);

        if (IsBusy)
        {
            StatusText = "Đang có thao tác khác chạy, vui lòng đợi.";
            return false;
        }

        using var cancellation = new CancellationTokenSource();
        operationCancellation = cancellation;
        IsBusy = true;
        ErrorText = null;
        ProgressText = null;
        StatusText = busyText;
        try
        {
            await action(cancellation.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Đã hủy.";
            return false;
        }
        catch (Exception exception)
        {
            // Messages may quote order data, so only the operation and the exception type are logged.
            Log.Error(
                "TikTok operation {Operation} failed with {ExceptionType}{NewLine}{StackTrace}",
                operationName,
                exception.GetType().FullName,
                Environment.NewLine,
                exception.StackTrace);
            ErrorText = exception.Message;
            StatusText = "Thao tác thất bại.";
            return false;
        }
        finally
        {
            operationCancellation = null;
            ProgressText = null;
            IsBusy = false;
        }
    }

    /// <summary>Shows a validation problem without running anything.</summary>
    public void ShowError(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ErrorText = message;
    }

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel()
    {
        operationCancellation?.Cancel();
        StatusText = "Đang hủy...";
    }

    [RelayCommand]
    private void DismissError()
    {
        ErrorText = null;
    }

    private void ReplaceShops(IReadOnlyList<TikTokShop> shops)
    {
        Shops.Clear();
        foreach (var shop in shops)
        {
            Shops.Add(shop);
        }
    }
}
