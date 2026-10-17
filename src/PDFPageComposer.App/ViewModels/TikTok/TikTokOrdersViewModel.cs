using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>Shell of the TikTok orders window: owns the shared workspace and the tab view models.</summary>
public sealed partial class TikTokOrdersViewModel : ObservableObject
{
    public TikTokOrdersViewModel(TikTokServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Workspace = new TikTokWorkspace(services);
        var printRunner = new PrintFileRunner(Workspace);
        Import = new ImportPanelViewModel(Workspace);
        Orders = new OrdersTabViewModel(Workspace, printRunner);
        Skus = new SkuConfigTabViewModel(Workspace);
        Library = new LibraryTabViewModel(Workspace);
        History = new HistoryTabViewModel(Workspace, printRunner);
        Settings = new SettingsTabViewModel(Workspace);

        Workspace.ReloadAllHandler = ReloadAllAsync;
        Workspace.RefreshOrdersHandler = Orders.ReloadAsync;
        Workspace.DefaultSeparatorModeChangedHandler = Orders.ApplySettings;
    }

    /// <summary>
    /// The window must not close while an export or import runs: its commit cannot be cancelled, and a reopened
    /// window would still show the orders being printed as ready.
    /// </summary>
    public bool TryConfirmClose()
    {
        if (!Workspace.IsBusy)
        {
            return true;
        }

        Workspace.ShowError("Đang có thao tác chạy (tạo file in/nhập đơn). Vui lòng đợi hoàn tất hoặc bấm Hủy trước khi đóng cửa sổ.");
        return false;
    }

    public TikTokWorkspace Workspace { get; }

    public ImportPanelViewModel Import { get; }

    public OrdersTabViewModel Orders { get; }

    public SkuConfigTabViewModel Skus { get; }

    public LibraryTabViewModel Library { get; }

    public HistoryTabViewModel History { get; }

    public SettingsTabViewModel Settings { get; }

    [ObservableProperty]
    private int selectedTabIndex;

    /// <summary>First load: initializes the database off the UI thread, then fills every tab.</summary>
    [RelayCommand]
    private Task LoadAsync() =>
        Workspace.RunAsync(
            "Đang tải dữ liệu TikTok...",
            async cancellationToken =>
            {
                await Workspace.EnsureInitializedAsync(cancellationToken);
                Orders.ApplySettings();
                await ReloadAllAsync(cancellationToken);
                Workspace.StatusText = $"Đã tải {Orders.AllRows.Count} đơn đang chờ vận chuyển.";
            },
            "LoadTikTokWindow");

    [RelayCommand]
    private Task ReloadAsync() =>
        Workspace.RunAsync(
            "Đang tải lại...",
            async cancellationToken =>
            {
                await Workspace.EnsureInitializedAsync(cancellationToken);
                await ReloadAllAsync(cancellationToken);
                Workspace.StatusText = "Đã tải lại dữ liệu.";
            },
            "ReloadTikTokWindow");

    /// <summary>Handles CSV files dropped on the window.</summary>
    public void QueueDroppedFiles(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Import.QueueFiles(paths);
    }

    private async Task ReloadAllAsync(CancellationToken cancellationToken)
    {
        await Workspace.ReloadCatalogAsync(cancellationToken);
        Settings.Refresh();
        Import.Refresh();
        Skus.Refresh();
        Library.Refresh();
        await Orders.ReloadAsync(cancellationToken);
        await History.ReloadAsync(cancellationToken);
    }
}
