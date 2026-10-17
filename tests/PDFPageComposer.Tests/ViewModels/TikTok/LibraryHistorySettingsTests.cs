using System.IO;
using PDFPageComposer.App.TikTok.Models;
using PDFPageComposer.App.ViewModels.TikTok;

namespace PDFPageComposer.Tests.ViewModels.TikTok;

public sealed class LibraryHistorySettingsTests : IDisposable
{
    private readonly TikTokUiHarness harness = new();

    public void Dispose() => harness.Dispose();

    [Fact]
    public async Task Library_scan_replaces_assets_and_reclassifies_orders()
    {
        var shopId = await harness.AddShopAsync();
        await harness.Repository.SaveSkusAsync(TikTokUiHarness.ExampleSkus(), CancellationToken.None);
        var old = TikTokUiHarness.Asset("IVAN", 3, 1);
        await harness.Repository.ReplacePdfAssetsAsync([old], CancellationToken.None);
        await harness.SeedOrdersAsync(shopId, TikTokUiHarness.Order("O1", shopId, 1, [(TikTokUiHarness.Cody30, 1)]));
        harness.Settings.Current.TikTok.PdfLibraryFolder = @"D:\Kho";
        var invalid = new PdfAsset(@"D:\Kho\sai ten.pdf", string.Empty, 0, default, null, DateTime.UtcNow, false, "Tên file không đúng quy ước");
        harness.Scanner.Result = [TikTokUiHarness.Asset("CODY", 4, 14), invalid];
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Equal(OrderCategory.MissingFile, viewModel.Orders.AllRows.Single().Category);
        Assert.Equal(@"D:\Kho", viewModel.Library.Folder);

        await viewModel.Library.ScanCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace.ErrorText);
        Assert.Equal(@"D:\Kho", harness.Scanner.ScannedFolder);
        Assert.Equal([old], harness.Scanner.Previous);
        Assert.Equal(new DateOnly(2026, 10, 16), harness.Scanner.Today);
        Assert.Equal(2, (await harness.Repository.GetPdfAssetsAsync(CancellationToken.None)).Count);
        Assert.Equal("2 file: 1 hợp lệ, 1 lỗi", viewModel.Library.Summary);
        Assert.Equal(OrderCategory.Ready, viewModel.Orders.AllRows.Single().Category);

