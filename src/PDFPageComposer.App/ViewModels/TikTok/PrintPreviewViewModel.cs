using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.TikTok.Models;
using Serilog;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>
/// "Xem trước file in": every page of the print file of the ticked orders, in print order and grouped per order
/// (blank separator pages included), like the composer's output preview. Each order can get other files ("Đổi file")
/// or leave the batch; the plan is rebuilt after every change so the preview always matches what "Tạo file in" prints.
/// </summary>
public sealed partial class PrintPreviewViewModel : ObservableObject
{
    private readonly Func<PrintPlan> buildPlan;
    private readonly Func<string, Task> editOrderFiles;
    private readonly Action<string> removeOrder;
    private readonly Func<string, bool>? copyText;
    private readonly PageImageCache images;

    public PrintPreviewViewModel(
        Func<PrintPlan> buildPlan,
        Func<string, Task> editOrderFiles,
        Action<string> removeOrder,
        IPdfRenderService? renderer,
        Func<string, bool>? copyText = null)
    {
        ArgumentNullException.ThrowIfNull(buildPlan);
        ArgumentNullException.ThrowIfNull(editOrderFiles);
        ArgumentNullException.ThrowIfNull(removeOrder);

        this.buildPlan = buildPlan;
        this.editOrderFiles = editOrderFiles;
        this.removeOrder = removeOrder;
        this.copyText = copyText;
        images = new PageImageCache(renderer);
        Refresh();
    }

    /// <summary>Raised when the window should close; <see cref="CreateRequested"/> says whether to print.</summary>
    public event EventHandler? CloseRequested;

    public ObservableCollection<PreviewOrderViewModel> Orders { get; } = [];

    public bool CreateRequested { get; private set; }

    [ObservableProperty]
    private string summary = string.Empty;

    [ObservableProperty]
    private string rejectedText = string.Empty;

    [ObservableProperty]
    private string copyStatus = string.Empty;

    public bool CanCopy => copyText is not null;

    public bool HasOrders => Orders.Count > 0;

    /// <summary>Rebuilds the plan of the ticked orders and the page list from it.</summary>
    public void Refresh()
    {
        var plan = buildPlan();
        Orders.Clear();
        var outputNumber = 0;
        var blankPages = 0;
        foreach (var planOrder in plan.Orders)
        {
            var pages = new List<PrintPreviewPageViewModel>();
            if (planOrder.IncludeSeparator)
            {
                pages.Add(PrintPreviewPageViewModel.Blank(++outputNumber));
                blankPages++;
            }

            foreach (var copy in planOrder.Copies)
            {
                for (var page = copy.FirstPage; page <= copy.LastPage; page++)
                {
                    pages.Add(new PrintPreviewPageViewModel(copy.Asset.FilePath, page, ++outputNumber));
                }
            }

            Orders.Add(new PreviewOrderViewModel(planOrder, pages));
        }

        Summary = plan.Orders.Count == 0
            ? "Chưa có đơn nào để in."
            : $"{plan.Orders.Count} đơn · {outputNumber} trang" + (blankPages > 0 ? $" (gồm {blankPages} trang trắng giữa đơn)" : string.Empty);
        RejectedText = plan.Rejected.Count == 0
            ? string.Empty
            : $"{plan.Rejected.Count} đơn đang chọn không ghép được và sẽ không in: " +
              string.Join("; ", plan.Rejected.Select(rejected => $"{rejected.OrderId} ({string.Join(", ", rejected.Reasons)})"));
        OnPropertyChanged(nameof(HasOrders));
    }

    /// <summary>Renders a page when its element appears (lazy, like the composer).</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RenderPageAsync(PrintPreviewPageViewModel? page)
    {
        if (page is null || page.IsBlank || page.Image is not null || page.HasError || !images.CanRender)
        {
            return;
        }

        try
        {
            page.Image = await images.GetAsync(page.FilePath!, page.PageNumber);
        }
        catch (Exception exception)
        {
            Log.Warning("TikTok print preview page failed with {ExceptionType}", exception.GetType().FullName);
            page.HasError = true;
        }
    }

