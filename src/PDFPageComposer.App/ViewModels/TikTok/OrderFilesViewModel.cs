using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Matching;
using PDFPageComposer.App.TikTok.Models;
using Serilog;

namespace PDFPageComposer.App.ViewModels.TikTok;

public enum OrderFilesResult
{
    Cancel,
    Save,
    Automatic,
}

/// <summary>
/// "Xem / chọn file in" of one order, laid out like the composer: every library file of the idol is a row of page
/// thumbnails (click a page to add or remove it), and the right side shows the pages chosen for the selected set in
/// print order. Saving turns each set into <see cref="FileOverride"/> runs of consecutive pages.
/// </summary>
public sealed partial class OrderFilesViewModel : ObservableObject
{
    private const int ThumbnailPixelWidth = 200;

    private readonly TikTokOrder order;
    private readonly IReadOnlyList<LibraryFileRowViewModel> library;
    private readonly IPdfRenderService? renderer;
    private readonly Func<string, bool> fileExists;
    private readonly Dictionary<(string Path, int Page), Task<PdfPageRenderResult>> renderCache = [];

    public OrderFilesViewModel(
        TikTokOrder order,
        IReadOnlyDictionary<string, SkuConfig> skus,
        IReadOnlyList<PdfAsset> assets,
        IReadOnlyList<IdolAlias> aliases,
        IReadOnlyList<PrintPlanCopy> currentCopies,
        IPdfRenderService? renderer,
        Func<string, bool>? fileExists = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(skus);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(aliases);
        ArgumentNullException.ThrowIfNull(currentCopies);

        this.order = order;
        this.renderer = renderer;
        this.fileExists = fileExists ?? File.Exists;

        var valid = assets.Where(asset => asset.IsValid && asset.PageCount > 0).ToList();
        var existing = valid.Where(asset => this.fileExists(asset.FilePath)).ToList();
        MissingFileCount = valid.Count - existing.Count;
        library = existing
            .OrderByDescending(asset => asset.FileDate)
            .ThenBy(asset => asset.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(asset => new LibraryFileRowViewModel(asset))
            .ToList();

        foreach (var line in order.Lines.OrderBy(line => line.LineNo))
        {
            if (!skus.TryGetValue(line.SkuId, out var sku) || !PdfAssetMatcher.IsUsableAuto(sku))
            {
                continue;
            }

            var idol = PdfAssetMatcher.ResolveIdol(sku.Idol!, aliases);
            var pages = PdfAssetMatcher.PagesForCards(sku.CardCount!.Value);
            for (var copyIndex = 1; copyIndex <= line.Quantity; copyIndex++)
            {
                var title = $"{idol} · {sku.CardCount} card = {pages} trang";
                if (line.Quantity > 1)
                {
                    title += $" · bộ {copyIndex}/{line.Quantity}";
                }

                var slot = new FileSlotViewModel(line.SkuId, copyIndex, title, pages, idol);
                foreach (var copy in currentCopies.Where(copy =>
                    copy.CopyIndex == copyIndex && string.Equals(copy.SkuId, line.SkuId, StringComparison.Ordinal)))
                {
                    for (var page = copy.FirstPage; page <= copy.LastPage; page++)
                    {
                        slot.Pages.Add(new PageThumbViewModel(copy.Asset, page));
                    }
                }

                slot.Pages.CollectionChanged += OnSlotPagesChanged;
                Slots.Add(slot);
            }
        }

        Header = $"Đơn {order.OrderId} · {string.Join(" + ", order.Lines.Select(line => $"{line.VariationName} x{line.Quantity}"))}";
        SelectedSlot = Slots.FirstOrDefault();
    }

    /// <summary>Raised when the dialog should close; <see cref="Result"/> says how.</summary>
    public event EventHandler? CloseRequested;

    public string Title => $"Xem / chọn file in · đơn {order.OrderId}";

    public string Header { get; }

    /// <summary>Library files that are no longer on disk (the library needs a new scan); they are not listed.</summary>
    public int MissingFileCount { get; }

    public string MissingFilesText => MissingFileCount == 0
        ? string.Empty
        : $"{MissingFileCount} file trong kho không còn trên ổ đĩa nên không hiện ở đây. Hãy bấm \"Quét kho\" để cập nhật.";

    public ObservableCollection<FileSlotViewModel> Slots { get; } = [];

    public bool HasManySlots => Slots.Count > 1;

    public ObservableCollection<LibraryFileRowViewModel> LibraryFiles { get; } = [];

    public OrderFilesResult Result { get; private set; } = OrderFilesResult.Cancel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPages), nameof(SelectedSlotStatus))]
    private FileSlotViewModel? selectedSlot;

    [ObservableProperty]
    private string filterText = string.Empty;

    public ObservableCollection<PageThumbViewModel>? SelectedPages => SelectedSlot?.Pages;

    public string SelectedSlotStatus => SelectedSlot is { } slot ? $"{slot.Title} · đã chọn {slot.StatusText}" : string.Empty;

    public bool CanSave => Slots.Count > 0 && Slots.All(slot => slot.IsComplete) && MissingSelectedPage() is null;

    public string ValidationText
    {
        get
        {
            if (Slots.Count == 0)
            {
                return "Đơn không có món ghép tự động nào.";
            }

            if (Slots.FirstOrDefault(slot => !slot.IsComplete) is { } incomplete)
            {
                return $"{incomplete.Title}: đang chọn {incomplete.StatusText}.";
            }

            return MissingSelectedPage() is { } missing
                ? $"File {Path.GetFileName(missing.Asset.FilePath)} không còn trên ổ đĩa, hãy bỏ các trang của file này."
                : string.Empty;
        }
    }

    partial void OnSelectedSlotChanged(FileSlotViewModel? value)
    {
        if (value is not null && !string.Equals(FilterText, value.IdolFilter, StringComparison.Ordinal))
        {
            FilterText = value.IdolFilter;
        }
        else
        {
            RefreshMarks();
        }
    }

    partial void OnFilterTextChanged(string value)
    {
        var filter = TextNormalizer.Normalize(value);
        LibraryFiles.Clear();
        foreach (var row in library.Where(row => filter.Length == 0 || row.SearchText.Contains(filter, StringComparison.Ordinal)))
        {
            LibraryFiles.Add(row);
        }

        RefreshMarks();
    }

    /// <summary>Each set as runs of consecutive pages of one file, Seq starting at 1 inside each set.</summary>
    public IReadOnlyList<FileOverride> BuildOverrides()
    {
        var overrides = new List<FileOverride>();
        foreach (var slot in Slots)
        {
            var seq = 0;
            PageThumbViewModel? runStart = null;
            var runLength = 0;
            foreach (var page in slot.Pages.Cast<PageThumbViewModel?>().Append(null))
            {
                if (runStart is not null && page is not null
                    && string.Equals(page.Asset.FilePath, runStart.Asset.FilePath, StringComparison.OrdinalIgnoreCase)
                    && page.PageNumber == runStart.PageNumber + runLength)
                {
                    runLength++;
                    continue;
                }

                if (runStart is not null)
                {
                    overrides.Add(new FileOverride(
                        order.OrderId, slot.SkuId, slot.CopyIndex, ++seq, runStart.Asset.FilePath, runLength, runStart.PageNumber));
                }

                runStart = page;
                runLength = 1;
            }
        }

        return overrides;
    }

    /// <summary>Renders a thumbnail when its element appears (lazy, like the composer); results are cached per page.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RenderThumbnailAsync(PageThumbViewModel? thumb)
    {
        if (thumb is null || thumb.Image is not null || thumb.HasError || renderer is null)
        {
            return;
        }

        var key = (thumb.Asset.FilePath, thumb.PageNumber);
        if (!renderCache.TryGetValue(key, out var task))
        {
            task = renderer.RenderPageAsync(thumb.Asset.FilePath, thumb.PageNumber, ThumbnailPixelWidth, CancellationToken.None);
            renderCache[key] = task;
        }

        try
        {
            thumb.Image = await task;
        }
        catch (Exception exception)
        {
            Log.Warning("TikTok page thumbnail failed with {ExceptionType}", exception.GetType().FullName);
            thumb.HasError = true;
        }
    }

    /// <summary>Clicking a library page adds it to the selected set, or removes it when it is already there.</summary>
    [RelayCommand]
    private void TogglePage(PageThumbViewModel? thumb)
    {
        var slot = SelectedSlot;
        if (thumb is null || slot is null)
        {
            return;
        }

        var existing = slot.Pages.FirstOrDefault(page => page.IsSamePage(thumb));
        if (existing is not null)
        {
            slot.Pages.Remove(existing);
        }
        else
        {
            slot.Pages.Add(new PageThumbViewModel(thumb.Asset, thumb.PageNumber) { Image = thumb.Image });
        }
    }

    /// <summary>"Lấy cả file": adds every page of the file that is not chosen yet, in page order.</summary>
    [RelayCommand]
    private void AddWholeFile(LibraryFileRowViewModel? row)
    {
        var slot = SelectedSlot;
        if (row is null || slot is null)
        {
            return;
        }

        foreach (var thumb in row.Pages.Where(thumb => !slot.Pages.Any(page => page.IsSamePage(thumb))))
        {
            slot.Pages.Add(new PageThumbViewModel(thumb.Asset, thumb.PageNumber) { Image = thumb.Image });
        }
    }

    [RelayCommand]
    private void RemovePage(PageThumbViewModel? page)
    {
        if (page is not null)
        {
            SelectedSlot?.Pages.Remove(page);
        }
    }

    [RelayCommand]
    private void MovePageLeft(PageThumbViewModel? page) => Move(page, -1);

    [RelayCommand]
    private void MovePageRight(PageThumbViewModel? page) => Move(page, 1);

    [RelayCommand]
    private void ClearSelectedSlot() => SelectedSlot?.Pages.Clear();

    [RelayCommand]
    private void Save()
    {
        if (CanSave)
        {
            Close(OrderFilesResult.Save);
        }
    }

    [RelayCommand]
    private void UseAutomatic() => Close(OrderFilesResult.Automatic);

    [RelayCommand]
    private void Cancel() => Close(OrderFilesResult.Cancel);

    private void Close(OrderFilesResult result)
    {
        Result = result;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Move(PageThumbViewModel? page, int offset)
    {
        var pages = SelectedSlot?.Pages;
        if (page is null || pages is null)
        {
            return;
        }

        var index = pages.IndexOf(page);
        var target = index + offset;
        if (index >= 0 && target >= 0 && target < pages.Count)
        {
            pages.Move(index, target);
        }
    }

    private PageThumbViewModel? MissingSelectedPage() =>
        Slots.SelectMany(slot => slot.Pages).FirstOrDefault(page => !fileExists(page.Asset.FilePath));

    private void OnSlotPagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshMarks();
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(ValidationText));
        OnPropertyChanged(nameof(SelectedSlotStatus));
    }

    /// <summary>Marks the library pages chosen for the selected set with their position in print order.</summary>
    private void RefreshMarks()
    {
        var positions = new Dictionary<(string, int), int>();
        var pages = SelectedSlot?.Pages ?? [];
        for (var index = 0; index < pages.Count; index++)
        {
            positions.TryAdd((pages[index].Asset.FilePath.ToUpperInvariant(), pages[index].PageNumber), index + 1);
            pages[index].Position = index + 1;
        }

        foreach (var row in LibraryFiles)
        {
            foreach (var thumb in row.Pages)
            {
                thumb.Position = positions.TryGetValue((thumb.Asset.FilePath.ToUpperInvariant(), thumb.PageNumber), out var position)
                    ? position
                    : 0;
            }
        }
    }
}