        viewModel.Library.ShowInvalidOnly = true;
        var row = Assert.Single(viewModel.Library.Rows);
        Assert.Equal("sai ten.pdf", row.FileName);
        Assert.Equal("Tên file không đúng quy ước", row.StatusText);
    }

    [Fact]
    public async Task Library_scan_without_folder_shows_an_error()
    {
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.Library.ScanCommand.ExecuteAsync(null);

        Assert.Equal("Chưa chọn thư mục kho PDF.", viewModel.Workspace.ErrorText);
        Assert.Null(harness.Scanner.ScannedFolder);
    }

    [Fact]
    public async Task Library_pick_folder_saves_the_setting()
    {
        harness.Dialogs.Folder = @"E:\Kho moi";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.Library.PickFolderCommand.ExecuteAsync(null);

        Assert.Equal(@"E:\Kho moi", viewModel.Library.Folder);
        Assert.Equal(@"E:\Kho moi", harness.Settings.Current.TikTok.PdfLibraryFolder);
    }

    [Fact]
    public async Task History_lists_batches_copies_order_ids_and_unmarks_orders()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\lo1.pdf";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);
        var history = viewModel.History;
        var batch = Assert.Single(history.PrintBatches);
        Assert.Equal("2 đơn, 11 trang", batch.Summary);
        Assert.Equal("lo1.pdf", batch.FileName);

        await history.LoadItemsAsync(batch, CancellationToken.None);

        Assert.Equal(["O1", "O2"], history.SelectedOrders.Select(order => order.OrderId));
        Assert.Equal(3, history.SelectedItems.Count);
        history.CopyOrderIdsCommand.Execute(null);
        Assert.Equal($"O1{Environment.NewLine}O2", harness.Clipboard.Text);
        Assert.Equal("Đã copy 2 mã đơn.", viewModel.Workspace.StatusText);

        await history.UnmarkOrderCommand.ExecuteAsync(history.SelectedOrders[0]);

        var o1 = (await harness.Repository.GetOrdersByIdsAsync(["O1"], CancellationToken.None)).Single();
        Assert.Null(o1.PrintedBatchId);
        Assert.Equal(OrderCategory.Ready, viewModel.Orders.AllRows.Single(row => row.OrderId == "O1").Category);
        Assert.Single(history.PrintBatches);

        harness.Foxit.OpenedFiles.Clear();
        await history.OpenBatchFileCommand.ExecuteAsync(history.PrintBatches[0]);
        Assert.Equal([@"D:\Out\lo1.pdf"], harness.Foxit.OpenedFiles);
    }

    [Fact]
    public async Task Regenerate_replays_the_same_files_into_a_new_batch()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\lo1.pdf";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);
        var original = harness.Exporter.Calls[0].Plan;
        viewModel.History.SelectedPrintBatch = viewModel.History.PrintBatches[0];
        harness.Dialogs.OutputPdf = @"D:\Out\lo1-lai.pdf";

        await viewModel.History.RegenerateCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace.ErrorText);
        var replay = harness.Exporter.Calls[1];
        Assert.Equal(@"D:\Out\lo1-lai.pdf", replay.OutputPath);
        Assert.Equal(original.TotalPages, replay.Plan.TotalPages);
        Assert.Equal(
            original.Orders.SelectMany(order => order.Copies).Select(copy => copy.Asset.FilePath),
            replay.Plan.Orders.SelectMany(order => order.Copies).Select(copy => copy.Asset.FilePath));
        Assert.Equal(
            original.Orders.Select(order => order.IncludeSeparator),
            replay.Plan.Orders.Select(order => order.IncludeSeparator));
        Assert.Equal(2, viewModel.History.PrintBatches.Count);
    }

    [Fact]
    public void Replay_fails_with_a_vietnamese_message_when_a_library_file_is_gone()
    {
        var batch = new PrintBatchRecord("B1", TikTokUiHarness.LocalNow, @"D:\Out\a.pdf", SeparatorMode.None, 1, 4, "Completed");
        var items = new[] { new PrintBatchItemRecord("B1", 1, "O1", TikTokUiHarness.Cody30, 1, 1, @"D:\Kho\CODY - 4 PAGE - 14.10.pdf", null) };
        var orders = new[] { TikTokUiHarness.Order("O1", 1, 1, [(TikTokUiHarness.Cody30, 1)]) };

        var error = Assert.Throws<InvalidOperationException>(() => PrintPlanReplay.Build(batch, items, orders, []));

        Assert.Equal("File kho CODY - 4 PAGE - 14.10.pdf không còn trong kho. Hãy quét lại kho rồi thử lại.", error.Message);
    }

    [Fact]
    public async Task Settings_save_persists_values_and_reclassifies_orders()
    {
        await harness.SeedExampleAsync();
        harness.Settings.Current.LastOpenDirectory = @"D:\Docs";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Equal(OrderCategory.Printed, viewModel.Orders.AllRows.Single(row => row.OrderId == "O6").Category);
        var settings = viewModel.Settings;

        settings.SellerNoteMarksPrinted = false;
        settings.OutputFolder = @"D:\Out";
        settings.DefaultSeparatorMode = settings.SeparatorModes.Single(option => option.Value == SeparatorMode.None);
        await settings.SaveCommand.ExecuteAsync(null);

        var saved = harness.Settings.Current;
        Assert.False(saved.TikTok.SellerNoteMarksPrinted);
        Assert.Equal(@"D:\Out", saved.TikTok.OutputFolder);
        Assert.Equal(SeparatorMode.None, saved.TikTok.DefaultSeparatorMode);
        Assert.Equal(@"D:\Docs", saved.LastOpenDirectory);
        Assert.Equal(OrderCategory.HasNote, viewModel.Orders.AllRows.Single(row => row.OrderId == "O6").Category);
    }

    [Fact]
    public async Task Settings_add_and_rename_shop()
    {
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var settings = viewModel.Settings;
        settings.NewShopName = "Shop A";
        await settings.AddShopCommand.ExecuteAsync(null);
        settings.SelectedShop = viewModel.Workspace.Shops.Single();
        settings.RenameShopName = "Shop Ánh";

        await settings.RenameShopCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace.ErrorText);
        Assert.Equal("Shop Ánh", Assert.Single(await harness.Repository.GetShopsAsync(CancellationToken.None)).Name);
        Assert.Equal("Shop Ánh", viewModel.Workspace.Shops.Single().Name);
    }

    [Fact]
    public async Task Cancel_command_cancels_the_running_operation()
    {
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var started = new TaskCompletionSource();

        var run = viewModel.Workspace.RunAsync(
            "Đang chạy...",
            async cancellationToken =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            },
            "Test");
        await started.Task;
        Assert.True(viewModel.Workspace.CancelCommand.CanExecute(null));
        viewModel.Workspace.CancelCommand.Execute(null);

        Assert.False(await run);
        Assert.Equal("Đã hủy.", viewModel.Workspace.StatusText);
        Assert.False(viewModel.Workspace.IsBusy);
    }

    [Fact]
    public async Task Copy_order_ids_reports_a_busy_clipboard_instead_of_crashing()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\lo1.pdf";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);
        await viewModel.History.LoadItemsAsync(viewModel.History.PrintBatches[0], CancellationToken.None);
        harness.Clipboard.Busy = true;

        viewModel.History.CopyOrderIdsCommand.Execute(null);

        Assert.Contains("Không copy được vào clipboard", viewModel.Workspace.ErrorText);
        Assert.Null(harness.Clipboard.Text);
    }

    [Fact]
    public async Task Regenerate_skips_orders_changed_or_unmarked_since_the_batch()
    {
        await harness.SeedExampleAsync();
        harness.Dialogs.OutputPdf = @"D:\Out\lo1.pdf";
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.Orders.CreatePrintFileCommand.ExecuteAsync(null);
        await harness.Repository.UnmarkPrintedAsync("O1", CancellationToken.None);
        await viewModel.ReloadCommand.ExecuteAsync(null);
        viewModel.History.SelectedPrintBatch = viewModel.History.PrintBatches[0];
        harness.Dialogs.OutputPdf = @"D:\Out\lo1-lai.pdf";

        await viewModel.History.RegenerateCommand.ExecuteAsync(null);

        Assert.Equal(["O2"], harness.Exporter.Calls[1].Plan.Orders.Select(order => order.Order.OrderId));
        Assert.Contains("O1 (đã bỏ đánh dấu đã in)", viewModel.Workspace.ErrorText);
        Assert.Null((await harness.Repository.GetOrdersByIdsAsync(["O1"], CancellationToken.None)).Single().PrintedBatchId);
    }

    [Fact]
    public void Replay_split_excludes_inactive_unmarked_and_changed_orders()
    {
        var items = new[] { "A", "B", "C", "D", "E" }
            .Select((id, index) => new PrintBatchItemRecord("B1", index + 1, id, TikTokUiHarness.Cody30, 1, 1, @"D:\Kho\x.pdf", null))
            .ToArray();
        TikTokOrder Printed(string id) => TikTokUiHarness.Order(id, 1, 1, [(TikTokUiHarness.Cody30, 1)]) with { PrintedBatchId = "B1" };
        var orders = new[]
        {
            Printed("A"),
            Printed("B") with { IsActive = false },
            Printed("C") with { PrintedBatchId = null },
            Printed("D") with { ChangedAfterPrint = true },
        };

        var (replayable, excluded) = PrintPlanReplay.SplitStaleOrders(items, orders);

        Assert.Equal(["A"], replayable.Select(order => order.OrderId));
        Assert.Equal(["B", "C", "D", "E"], excluded.Select(entry => entry.OrderId));
    }

    [Fact]
    public async Task Failed_settings_save_leaves_the_live_settings_unchanged()
    {
        var viewModel = harness.CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        harness.Settings.SaveFailure = new IOException("locked");
        viewModel.Settings.OutputFolder = @"E:\Moi";

        await viewModel.Settings.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Thao tác thất bại.", viewModel.Workspace.StatusText);
        Assert.Null(viewModel.Workspace.Settings.OutputFolder);
    }

    [Fact]
    public async Task Corrupt_settings_file_does_not_block_the_window()
    {
        await harness.SeedExampleAsync();
        harness.Settings.LoadFailure = new System.Text.Json.JsonException("bad");
        var viewModel = harness.CreateViewModel();

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(TikTokSettingsLoaderNotice, viewModel.Workspace.ErrorText);
        Assert.Equal(6, viewModel.Orders.AllRows.Count);

        await viewModel.Settings.SaveCommand.ExecuteAsync(null);

        Assert.Contains("settings.json đang bị lỗi định dạng", viewModel.Workspace.ErrorText);
        Assert.Equal(0, harness.Settings.SaveCount);
    }

    private static string TikTokSettingsLoaderNotice => PDFPageComposer.App.TikTok.TikTokSettingsLoader.CorruptNotice;
}