    [RelayCommand]
    private async Task EditOrderAsync(PreviewOrderViewModel? order)
    {
        if (order is null)
        {
            return;
        }

        await editOrderFiles(order.OrderId);
        Refresh();
    }

    [RelayCommand]
    private void RemoveOrder(PreviewOrderViewModel? order)
    {
        if (order is null)
        {
            return;
        }

        removeOrder(order.OrderId);
        Refresh();
    }

    /// <summary>Copies one order id (button next to the id, or double-click on the order).</summary>
    [RelayCommand]
    private void CopyOrderId(PreviewOrderViewModel? order)
    {
        if (order is not null && copyText?.Invoke(order.OrderId) == true)
        {
            CopyStatus = $"Đã copy mã đơn {order.OrderId}.";
        }
    }

    /// <summary>Copies the ids of every order in the preview, one per line, in print order.</summary>
    [RelayCommand]
    private void CopyAllOrderIds()
    {
        if (Orders.Count > 0 && copyText?.Invoke(string.Join(Environment.NewLine, Orders.Select(order => order.OrderId))) == true)
        {
            CopyStatus = $"Đã copy {Orders.Count} mã đơn.";
        }
    }

    [RelayCommand]
    private void CreatePrintFile()
    {
        if (!HasOrders)
        {
            return;
        }

        CreateRequested = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>One order of the preview: its pages in print order.</summary>
public sealed class PreviewOrderViewModel
{
    public PreviewOrderViewModel(PrintPlanOrder planOrder, IReadOnlyList<PrintPreviewPageViewModel> pages)
    {
        ArgumentNullException.ThrowIfNull(planOrder);
        ArgumentNullException.ThrowIfNull(pages);
        OrderId = planOrder.Order.OrderId;
        var lines = planOrder.Order.Lines.OrderBy(line => line.LineNo).ToList();
        ItemsText = string.Join(" + ", lines.Select(line => $"{line.VariationName} x{line.Quantity}"));
        ProductsText = string.Join(Environment.NewLine, lines.Select(line => $"{line.ProductName} · {line.VariationName} x{line.Quantity}"));
        SheetText = $"{planOrder.SheetCount} tờ";
        HasManualChoice = planOrder.Copies.Any(copy => copy.IsManualChoice);
        Pages = pages;
    }

    public string OrderId { get; }

    public string ItemsText { get; }

    /// <summary>Product name, variation and quantity of every line, one line each.</summary>
    public string ProductsText { get; }

    public string SheetText { get; }

    public bool HasManualChoice { get; }

    public IReadOnlyList<PrintPreviewPageViewModel> Pages { get; }
}

/// <summary>One page of the print file; a blank separator page has no file.</summary>
public sealed partial class PrintPreviewPageViewModel : ObservableObject
{
    public PrintPreviewPageViewModel(string? filePath, int pageNumber, int outputNumber)
    {
        FilePath = filePath;
        PageNumber = pageNumber;
        OutputNumber = outputNumber;
    }

    public static PrintPreviewPageViewModel Blank(int outputNumber) => new(null, 0, outputNumber);

    public string? FilePath { get; }

    public int PageNumber { get; }

    /// <summary>Position of the page in the print file (1-based).</summary>
    public int OutputNumber { get; }

    public bool IsBlank => FilePath is null;

    public string Caption => IsBlank
        ? "Trang trắng giữa đơn"
        : $"{Path.GetFileNameWithoutExtension(FilePath)} · tr {PageNumber}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pixels), nameof(PixelWidth), nameof(PixelHeight), nameof(Stride))]
    private PdfPageRenderResult? image;

    [ObservableProperty]
    private bool hasError;

    public byte[]? Pixels => Image?.Bgra32Pixels;

    public int PixelWidth => Image?.PixelWidth ?? 0;

    public int PixelHeight => Image?.PixelHeight ?? 0;

    public int Stride => Image?.Stride ?? 0;
}
