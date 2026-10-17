using System.IO;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Models;
using PDFPageComposer.App.ViewModels.TikTok;

namespace PDFPageComposer.Tests.ViewModels.TikTok;

public sealed class OrdersTabViewModelTests : IDisposable
{
    private readonly TikTokUiHarness harness = new();

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Load_classifies_the_PRD_example_and_preselects_ready_orders()
    {
        await harness.SeedExampleAsync();
        var viewModel = harness.CreateViewModel();

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace.ErrorText);
        var rows = viewModel.Orders.AllRows.ToDictionary(row => row.OrderId);
        Assert.Equal(OrderCategory.Ready, rows["O1"].Category);
        Assert.Equal(OrderCategory.Ready, rows["O2"].Category);
        Assert.Equal(OrderCategory.Manual, rows["O3"].Category);
        Assert.Equal(OrderCategory.NoCardNeeded, rows["O4"].Category);
        Assert.Equal(OrderCategory.HasNote, rows["O5"].Category);
        Assert.Equal(OrderCategory.Printed, rows["O6"].Category);

        Assert.Equal(["O1", "O2"], viewModel.Orders.SelectedRows.Select(row => row.OrderId));
        Assert.False(rows["O6"].CanSelect);
        Assert.Equal(6, rows["O2"].SheetCount);
        Assert.Contains("IVAN - 3 PAGE - 15.10", rows["O2"].PlannedFilesText);
        Assert.Contains("IVAN - 3 PAGE - 10.10", rows["O2"].PlannedFilesText);
        Assert.Contains("cho em ảnh khác nhé", rows["O5"].ReasonsText);
        Assert.Equal("cho em ảnh khác nhé", rows["O5"].BuyerMessage);
        Assert.Equal("ĐÃ IN IVAN mini", rows["O6"].SellerNote);
        Assert.Equal("Đã chọn 2 đơn, khoảng 10 tờ", viewModel.Orders.SelectionSummary);
    }

    [Fact]
    public async Task Category_chips_show_counts_and_hide_no_card_orders_by_default()
    {
        await harness.SeedExampleAsync();
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var orders = viewModel.Orders;

        Assert.DoesNotContain(orders.Rows, row => row.OrderId == "O4");
        Assert.Equal(5, orders.Rows.Count);
        var ready = orders.CategoryFilters.Single(filter => filter.Category == OrderCategory.Ready);
        Assert.Equal("Sẵn sàng (2)", ready.Label);
        var noCard = orders.CategoryFilters.Single(filter => filter.Category == OrderCategory.NoCardNeeded);
        Assert.False(noCard.IsChecked);
        Assert.Equal("Không cần in card (1)", noCard.Label);

        noCard.IsChecked = true;
        ready.IsChecked = false;

        Assert.Contains(orders.Rows, row => row.OrderId == "O4");
        Assert.DoesNotContain(orders.Rows, row => row.Category == OrderCategory.Ready);
    }

    [Fact]
    public async Task Shop_filter_keeps_only_orders_of_the_chosen_shop()
    {
        var shopA = await harness.SeedExampleAsync();
        var shopB = (await harness.Repository.AddShopAsync("Shop B", CancellationToken.None)).ShopId;
        await harness.SeedOrdersAsync(shopB, TikTokUiHarness.Order("B1", shopB, 30, [(TikTokUiHarness.Cody30, 1)]));
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var orders = viewModel.Orders;

        Assert.Equal(3, orders.ShopFilters.Count);
        orders.SelectedShopFilter = orders.ShopFilters.Single(option => option.ShopId == shopB);

        var row = Assert.Single(orders.Rows);
        Assert.Equal("B1", row.OrderId);
        Assert.Equal("Shop B", row.ShopName);

        orders.SelectedShopFilter = orders.ShopFilters.Single(option => option.ShopId == shopA);
        Assert.DoesNotContain(orders.Rows, candidate => candidate.OrderId == "B1");
    }

    [Fact]
    public async Task Note_review_stores_the_note_hash_and_reclassifies_the_order()
    {
        await harness.SeedExampleAsync();
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var o5 = viewModel.Orders.AllRows.Single(row => row.OrderId == "O5");
        Assert.True(o5.CanReviewNote);

        await viewModel.Orders.MarkNoteReviewedCommand.ExecuteAsync(o5);

        Assert.Null(viewModel.Workspace.ErrorText);
        var stored = (await harness.Repository.GetOrdersByIdsAsync(["O5"], CancellationToken.None)).Single();
        Assert.Equal(OrderNotes.ComputeHash("cho em ảnh khác nhé", null), stored.NoteReviewedHash);
        var reloaded = viewModel.Orders.AllRows.Single(row => row.OrderId == "O5");
        Assert.Equal(OrderCategory.Ready, reloaded.Category);
        Assert.True(reloaded.IsSelected);
        Assert.Equal("Đã cho phép ghép đơn O5.", viewModel.Workspace.StatusText);
    }

    [Fact]
    public async Task Create_print_file_exports_selected_orders_opens_foxit_and_reloads()
    {
        await harness.SeedExampleAsync();
        harness.Settings.Current.TikTok.OutputFolder = @"D:\Out";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace.ErrorText);
        var call = Assert.Single(harness.Exporter.Calls);
        Assert.Equal(@"D:\Out\TIKTOK_20261016_0930_2don.pdf", call.OutputPath);
        Assert.Equal(["O1", "O2"], call.Plan.Orders.Select(order => order.Order.OrderId));
        Assert.Equal(11, call.Plan.TotalPages);
        Assert.Equal(SeparatorMode.ComplexOnly, call.Plan.SeparatorMode);
        Assert.Equal("Shop A", Assert.Single(call.ShopNames).Value);
        Assert.Equal([@"D:\Out\TIKTOK_20261016_0930_2don.pdf"], harness.Foxit.OpenedFiles);

        var rows = viewModel.Orders.AllRows.ToDictionary(row => row.OrderId);
        Assert.Equal(OrderCategory.Printed, rows["O1"].Category);
        Assert.Equal(OrderCategory.Printed, rows["O2"].Category);
        Assert.Empty(viewModel.Orders.SelectedRows);
        Assert.Single(viewModel.History.PrintBatches);
        Assert.Contains("2 đơn, 11 trang", viewModel.Orders.PlanSummary);
        Assert.Equal("Đã tạo file in lô B1 (2 đơn, 11 trang).", viewModel.Workspace.StatusText);
    }

    [Fact]
    public async Task Create_print_file_asks_for_the_path_when_no_output_folder_is_set()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = null;
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);

        Assert.Equal("TIKTOK_20261016_0930_2don.pdf", harness.Dialogs.LastSuggestedFileName);
        Assert.Empty(harness.Exporter.Calls);
        Assert.Empty(harness.Foxit.OpenedFiles);
        Assert.Equal("Đã hủy chọn nơi lưu file.", viewModel.Workspace.StatusText);
    }

    [Fact]
    public async Task Export_failure_keeps_orders_unprinted_and_shows_the_error()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\x.pdf";
        harness.Exporter.Failure = new InvalidOperationException("Không ghi được file in.");
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);

        Assert.Equal("Không ghi được file in.", viewModel.Workspace.ErrorText);
        Assert.Equal("Thao tác thất bại.", viewModel.Workspace.StatusText);
        Assert.False(viewModel.Workspace.IsBusy);
        Assert.Empty(harness.Foxit.OpenedFiles);
        Assert.Empty(await harness.Repository.GetPrintBatchesAsync(CancellationToken.None));
        var stored = await harness.Repository.GetOrdersByIdsAsync(["O1", "O2"], CancellationToken.None);
        Assert.All(stored, order => Assert.Null(order.PrintedBatchId));
        Assert.All(
            viewModel.Orders.AllRows.Where(row => row.OrderId is "O1" or "O2"),
            row => Assert.Equal(OrderCategory.Ready, row.Category));
    }

    [Fact]
    public async Task Foxit_failure_after_export_is_reported_but_orders_stay_printed()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\x.pdf";
        harness.Foxit.Failure = new FileNotFoundException("Không tìm thấy Foxit.");
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);

        Assert.Contains("không mở được bằng Foxit", viewModel.Workspace.ErrorText);
        Assert.Equal(OrderCategory.Printed, viewModel.Orders.AllRows.Single(row => row.OrderId == "O1").Category);
    }

    [Fact]
    public async Task Reprint_lets_a_batch_printed_order_be_selected_and_exported_again()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\lo1.pdf";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var orders = viewModel.Orders;
        await orders.CreatePrintFileCommand.ExecuteAsync(null);
        var o1 = orders.AllRows.Single(row => row.OrderId == "O1");
        Assert.True(o1.IsPrinted);

        o1.IsSelected = true;
        Assert.False(o1.IsSelected);

        orders.ToggleReprintCommand.Execute(o1);
        Assert.True(o1.IsSelected);
        Assert.Equal("In lại", o1.CategoryText);
        harness.Dialogs.OutputPdf = @"D:\Out\reprint.pdf";

        await orders.CreatePrintFileCommand.ExecuteAsync(null);

        Assert.Equal(2, harness.Exporter.Calls.Count);
        Assert.Equal(["O1"], harness.Exporter.Calls[1].Plan.Orders.Select(order => order.Order.OrderId));
    }

    [Fact]
    public async Task Reprint_of_a_seller_note_printed_order_is_exported_despite_its_note()
    {
        // TT-11 asks the user to write "ĐÃ IN" into the Seller Note; choosing "In lại" counts as having read it.
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\reprint.pdf";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var orders = viewModel.Orders;
        orders.ClearSelectionCommand.Execute(null);
        var o6 = orders.AllRows.Single(row => row.OrderId == "O6");
        orders.ToggleReprintCommand.Execute(o6);

        await orders.CreatePrintFileCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace.ErrorText);
        Assert.Equal(["O6"], Assert.Single(harness.Exporter.Calls).Plan.Orders.Select(order => order.Order.OrderId));
    }

    [Fact]
    public async Task Reprint_still_blocks_an_unprinted_order_with_a_note_in_the_same_batch()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\reprint.pdf";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var orders = viewModel.Orders;
        orders.ToggleReprintCommand.Execute(orders.AllRows.Single(row => row.OrderId == "O6"));

        await orders.CreatePrintFileCommand.ExecuteAsync(null);

        var plan = Assert.Single(harness.Exporter.Calls).Plan;
        Assert.Equal(["O1", "O2", "O6"], plan.Orders.Select(order => order.Order.OrderId));
        Assert.Equal(OrderCategory.HasNote, viewModel.Orders.AllRows.Single(row => row.OrderId == "O5").Category);
    }

    [Fact]
    public async Task Ready_orders_hidden_by_the_shop_filter_are_not_exported()
    {
        var shopA = await harness.SeedExampleAsync();
        var shopB = (await harness.Repository.AddShopAsync("Shop B", CancellationToken.None)).ShopId;
        await harness.SeedOrdersAsync(shopB, TikTokUiHarness.Order("B1", shopB, 30, [(TikTokUiHarness.Cody30, 1)]));
        harness.Dialogs.OutputPdf = @"D:\Out\a.pdf";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var orders = viewModel.Orders;
        Assert.True(orders.AllRows.Single(row => row.OrderId == "B1").IsSelected);

        orders.SelectedShopFilter = orders.ShopFilters.Single(option => option.ShopId == shopA);
        Assert.Equal(["O1", "O2"], orders.SelectedRows.Select(row => row.OrderId));
        Assert.StartsWith("Đã chọn 2 đơn", orders.SelectionSummary);

        await orders.CreatePrintFileCommand.ExecuteAsync(null);

        Assert.Equal(["O1", "O2"], Assert.Single(harness.Exporter.Calls).Plan.Orders.Select(order => order.Order.OrderId));
        Assert.Null((await harness.Repository.GetOrdersByIdsAsync(["B1"], CancellationToken.None)).Single().PrintedBatchId);
    }

    [Fact]
    public async Task Reload_keeps_unticked_rows_and_reprint_toggles_but_ticks_newly_ready_orders()
    {
        var shopId = await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\a.pdf";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var orders = viewModel.Orders;
        orders.AllRows.Single(row => row.OrderId == "O1").IsSelected = false;
        orders.ToggleReprintCommand.Execute(orders.AllRows.Single(row => row.OrderId == "O6"));
        await harness.SeedOrdersAsync(shopId, TikTokUiHarness.Order("N1", shopId, 40, [(TikTokUiHarness.Cody30, 1)]));

        // "Đã xem – cho phép ghép" reloads the list.
        await orders.MarkNoteReviewedCommand.ExecuteAsync(orders.AllRows.Single(row => row.OrderId == "O5"));

        var rows = orders.AllRows.ToDictionary(row => row.OrderId);
        Assert.False(rows["O1"].IsSelected);
        Assert.True(rows["O2"].IsSelected);
        Assert.True(rows["O6"].IsReprint);
        Assert.True(rows["O6"].IsSelected);
        Assert.True(rows["O5"].IsSelected);
        Assert.True(rows["N1"].IsSelected);
    }

    [Fact]
    public async Task Preview_follows_the_selection()
    {
        var shopId = await harness.SeedExampleAsync();
        await harness.SeedOrdersAsync(
            shopId,
            TikTokUiHarness.Order("O7", shopId, 7, [(TikTokUiHarness.Ivan20, 1)]),
            TikTokUiHarness.Order("O8", shopId, 8, [(TikTokUiHarness.Ivan20, 1)]));
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var orders = viewModel.Orders;
        var o7 = orders.AllRows.Single(row => row.OrderId == "O7");
        var o8 = orders.AllRows.Single(row => row.OrderId == "O8");
        // O2 (x2) takes 15.10 and 10.10; every other order starts again at the newest file (TT-04).
        Assert.Contains("IVAN - 3 PAGE - 15.10", o7.PlannedFilesText);
        Assert.Contains("IVAN - 3 PAGE - 15.10", o8.PlannedFilesText);

        o7.IsSelected = false;

        await WaitUntilAsync(() => o8.PlannedFilesText.Contains("IVAN - 3 PAGE - 15.10", StringComparison.Ordinal));
        await WaitUntilAsync(() => o7.PlannedFilesText == OrderRowViewModel.NotSelectedText);
        Assert.Null(o7.SheetCount);
    }

    [Fact]
    public async Task Auto_named_output_never_replaces_an_existing_file()
    {
        await harness.SeedExampleAsync();
        harness.Settings.Current.TikTok.OutputFolder = harness.TempDirectory;
        harness.WriteTempFile("TIKTOK_20261016_0930_2don.pdf", "earlier batch");
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace.ErrorText);
        Assert.Equal(
            Path.Combine(harness.TempDirectory, "TIKTOK_20261016_0930_2don_2.pdf"),
            Assert.Single(harness.Exporter.Calls).OutputPath);
    }

    [Fact]
    public async Task Auto_named_output_skips_paths_of_earlier_batches()
    {
        await harness.SeedExampleAsync();
        harness.Settings.Current.TikTok.OutputFolder = @"D:\Out";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);

        // Same minute, same order count: reprint both orders.
        foreach (var row in viewModel.Orders.AllRows.Where(row => row.OrderId is "O1" or "O2"))
        {
            viewModel.Orders.ToggleReprintCommand.Execute(row);
        }

        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);

        Assert.Equal(
            [@"D:\Out\TIKTOK_20261016_0930_2don.pdf", @"D:\Out\TIKTOK_20261016_0930_2don_2.pdf"],
            harness.Exporter.Calls.Select(call => call.OutputPath));
    }

    [Fact]
    public async Task Picking_the_file_of_an_earlier_batch_is_refused()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\lo1.pdf";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);
        viewModel.Orders.ToggleReprintCommand.Execute(viewModel.Orders.AllRows.Single(row => row.OrderId == "O1"));

        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);

        Assert.Single(harness.Exporter.Calls);
        Assert.Contains("lô cũ", viewModel.Workspace.ErrorText);
    }

    [Fact]
    public async Task Pdf_export_errors_are_shown_in_vietnamese()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\x.pdf";
        harness.Exporter.Failure = new PDFPageComposer.App.Models.PdfExportException(
            PDFPageComposer.App.Models.PdfExportError.DestinationUnavailable,
            "Output directory is unavailable.");
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);

        Assert.StartsWith(@"Không ghi được file in vào 'D:\Out'", viewModel.Workspace.ErrorText);
    }

    [Fact]
    public async Task Order_printed_elsewhere_after_loading_is_not_printed_again()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\x.pdf";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        await harness.Repository.CommitPrintBatchAsync(
            new PrintBatchRecord("OTHER", TikTokUiHarness.LocalNow, @"D:\Out\other.pdf", SeparatorMode.None, 1, 4, "Completed"),
            [new PrintBatchItemRecord("OTHER", 1, "O1", TikTokUiHarness.Cody30, 1, 1, @"D:\Kho\CODY - 4 PAGE - 14.10.pdf", null)],
            ["O1"],
            CancellationToken.None);

        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);

        Assert.Equal(["O2"], Assert.Single(harness.Exporter.Calls).Plan.Orders.Select(order => order.Order.OrderId));
        Assert.Contains("O1: đã được in ở lô khác", viewModel.Orders.PlanSummary);
    }

    [Fact]
    public async Task Saving_a_new_default_separator_mode_updates_the_orders_tab()
    {
        await harness.SeedExampleAsync();
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.Settings.DefaultSeparatorMode = viewModel.Settings.SeparatorModes.Single(option => option.Value == SeparatorMode.None);

        await viewModel.Settings.SaveCommand.ExecuteAsync(null);

        Assert.Equal(SeparatorMode.None, viewModel.Orders.SelectedSeparatorMode.Value);
    }

    [Fact]
    public async Task Window_cannot_close_while_an_operation_runs()
    {
        var viewModel = harness.CreateViewModel();
        Assert.True(viewModel.TryConfirmClose());

        viewModel.Workspace.IsBusy = true;

        Assert.False(viewModel.TryConfirmClose());
        Assert.Contains("Đang có thao tác chạy", viewModel.Workspace.ErrorText);
    }

    [Fact]
    public async Task Choosing_files_by_hand_is_saved_and_can_go_back_to_automatic()
    {
        await harness.SeedExampleAsync();
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var orders = viewModel.Orders;
        orders.FileExists = _ => true;
        OrderFilesViewModel? shown = null;
        var result = OrderFilesResult.Save;
        orders.ShowOrderFilesDialog = dialog =>
        {
            shown = dialog;
            if (result == OrderFilesResult.Save)
            {
                dialog.SaveCommand.Execute(null);
            }
            else
            {
                dialog.UseAutomaticCommand.Execute(null);
            }
        };

        var o1 = orders.AllRows.Single(row => row.OrderId == "O1");
        Assert.True(o1.CanChooseFiles);
        await orders.ChooseFilesCommand.ExecuteAsync(o1);

        Assert.NotNull(shown);
        Assert.Null(viewModel.Workspace.ErrorText);
        var saved = await harness.Repository.GetFileOverridesAsync(CancellationToken.None);
        Assert.NotEmpty(saved);
        Assert.All(saved, choice => Assert.Equal("O1", choice.OrderId));
        o1 = orders.AllRows.Single(row => row.OrderId == "O1");
        await WaitUntilAsync(() => o1.PlannedFilesText.Contains("tự chọn", StringComparison.Ordinal));

        result = OrderFilesResult.Automatic;
        await orders.ChooseFilesCommand.ExecuteAsync(o1);

        Assert.Empty(await harness.Repository.GetFileOverridesAsync(CancellationToken.None));
        o1 = orders.AllRows.Single(row => row.OrderId == "O1");
        await WaitUntilAsync(() => o1.PlannedFilesText.Length > 0 && !o1.PlannedFilesText.Contains("tự chọn", StringComparison.Ordinal));
        Assert.False(orders.AllRows.Single(row => row.OrderId == "O3").CanChooseFiles);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Condition was not met in time.");
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task Create_print_file_without_selection_shows_an_error()
    {
        await harness.SeedExampleAsync();
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.Orders.ClearSelectionCommand.Execute(null);

        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);

        Assert.Equal("Chưa chọn đơn nào để tạo file in.", viewModel.Workspace.ErrorText);
        Assert.Empty(harness.Exporter.Calls);
    }

    [Fact]
    public async Task Separator_mode_defaults_to_the_settings_value()
    {
        await harness.SeedExampleAsync();
        harness.Settings.Current.TikTok.DefaultSeparatorMode = SeparatorMode.All;
        var viewModel = harness.CreateViewModel();

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(SeparatorMode.All, viewModel.Orders.SelectedSeparatorMode.Value);
    }

    [Fact]
    public async Task Changed_after_print_orders_show_a_warning()
    {
        var shopId = await harness.AddShopAsync();
        await harness.Repository.SaveSkusAsync(TikTokUiHarness.ExampleSkus(), CancellationToken.None);
        var changed = TikTokUiHarness.Order("C1", shopId, 1, [(TikTokUiHarness.Cody30, 2)]) with
        {
            PrintedBatchId = "OLD",
            PrintedAt = TikTokUiHarness.LocalNow,
            PrintedSource = PrintedSource.Batch,
            ChangedAfterPrint = true,
        };
        await harness.SeedOrdersAsync(shopId, changed);
        var viewModel = harness.CreateViewModel();

        await viewModel.LoadCommand.ExecuteAsync(null);

        var row = Assert.Single(viewModel.Orders.AllRows);
        Assert.Equal("Đơn đã thay đổi sau khi in", row.WarningText);
    }
}
