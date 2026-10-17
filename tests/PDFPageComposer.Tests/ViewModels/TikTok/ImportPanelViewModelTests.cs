using System.IO;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Models;
using PDFPageComposer.App.ViewModels.TikTok;

namespace PDFPageComposer.Tests.ViewModels.TikTok;

public sealed class ImportPanelViewModelTests : IDisposable
{
    private static readonly string SamplePath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "tiktok", "orders-sample.csv");

    private readonly TikTokUiHarness harness = new();

    public void Dispose() => harness.Dispose();

    [Fact]
    public void Report_text_uses_the_vietnamese_summary()
    {
        var report = new ImportReport(7, 3, 2, 1, 4, 5, 6, []);

        Assert.Equal(
            "3 đơn mới, 2 cập nhật, 1 đã in trước đó, 4 dòng bỏ qua, 5 đơn ẩn, 6 SKU mới",
            TikTokLabels.FormatImportReport(report));
    }

    [Fact]
    public async Task Importing_the_sample_twice_reports_new_then_updated_orders()
    {
        var shopId = await harness.AddShopAsync("Shop Mẫu");
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var import = viewModel.Import;
        Assert.Equal(shopId, import.SelectedShop?.ShopId);
        Assert.True(import.IsFullMode);

        harness.Dialogs.CsvFiles = [SamplePath];
        import.PickFilesCommand.Execute(null);
        Assert.True(import.IsOpen);
        Assert.Equal([SamplePath], import.PendingFiles);

        await import.ImportCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace.ErrorText);
        var orderCount = viewModel.Orders.AllRows.Count;
        Assert.InRange(orderCount, 1, 33);
        Assert.StartsWith($"Shop Mẫu (File đầy đủ): {orderCount} đơn mới, 0 cập nhật", import.ReportText);
        Assert.Empty(import.PendingFiles);
        Assert.Equal(shopId, harness.Settings.Current.TikTok.LastImportShopId);
        Assert.NotEmpty(viewModel.Skus.AllRows);
        Assert.All(viewModel.Skus.AllRows, row => Assert.False(row.IsConfirmed));
        Assert.Single(viewModel.History.ImportBatches);

        import.QueueFiles([SamplePath]);
        await import.ImportCommand.ExecuteAsync(null);

        Assert.StartsWith($"Shop Mẫu (File đầy đủ): 0 đơn mới, {orderCount} cập nhật", import.ReportText);
        Assert.Contains("0 SKU mới", import.ReportText);
        Assert.Equal(orderCount, viewModel.Orders.AllRows.Count);
        Assert.Equal(2, viewModel.History.ImportBatches.Count);
    }

    [Fact]
    public async Task Seller_note_da_in_from_the_sample_shows_the_order_as_printed()
    {
        await harness.AddShopAsync();
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.Import.QueueFiles([SamplePath]);

        await viewModel.Import.ImportCommand.ExecuteAsync(null);

        var row = viewModel.Orders.AllRows.Single(candidate => candidate.OrderId == "586605624104421255");
        Assert.Equal("ĐÃ IN IVAN mini", row.SellerNote);
        Assert.Equal(OrderCategory.Printed, row.Category);
    }

    [Fact]
    public async Task Bad_csv_header_shows_a_friendly_error_and_imports_nothing()
    {
        await harness.AddShopAsync();
        var bad = harness.WriteTempFile("bad.csv", "a,b\r\n1,2\r\n");
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.Import.QueueFiles([bad]);

        await viewModel.Import.ImportCommand.ExecuteAsync(null);

        Assert.StartsWith("Không đọc được file bad.csv: File không đúng định dạng", viewModel.Workspace.ErrorText);
        Assert.Contains("Thiếu cột bắt buộc", viewModel.Workspace.ErrorText);
        Assert.Null(viewModel.Import.ReportText);
        Assert.Empty(await harness.Repository.GetImportBatchesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Import_requires_a_shop()
    {
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.Import.QueueFiles([SamplePath]);

        await viewModel.Import.ImportCommand.ExecuteAsync(null);

        Assert.Equal("Hãy chọn shop trước khi import.", viewModel.Workspace.ErrorText);
    }

    [Fact]
    public async Task Inline_add_shop_selects_the_new_shop()
    {
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.False(viewModel.Import.HasShops);
        viewModel.Import.NewShopName = "  Shop Mới ";

        await viewModel.Import.AddShopCommand.ExecuteAsync(null);

        Assert.Equal("Shop Mới", viewModel.Import.SelectedShop?.Name);
        Assert.True(viewModel.Import.HasShops);
        Assert.Null(viewModel.Import.NewShopName);
    }

    [Fact]
    public async Task Duplicate_shop_name_shows_the_repository_message()
    {
        await harness.AddShopAsync("Shop A");
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.Import.NewShopName = "shop a";

        await viewModel.Import.AddShopCommand.ExecuteAsync(null);

        Assert.Contains("đã tồn tại", viewModel.Workspace.ErrorText);
    }

    [Fact]
    public void Dropped_files_keep_only_csv()
    {
        var viewModel = harness.CreateViewModel();

        viewModel.QueueDroppedFiles([@"D:\a.pdf", @"D:\orders.CSV", @"D:\orders.csv"]);

        Assert.True(viewModel.Import.IsOpen);
        Assert.Equal([@"D:\orders.CSV"], viewModel.Import.PendingFiles);
    }

    [Fact]
    public async Task Partial_mode_is_passed_to_the_import()
    {
        await harness.AddShopAsync();
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.Import.IsPartialMode = true;
        viewModel.Import.QueueFiles([SamplePath]);

        await viewModel.Import.ImportCommand.ExecuteAsync(null);

        var batch = Assert.Single(await harness.Repository.GetImportBatchesAsync(CancellationToken.None));
        Assert.Equal(ImportMode.Partial, batch.Mode);
        Assert.Equal("orders-sample.csv", batch.FileName);
        Assert.Contains("(Bổ sung)", viewModel.Import.ReportText);
    }

    private const string SmallHeader =
        "Order ID,Order Status,SKU ID,Product Name,Variation,Quantity,Created Time,Recipient,Phone #,Detail Address,Seller Note";

    [Fact]
    public async Task Settings_save_failure_after_a_committed_import_still_shows_the_report()
    {
        await harness.AddShopAsync();
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        harness.Settings.SaveFailure = new IOException("locked");
        viewModel.Import.QueueFiles([SamplePath]);

        await viewModel.Import.ImportCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace.ErrorText);
        Assert.StartsWith("Đã import orders-sample.csv", viewModel.Workspace.StatusText);
        Assert.NotNull(viewModel.Import.ReportText);
        Assert.Empty(viewModel.Import.PendingFiles);
        Assert.NotEmpty(viewModel.Orders.AllRows);
    }

    [Fact]
    public void Files_dropped_while_busy_are_refused_and_the_running_queue_is_kept()
    {
        var viewModel = harness.CreateViewModel();
        viewModel.Import.QueueFiles([@"D:\a.csv"]);
        viewModel.Workspace.IsBusy = true;

        viewModel.QueueDroppedFiles([@"D:\b.csv"]);

        Assert.Equal([@"D:\a.csv"], viewModel.Import.PendingFiles);
        Assert.Contains("hãy đợi xong", viewModel.Workspace.ErrorText);
    }

    [Fact]
    public async Task Full_import_with_a_bad_row_neither_halves_nor_hides_the_order()
    {
        var shopId = await harness.AddShopAsync();
        var first = harness.WriteTempFile(
            "first.csv",
            string.Join(
                "\r\n",
                SmallHeader,
                "71,Chờ vận chuyển,S1,P,V,1,16/10/2026 10:00:00,A,B,C,",
                "71,Chờ vận chuyển,S2,P,V,1,16/10/2026 10:00:00,A,B,C,",
                "72,Chờ vận chuyển,S1,P,V,1,16/10/2026 10:05:00,A,B,C,",
                string.Empty));
        var second = harness.WriteTempFile(
            "second.csv",
            string.Join(
                "\r\n",
                SmallHeader,
                "71,Chờ vận chuyển,S1,P,V,1,16/10/2026 10:00:00,A,B,C,",
                "71,Chờ vận chuyển,S2,P,V,1.0,16/10/2026 10:00:00,A,B,C,",
                "72,Chờ vận chuyển,S1,P,V,1,16/10/2026 10:05:00,A,B,C,",
                string.Empty));
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.Import.QueueFiles([first]);
        await viewModel.Import.ImportCommand.ExecuteAsync(null);

        viewModel.Import.QueueFiles([second]);
        await viewModel.Import.ImportCommand.ExecuteAsync(null);

        var stored = (await harness.Repository.GetOrdersByIdsAsync(["71"], CancellationToken.None)).Single();
        Assert.True(stored.IsActive);
        Assert.Equal(["S1", "S2"], stored.Lines.Select(line => line.SkuId));
        Assert.Contains(viewModel.Import.Warnings, warning => warning.StartsWith("Đơn 71: có dòng dữ liệu lỗi", StringComparison.Ordinal));
        Assert.Contains(viewModel.Import.Warnings, warning => warning.StartsWith("Đơn 71 có dòng không hợp lệ", StringComparison.Ordinal));
        Assert.Equal(shopId, stored.ShopId);
    }

    [Fact]
    public async Task Deleting_imports_asks_first_then_removes_the_orders()
    {
        await harness.AddShopAsync();
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.Import.QueueFiles([SamplePath]);
        await viewModel.Import.ImportCommand.ExecuteAsync(null);
        var orderCount = viewModel.Orders.AllRows.Count;
        Assert.True(orderCount > 0);
        var history = viewModel.History;
        history.SelectedImportBatch = Assert.Single(history.ImportBatches);

        harness.Dialogs.ConfirmResult = false;
        await history.DeleteImportCommand.ExecuteAsync(null);

        Assert.Contains("Sẽ xóa", harness.Dialogs.LastConfirmMessage);
        Assert.Equal(orderCount, viewModel.Orders.AllRows.Count);
        Assert.Equal("Đã hủy xóa.", viewModel.Workspace.StatusText);

        harness.Dialogs.ConfirmResult = true;
        await history.DeleteImportCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace.ErrorText);
        Assert.Empty(viewModel.Orders.AllRows);
        Assert.Empty(history.ImportBatches);

        viewModel.Import.QueueFiles([SamplePath]);
        await viewModel.Import.ImportCommand.ExecuteAsync(null);
        viewModel.Import.QueueFiles([SamplePath]);
        await viewModel.Import.ImportCommand.ExecuteAsync(null);
        Assert.Equal(orderCount, viewModel.Orders.AllRows.Count);

        await history.DeleteAllOrdersCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace.ErrorText);
        Assert.Empty(viewModel.Orders.AllRows);
        Assert.Empty(history.ImportBatches);
        Assert.NotEmpty(viewModel.Skus.AllRows);
    }

    [Fact]
    public async Task Orders_follow_the_csv_row_order_and_order_ids_can_be_copied()
    {
        await harness.AddShopAsync();
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.Import.QueueFiles([SamplePath]);
        await viewModel.Import.ImportCommand.ExecuteAsync(null);

        var csvOrder = File.ReadLines(SamplePath)
            .Skip(1)
            .Select(line => line.Split(',')[0].Trim('"', '\t', ' '))
            .Distinct()
            .ToList();
        var shown = viewModel.Orders.AllRows.Select(row => row.OrderId).ToList();
        Assert.Equal(csvOrder.Where(shown.Contains), shown);

        viewModel.Orders.ClearSelectionCommand.Execute(null);
        viewModel.Orders.CopyOrderIdsCommand.Execute(null);
        Assert.Equal(string.Join(Environment.NewLine, viewModel.Orders.Rows.Select(row => row.OrderId)), harness.Clipboard.Text);
        Assert.Equal($"Đã copy {viewModel.Orders.Rows.Count} mã đơn đang hiển thị.", viewModel.Workspace.StatusText);

        var first = viewModel.Orders.Rows[0];
        viewModel.Orders.CopyOrderIdCommand.Execute(first);
        Assert.Equal(first.OrderId, harness.Clipboard.Text);
        Assert.Equal($"Đã copy mã đơn {first.OrderId}.", viewModel.Workspace.StatusText);
    }
}
