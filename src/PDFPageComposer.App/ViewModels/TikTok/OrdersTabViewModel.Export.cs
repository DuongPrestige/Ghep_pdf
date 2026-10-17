using CommunityToolkit.Mvvm.Input;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>"Tạo file in" of the Orders tab.</summary>
public sealed partial class OrdersTabViewModel
{
    [RelayCommand]
    private Task CreatePrintFileAsync()
    {
        var selected = SelectedRows;
        if (selected.Count == 0)
        {
            workspace.ShowError("Chưa chọn đơn nào để tạo file in.");
            return Task.CompletedTask;
        }

        return workspace.RunAsync("Đang tạo file in...", cancellationToken => ExportAsync(selected, cancellationToken), "CreatePrintFile");
    }

    private async Task ExportAsync(IReadOnlyList<OrderRowViewModel> selected, CancellationToken cancellationToken)
    {
        var services = workspace.Services;
        var (orders, skipped) = await ReadCurrentOrdersAsync(selected, cancellationToken);
        var options = workspace.ClassificationOptions(allowReprint: selected.Any(row => row.IsReprint));
        var skus = workspace.SkuMap();
        var assets = workspace.Assets;
        var separatorMode = SelectedSeparatorMode.Value;
        var plan = await Task.Run(
            () => services.PlanBuilder.Build(orders, skus, assets, separatorMode, options),
            cancellationToken);

        PlanSummary = PrintFileRunner.DescribePlan(plan);
        if (plan.Orders.Count == 0)
        {
            workspace.ShowError("Không có đơn nào đủ điều kiện để tạo file in. " + DescribeRejected(plan, skipped));
            workspace.StatusText = "Chưa tạo file in.";
            return;
        }

        var outcome = await printRunner.ExportAsync(plan, cancellationToken);
        if (outcome is null)
        {
            workspace.StatusText = "Đã hủy chọn nơi lưu file.";
            return;
        }

        // The batch is committed: nothing below may report the operation as failed (a retry would print twice).
        LastOutputPath = outcome.Result.OutputPath;
        PlanSummary = $"Đã tạo {outcome.Result.OutputPath}: {PrintFileRunner.DescribePlan(plan)}";
        if (plan.Rejected.Count > 0 || skipped.Count > 0)
        {
            PlanSummary += Environment.NewLine + DescribeRejected(plan, skipped);
        }

        ClearExportedRows(selected);
        var notices = outcome.FoxitWarning is null ? Array.Empty<string>() : [outcome.FoxitWarning];
        await workspace.ShowCommittedResultAsync($"Đã tạo file in lô {outcome.Result.BatchId}", notices);
        workspace.StatusText = $"Đã tạo file in lô {outcome.Result.BatchId} ({outcome.Result.OrderCount} đơn, {outcome.Result.PageCount} trang).";
    }

    /// <summary>
    /// Re-reads the selected orders, so an order printed or hidden since the list was loaded (another batch,
    /// another window) is not printed again from a stale row.
    /// </summary>
    private async Task<(List<TikTokOrder> Orders, List<string> Skipped)> ReadCurrentOrdersAsync(
        IReadOnlyList<OrderRowViewModel> selected,
        CancellationToken cancellationToken)
    {
        var repository = workspace.Services.Repository;
        var ids = selected.Select(row => row.OrderId).ToList();
        var current = (await Task.Run(() => repository.GetOrdersByIdsAsync(ids, cancellationToken), cancellationToken))
            .ToDictionary(order => order.OrderId, StringComparer.Ordinal);

        var orders = new List<TikTokOrder>();
        var skipped = new List<string>();
        foreach (var row in selected)
        {
            if (!current.TryGetValue(row.OrderId, out var order) || !order.IsActive)
            {
                skipped.Add($"{row.OrderId}: không còn chờ vận chuyển");
            }
            else if (!row.IsReprint &&
                     (!string.Equals(order.PrintedBatchId, row.Order.PrintedBatchId, StringComparison.Ordinal) ||
                      order.PrintedSource != row.Order.PrintedSource))
            {
                skipped.Add($"{row.OrderId}: đã được in ở lô khác, bấm Tải lại");
            }
            else
            {
                orders.Add(order);
            }
        }

        return (orders, skipped);
    }

    /// <summary>Exported rows must not stay ticked or flagged "In lại" into the next batch.</summary>
    private void ClearExportedRows(IReadOnlyList<OrderRowViewModel> exported)
    {
        suppressPreviewRefresh = true;
        try
        {
            foreach (var row in exported)
            {
                row.IsReprint = false;
                row.IsSelected = false;
            }
        }
        finally
        {
            suppressPreviewRefresh = false;
        }
    }

    private static string DescribeRejected(PrintPlan plan, IReadOnlyList<string> skipped) =>
        string.Join(
            "; ",
            skipped.Concat(plan.Rejected.Select(rejected => $"{rejected.OrderId}: {TikTokLabels.Of(rejected.Category)}")));
}
