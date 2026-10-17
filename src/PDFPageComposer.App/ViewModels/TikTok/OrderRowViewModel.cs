using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>One order in the Orders tab grid.</summary>
public sealed partial class OrderRowViewModel : ObservableObject
{

    public OrderRowViewModel(
        TikTokOrder order,
        OrderClassification classification,
        string shopName,
        PrintPlanOrder? previewPlan)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentNullException.ThrowIfNull(shopName);

        Order = order;
        Classification = classification;
        ShopName = shopName;
        var lines = order.Lines.OrderBy(line => line.LineNo).ToList();
        ProductsText = string.Join(Environment.NewLine, lines.Select(line => TextNormalizer.TrimCell(line.ProductName) ?? line.SkuId));
        VariationsText = string.Join(
            Environment.NewLine,
            lines.Select(line => $"{TextNormalizer.TrimCell(line.VariationName) ?? "-"} x{line.Quantity}"));
        ItemsFullText = string.Join(Environment.NewLine, lines.Select(line => $"{line.ProductName} | {line.VariationName} x{line.Quantity}"));
        ReasonsText = string.Join(Environment.NewLine, classification.Reasons);
        isSelected = classification.Category == OrderCategory.Ready;
        ApplyPreview(previewPlan);
    }

    /// <summary>
    /// Shows the files this order gets in the plan of the current selection (TT-04 resets the rotation per
    /// batch, so the preview must be planned over exactly the orders that will be exported).
    /// </summary>
    public void ApplyPreview(PrintPlanOrder? previewPlan)
    {
        SheetCount = previewPlan?.SheetCount;
        PlannedFilesText = previewPlan is not null
            ? string.Join(Environment.NewLine, previewPlan.Copies.Select(DescribeCopy))
            : CanSelect && !IsSelected ? NotSelectedText : string.Empty;
    }

    public const string NotSelectedText = "Chưa chọn";

    public TikTokOrder Order { get; }

    public OrderClassification Classification { get; }

    public OrderCategory Category => Classification.Category;

    public string CategoryText => IsReprint ? "In lại" : TikTokLabels.Of(Category);

    public string OrderId => Order.OrderId;

    public long ShopId => Order.ShopId;

    public string ShopName { get; }

    public string CreatedAtText => TikTokLabels.FormatDateTime(Order.CreatedAt);

    /// <summary>Product name of each line, one per line (the grid trims long names).</summary>
    public string ProductsText { get; }

    /// <summary>Variation and quantity of each line, aligned with <see cref="ProductsText"/>.</summary>
    public string VariationsText { get; }

    /// <summary>Product, variation and quantity of each line, shown as the tooltip of both item cells.</summary>
    public string ItemsFullText { get; }

    /// <summary>Second line under the order id: order time and shop.</summary>
    public string MetaText => $"{Order.CreatedAt:dd/MM HH:mm} · {ShopName}";

    /// <summary>Buyer Message and Seller Note, one per line; empty when the order has none.</summary>
    public string NoteText => string.Join(
        Environment.NewLine,
        new[] { ("Khách: ", TextNormalizer.TrimCell(BuyerMessage)), ("Shop: ", TextNormalizer.TrimCell(SellerNote)) }
            .Where(note => note.Item2 is not null)
            .Select(note => note.Item1 + note.Item2));

    [ObservableProperty]
    private int? sheetCount;

    [ObservableProperty]
    private string plannedFilesText = string.Empty;

    public string ReasonsText { get; }

    public string? BuyerMessage => Order.BuyerMessage;

    public string? SellerNote => Order.SellerNote;

    public string WarningText => Order.ChangedAfterPrint ? "Đơn đã thay đổi sau khi in" : string.Empty;

    public bool IsPrinted => Category == OrderCategory.Printed;

    public bool CanReviewNote => Category == OrderCategory.HasNote;

    /// <summary>"Xem / chọn": orders whose card lines are all configured (ready, missing a file, or printed).</summary>
    public bool CanChooseFiles => Category is OrderCategory.Ready or OrderCategory.MissingFile or OrderCategory.Printed;

    /// <summary>Ready orders can always be picked; printed orders only after "In lại".</summary>
    public bool CanSelect => Category == OrderCategory.Ready || IsReprint;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelect))]
    [NotifyPropertyChangedFor(nameof(CategoryText))]
    private bool isReprint;

    [ObservableProperty]
    private bool isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value && !CanSelect)
        {
            IsSelected = false;
        }
    }

    partial void OnIsReprintChanged(bool value)
    {
        IsSelected = value || (Category == OrderCategory.Ready && IsSelected);
    }

    /// <summary>"DINH BAC - 5 PAGE - 24.09 · 3/5 trang": copy number only for quantity > 1, page part only when partial.</summary>
    private static string DescribeCopy(PrintPlanCopy copy)
    {
        var text = Path.GetFileNameWithoutExtension(copy.Asset.FilePath);
        if (copy.Quantity > 1)
        {
            text += $" · bộ {copy.CopyIndex}/{copy.Quantity}";
        }

        if (copy.UsesPartOfFile)
        {
            text += copy.FirstPage == 1
                ? $" · {copy.PageCount}/{copy.Asset.PageCount} trang"
                : copy.PageCount == 1 ? $" · tr {copy.FirstPage}" : $" · tr {copy.FirstPage}-{copy.LastPage}";
        }

        if (copy.IsManualChoice)
        {
            text += " · tự chọn";
        }

        return copy.DuplicateImage ? $"{text} · TRÙNG HÌNH" : text;
    }
}

/// <summary>One category chip with its order count.</summary>
public sealed partial class CategoryFilterViewModel : ObservableObject
{
    public CategoryFilterViewModel(OrderCategory category, bool isChecked)
    {
        Category = category;
        this.isChecked = isChecked;
    }

    public OrderCategory Category { get; }

    public string Label => $"{TikTokLabels.Of(Category)} ({Count})";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Label))]
    private int count;

    [ObservableProperty]
    private bool isChecked;
}
