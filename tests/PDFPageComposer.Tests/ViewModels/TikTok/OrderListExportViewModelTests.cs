using System.IO;
using PDFPageComposer.App.TikTok.Export;
using PDFPageComposer.App.TikTok.Import;
using PDFPageComposer.App.ViewModels.TikTok;

namespace PDFPageComposer.Tests.ViewModels.TikTok;

/// <summary>"Xuất danh sách CSV" from the Orders and History tabs (PRD_TIKTOK TT-12).</summary>
public sealed class OrderListExportViewModelTests : IDisposable
{
    private readonly TikTokUiHarness harness = new();

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Cancelled_save_dialog_writes_nothing()
    {
        await harness.SeedExampleAsync();
        var viewModel = await LoadAsync();
        viewModel.Orders.SelectedOrderListScope = Scope(OrderListExportScope.AllImported);
        harness.Dialogs.SaveCsv = null;

        await viewModel.Orders.ExportOrderListCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.Dialogs.SaveCsvCalls);
        Assert.Equal("DS_DON_TAT_CA_20261016_0930.csv", harness.Dialogs.LastSuggestedCsvName);
        Assert.Equal(OrderListExportRunner.CancelledText, viewModel.Workspace.StatusText);
        Assert.Null(viewModel.Workspace.ErrorText);
        Assert.Empty(Directory.GetFiles(harness.TempDirectory, "*.csv"));
    }

    [Fact]
    public async Task Empty_result_shows_a_status_and_does_not_open_the_dialog()
    {
        await harness.AddShopAsync();
        var viewModel = await LoadAsync();
        harness.Dialogs.SaveCsv = CsvPath();

        await viewModel.Orders.ExportOrderListCommand.ExecuteAsync(null);

        Assert.Equal(OrderListExportScope.PrintedOnly, viewModel.Orders.SelectedOrderListScope.Value);
        Assert.Equal(OrderListExportRunner.EmptyText, viewModel.Workspace.StatusText);
        Assert.Equal(0, harness.Dialogs.SaveCsvCalls);
        Assert.False(File.Exists(CsvPath()));
    }

    [Fact]
    public async Task Printed_only_export_writes_printed_orders_with_their_files()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\lo1.pdf";
        var viewModel = await LoadAsync();
        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);
        harness.Dialogs.SaveCsv = CsvPath();

        await viewModel.Orders.ExportOrderListCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace.ErrorText);
        Assert.Equal("DS_DON_DA_IN_20261016_0930.csv", harness.Dialogs.LastSuggestedCsvName);
        Assert.Equal($"Đã xuất 4 dòng (3 đơn) ra {CsvPath()}", viewModel.Workspace.StatusText);
        var rows = ReadCsv();
        Assert.Equal(["O1\t", "O2\t", "O2\t", "O6\t"], rows.Select(row => row[0]));
        Assert.Equal(["Có", "Lô in", "B1"], rows[0].Skip(3).Take(3));
        Assert.Equal("Đã tạo file in", rows[0][7]);
        Assert.Equal("CODY - 4 PAGE - 14.10.pdf", rows[0][13]);
        Assert.Equal(string.Empty, rows[2][13]);
        Assert.Equal("Seller Note", rows[3][4]);
    }

    [Fact]
    public async Task All_imported_export_marks_every_waiting_order()
    {
        await harness.SeedExampleAsync();
        var viewModel = await LoadAsync();
        viewModel.Orders.SelectedOrderListScope = Scope(OrderListExportScope.AllImported);
        harness.Dialogs.SaveCsv = CsvPath();

        await viewModel.Orders.ExportOrderListCommand.ExecuteAsync(null);

        Assert.Equal($"Đã xuất 8 dòng (6 đơn) ra {CsvPath()}", viewModel.Workspace.StatusText);
        var byOrder = ReadCsv().GroupBy(row => row[0].TrimEnd('\t')).ToDictionary(group => group.Key, group => group.First());
        Assert.Equal("Không", byOrder["O1"][3]);
        Assert.Equal("Sẵn sàng", byOrder["O1"][7]);
        Assert.Equal("Thủ công", byOrder["O3"][7]);
        Assert.Equal("Có ghi chú", byOrder["O5"][7]);
        Assert.Equal("Có", byOrder["O6"][3]);
    }

    [Fact]
    public async Task Latest_import_export_keeps_only_orders_of_the_newest_import()
    {
        var shopId = await harness.SeedExampleAsync();
        await harness.SeedOrdersAsync(shopId, TikTokUiHarness.Order("O7", shopId, 7, [(TikTokUiHarness.Cody30, 1)]));
        var viewModel = await LoadAsync();
        viewModel.Orders.SelectedOrderListScope = Scope(OrderListExportScope.LatestImport);
        harness.Dialogs.SaveCsv = CsvPath();

        await viewModel.Orders.ExportOrderListCommand.ExecuteAsync(null);

        Assert.Equal("DS_DON_IMPORT_MOI_20261016_0930.csv", harness.Dialogs.LastSuggestedCsvName);
        Assert.Equal(["O7\t"], ReadCsv().Select(row => row[0]));
    }

    [Fact]
    public async Task History_exports_the_orders_of_the_selected_batch()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\lo1.pdf";
        var viewModel = await LoadAsync();
        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);
        viewModel.History.SelectedPrintBatch = viewModel.History.PrintBatches[0];
        harness.Dialogs.SaveCsv = CsvPath();

        await viewModel.History.ExportBatchOrderListCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace.ErrorText);
        Assert.Equal("DS_DON_LO_B1_20261016_0930.csv", harness.Dialogs.LastSuggestedCsvName);
        Assert.Equal($"Đã xuất 3 dòng (2 đơn) ra {CsvPath()}", viewModel.Workspace.StatusText);
        Assert.Equal(["O1\t", "O2\t", "O2\t"], ReadCsv().Select(row => row[0]));
    }

    [Fact]
    public async Task History_export_without_a_selected_batch_shows_an_error()
    {
        await harness.SeedExampleAsync();
        var viewModel = await LoadAsync();

        await viewModel.History.ExportBatchOrderListCommand.ExecuteAsync(null);

        Assert.Equal("Chọn một lô in để xuất danh sách đơn.", viewModel.Workspace.ErrorText);
        Assert.Equal(0, harness.Dialogs.SaveCsvCalls);
    }

    [Fact]
    public async Task Write_failure_is_reported_in_vietnamese()
    {
        await harness.SeedExampleAsync();
        var viewModel = await LoadAsync();
        harness.Dialogs.SaveCsv = Path.Combine(harness.TempDirectory, "no-such-folder", "ds.csv");

        await viewModel.Orders.ExportOrderListCommand.ExecuteAsync(null);

        Assert.Equal("Thao tác thất bại.", viewModel.Workspace.StatusText);
        Assert.Contains("Không ghi được file CSV", viewModel.Workspace.ErrorText, StringComparison.Ordinal);
    }

    private async Task<TikTokOrdersViewModel> LoadAsync()
    {
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Null(viewModel.Workspace.ErrorText);
        return viewModel;
    }

    private static TikTokOption<OrderListExportScope> Scope(OrderListExportScope scope) =>
        OrderListExportRunner.ScopeOptions.Single(option => option.Value == scope);

    private string CsvPath() => Path.Combine(harness.TempDirectory, "ds.csv");

    private List<IReadOnlyList<string>> ReadCsv()
    {
        using var reader = new StreamReader(CsvPath());
        return CsvParser.ReadRecords(reader).Skip(1).ToList();
    }
}
