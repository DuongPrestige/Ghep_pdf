using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFPageComposer.App.TikTok.Export;
using PDFPageComposer.App.TikTok.Models;
using Serilog;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>Tab "Lịch sử": import batches and print batches (open, details, unmark, regenerate, copy order ids).</summary>
public sealed partial class HistoryTabViewModel : ObservableObject
{
    private readonly TikTokWorkspace workspace;
    private readonly PrintFileRunner printRunner;
    private readonly OrderListExportRunner orderListRunner;
    private int itemsRequestVersion;

    public HistoryTabViewModel(TikTokWorkspace workspace, PrintFileRunner printRunner)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(printRunner);
        this.workspace = workspace;
        this.printRunner = printRunner;
        orderListRunner = new OrderListExportRunner(workspace);
    }

    public TikTokWorkspace Workspace => workspace;

    public ObservableCollection<ImportBatchRow> ImportBatches { get; } = [];

    public ObservableCollection<PrintBatchRow> PrintBatches { get; } = [];

    public ObservableCollection<PrintBatchItemRow> SelectedItems { get; } = [];

    public ObservableCollection<BatchOrderRow> SelectedOrders { get; } = [];

    [ObservableProperty]
    private PrintBatchRow? selectedPrintBatch;

    [ObservableProperty]
    private ImportBatchRow? selectedImportBatch;

    partial void OnSelectedPrintBatchChanged(PrintBatchRow? value)
    {
        SelectedItems.Clear();
        SelectedOrders.Clear();
        if (value is not null)
        {
            _ = LoadItemsSafelyAsync(value);
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        var repository = workspace.Services.Repository;
        var (imports, prints) = await Task.Run(
            async () => (
                await repository.GetImportBatchesAsync(cancellationToken),
                await repository.GetPrintBatchesAsync(cancellationToken)),
            cancellationToken);

        var selectedId = SelectedPrintBatch?.BatchId;
        ImportBatches.Clear();
        foreach (var batch in imports)
        {
            ImportBatches.Add(new ImportBatchRow(batch, workspace.ShopName(batch.ShopId)));
        }

        PrintBatches.Clear();
        foreach (var batch in prints)
        {
            PrintBatches.Add(new PrintBatchRow(batch));
        }

        SelectedPrintBatch = PrintBatches.FirstOrDefault(row => row.BatchId == selectedId);
    }

    /// <summary>Loads the copies and orders of one print batch into the detail lists.</summary>
    public async Task LoadItemsAsync(PrintBatchRow row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        var version = ++itemsRequestVersion;
        var repository = workspace.Services.Repository;
        var items = await Task.Run(() => repository.GetPrintBatchItemsAsync(row.BatchId, cancellationToken), cancellationToken);
        if (version != itemsRequestVersion)
        {
            return;
        }

        SelectedItems.Clear();
        SelectedOrders.Clear();
        foreach (var item in items.OrderBy(item => item.Seq))
        {
            SelectedItems.Add(new PrintBatchItemRow(item));
        }

        foreach (var group in items.OrderBy(item => item.Seq).GroupBy(item => item.OrderId, StringComparer.Ordinal))
        {
            SelectedOrders.Add(new BatchOrderRow(group.Key, group.Count()));
        }
    }

    [RelayCommand]
    private Task OpenBatchFileAsync(PrintBatchRow? row)
    {
        var target = row ?? SelectedPrintBatch;
        if (target is null)
        {
            return Task.CompletedTask;
        }

        return workspace.RunAsync(
            "Đang mở file in...",
            async cancellationToken =>
            {
                await workspace.Services.FoxitLauncher.OpenPdfAsync(target.OutputPath, cancellationToken);
                workspace.StatusText = $"Đã mở {target.FileName}.";
            },
            "OpenPrintFile");
    }

    [RelayCommand]
    private void CopyOrderIds()
    {
        if (SelectedOrders.Count == 0)
        {
            workspace.ShowError("Chọn một lô in có đơn để copy mã đơn.");
            return;
        }

        var text = string.Join(Environment.NewLine, SelectedOrders.Select(order => order.OrderId));
        try
        {
            workspace.Services.Clipboard.SetText(text);
        }
        catch (InvalidOperationException exception)
        {
            // Another application holds the clipboard; the order ids are not logged.
            workspace.ShowError(exception.Message);
            return;
        }

        workspace.StatusText = $"Đã copy {SelectedOrders.Count} mã đơn.";
    }

    /// <summary>"Xuất DS đơn của lô": the orders of the selected print batch as CSV (PRD_TIKTOK TT-12).</summary>
    [RelayCommand]
    private Task ExportBatchOrderListAsync()
    {
        var batch = SelectedPrintBatch;
        if (batch is null)
        {
            workspace.ShowError("Chọn một lô in để xuất danh sách đơn.");
            return Task.CompletedTask;
        }

        return orderListRunner.ExportAsync(OrderListExportScope.PrintBatch, batch.BatchId);
    }

    /// <summary>"Xóa lần import": removes the selected import and the orders seen only in it.</summary>
    [RelayCommand]
    private Task DeleteImportAsync()
    {
        var batch = SelectedImportBatch;
        if (batch is null)
        {
            workspace.ShowError("Chọn một lần import để xóa.");
            return Task.CompletedTask;
        }

        return workspace.RunAsync(
            "Đang xóa lần import...",
            async cancellationToken =>
            {
                var repository = workspace.Services.Repository;
                var orders = await Task.Run(() => repository.GetOrdersAsync(includeInactive: true, cancellationToken), cancellationToken);
                var onlyHere = orders
                    .Where(order => order.FirstImportId == batch.ImportId && order.LastSeenImportId == batch.ImportId)
                    .ToList();
                var message =
                    $"Xóa lần import #{batch.ImportId} ({batch.FileName})?{Environment.NewLine}{Environment.NewLine}" +
                    $"Sẽ xóa {onlyHere.Count} đơn chỉ có trong lần import này. Đơn có trong lần import khác được giữ lại." +
                    PrintedWarning(onlyHere.Count(IsPrinted));
                if (!workspace.Services.FileDialogs.Confirm(message, "Xóa lần import"))
                {
                    workspace.StatusText = "Đã hủy xóa.";
                    return;
                }

                var deleted = await Task.Run(() => repository.DeleteImportAsync(batch.ImportId, cancellationToken), cancellationToken);
                await workspace.ReloadAfterCommitAsync($"Đã xóa lần import #{batch.ImportId}");
                workspace.StatusText = $"Đã xóa lần import #{batch.ImportId} và {deleted} đơn.";
            },
            "DeleteImport");
    }

    /// <summary>"Xóa toàn bộ đơn": clears every order and import; SKU settings, library and print history stay.</summary>
    [RelayCommand]
    private Task DeleteAllOrdersAsync() =>
        workspace.RunAsync(
            "Đang xóa toàn bộ đơn...",
            async cancellationToken =>
            {
                var repository = workspace.Services.Repository;
                var orders = await Task.Run(() => repository.GetOrdersAsync(includeInactive: true, cancellationToken), cancellationToken);
                var message =
                    $"Xóa toàn bộ {orders.Count} đơn và mọi lần import?{Environment.NewLine}{Environment.NewLine}" +
                    "Cấu hình SKU, kho PDF và lịch sử lô in được giữ lại." +
                    PrintedWarning(orders.Count(IsPrinted));
                if (!workspace.Services.FileDialogs.Confirm(message, "Xóa toàn bộ đơn"))
                {
                    workspace.StatusText = "Đã hủy xóa.";
                    return;
                }

                var deleted = await Task.Run(() => repository.DeleteAllOrdersAsync(cancellationToken), cancellationToken);
                await workspace.ReloadAfterCommitAsync("Đã xóa toàn bộ đơn");
                workspace.StatusText = $"Đã xóa {deleted} đơn và toàn bộ lịch sử import.";
            },
            "DeleteAllOrders");

    private static bool IsPrinted(TikTokOrder order) =>
        order.PrintedBatchId is not null || order.PrintedSource is not null;

    private static string PrintedWarning(int printedCount) =>
        printedCount == 0
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}Có {printedCount} đơn đã tạo file in: nếu import lại, các đơn này sẽ hiện là CHƯA IN.";

    [RelayCommand]
    private Task UnmarkOrderAsync(BatchOrderRow? row)
    {
        if (row is null)
        {
            return Task.CompletedTask;
        }

        return workspace.RunAsync(
            "Đang bỏ đánh dấu đơn...",
            async cancellationToken =>
            {
                var repository = workspace.Services.Repository;
                await Task.Run(() => repository.UnmarkPrintedAsync(row.OrderId, cancellationToken), cancellationToken);
                await workspace.ReloadAllAsync(cancellationToken);
                workspace.StatusText = $"Đã bỏ đánh dấu đã in cho đơn {row.OrderId}.";
            },
            "UnmarkPrinted");
    }

    [RelayCommand]
    private Task RegenerateAsync()
    {
        var batch = SelectedPrintBatch;
        if (batch is null)
        {
            workspace.ShowError("Chọn một lô in để tạo lại.");
            return Task.CompletedTask;
        }

        return workspace.RunAsync(
            "Đang tạo lại file in...",
            async cancellationToken =>
            {
                var repository = workspace.Services.Repository;
                var (items, orders) = await Task.Run(
                    async () =>
                    {
                        var batchItems = await repository.GetPrintBatchItemsAsync(batch.BatchId, cancellationToken);
                        var orderIds = batchItems.Select(item => item.OrderId).Distinct(StringComparer.Ordinal).ToList();
                        var batchOrders = await repository.GetOrdersByIdsAsync(orderIds, cancellationToken);
                        return (batchItems, batchOrders);
                    },
                    cancellationToken);

                // Orders that changed, were un-marked or left the queue since this batch are not re-marked printed
                // silently with the old content; the user prints them from the Orders tab instead.
                if (items.Count == 0)
                {
                    throw new InvalidOperationException($"Lô in {batch.BatchId} không có dữ liệu chi tiết để tạo lại.");
                }

                var (replayable, excluded) = PrintPlanReplay.SplitStaleOrders(items, orders);
                if (replayable.Count == 0)
                {
                    workspace.ShowError(
                        $"Không tạo lại được lô {batch.BatchId}: mọi đơn của lô đã thay đổi, đã bỏ đánh dấu hoặc không còn chờ vận chuyển ({PrintPlanReplay.DescribeExcluded(excluded)}).");
                    workspace.StatusText = "Chưa tạo lại file in.";
                    return;
                }

                var replayIds = replayable.Select(order => order.OrderId).ToHashSet(StringComparer.Ordinal);
                var replayItems = items.Where(item => replayIds.Contains(item.OrderId)).ToList();
                var plan = PrintPlanReplay.Build(batch.Record, replayItems, replayable, workspace.Assets);
                var outcome = await printRunner.ExportAsync(plan, cancellationToken);
                if (outcome is null)
                {
                    workspace.StatusText = "Đã hủy chọn nơi lưu file.";
                    return;
                }

                // The new batch is committed: nothing below may report the operation as failed.
                var committedText = $"Đã tạo lại lô {batch.BatchId} thành lô mới {outcome.Result.BatchId}";
                var notices = new List<string>();
                if (outcome.FoxitWarning is not null)
                {
                    notices.Add(outcome.FoxitWarning);
                }

                if (excluded.Count > 0)
                {
                    notices.Add(
                        $"Bỏ qua {excluded.Count} đơn đã thay đổi, đã bỏ đánh dấu hoặc không còn chờ vận chuyển: {PrintPlanReplay.DescribeExcluded(excluded)}. Hãy in các đơn này từ tab Đơn hàng.");
                }

                await workspace.ShowCommittedResultAsync(committedText, notices);
                workspace.StatusText = $"{committedText} ({outcome.Result.PageCount} trang).";
            },
            "RegeneratePrintBatch");
    }

    private async Task LoadItemsSafelyAsync(PrintBatchRow row)
    {
        try
        {
            await LoadItemsAsync(row, CancellationToken.None);
        }
        catch (Exception exception)
        {
            Log.Error(
                "Loading TikTok print batch items failed with {ExceptionType}{NewLine}{StackTrace}",
                exception.GetType().FullName,
                Environment.NewLine,
                exception.StackTrace);
            workspace.ShowError($"Không tải được chi tiết lô in: {exception.Message}");
        }
    }
}
