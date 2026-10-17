using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFPageComposer.App.TikTok.Export;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>"Xuất danh sách CSV" of the Orders tab (PRD_TIKTOK TT-12).</summary>
public sealed partial class OrdersTabViewModel
{
    private OrderListExportRunner? orderListRunner;

    public IReadOnlyList<TikTokOption<OrderListExportScope>> OrderListScopes => OrderListExportRunner.ScopeOptions;

    [ObservableProperty]
    private TikTokOption<OrderListExportScope> selectedOrderListScope = OrderListExportRunner.ScopeOptions[0];

    private OrderListExportRunner OrderListRunner => orderListRunner ??= new OrderListExportRunner(workspace);

    [RelayCommand]
    private Task ExportOrderListAsync() => OrderListRunner.ExportAsync(SelectedOrderListScope.Value);
}
