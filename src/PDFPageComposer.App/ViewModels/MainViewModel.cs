using System.IO;
using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;

namespace PDFPageComposer.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const int HistoryLimit = 100;
    private readonly IFileDialogService fileDialogService;
    private readonly IPdfMetadataService pdfMetadataService;
    private readonly IPdfRenderService pdfRenderService;
    private readonly IPdfExportService pdfExportService;
    private readonly IFoxitLauncherService foxitLauncherService;
    private readonly IThumbnailRenderQueue thumbnailRenderQueue;
    private readonly IThumbnailCacheService thumbnailCacheService;
    private readonly IAutoSaveService autoSaveService;
    private readonly IUpdateService updateService;
    private readonly List<AppHistorySnapshot> undoHistory = [];
    private readonly List<AppHistorySnapshot> redoHistory = [];
    private CancellationTokenSource? previewRenderCancellation;
    private SourcePdfPage? selectionAnchor;

    public MainViewModel(
        IFileDialogService fileDialogService,
        IPdfMetadataService pdfMetadataService,
        IPdfRenderService pdfRenderService,
        IPdfExportService pdfExportService,
        IFoxitLauncherService foxitLauncherService,
        IThumbnailRenderQueue thumbnailRenderQueue,
        IThumbnailCacheService thumbnailCacheService,
        IAutoSaveService autoSaveService,
        IUpdateService updateService)
    {
        this.fileDialogService = fileDialogService;
        this.pdfMetadataService = pdfMetadataService;
        this.pdfRenderService = pdfRenderService;
        this.pdfExportService = pdfExportService;
        this.foxitLauncherService = foxitLauncherService;
        this.thumbnailRenderQueue = thumbnailRenderQueue;
        this.thumbnailCacheService = thumbnailCacheService;
        this.autoSaveService = autoSaveService;
        this.updateService = updateService;
    }

    [ObservableProperty]
    private double thumbnailZoom = 1.0;

    [ObservableProperty]
    private string renderStatus = "Sẵn sàng";

    [ObservableProperty]
    private string sourceSearchText = string.Empty;

    [ObservableProperty]
    private bool isExporting;

    [ObservableProperty]
    private bool isUpdating;

    [ObservableProperty]
    private int exportProgressPercent;

    [ObservableProperty]
    private int exportProgressPageCount;

    [ObservableProperty]
    private int exportProgressTotalPageCount;

    [ObservableProperty]
    private bool isPreviewOpen;

    [ObservableProperty]
    private SourcePdfPage? previewPage;

    [ObservableProperty]
    private SourcePdfFile? previewSourceFile;

    [ObservableProperty]
    private bool isPreviewGridOpen;

    [ObservableProperty]
    private bool isOutputPreviewMode;

    [ObservableProperty]
    private int outputPreviewIndex;

    [ObservableProperty]
    private int outputPreviewGridPageIndex;

    [ObservableProperty]
    private RenderedPageImage? previewImage;

    [ObservableProperty]
    private ThumbnailState previewState = ThumbnailState.NotRequested;

    [ObservableProperty]
    private string? previewError;

    [ObservableProperty]
    private double previewZoom = 1.0;

    [ObservableProperty]
    private PreviewFitMode previewFitMode = PreviewFitMode.FitPage;

    private List<OutputTrayItemView> outputPreviewItems = [];

    [ObservableProperty]
    private bool hasRecoverySession;

    [ObservableProperty]
    private string recoveryStatus = string.Empty;

    public double ThumbnailCardWidth => Math.Round(132 * ThumbnailZoom);

    public double ThumbnailPreviewHeight => Math.Round(132 * ThumbnailZoom);

    public double ThumbnailCardMinHeight => Math.Round(172 * ThumbnailZoom);

    public ObservableCollection<SourcePdfFile> SourceFiles { get; } = [];

    public IEnumerable<SourcePdfFile> FilteredSourceFiles
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SourceSearchText))
            {
                return SourceFiles;
            }

            return SourceFiles.Where(file => file.DisplayName.Contains(SourceSearchText, StringComparison.OrdinalIgnoreCase));
        }
    }

    public ObservableCollection<OutputGroup> OutputGroups { get; } = [];

    public ObservableCollection<string> ImportErrors { get; } = [];

    public IEnumerable<OutputTrayItemView> OutputTrayItems => OutputGroups
        .Where(group => !group.IsCollapsed)
        .SelectMany(group => group.Items.Select(item => new { Group = group, Item = item }))
        .Select((entry, index) =>
        {
            var sourceFile = SourceFiles.FirstOrDefault(file => file.Id == entry.Item.SourceFileId);
            return new OutputTrayItemView(
                index + 1,
                entry.Group.Name,
                sourceFile?.DisplayName ?? "Thiếu file nguồn",
                entry.Item.SourcePageNumber,
                sourceFile?.Pages.FirstOrDefault(page => page.PageNumber == entry.Item.SourcePageNumber),
                entry.Group,
                entry.Item);
        });

    public int SourceFileCount => SourceFiles.Count;

    public int SourcePageCount => SourceFiles.Sum(file => file.PageCount);

    public int SelectedPageCount => SourceFiles.Sum(file => file.Pages.Count(page => page.IsSelected));

    public int OutputPageCount => OutputGroups.Sum(group => group.Items.Count);

    public bool HasSelectedPages => SelectedPageCount > 0;

    public bool HasOutputPages => OutputPageCount > 0;

    public bool HasSourceFiles => SourceFiles.Count > 0;

    public bool HasSourceSearch => !string.IsNullOrWhiteSpace(SourceSearchText);

    public string AppVersionText => $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

    public string ExportProgressText => ExportProgressTotalPageCount > 0
        ? $"Đang xuất {ExportProgressPageCount}/{ExportProgressTotalPageCount} trang ({ExportProgressPercent}%)"
        : "Đang xuất PDF...";

    public bool CanCheckForUpdate => !IsUpdating;

    public bool CanUndo => undoHistory.Count > 0;

    public bool CanRedo => redoHistory.Count > 0;

    public string PreviewTitle
    {
        get
        {
            if (PreviewPage is null)
            {
                return "Preview";
            }

            if (IsOutputPreviewMode)
            {
                if (IsPreviewGridOpen)
                {
                    return $"Xem trước đầu ra - tất cả {outputPreviewItems.Count} trang";
                }

                var item = outputPreviewItems.ElementAtOrDefault(OutputPreviewIndex);
                return item is null
                    ? "Xem trước đầu ra"
                    : $"Xem trước đầu ra - trang {OutputPreviewIndex + 1}/{outputPreviewItems.Count}: {item.SourceDisplayName} - trang {item.SourcePageNumber}";
            }

            if (IsPreviewGridOpen)
            {
                var gridSourceFile = PreviewSourceFile ?? SourceFiles.FirstOrDefault(file => file.Id == PreviewPage.SourceFileId);
                return $"{gridSourceFile?.DisplayName ?? "Thieu file nguon"} - tat ca trang";
            }

            var sourceFile = SourceFiles.FirstOrDefault(file => file.Id == PreviewPage.SourceFileId);
            return $"{sourceFile?.DisplayName ?? "Thiếu file nguồn"} - trang {PreviewPage.PageNumber}";
        }
    }

    public IEnumerable<SourcePdfPage> PreviewSourcePages => PreviewSourceFile?.Pages ?? [];

    public IEnumerable<SourcePdfPage> PreviewGridPages => IsPreviewGridOpen && !IsOutputPreviewMode
        ? PreviewSourcePages
        : [];

    public IEnumerable<OutputTrayItemView> OutputPreviewGridItems => IsPreviewGridOpen && IsOutputPreviewMode
        ? outputPreviewItems
        : [];

    public IEnumerable<IReadOnlyList<OutputTrayItemView>> OutputPreviewGridPages => IsPreviewGridOpen && IsOutputPreviewMode
        ? outputPreviewItems.Chunk(14).Select(chunk => (IReadOnlyList<OutputTrayItemView>)chunk.ToList())
        : [];

    public IReadOnlyList<OutputTrayItemView> CurrentOutputPreviewGridItems => OutputPreviewGridPages
        .ElementAtOrDefault(OutputPreviewGridPageIndex)?
        .Select(MarkCurrentOutputPreviewItem)
        .ToList()
        ?? [];

    public OutputTrayItemView? CurrentOutputPreviewItem => IsOutputPreviewMode
        ? outputPreviewItems.ElementAtOrDefault(OutputPreviewIndex)
        : null;

    public bool HasCurrentOutputPreviewItem => CurrentOutputPreviewItem is not null;

    public int OutputPreviewGridPageCount => IsPreviewGridOpen && IsOutputPreviewMode
        ? Math.Max(1, (int)Math.Ceiling(outputPreviewItems.Count / 14.0))
        : 0;

    public bool CanNavigatePreviewPrevious => PreviewPage is not null &&
        !IsPreviewGridOpen &&
        (IsOutputPreviewMode ? OutputPreviewIndex > 0 : PageNumberIsAfterFirst);

    private bool PageNumberIsAfterFirst => PreviewPage is not null && PreviewPage.PageNumber > 1;

    public bool CanNavigatePreviewNext
    {
        get
        {
            if (PreviewPage is null)
            {
                return false;
            }

            if (IsPreviewGridOpen)
            {
                return false;
            }

            if (IsOutputPreviewMode)
            {
                return OutputPreviewIndex < outputPreviewItems.Count - 1;
            }

            var sourceFile = SourceFiles.FirstOrDefault(file => file.Id == PreviewPage.SourceFileId);
            return sourceFile is not null && PreviewPage.PageNumber < sourceFile.PageCount;
        }
    }

    public Task CheckRecoveryOnStartupAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HasRecoverySession = autoSaveService.HasRecovery;
        RecoveryStatus = HasRecoverySession
            ? "Phát hiện phiên làm việc chưa phục hồi"
            : string.Empty;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RecoverSessionAsync(CancellationToken cancellationToken)
    {
        var projectState = await autoSaveService.LoadRecoveryAsync(cancellationToken);
        if (projectState is null)
        {
            HasRecoverySession = false;
            RecoveryStatus = string.Empty;
            return;
        }

        ApplyProjectSnapshot(ProjectStateMapper.ToSession(projectState));
        HasRecoverySession = false;
        RecoveryStatus = "Đã phục hồi phiên auto-save";
        RenderStatus = RecoveryStatus;
    }

    [RelayCommand]
    private void DismissRecovery()
    {
        autoSaveService.ClearRecovery();
        HasRecoverySession = false;
        RecoveryStatus = string.Empty;
    }

    [RelayCommand]
    private void ClearSourceSearch()
    {
        SourceSearchText = string.Empty;
    }

    [RelayCommand]
    private async Task AddPdfAsync(CancellationToken cancellationToken)
    {
        var filePaths = fileDialogService.PickPdfFiles();
        await ImportPdfFilesAsync(filePaths, cancellationToken);
    }

    [RelayCommand]
    private void RemoveSourceFile(SourcePdfFile? sourceFile)
    {
        if (sourceFile is null)
        {
            return;
        }

        var hasOutputItems = OutputGroups.Any(group => group.Items.Any(item => item.SourceFileId == sourceFile.Id));
        if (hasOutputItems)
        {
            RenderStatus = $"{sourceFile.DisplayName}: đang có trang trong đầu ra";
            return;
        }

        SourceFiles.Remove(sourceFile);
        thumbnailRenderQueue.CancelAll();
        thumbnailCacheService.Clear();
        NotifyStatisticsChanged();
        RenderStatus = $"Đã xóa {sourceFile.DisplayName} khỏi phiên";
    }

    [RelayCommand]
    private async Task RelinkSourceFileAsync(SourcePdfFile? sourceFile, CancellationToken cancellationToken)
    {
        if (sourceFile is null)
        {
            return;
        }

        var replacementPath = fileDialogService.PickPdfFiles().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(replacementPath))
        {
            RenderStatus = "Chưa chọn file để liên kết lại";
            return;
        }

        await RelinkSourceFileAsync(sourceFile, replacementPath, cancellationToken);
    }

    public async Task RelinkSourceFileAsync(SourcePdfFile sourceFile, string replacementPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementPath);

        SourcePdfFile replacement;
        try
        {
            replacement = await pdfMetadataService.ReadAsync(replacementPath, cancellationToken);
        }
        catch (PdfMetadataException ex)
        {
            RenderStatus = $"{Path.GetFileName(ex.FilePath)}: {DescribeError(ex.Error)}";
            return;
        }

        if (replacement.PageCount != sourceFile.PageCount)
        {
            RenderStatus = "File liên kết lại không khớp số trang";
            return;
        }

        if (!string.Equals(replacement.Fingerprint, sourceFile.Fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            RenderStatus = "File liên kết lại không khớp fingerprint";
            return;
        }

        sourceFile.Relink(replacement.FilePath, replacement.DisplayName, replacement.FileSize, replacement.Fingerprint);
        RenderStatus = $"Đã liên kết lại {sourceFile.DisplayName}";
        ScheduleAutoSave();
    }

    public async Task ImportPdfFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken)
    {
        var validPaths = filePaths
            .Where(path => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validPaths.Count == 0)
        {
            RenderStatus = "Không có file PDF nào được chọn";
            return;
        }

        var imported = 0;
        foreach (var filePath in validPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var sourceFile = await pdfMetadataService.ReadAsync(filePath, cancellationToken);
                SourceFiles.Add(sourceFile);
                imported++;
            }
            catch (PdfMetadataException ex)
            {
                var errorMessage = $"{Path.GetFileName(ex.FilePath)}: {DescribeError(ex.Error)}";
                ImportErrors.Add(errorMessage);
                RenderStatus = errorMessage;
            }
        }

        NotifyStatisticsChanged();
        RenderStatus = imported > 0
            ? $"Đã import {imported}/{validPaths.Count} file PDF"
            : "Không import được file PDF nào";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPages))]
    private void AddSelectionToOutput()
    {
        var selectedPages = SourceFiles
            .SelectMany((file, fileIndex) => file.Pages
                .Where(page => page.IsSelected)
                .OrderBy(page => page.PageNumber)
                .Select(page => new { FileIndex = fileIndex, Page = page }))
            .OrderBy(selection => selection.FileIndex)
            .ThenBy(selection => selection.Page.PageNumber)
            .Select(selection => selection.Page)
            .ToList();

        if (selectedPages.Count == 0)
        {
            RenderStatus = "Chưa có trang nào được chọn";
            return;
        }

        CaptureUndoSnapshot();
        var groupId = Guid.NewGuid();
        var group = new OutputGroup(
            groupId,
            $"Group {OutputGroups.Count + 1}",
            DateTimeOffset.UtcNow,
            selectedPages.Select(page => new OutputPageItem(Guid.NewGuid(), groupId, page.SourceFileId, page.PageNumber)));

        OutputGroups.Add(group);
        foreach (var page in selectedPages)
        {
            page.OutputOccurrenceCount++;
        }

        NotifyStatisticsChanged();
        RenderStatus = $"Đã thêm {selectedPages.Count} trang vào đầu ra";
    }

    [RelayCommand]
    private void SelectPage(PageSelectionRequest? request)
    {
        if (request is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        switch (request.Gesture)
        {
            case SelectionGesture.Range:
                SelectRange(request.Page);
                break;
            case SelectionGesture.AddOrRemove:
                request.Page.IsSelected = !request.Page.IsSelected;
                selectionAnchor = request.Page;
                break;
            default:
                request.Page.IsSelected = !request.Page.IsSelected;
                selectionAnchor = request.Page;
                break;
        }

        NotifyStatisticsChanged();
    }

    [RelayCommand]
    private void ToggleSourceFileCollapse(SourcePdfFile? sourceFile)
    {
        if (sourceFile is null)
        {
            return;
        }

        sourceFile.IsCollapsed = !sourceFile.IsCollapsed;
        RenderStatus = sourceFile.IsCollapsed
            ? $"Đã thu gọn {sourceFile.DisplayName}"
            : $"Đã mở rộng {sourceFile.DisplayName}";
        ScheduleAutoSave();
    }

    public void AdjustThumbnailZoom(double delta)
    {
        ThumbnailZoom = Math.Clamp(ThumbnailZoom + delta, 0.6, 1.6);
    }

    [RelayCommand]
    private void SelectAllInFile(SourcePdfFile? sourceFile)
    {
        if (sourceFile is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        foreach (var page in sourceFile.Pages)
        {
            page.IsSelected = true;
        }

        selectionAnchor = sourceFile.Pages.LastOrDefault();
        NotifyStatisticsChanged();
    }

    [RelayCommand]
    private void ClearSelectionInFile(SourcePdfFile? sourceFile)
    {
        if (sourceFile is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        foreach (var page in sourceFile.Pages)
        {
            page.IsSelected = false;
        }

        if (selectionAnchor is not null && selectionAnchor.SourceFileId == sourceFile.Id)
        {
            selectionAnchor = null;
        }

        NotifyStatisticsChanged();
    }

    [RelayCommand]
    private void SelectAllPages()
    {
        if (SourceFiles.Count == 0)
        {
            return;
        }

        CaptureUndoSnapshot();
        foreach (var page in SourceFiles.SelectMany(file => file.Pages))
        {
            page.IsSelected = true;
        }

        selectionAnchor = SourceFiles.LastOrDefault()?.Pages.LastOrDefault();
        NotifyStatisticsChanged();
    }

    [RelayCommand]
    private void ClearAllSelection()
    {
        if (SelectedPageCount == 0)
        {
            return;
        }

        CaptureUndoSnapshot();
        foreach (var page in SourceFiles.SelectMany(file => file.Pages))
        {
            page.IsSelected = false;
        }

        selectionAnchor = null;
        NotifyStatisticsChanged();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RenderThumbnailAsync(SourcePdfPage? page)
    {
        if (page is null || page.ThumbnailState is ThumbnailState.Ready or ThumbnailState.Loading)
        {
            return;
        }

        var sourceFile = SourceFiles.FirstOrDefault(file => file.Id == page.SourceFileId);
        if (sourceFile is null)
        {
            page.SetThumbnailError("Không tìm thấy file nguồn");
            return;
        }

        try
        {
            page.ThumbnailState = ThumbnailState.Loading;
            var pixelWidth = Math.Clamp((int)Math.Round(150 * ThumbnailZoom), 90, 260);
            var request = new ThumbnailRenderRequest(
                CreateThumbnailCacheKey(sourceFile, page, pixelWidth),
                sourceFile.FilePath,
                sourceFile.Fingerprint,
                page.PageNumber,
                pixelWidth);
            var result = await thumbnailRenderQueue.RenderAsync(request, CancellationToken.None);
            if (page.ThumbnailState != ThumbnailState.Loading)
            {
                return;
            }

            page.SetThumbnail(result.PixelWidth, result.PixelHeight, result.Stride, result.Bgra32Pixels);
        }
        catch (OperationCanceledException)
        {
            page.ThumbnailState = ThumbnailState.NotRequested;
        }
        catch (Exception ex)
        {
            page.SetThumbnailError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task RetryThumbnailAsync(SourcePdfPage? page)
    {
        if (page is null)
        {
            return;
        }

        page.ThumbnailState = ThumbnailState.NotRequested;
        await RenderThumbnailAsync(page);
    }

    [RelayCommand]
    private async Task OpenPreviewAsync(SourcePdfPage? page, CancellationToken cancellationToken)
    {
        if (page is null)
        {
            return;
        }

        IsPreviewOpen = true;
        PreviewPage = page;
        PreviewSourceFile = SourceFiles.FirstOrDefault(file => file.Id == page.SourceFileId);
        IsPreviewGridOpen = false;
        IsOutputPreviewMode = false;
        outputPreviewItems = [];
        OutputPreviewIndex = 0;
        PreviewFitMode = PreviewFitMode.FitPage;
        PreviewZoom = 1.0;
        CancelThumbnailRenderingForSourceFile(page.SourceFileId, exceptPage: page);
        NotifyPreviewNavigationChanged();
        await RenderPreviewAsync(cancellationToken);
    }

    [RelayCommand]
    private void ClosePreview()
    {
        previewRenderCancellation?.Cancel();
        previewRenderCancellation?.Dispose();
        previewRenderCancellation = null;
        PreviewImage = null;
        PreviewError = null;
        PreviewState = ThumbnailState.NotRequested;
        PreviewPage = null;
        PreviewSourceFile = null;
        IsPreviewGridOpen = false;
        IsOutputPreviewMode = false;
        outputPreviewItems = [];
        OutputPreviewIndex = 0;
        IsPreviewOpen = false;
        NotifyPreviewNavigationChanged();
    }

    [RelayCommand(CanExecute = nameof(HasOutputPages))]
    private async Task PreviewOutputAsync(CancellationToken cancellationToken)
    {
        var items = OutputTrayItems
            .Where(item => item.SourcePage is not null)
            .ToList();
        if (items.Count == 0)
        {
            RenderStatus = "Khay đầu ra đang trống hoặc thiếu file nguồn";
            return;
        }

        outputPreviewItems = items;
        OutputPreviewIndex = 0;
        OutputPreviewGridPageIndex = 0;
        IsPreviewOpen = true;
        IsOutputPreviewMode = true;
        IsPreviewGridOpen = true;
        PreviewImage = null;
        PreviewError = null;
        PreviewState = ThumbnailState.NotRequested;
        PreviewFitMode = PreviewFitMode.FitPage;
        PreviewZoom = 1.0;
        SetOutputPreviewPage(0);
        NotifyPreviewNavigationChanged();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ShowPreviewGrid()
    {
        if (!IsPreviewOpen || (!IsOutputPreviewMode && PreviewSourceFile is null))
        {
            return;
        }

        previewRenderCancellation?.Cancel();
        PreviewImage = null;
        PreviewState = ThumbnailState.NotRequested;
        OutputPreviewGridPageIndex = 0;
        IsPreviewGridOpen = true;
        NotifyPreviewNavigationChanged();
    }

    public void MoveOutputPreviewGridPage(int delta)
    {
        if (!IsPreviewGridOpen || !IsOutputPreviewMode || OutputPreviewGridPageCount <= 1)
        {
            return;
        }

        OutputPreviewGridPageIndex = Math.Clamp(
            OutputPreviewGridPageIndex + delta,
            0,
            OutputPreviewGridPageCount - 1);
        NotifyPreviewNavigationChanged();
    }

    [RelayCommand]
    private void SelectOutputPreviewItem(OutputTrayItemView? itemView)
    {
        if (!IsOutputPreviewMode || itemView is null || itemView.SourcePage is null)
        {
            return;
        }

        var index = outputPreviewItems.FindIndex(item => item.Item.Id == itemView.Item.Id);
        if (index < 0)
        {
            return;
        }

        SetOutputPreviewPage(index);
        NotifyPreviewNavigationChanged();
    }

    [RelayCommand]
    private async Task OpenOutputPreviewItemAsync(OutputTrayItemView? itemView, CancellationToken cancellationToken)
    {
        if (!IsOutputPreviewMode || itemView is null || itemView.SourcePage is null)
        {
            return;
        }

        var index = outputPreviewItems.FindIndex(item => item.Item.Id == itemView.Item.Id);
        if (index < 0)
        {
            return;
        }

        IsPreviewGridOpen = false;
        SetOutputPreviewPage(index);
        NotifyPreviewNavigationChanged();
        await RenderPreviewAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task DuplicateCurrentOutputPreviewItemAsync(CancellationToken cancellationToken)
    {
        var item = CurrentOutputPreviewItem;
        if (item is null)
        {
            return;
        }

        var targetIndex = OutputPreviewIndex + 1;
        DuplicateOutputItem(item);
        RefreshOutputPreviewItems(targetIndex);
        if (!IsPreviewGridOpen && PreviewPage is not null)
        {
            await RenderPreviewAsync(cancellationToken);
        }
    }

    [RelayCommand]
    private async Task DeleteCurrentOutputPreviewItemAsync(CancellationToken cancellationToken)
    {
        var item = CurrentOutputPreviewItem;
        if (item is null)
        {
            return;
        }

        var targetIndex = OutputPreviewIndex;
        DeleteOutputItem(item);
        RefreshOutputPreviewItems(targetIndex);
        if (!IsPreviewGridOpen && PreviewPage is not null)
        {
            await RenderPreviewAsync(cancellationToken);
        }
    }

    [RelayCommand]
    private async Task MoveCurrentOutputPreviewItemUpAsync(CancellationToken cancellationToken)
    {
        var item = CurrentOutputPreviewItem;
        if (item is null)
        {
            return;
        }

        var targetIndex = Math.Max(0, OutputPreviewIndex - 1);
        MoveOutputItem(item, -1);
        RefreshOutputPreviewItems(targetIndex);
        if (!IsPreviewGridOpen && PreviewPage is not null)
        {
            await RenderPreviewAsync(cancellationToken);
        }
    }

    [RelayCommand]
    private async Task MoveCurrentOutputPreviewItemDownAsync(CancellationToken cancellationToken)
    {
        var item = CurrentOutputPreviewItem;
        if (item is null)
        {
            return;
        }

        var targetIndex = OutputPreviewIndex + 1;
        MoveOutputItem(item, 1);
        RefreshOutputPreviewItems(targetIndex);
        if (!IsPreviewGridOpen && PreviewPage is not null)
        {
            await RenderPreviewAsync(cancellationToken);
        }
    }

    [RelayCommand]
    private async Task ShowSinglePreviewAsync(CancellationToken cancellationToken)
    {
        if (!IsPreviewOpen || PreviewPage is null)
        {
            return;
        }

        IsPreviewGridOpen = false;
        NotifyPreviewNavigationChanged();
        await RenderPreviewAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task PreviewPreviousAsync(CancellationToken cancellationToken)
    {
        if (!CanNavigatePreviewPrevious || PreviewPage is null)
        {
            return;
        }

        if (IsOutputPreviewMode)
        {
            SetOutputPreviewPage(OutputPreviewIndex - 1);
            NotifyPreviewNavigationChanged();
            await RenderPreviewAsync(cancellationToken);
            return;
        }

        var sourceFile = SourceFiles.First(file => file.Id == PreviewPage.SourceFileId);
        PreviewPage = sourceFile.Pages[PreviewPage.PageNumber - 2];
        NotifyPreviewNavigationChanged();
        await RenderPreviewAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task PreviewNextAsync(CancellationToken cancellationToken)
    {
        if (!CanNavigatePreviewNext || PreviewPage is null)
        {
            return;
        }

        if (IsOutputPreviewMode)
        {
            SetOutputPreviewPage(OutputPreviewIndex + 1);
            NotifyPreviewNavigationChanged();
            await RenderPreviewAsync(cancellationToken);
            return;
        }

        var sourceFile = SourceFiles.First(file => file.Id == PreviewPage.SourceFileId);
        PreviewPage = sourceFile.Pages[PreviewPage.PageNumber];
        NotifyPreviewNavigationChanged();
        await RenderPreviewAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task SetPreviewFitModeAsync(PreviewFitMode fitMode, CancellationToken cancellationToken)
    {
        if (!IsPreviewOpen)
        {
            return;
        }

        PreviewFitMode = fitMode;
        await RenderPreviewAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task AdjustPreviewZoomAsync(double delta, CancellationToken cancellationToken)
    {
        if (!IsPreviewOpen)
        {
            return;
        }

        PreviewFitMode = PreviewFitMode.Zoom;
        PreviewZoom = Math.Clamp(PreviewZoom + delta, 0.25, 3.0);
        await RenderPreviewAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task PreviewZoomInAsync(CancellationToken cancellationToken)
    {
        await AdjustPreviewZoomAsync(0.1, cancellationToken);
    }

    [RelayCommand]
    private async Task PreviewZoomOutAsync(CancellationToken cancellationToken)
    {
        await AdjustPreviewZoomAsync(-0.1, cancellationToken);
    }

    [RelayCommand]
    private void TogglePreviewSelection()
    {
        if (PreviewPage is null || IsOutputPreviewMode)
        {
            return;
        }

        CaptureUndoSnapshot();
        PreviewPage.IsSelected = !PreviewPage.IsSelected;
        selectionAnchor = PreviewPage;
        NotifyStatisticsChanged();
    }

    [RelayCommand]
    private void CancelThumbnail(SourcePdfPage? page)
    {
        if (page is null || page.ThumbnailState != ThumbnailState.Loading)
        {
            return;
        }

        var sourceFile = SourceFiles.FirstOrDefault(file => file.Id == page.SourceFileId);
        if (sourceFile is not null)
        {
            var pixelWidth = Math.Clamp((int)Math.Round(150 * ThumbnailZoom), 90, 260);
            thumbnailRenderQueue.Cancel(CreateThumbnailCacheKey(sourceFile, page, pixelWidth));
        }

        page.ThumbnailState = ThumbnailState.NotRequested;
    }

    private async Task RenderPreviewAsync(CancellationToken cancellationToken)
    {
        if (PreviewPage is null)
        {
            return;
        }

        var sourceFile = SourceFiles.FirstOrDefault(file => file.Id == PreviewPage.SourceFileId);
        if (sourceFile is null)
        {
            PreviewImage = null;
            PreviewError = "Không tìm thấy file nguồn";
            PreviewState = ThumbnailState.Error;
            return;
        }

        previewRenderCancellation?.Cancel();
        previewRenderCancellation?.Dispose();
        previewRenderCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = previewRenderCancellation.Token;
        var requestedPage = PreviewPage;

        try
        {
            PreviewState = ThumbnailState.Loading;
            PreviewError = null;
            PreviewImage = null;

            var pixelWidth = CalculatePreviewPixelWidth(requestedPage);
            var result = await pdfRenderService.RenderPageAsync(sourceFile.FilePath, requestedPage.PageNumber, pixelWidth, token);
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(PreviewPage, requestedPage))
            {
                return;
            }

            PreviewImage = new RenderedPageImage(result.PixelWidth, result.PixelHeight, result.Stride, result.Bgra32Pixels);
            PreviewState = ThumbnailState.Ready;
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(PreviewPage, requestedPage))
            {
                PreviewState = ThumbnailState.NotRequested;
            }
        }
        catch (Exception ex)
        {
            PreviewImage = null;
            PreviewError = ex.Message;
            PreviewState = ThumbnailState.Error;
        }
    }

    [RelayCommand]
    private void DeleteOutputItem(OutputTrayItemView? itemView)
    {
        if (itemView is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        if (itemView.Group.Items.Remove(itemView.Item) && itemView.Group.Items.Count == 0)
        {
            OutputGroups.Remove(itemView.Group);
        }

        RecalculateOutputOccurrences();
        NotifyStatisticsChanged();
        RenderStatus = "Đã xóa trang khỏi đầu ra";
    }

    [RelayCommand]
    private void DeleteOutputItems(IReadOnlyList<OutputTrayItemView>? itemViews)
    {
        if (itemViews is null || itemViews.Count == 0)
        {
            return;
        }

        CaptureUndoSnapshot();
        var itemsToDelete = itemViews
            .Select(itemView => itemView.Item)
            .DistinctBy(item => item.Id)
            .ToHashSet();

        foreach (var group in OutputGroups.ToList())
        {
            for (var index = group.Items.Count - 1; index >= 0; index--)
            {
                if (itemsToDelete.Contains(group.Items[index]))
                {
                    group.Items.RemoveAt(index);
                }
            }

            if (group.Items.Count == 0)
            {
                OutputGroups.Remove(group);
            }
        }

        RecalculateOutputOccurrences();
        NotifyStatisticsChanged();
        RenderStatus = $"Đã xóa {itemsToDelete.Count} trang khỏi đầu ra";
    }


    [RelayCommand]
    private void DeleteOutputGroup(OutputGroup? group)
    {
        if (group is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        OutputGroups.Remove(group);
        RecalculateOutputOccurrences();
        NotifyStatisticsChanged();
        RenderStatus = $"Đã xóa {group.Name}";
    }

    [RelayCommand]
    private void DuplicateOutputItem(OutputTrayItemView? itemView)
    {
        if (itemView is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        var duplicate = new OutputPageItem(
            Guid.NewGuid(),
            itemView.Group.Id,
            itemView.Item.SourceFileId,
            itemView.Item.SourcePageNumber);
        var index = itemView.Group.Items.IndexOf(itemView.Item);
        itemView.Group.Items.Insert(index + 1, duplicate);

        RecalculateOutputOccurrences();
        NotifyStatisticsChanged();
        RenderStatus = "Đã nhân bản 1 trang đầu ra";
    }

    [RelayCommand]
    private void DuplicateOutputItems(IReadOnlyList<OutputTrayItemView>? itemViews)
    {
        if (itemViews is null || itemViews.Count == 0)
        {
            return;
        }

        CaptureUndoSnapshot();
        foreach (var group in itemViews
                     .GroupBy(itemView => itemView.Group)
                     .OrderByDescending(grouping => OutputGroups.IndexOf(grouping.Key)))
        {
            foreach (var itemView in group
                         .OrderByDescending(itemView => itemView.Group.Items.IndexOf(itemView.Item)))
            {
                var index = itemView.Group.Items.IndexOf(itemView.Item);
                if (index < 0)
                {
                    continue;
                }

                itemView.Group.Items.Insert(
                    index + 1,
                    new OutputPageItem(
                        Guid.NewGuid(),
                        itemView.Group.Id,
                        itemView.Item.SourceFileId,
                        itemView.Item.SourcePageNumber));
            }
        }

        RecalculateOutputOccurrences();
        NotifyStatisticsChanged();
        RenderStatus = $"Đã nhân bản {itemViews.Count} trang đầu ra";
    }

    [RelayCommand]
    private void DuplicateOutputGroup(OutputGroup? group)
    {
        if (group is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        var duplicateGroupId = Guid.NewGuid();
        var duplicate = new OutputGroup(
            duplicateGroupId,
            $"Group {OutputGroups.Count + 1}",
            DateTimeOffset.UtcNow,
            group.Items.Select(item => new OutputPageItem(
                Guid.NewGuid(),
                duplicateGroupId,
                item.SourceFileId,
                item.SourcePageNumber)));
        var index = OutputGroups.IndexOf(group);
        OutputGroups.Insert(index + 1, duplicate);

        RecalculateOutputOccurrences();
        NotifyStatisticsChanged();
        RenderStatus = $"Đã nhân bản {group.Name}";
    }

    [RelayCommand]
    private void MoveOutputItemUp(OutputTrayItemView? itemView)
    {
        MoveOutputItem(itemView, -1);
    }

    [RelayCommand]
    private void MoveOutputItemDown(OutputTrayItemView? itemView)
    {
        MoveOutputItem(itemView, 1);
    }

    [RelayCommand]
    private void MoveOutputItemBefore(OutputItemMoveRequest? request)
    {
        if (request is null || request.SourceItem.Id == request.TargetItem.Id)
        {
            return;
        }

        var flattened = OutputGroups
            .SelectMany(group => group.Items.Select(item => new { Group = group, Item = item }))
            .ToList();
        var source = flattened.FirstOrDefault(entry => entry.Item.Id == request.SourceItem.Id);
        var target = flattened.FirstOrDefault(entry => entry.Item.Id == request.TargetItem.Id);
        if (source is null || target is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        source.Group.Items.Remove(source.Item);
        var movedItem = target.Group.Id == source.Item.GroupId
            ? source.Item
            : new OutputPageItem(source.Item.Id, target.Group.Id, source.Item.SourceFileId, source.Item.SourcePageNumber);
        var targetIndex = target.Group.Items.IndexOf(target.Item);
        target.Group.Items.Insert(Math.Max(0, targetIndex), movedItem);

        if (source.Group.Items.Count == 0)
        {
            OutputGroups.Remove(source.Group);
        }

        NotifyStatisticsChanged();
        RenderStatus = "Đã thả trang đầu ra";
    }

    [RelayCommand]
    private void MoveOutputGroupUp(OutputGroup? group)
    {
        MoveOutputGroup(group, -1);
    }

    [RelayCommand]
    private void MoveOutputGroupDown(OutputGroup? group)
    {
        MoveOutputGroup(group, 1);
    }

    [RelayCommand]
    private void MoveOutputGroupBefore(OutputGroupMoveRequest? request)
    {
        if (request is null || request.SourceGroup.Id == request.TargetGroup.Id)
        {
            return;
        }

        var sourceIndex = OutputGroups.IndexOf(request.SourceGroup);
        var targetIndex = OutputGroups.IndexOf(request.TargetGroup);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return;
        }

        CaptureUndoSnapshot();
        OutputGroups.RemoveAt(sourceIndex);
        if (sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        OutputGroups.Insert(Math.Max(0, targetIndex), request.SourceGroup);
        NotifyStatisticsChanged();
        RenderStatus = $"Đã thả {request.SourceGroup.Name}";
    }

    [RelayCommand]
    private void ToggleOutputGroupCollapse(OutputGroup? group)
    {
        if (group is null)
        {
            return;
        }

        CaptureUndoSnapshot();
        group.IsCollapsed = !group.IsCollapsed;
        NotifyStatisticsChanged();
        RenderStatus = group.IsCollapsed
            ? $"Đã thu gọn {group.Name}"
            : $"Đã mở rộng {group.Name}";
    }

    [RelayCommand]
    private async Task ExportPdfAsync(CancellationToken cancellationToken)
    {
        if (OutputPageCount == 0)
        {
            RenderStatus = "Khay đầu ra đang trống";
            return;
        }

        var outputPath = fileDialogService.PickOutputPdfFile();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            RenderStatus = "Chưa chọn nơi lưu PDF";
            return;
        }

        try
        {
            ExportProgressPageCount = 0;
            ExportProgressTotalPageCount = OutputPageCount;
            ExportProgressPercent = 0;
            IsExporting = true;
            var progress = new ExportProgress(pageCount =>
            {
                ExportProgressPageCount = pageCount;
                ExportProgressPercent = ExportProgressTotalPageCount == 0
                    ? 0
                    : Math.Clamp((int)Math.Round(pageCount * 100.0 / ExportProgressTotalPageCount), 0, 100);
                OnPropertyChanged(nameof(ExportProgressText));
                RenderStatus = $"Đang xuất {pageCount}/{OutputPageCount} trang";
            });

            await pdfExportService.ExportAsync(SourceFiles, OutputGroups, outputPath, progress, cancellationToken);
            ExportProgressPercent = 100;
            OnPropertyChanged(nameof(ExportProgressText));
            RenderStatus = $"Đã xuất PDF: {Path.GetFileName(outputPath)}";
            try
            {
                await foxitLauncherService.OpenPdfAsync(outputPath, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                RenderStatus = $"Đã xuất PDF nhưng không mở được file: {Path.GetFileName(outputPath)}";
            }
        }
        catch (OperationCanceledException)
        {
            RenderStatus = "Đã hủy xuất PDF";
        }
        catch (PdfExportException ex)
        {
            RenderStatus = DescribeExportError(ex.Error);
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCheckForUpdate))]
    private async Task CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsUpdating = true;
            RenderStatus = "Dang kiem tra cap nhat...";
            var result = await updateService.CheckAndInstallUpdateAsync(cancellationToken);
            RenderStatus = result.Message;

            if (result.Status == AppUpdateStatus.UpdateStarted)
            {
                await Task.Delay(500, cancellationToken);
                Environment.Exit(0);
            }
        }
        catch (OperationCanceledException)
        {
            RenderStatus = "Da huy kiem tra cap nhat";
        }
        finally
        {
            IsUpdating = false;
        }
    }

    partial void OnExportProgressPercentChanged(int value)
    {
        OnPropertyChanged(nameof(ExportProgressText));
    }

    partial void OnExportProgressPageCountChanged(int value)
    {
        OnPropertyChanged(nameof(ExportProgressText));
    }

    partial void OnExportProgressTotalPageCountChanged(int value)
    {
        OnPropertyChanged(nameof(ExportProgressText));
    }

    partial void OnIsUpdatingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheckForUpdate));
        CheckForUpdateCommand.NotifyCanExecuteChanged();
    }

    private void ExportPdfPlaceholder()
    {
        RenderStatus = "Chưa cấu hình export PDF";
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        redoHistory.Add(CaptureSnapshot());
        var snapshot = undoHistory[^1];
        undoHistory.RemoveAt(undoHistory.Count - 1);
        RestoreSnapshot(snapshot);
        RenderStatus = "Đã hoàn tác";
        NotifyHistoryChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        undoHistory.Add(CaptureSnapshot());
        var snapshot = redoHistory[^1];
        redoHistory.RemoveAt(redoHistory.Count - 1);
        RestoreSnapshot(snapshot);
        RenderStatus = "Đã làm lại";
        NotifyHistoryChanged();
    }

    private void NotifyStatisticsChanged()
    {
        OnPropertyChanged(nameof(SourceFileCount));
        OnPropertyChanged(nameof(SourcePageCount));
        OnPropertyChanged(nameof(SelectedPageCount));
        OnPropertyChanged(nameof(OutputPageCount));
        OnPropertyChanged(nameof(OutputTrayItems));
        OnPropertyChanged(nameof(HasSelectedPages));
        OnPropertyChanged(nameof(HasOutputPages));
        OnPropertyChanged(nameof(HasSourceFiles));
        OnPropertyChanged(nameof(FilteredSourceFiles));
        AddSelectionToOutputCommand.NotifyCanExecuteChanged();
        PreviewOutputCommand.NotifyCanExecuteChanged();
        NotifyHistoryChanged();
        ScheduleAutoSave();
    }

    partial void OnSourceSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredSourceFiles));
        OnPropertyChanged(nameof(HasSourceSearch));
        ClearSourceSearchCommand.NotifyCanExecuteChanged();
    }

    partial void OnThumbnailZoomChanged(double value)
    {
        OnPropertyChanged(nameof(ThumbnailCardWidth));
        OnPropertyChanged(nameof(ThumbnailPreviewHeight));
        OnPropertyChanged(nameof(ThumbnailCardMinHeight));
        ScheduleAutoSave();
    }

    partial void OnPreviewPageChanged(SourcePdfPage? value)
    {
        PreviewSourceFile = value is null
            ? null
            : SourceFiles.FirstOrDefault(file => file.Id == value.SourceFileId);
        NotifyPreviewNavigationChanged();
    }

    partial void OnPreviewSourceFileChanged(SourcePdfFile? value)
    {
        OnPropertyChanged(nameof(PreviewSourcePages));
        OnPropertyChanged(nameof(PreviewGridPages));
    }

    partial void OnIsPreviewGridOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(PreviewGridPages));
        OnPropertyChanged(nameof(OutputPreviewGridItems));
        OnPropertyChanged(nameof(OutputPreviewGridPages));
        OnPropertyChanged(nameof(CurrentOutputPreviewGridItems));
        OnPropertyChanged(nameof(OutputPreviewGridPageCount));
        NotifyPreviewNavigationChanged();
    }

    partial void OnIsOutputPreviewModeChanged(bool value)
    {
        OnPropertyChanged(nameof(PreviewGridPages));
        OnPropertyChanged(nameof(OutputPreviewGridItems));
        OnPropertyChanged(nameof(OutputPreviewGridPages));
        OnPropertyChanged(nameof(CurrentOutputPreviewGridItems));
        OnPropertyChanged(nameof(OutputPreviewGridPageCount));
        NotifyPreviewNavigationChanged();
    }

    partial void OnOutputPreviewIndexChanged(int value)
    {
        NotifyPreviewNavigationChanged();
    }

    partial void OnOutputPreviewGridPageIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentOutputPreviewGridItems));
        NotifyPreviewNavigationChanged();
    }

    private static string DescribeError(PdfMetadataError error)
    {
        return error switch
        {
            PdfMetadataError.NotFound => "không tìm thấy file",
            PdfMetadataError.NotPdf => "không phải PDF",
            PdfMetadataError.PermissionDenied => "không có quyền đọc",
            PdfMetadataError.PasswordRequired => "cần mật khẩu",
            PdfMetadataError.InvalidPdf => "PDF lỗi hoặc hỏng",
            _ => "không thể đọc metadata"
        };
    }

    private static string DescribeExportError(PdfExportError error)
    {
        return error switch
        {
            PdfExportError.EmptyOutput => "Khay đầu ra đang trống",
            PdfExportError.OutputMatchesSource => "Không được ghi đè file PDF nguồn",
            PdfExportError.MissingSource => "Thiếu file PDF nguồn",
            PdfExportError.SourceChanged => "File PDF nguồn đã thay đổi",
            PdfExportError.InvalidPage => "Trang đầu ra không hợp lệ",
            PdfExportError.DestinationUnavailable => "Không thể ghi file PDF đầu ra",
            _ => "Không thể xuất PDF"
        };
    }

    private sealed class ExportProgress : IProgress<int>
    {
        private readonly Action<int> report;

        public ExportProgress(Action<int> report)
        {
            this.report = report;
        }

        public void Report(int value)
        {
            report(value);
        }
    }

    private static string CreateThumbnailCacheKey(SourcePdfFile sourceFile, SourcePdfPage page, int pixelWidth)
    {
        return string.Join('|', sourceFile.Fingerprint, sourceFile.FileSize, page.PageNumber, pixelWidth);
    }

    private int CalculatePreviewPixelWidth(SourcePdfPage page)
    {
        var baseWidth = PreviewFitMode switch
        {
            PreviewFitMode.FitHeight when page.Height > 0 => (int)Math.Round(900 * (page.Width / page.Height)),
            PreviewFitMode.FitPage => 1100,
            PreviewFitMode.FitWidth => 1200,
            _ => (int)Math.Round(900 * PreviewZoom)
        };

        return Math.Clamp(baseWidth, 300, 2400);
    }

    private void NotifyPreviewNavigationChanged()
    {
        OnPropertyChanged(nameof(PreviewTitle));
        OnPropertyChanged(nameof(PreviewSourcePages));
        OnPropertyChanged(nameof(PreviewGridPages));
        OnPropertyChanged(nameof(OutputPreviewGridItems));
        OnPropertyChanged(nameof(OutputPreviewGridPages));
        OnPropertyChanged(nameof(CurrentOutputPreviewGridItems));
        OnPropertyChanged(nameof(CurrentOutputPreviewItem));
        OnPropertyChanged(nameof(HasCurrentOutputPreviewItem));
        OnPropertyChanged(nameof(OutputPreviewGridPageCount));
        OnPropertyChanged(nameof(CanNavigatePreviewPrevious));
        OnPropertyChanged(nameof(CanNavigatePreviewNext));
    }

    private OutputTrayItemView MarkCurrentOutputPreviewItem(OutputTrayItemView item)
    {
        return item with
        {
            IsPreviewSelected = CurrentOutputPreviewItem?.Item.Id == item.Item.Id
        };
    }

    private void RefreshOutputPreviewItems(int preferredIndex)
    {
        if (!IsOutputPreviewMode)
        {
            return;
        }

        outputPreviewItems = OutputTrayItems
            .Where(item => item.SourcePage is not null)
            .ToList();

        if (outputPreviewItems.Count == 0)
        {
            previewRenderCancellation?.Cancel();
            PreviewImage = null;
            PreviewPage = null;
            PreviewSourceFile = null;
            PreviewState = ThumbnailState.NotRequested;
            PreviewError = null;
            IsPreviewOpen = false;
            IsOutputPreviewMode = false;
            IsPreviewGridOpen = false;
            OutputPreviewIndex = 0;
            OutputPreviewGridPageIndex = 0;
            NotifyPreviewNavigationChanged();
            return;
        }

        var nextIndex = Math.Clamp(preferredIndex, 0, outputPreviewItems.Count - 1);
        OutputPreviewGridPageIndex = Math.Clamp(
            OutputPreviewGridPageIndex,
            0,
            Math.Max(0, OutputPreviewGridPageCount - 1));
        SetOutputPreviewPage(nextIndex);
        NotifyPreviewNavigationChanged();
    }

    private void SetOutputPreviewPage(int index)
    {
        if (index < 0 || index >= outputPreviewItems.Count)
        {
            return;
        }

        var item = outputPreviewItems[index];
        if (item.SourcePage is null)
        {
            return;
        }

        OutputPreviewIndex = index;
        PreviewPage = item.SourcePage;
        PreviewSourceFile = SourceFiles.FirstOrDefault(file => file.Id == item.SourcePage.SourceFileId);
        CancelThumbnailRenderingForSourceFile(item.SourcePage.SourceFileId, exceptPage: item.SourcePage);
    }

    private void CancelThumbnailRenderingForSourceFile(Guid sourceFileId, SourcePdfPage? exceptPage = null)
    {
        var sourceFile = SourceFiles.FirstOrDefault(file => file.Id == sourceFileId);
        if (sourceFile is null)
        {
            return;
        }

        var pixelWidth = Math.Clamp((int)Math.Round(150 * ThumbnailZoom), 90, 260);
        foreach (var page in sourceFile.Pages.Where(page => !ReferenceEquals(page, exceptPage) && page.ThumbnailState == ThumbnailState.Loading))
        {
            thumbnailRenderQueue.Cancel(CreateThumbnailCacheKey(sourceFile, page, pixelWidth));
            page.ThumbnailState = ThumbnailState.NotRequested;
        }
    }

    private void ApplyProjectSnapshot(ProjectSessionSnapshot snapshot)
    {
        SourceFiles.Clear();
        OutputGroups.Clear();

        foreach (var sourceFile in snapshot.SourceFiles)
        {
            SourceFiles.Add(sourceFile);
        }

        foreach (var outputGroup in snapshot.OutputGroups)
        {
            OutputGroups.Add(outputGroup);
        }

        ThumbnailZoom = Math.Clamp(snapshot.ThumbnailZoom, 0.6, 1.6);
        RecalculateOutputOccurrences();
        undoHistory.Clear();
        redoHistory.Clear();
        selectionAnchor = null;
        NotifyStatisticsChanged();
    }

    private void ScheduleAutoSave()
    {
        if (SourceFiles.Count == 0 && OutputGroups.Count == 0)
        {
            return;
        }

        autoSaveService.Schedule(ProjectStateMapper.FromSession(SourceFiles, OutputGroups, ThumbnailZoom));
    }

    private void SelectRange(SourcePdfPage page)
    {
        if (selectionAnchor is null || selectionAnchor.SourceFileId != page.SourceFileId)
        {
            page.IsSelected = true;
            selectionAnchor = page;
            return;
        }

        var sourceFile = SourceFiles.FirstOrDefault(file => file.Id == page.SourceFileId);
        if (sourceFile is null)
        {
            page.IsSelected = true;
            selectionAnchor = page;
            return;
        }

        var minPage = Math.Min(selectionAnchor.PageNumber, page.PageNumber);
        var maxPage = Math.Max(selectionAnchor.PageNumber, page.PageNumber);
        foreach (var candidate in sourceFile.Pages.Where(candidate => candidate.PageNumber >= minPage && candidate.PageNumber <= maxPage))
        {
            candidate.IsSelected = true;
        }
    }

    private void RecalculateOutputOccurrences()
    {
        foreach (var page in SourceFiles.SelectMany(file => file.Pages))
        {
            page.OutputOccurrenceCount = 0;
        }

        foreach (var item in OutputGroups.SelectMany(group => group.Items))
        {
            var page = SourceFiles
                .FirstOrDefault(file => file.Id == item.SourceFileId)?
                .Pages.FirstOrDefault(page => page.PageNumber == item.SourcePageNumber);
            if (page is not null)
            {
                page.OutputOccurrenceCount++;
            }
        }
    }

    private void MoveOutputGroup(OutputGroup? group, int delta)
    {
        if (group is null)
        {
            return;
        }

        var index = OutputGroups.IndexOf(group);
        var newIndex = index + delta;
        if (index < 0 || newIndex < 0 || newIndex >= OutputGroups.Count)
        {
            return;
        }

        CaptureUndoSnapshot();
        OutputGroups.Move(index, newIndex);
        NotifyStatisticsChanged();
        RenderStatus = $"Đã di chuyển {group.Name}";
    }

    private void MoveOutputItem(OutputTrayItemView? itemView, int delta)
    {
        if (itemView is null)
        {
            return;
        }

        var flattened = OutputGroups
            .SelectMany(group => group.Items.Select(item => new { Group = group, Item = item }))
            .ToList();
        var index = flattened.FindIndex(entry => entry.Item.Id == itemView.Item.Id);
        var newIndex = index + delta;
        if (index < 0 || newIndex < 0 || newIndex >= flattened.Count)
        {
            return;
        }

        var target = flattened[newIndex];
        var sourceGroup = itemView.Group;
        var sourceItem = itemView.Item;
        var removedIndex = sourceGroup.Items.IndexOf(sourceItem);
        if (removedIndex < 0)
        {
            return;
        }

        CaptureUndoSnapshot();
        sourceGroup.Items.RemoveAt(removedIndex);
        var movedItem = target.Group.Id == sourceItem.GroupId
            ? sourceItem
            : new OutputPageItem(sourceItem.Id, target.Group.Id, sourceItem.SourceFileId, sourceItem.SourcePageNumber);
        var targetIndex = target.Group.Items.IndexOf(target.Item);
        if (delta > 0)
        {
            targetIndex++;
        }

        targetIndex = Math.Clamp(targetIndex, 0, target.Group.Items.Count);
        target.Group.Items.Insert(targetIndex, movedItem);

        if (sourceGroup.Items.Count == 0)
        {
            OutputGroups.Remove(sourceGroup);
        }

        NotifyStatisticsChanged();
        RenderStatus = "Đã di chuyển trang đầu ra";
    }

    private void CaptureUndoSnapshot()
    {
        undoHistory.Add(CaptureSnapshot());
        if (undoHistory.Count > HistoryLimit)
        {
            undoHistory.RemoveAt(0);
        }

        redoHistory.Clear();
        NotifyHistoryChanged();
    }

    private AppHistorySnapshot CaptureSnapshot()
    {
        return new AppHistorySnapshot(
            OutputGroups.Select(group => new OutputGroupSnapshot(
                group.Id,
                group.Name,
                group.CreatedAt,
                group.IsCollapsed,
                group.Items.Select(item => new OutputItemSnapshot(
                    item.Id,
                    item.GroupId,
                    item.SourceFileId,
                    item.SourcePageNumber)).ToList())).ToList(),
            SourceFiles
                .SelectMany(file => file.Pages)
                .Where(page => page.IsSelected)
                .Select(page => page.Id)
                .ToHashSet());
    }

    private void RestoreSnapshot(AppHistorySnapshot snapshot)
    {
        OutputGroups.Clear();
        foreach (var groupSnapshot in snapshot.OutputGroups)
        {
            var group = new OutputGroup(
                groupSnapshot.Id,
                groupSnapshot.Name,
                groupSnapshot.CreatedAt,
                groupSnapshot.Items.Select(item => new OutputPageItem(
                    item.Id,
                    item.GroupId,
                    item.SourceFileId,
                    item.SourcePageNumber)))
            {
                IsCollapsed = groupSnapshot.IsCollapsed
            };
            OutputGroups.Add(group);
        }

        foreach (var page in SourceFiles.SelectMany(file => file.Pages))
        {
            page.IsSelected = snapshot.SelectedPageIds.Contains(page.Id);
        }

        selectionAnchor = SourceFiles
            .SelectMany(file => file.Pages)
            .LastOrDefault(page => page.IsSelected);
        RecalculateOutputOccurrences();
        NotifyStatisticsChanged();
    }

    private void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private sealed record AppHistorySnapshot(
        IReadOnlyList<OutputGroupSnapshot> OutputGroups,
        IReadOnlySet<Guid> SelectedPageIds);

    private sealed record OutputGroupSnapshot(
        Guid Id,
        string Name,
        DateTimeOffset CreatedAt,
        bool IsCollapsed,
        IReadOnlyList<OutputItemSnapshot> Items);

    private sealed record OutputItemSnapshot(
        Guid Id,
        Guid GroupId,
        Guid SourceFileId,
        int SourcePageNumber);
}