/// <summary>One printed set of the order (a line copy) and the pages chosen for it, in print order.</summary>
public sealed class FileSlotViewModel : ObservableObject
{
    public FileSlotViewModel(string skuId, int copyIndex, string title, int requiredPages, string idolFilter)
    {
        SkuId = skuId;
        CopyIndex = copyIndex;
        Title = title;
        RequiredPages = requiredPages;
        IdolFilter = idolFilter;
        Pages.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsComplete));
            OnPropertyChanged(nameof(StatusText));
        };
    }

    public string SkuId { get; }

    public int CopyIndex { get; }

    public string Title { get; }

    public int RequiredPages { get; }

    /// <summary>Library filter used when this set is selected (the idol after aliases).</summary>
    public string IdolFilter { get; }

    public ObservableCollection<PageThumbViewModel> Pages { get; } = [];

    public bool IsComplete => Pages.Count == RequiredPages;

    public string StatusText => $"{Pages.Count}/{RequiredPages} trang";
}

/// <summary>One library file: a row of page thumbnails.</summary>
public sealed class LibraryFileRowViewModel
{
    public LibraryFileRowViewModel(PdfAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        Asset = asset;
        FileName = Path.GetFileNameWithoutExtension(asset.FilePath);
        InfoText = $"{asset.PageCount} trang · {asset.FileDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
        SearchText = TextNormalizer.Normalize(Path.GetFileName(asset.FilePath));
        Pages = Enumerable.Range(1, asset.PageCount).Select(page => new PageThumbViewModel(asset, page)).ToList();
    }

