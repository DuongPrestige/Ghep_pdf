using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Models;
using Serilog;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>Shop filter entry; ShopId null means every shop.</summary>
public sealed record ShopFilterOption(long? ShopId, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// Tab "Đơn hàng": classification, filters, note review, reprint and "Tạo file in" (TASK-068).
/// Only rows that are visible (shop and category filter) and ticked are exported, and the "File dự kiến" preview
/// is planned over exactly that selection.
/// </summary>
public sealed partial class OrdersTabViewModel : ObservableObject
{
    private static readonly ShopFilterOption AllShops = new(null, "Tất cả shop");
    private static readonly TimeSpan PreviewDebounce = TimeSpan.FromMilliseconds(120);

    private readonly TikTokWorkspace workspace;
    private readonly PrintFileRunner printRunner;
    private List<OrderRowViewModel> allRows = [];
    private CancellationTokenSource? previewCancellation;
    private bool suppressPreviewRefresh;

    public OrdersTabViewModel(TikTokWorkspace workspace, PrintFileRunner printRunner)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(printRunner);
        this.workspace = workspace;
        this.printRunner = printRunner;

        foreach (var category in TikTokLabels.CategoryDisplayOrder)
        {
            var filter = new CategoryFilterViewModel(category, category != OrderCategory.NoCardNeeded);
            filter.PropertyChanged += OnCategoryFilterChanged;
            CategoryFilters.Add(filter);
        }

        ShopFilters.Add(AllShops);
        selectedShopFilter = AllShops;
        selectedSeparatorMode = TikTokLabels.SeparatorModeOptions[1];
    }

    public TikTokWorkspace Workspace => workspace;

    public ObservableCollection<OrderRowViewModel> Rows { get; } = [];

    public IReadOnlyList<OrderRowViewModel> AllRows => allRows;

    public ObservableCollection<CategoryFilterViewModel> CategoryFilters { get; } = [];

    public ObservableCollection<ShopFilterOption> ShopFilters { get; } = [];

    public IReadOnlyList<TikTokOption<SeparatorMode>> SeparatorModes => TikTokLabels.SeparatorModeOptions;

    /// <summary>Ticked rows that are visible under the current shop and category filters.</summary>
    public IReadOnlyList<OrderRowViewModel> SelectedRows => Rows.Where(row => row.IsSelected && row.CanSelect).ToList();

    public string SelectionSummary
    {
        get
        {
            var selected = SelectedRows;
            var sheets = selected.Sum(row => row.SheetCount ?? 0);
            return $"Đã chọn {selected.Count} đơn, khoảng {sheets} tờ";
        }
    }

    [ObservableProperty]
    private ShopFilterOption? selectedShopFilter;

    [ObservableProperty]
    private TikTokOption<SeparatorMode> selectedSeparatorMode;

    [ObservableProperty]
    private string? planSummary;

    [ObservableProperty]
    private string? lastOutputPath;

    /// <summary>Order id or product/variation text; while set, rows of every category are searched.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearch))]
    [NotifyCanExecuteChangedFor(nameof(ClearSearchCommand))]
    private string searchText = string.Empty;

    public bool HasSearch => !string.IsNullOrWhiteSpace(SearchText);

    partial void OnSelectedShopFilterChanged(ShopFilterOption? value)
    {
        ApplyFilter();
        SchedulePreviewRefresh();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
        SchedulePreviewRefresh();
    }

    [RelayCommand(CanExecute = nameof(HasSearch))]
    private void ClearSearch() => SearchText = string.Empty;

    partial void OnSelectedSeparatorModeChanged(TikTokOption<SeparatorMode> value) => SchedulePreviewRefresh();

    /// <summary>Applies the default separator mode from the settings (after loading or saving the settings).</summary>
    public void ApplySettings()
    {
        SelectedSeparatorMode = SeparatorModes.FirstOrDefault(option => option.Value == workspace.Settings.DefaultSeparatorMode)
            ?? SeparatorModes[1];
    }

    /// <summary>
    /// Reloads active orders and classifies them with the current SKUs, library and settings. The user's ticks
    /// and "In lại" toggles survive the reload; only orders that were not selectable before get the default
    /// (Ready = ticked).
    /// </summary>
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var services = workspace.Services;
        var skus = workspace.SkuMap();
        var assets = workspace.Assets;
        var options = workspace.ClassificationOptions();
        var shopNames = workspace.ShopNames();

        var rows = await Task.Run(
            async () =>
            {
                var orders = await services.Repository.GetOrdersAsync(includeInactive: false, cancellationToken);
                return orders
                    .Select(order => new OrderRowViewModel(
                        order,
                        services.Classifier.Classify(order, skus, assets, options),
                        shopNames.TryGetValue(order.ShopId, out var name) ? name : $"Shop {order.ShopId}",
                        null))
                    .ToList();
            },
            cancellationToken);

        CancelPendingPreview();
        suppressPreviewRefresh = true;
        try
        {
            var previous = allRows.ToDictionary(
                row => row.OrderId,
                row => (row.IsSelected, row.IsReprint, row.CanSelect),
                StringComparer.Ordinal);
            foreach (var row in allRows)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
            }

            RestoreSelection(rows, previous);
            allRows = rows;
            foreach (var row in allRows)
            {
                row.PropertyChanged += OnRowPropertyChanged;
            }

            RefreshShopFilters();
            foreach (var filter in CategoryFilters)
            {
                filter.Count = allRows.Count(row => row.Category == filter.Category);
            }

            ApplyFilter();
            OnPropertyChanged(nameof(AllRows));
        }
        finally
        {
            suppressPreviewRefresh = false;
        }

        await RefreshPreviewAsync(cancellationToken);
    }

    [RelayCommand]
    private Task MarkNoteReviewedAsync(OrderRowViewModel? row)
    {
        if (row is null || !row.CanReviewNote)
        {
            return Task.CompletedTask;
        }

        var order = row.Order;
        return workspace.RunAsync(
            "Đang lưu xác nhận ghi chú...",
            async cancellationToken =>
            {
                var hash = OrderNotes.ComputeHash(order.BuyerMessage, order.SellerNote);
                var repository = workspace.Services.Repository;
                await Task.Run(() => repository.SetNoteReviewedAsync(order.OrderId, hash, cancellationToken), cancellationToken);
                await ReloadAsync(cancellationToken);
                workspace.StatusText = $"Đã cho phép ghép đơn {order.OrderId}.";
            },
            "MarkNoteReviewed");
    }

    [RelayCommand]
    private void ToggleReprint(OrderRowViewModel? row)
    {
        if (row is null || !row.IsPrinted)
        {
            return;
        }

        row.IsReprint = !row.IsReprint;
    }

    /// <summary>Set by the window: shows the "Xem trước file in" window modally.</summary>
    public Action<PrintPreviewViewModel>? ShowPrintPreviewDialog { get; set; }

    /// <summary>
    /// "Xem trước file in": every page of the ticked orders in print order. Files can be changed per order and orders
    /// can leave the batch; "Tạo file in" in the preview runs the normal print file export.
    /// </summary>
    [RelayCommand]
    private async Task PreviewPrintFileAsync()
    {
        if (ShowPrintPreviewDialog is null)
        {
            return;
        }

        if (SelectedRows.Count == 0)
        {
            workspace.ShowError("Chưa chọn đơn nào để xem trước.");
            return;
        }

        var preview = new PrintPreviewViewModel(
            BuildSelectionPlan,
            EditOrderFilesAsync,
            orderId =>
            {
                if (allRows.FirstOrDefault(row => string.Equals(row.OrderId, orderId, StringComparison.Ordinal)) is { } row)
                {
                    row.IsSelected = false;
                }
            },
            workspace.Services.PageRenderer,
            CopyToClipboard);
        ShowPrintPreviewDialog(preview);
        if (preview.CreateRequested)
        {
            await CreatePrintFileCommand.ExecuteAsync(null);
        }
    }

    private bool CopyToClipboard(string text)
    {
        try
        {
            workspace.Services.Clipboard.SetText(text);
            return true;
        }
        catch (InvalidOperationException exception)
        {
            workspace.ShowError(exception.Message);
            return false;
        }
    }

    /// <summary>The plan "Tạo file in" would export now for the ticked orders.</summary>
    private PrintPlan BuildSelectionPlan()
    {
        var selected = SelectedRows;
        return workspace.Services.PlanBuilder.Build(
            selected.Select(row => row.Order).ToList(),
            workspace.SkuMap(),
            workspace.Assets,
            SelectedSeparatorMode.Value,
            workspace.ClassificationOptions(allowReprint: selected.Any(row => row.IsReprint)));
    }

    private async Task EditOrderFilesAsync(string orderId, OrderSetKey? set)
    {
        if (allRows.FirstOrDefault(row => string.Equals(row.OrderId, orderId, StringComparison.Ordinal)) is { } row)
        {
            await ChooseFilesForSetAsync(row, set);
        }
    }

    /// <summary>Set by the window: shows the "Xem / chọn file in" dialog modally.</summary>
    public Action<OrderFilesViewModel>? ShowOrderFilesDialog { get; set; }

    /// <summary>Page count of a PDF picked from outside the library (PDFsharp, so it never races the PDFium renderer).</summary>
    public Func<string, int> CountPdfPages { get; set; } = path =>
    {
        using var document = PdfSharp.Pdf.IO.PdfReader.Open(path, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        return document.PageCount;
    };

    /// <summary>Disk check used by the dialog to hide library files that were deleted after the last scan.</summary>
    public Func<string, bool> FileExists { get; set; } = System.IO.File.Exists;

    /// <summary>
    /// "Xem / chọn": opens the dialog on the files this order would print now (planned alone, which matches the batch
    /// plan because every order starts from the newest file), then saves or clears the files chosen by hand.
    /// </summary>
    [RelayCommand]
    private Task ChooseFilesAsync(OrderRowViewModel? row) => ChooseFilesForSetAsync(row, null);

    /// <summary>Opens "Xem / chọn file" for the order, on <paramref name="set"/> when given (from the print preview).</summary>
    private Task ChooseFilesForSetAsync(OrderRowViewModel? row, OrderSetKey? set)
    {
        if (row is null || !row.CanChooseFiles || ShowOrderFilesDialog is null)
        {
            return Task.CompletedTask;
        }

        var order = row.Order;
        var services = workspace.Services;
        var skus = workspace.SkuMap();
        var plan = services.PlanBuilder.Build(
            [order],
            skus,
            workspace.Assets,
            SeparatorMode.None,
            workspace.ClassificationOptions(allowReprint: true));
        var dialog = new OrderFilesViewModel(
            order,
            skus,
            workspace.Assets,
            workspace.Aliases,
            plan.Orders.FirstOrDefault()?.Copies ?? [],
            services.PageRenderer,
            FileExists,
            services.FileDialogs.PickPdfFiles,
            CountPdfPages,
            CopyToClipboard,
            set);
        ShowOrderFilesDialog(dialog);
        if (dialog.Result == OrderFilesResult.Cancel)
        {
            return Task.CompletedTask;
        }

        var overrides = dialog.Result == OrderFilesResult.Save ? dialog.BuildOverrides() : [];
        return workspace.RunAsync(
            "Đang lưu file in đã chọn...",
            async cancellationToken =>
            {
                await workspace.SaveFileOverridesAsync(order.OrderId, overrides, cancellationToken);
                await ReloadAsync(cancellationToken);
                workspace.StatusText = overrides.Count > 0
                    ? $"Đã lưu file tự chọn cho đơn {order.OrderId}."
                    : $"Đơn {order.OrderId} dùng lại chọn file tự động.";
            },
            "SaveFileOverrides");
    }

    /// <summary>Copies one order id (row "Copy" button or double-click).</summary>
    [RelayCommand]
    private void CopyOrderId(OrderRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            workspace.Services.Clipboard.SetText(row.OrderId);
        }
        catch (InvalidOperationException exception)
        {
            workspace.ShowError(exception.Message);
            return;
        }

        workspace.StatusText = $"Đã copy mã đơn {row.OrderId}.";
    }

    /// <summary>Copies the order ids of the ticked rows, or of every visible row when none is ticked, one per line.</summary>
    [RelayCommand]
    private void CopyOrderIds()
    {
        var ticked = Rows.Where(row => row.IsSelected).ToList();
        var rows = ticked.Count > 0 ? ticked : Rows.ToList();
        if (rows.Count == 0)
        {
            workspace.ShowError("Không có đơn nào để copy mã.");
            return;
        }

        try
        {
            workspace.Services.Clipboard.SetText(string.Join(Environment.NewLine, rows.Select(row => row.OrderId)));
        }
        catch (InvalidOperationException exception)
        {
            // Another application holds the clipboard; the order ids are not logged.
            workspace.ShowError(exception.Message);
            return;
        }

        workspace.StatusText = ticked.Count > 0
            ? $"Đã copy {rows.Count} mã đơn đang chọn."
            : $"Đã copy {rows.Count} mã đơn đang hiển thị.";
    }

    [RelayCommand]
    private void SelectAllReady()
    {
        foreach (var row in Rows.Where(row => row.CanSelect))
        {
            row.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var row in allRows)
        {
            row.IsSelected = false;
        }
    }

    private static void RestoreSelection(
        IReadOnlyList<OrderRowViewModel> rows,
        IReadOnlyDictionary<string, (bool IsSelected, bool IsReprint, bool CanSelect)> previous)
    {
        foreach (var row in rows)
        {
            if (!previous.TryGetValue(row.OrderId, out var state))
            {
                continue;
            }

            if (state.IsReprint && row.IsPrinted)
            {
                row.IsReprint = true;
            }

            // A row that could not be picked before (e.g. "Có ghi chú" just reviewed) keeps the default.
            if (state.CanSelect)
            {
                // After IsReprint, so CanSelect is already correct; OnIsSelectedChanged un-ticks unselectable rows.
                row.IsSelected = state.IsSelected && row.CanSelect;
            }
        }
    }

    private void RefreshShopFilters()
    {
        var selectedId = SelectedShopFilter?.ShopId;
        var options = workspace.Shops.Select(shop => new ShopFilterOption(shop.ShopId, shop.Name)).ToList();
        ShopFilters.Clear();
        ShopFilters.Add(AllShops);
        foreach (var option in options)
        {
            ShopFilters.Add(option);
        }

        SelectedShopFilter = ShopFilters.FirstOrDefault(option => option.ShopId == selectedId) ?? AllShops;
    }

    private void ApplyFilter()
    {
        var visibleCategories = CategoryFilters
            .Where(filter => filter.IsChecked)
            .Select(filter => filter.Category)
            .ToHashSet();
        var shopId = SelectedShopFilter?.ShopId;
        var searching = HasSearch;

        Rows.Clear();
        foreach (var row in allRows)
        {
            var inCategory = searching
                ? MatchesSearch(row, SearchText)
                : visibleCategories.Contains(row.Category);
            if (inCategory && (shopId is null || row.ShopId == shopId))
            {
                Rows.Add(row);
            }
        }

        OnPropertyChanged(nameof(SelectionSummary));
    }

    /// <summary>Matches the order id (any part of it) or the product and variation names, ignoring diacritics.</summary>
    internal static bool MatchesSearch(OrderRowViewModel row, string query) =>
        TextNormalizer.MatchesSearch($"{row.OrderId} {row.ProductsText} {row.VariationsText}", query);

    private void OnCategoryFilterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CategoryFilterViewModel.IsChecked))
        {
            ApplyFilter();
            SchedulePreviewRefresh();
        }
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OrderRowViewModel.IsSelected) or nameof(OrderRowViewModel.IsReprint))
        {
            OnPropertyChanged(nameof(SelectionSummary));
            SchedulePreviewRefresh();
        }
    }

    /// <summary>Re-plans the preview shortly after the selection changed (debounced: "Chọn tất cả" flips many rows).</summary>
    private void SchedulePreviewRefresh()
    {
        if (suppressPreviewRefresh)
        {
            return;
        }

        CancelPendingPreview();
        var cancellation = new CancellationTokenSource();
        previewCancellation = cancellation;
        _ = RefreshPreviewDebouncedAsync(cancellation.Token);
    }

    private void CancelPendingPreview()
    {
        previewCancellation?.Cancel();
        previewCancellation?.Dispose();
        previewCancellation = null;
    }

    private async Task RefreshPreviewDebouncedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(PreviewDebounce, cancellationToken);
            await RefreshPreviewAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A newer selection change or a reload replaced this preview run.
            return;
        }
        catch (Exception exception)
        {
            Log.Warning("TikTok print preview failed with {ExceptionType}", exception.GetType().FullName);
            workspace.ShowError($"Không tính được file dự kiến: {exception.Message}");
        }
    }

    /// <summary>Plans the current selection exactly like "Tạo file in" does and shows the result per row.</summary>
    private async Task RefreshPreviewAsync(CancellationToken cancellationToken)
    {
        var rows = allRows;
        var selected = SelectedRows;
        var orders = selected.Select(row => row.Order).ToList();
        var options = workspace.ClassificationOptions(allowReprint: selected.Any(row => row.IsReprint));
        var skus = workspace.SkuMap();
        var assets = workspace.Assets;
        var separatorMode = SelectedSeparatorMode.Value;
        var planBuilder = workspace.Services.PlanBuilder;

        var planned = orders.Count == 0
            ? new Dictionary<string, PrintPlanOrder>(StringComparer.Ordinal)
            : await Task.Run(
                () => planBuilder.Build(orders, skus, assets, separatorMode, options)
                    .Orders
                    .ToDictionary(planOrder => planOrder.Order.OrderId, StringComparer.Ordinal),
                cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ReferenceEquals(rows, allRows))
        {
            return;
        }

        var selectedIds = selected.Select(row => row.OrderId).ToHashSet(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            row.ApplyPreview(selectedIds.Contains(row.OrderId) ? planned.GetValueOrDefault(row.OrderId) : null);
        }

        OnPropertyChanged(nameof(SelectionSummary));
    }
}