    public PdfAsset Asset { get; }

    public string FileName { get; }

    public string InfoText { get; }

    public IReadOnlyList<PageThumbViewModel> Pages { get; }

    internal string SearchText { get; }
}

/// <summary>One PDF page thumbnail; <see cref="Position"/> &gt; 0 is its place in the selected set's print order.</summary>
public sealed partial class PageThumbViewModel : ObservableObject
{
    public PageThumbViewModel(PdfAsset asset, int pageNumber)
    {
        ArgumentNullException.ThrowIfNull(asset);
        Asset = asset;
        PageNumber = pageNumber;
    }

    public PdfAsset Asset { get; }

    public int PageNumber { get; }

    public string Caption => $"{Path.GetFileNameWithoutExtension(Asset.FilePath)} · tr {PageNumber}";

    public string PageLabel => $"Trang {PageNumber}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pixels), nameof(PixelWidth), nameof(PixelHeight), nameof(Stride))]
    private PdfPageRenderResult? image;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelected))]
    private int position;

    public bool IsSelected => Position > 0;

    public byte[]? Pixels => Image?.Bgra32Pixels;

    public int PixelWidth => Image?.PixelWidth ?? 0;

    public int PixelHeight => Image?.PixelHeight ?? 0;

    public int Stride => Image?.Stride ?? 0;

    public bool IsSamePage(PageThumbViewModel other) =>
        PageNumber == other.PageNumber && string.Equals(Asset.FilePath, other.Asset.FilePath, StringComparison.OrdinalIgnoreCase);
}
