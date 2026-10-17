using System.IO;
using System.Text;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Import;
using PDFPageComposer.App.TikTok.Models;
using static PDFPageComposer.Tests.TikTok.Import.OrderLines;

namespace PDFPageComposer.Tests.TikTok.Import;

public sealed class OrderImportServiceTests
{
    private const long ShopA = 1;
    private const long ShopB = 2;

    private readonly InMemoryTikTokRepository _repository = new();
    private readonly RecordingSuggester _suggester = new();
    private readonly AppSettings _settings = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 16, 3, 0, 0, TimeSpan.Zero));
    private readonly OrderImportService _service;

    public OrderImportServiceTests()
    {
        _service = new OrderImportService(_repository, _suggester, new StubSettingsService(_settings), _time);
    }

    private Task<ImportReport> ImportAsync(long shopId, ImportMode mode, params ImportedOrderLine[] lines) =>
        _service.ImportAsync(shopId, "orders.csv", mode, lines, CancellationToken.None);

    [Fact]
    public async Task Importing_same_lines_twice_is_idempotent()
    {
        ImportedOrderLine[] lines = [Line("O1", "S1"), Line("O1", "S2", 2), Line("O2", "S1")];

        var first = await ImportAsync(ShopA, ImportMode.Full, lines);
        var second = await ImportAsync(ShopA, ImportMode.Full, lines);

        Assert.Equal((2, 0, 2), (first.Inserted, first.Updated, first.NewSkus));
        Assert.Equal((0, 2, 0, 0), (second.Inserted, second.Updated, second.NewSkus, second.Deactivated));
        Assert.Equal(2, second.ImportId);
        Assert.Equal(2, _repository.Orders.Count);
        Assert.Equal(2, _repository.Skus.Count);

        var order = _repository.Orders["O1"];
        Assert.Equal(1, order.FirstImportId);
        Assert.Equal(2, order.LastSeenImportId);
        Assert.True(order.IsActive);
        Assert.Equal(ShopA, order.ShopId);
        Assert.Equal([("S1", 1, 1), ("S2", 2, 2)], order.Lines.Select(line => (line.SkuId, line.LineNo, line.Quantity)));
        Assert.Equal(Created, order.CreatedAt);
    }

    [Fact]
    public async Task Duplicate_order_and_sku_keeps_first_line_and_warns()
    {
        var report = await ImportAsync(
            ShopA,
            ImportMode.Partial,
            Line("O1", "S1", 1, row: 1),
            Line("O1", "S2", 3, row: 2),
            Line("O1", "S1", 5, row: 3));

        var warning = Assert.Single(report.Warnings);
        Assert.StartsWith("Dòng trùng mã đơn + SKU", warning);
        Assert.Contains("O1", warning);
        var lines = _repository.Orders["O1"].Lines;
        Assert.Equal([("S1", 1, 1), ("S2", 2, 3)], lines.Select(line => (line.SkuId, line.LineNo, line.Quantity)));
    }

    [Fact]
    public async Task Status_filter_counts_skipped_rows_and_compares_normalized_text()
    {
        var report = await ImportAsync(
            ShopA,
            ImportMode.Partial,
            Line("O1", "S1", status: "CAN VAN CHUYEN"),
            Line("O2", "S1", status: "awaiting_shipment"),
            Line("O3", "S1", status: "Đã giao"),
            Line("O3", "S2", status: "Đã giao"),
            Line("O4", "S1", status: "  chờ   vận chuyển "));

        Assert.Equal(3, report.Inserted);
        Assert.Equal(2, report.Skipped);
        Assert.False(_repository.Orders.ContainsKey("O3"));
        Assert.Equal(2, _repository.AppliedChangeSets[0].Skipped);
    }

    [Fact]
    public async Task Full_mode_deactivates_missing_orders_of_same_shop_only()
    {
        await ImportAsync(ShopA, ImportMode.Full, Line("A1", "S1"), Line("A2", "S1"));
        await ImportAsync(ShopB, ImportMode.Full, Line("B1", "S1"));

        var partial = await ImportAsync(ShopA, ImportMode.Partial, Line("A1", "S1"));
        Assert.Equal(0, partial.Deactivated);
        Assert.True(_repository.Orders["A2"].IsActive);

        var full = await ImportAsync(ShopA, ImportMode.Full, Line("A1", "S1"));
        Assert.Equal(1, full.Deactivated);
        Assert.Equal(["A2"], _repository.AppliedChangeSets[^1].OrderIdsToDeactivate);
        Assert.False(_repository.Orders["A2"].IsActive);
        Assert.True(_repository.Orders["A1"].IsActive);
        Assert.True(_repository.Orders["B1"].IsActive);

        var back = await ImportAsync(ShopA, ImportMode.Partial, Line("A2", "S1"));
        Assert.Equal(1, back.Updated);
        Assert.True(_repository.Orders["A2"].IsActive);
    }

    [Fact]
    public async Task Full_mode_with_no_accepted_orders_hides_nothing()
    {
        await ImportAsync(ShopA, ImportMode.Full, Line("A1", "S1"));

        var report = await ImportAsync(ShopA, ImportMode.Full, Line("A1", "S1", status: "Đã giao"));

        Assert.Equal(0, report.Deactivated);
        Assert.Single(report.Warnings);
        Assert.True(_repository.Orders["A1"].IsActive);
    }

    [Fact]
    public async Task Seller_note_marker_sets_and_later_clears_printed_source()
    {
        var first = await ImportAsync(ShopA, ImportMode.Partial, Line("O1", "S1", sellerNote: "đã in IVAN mini"));

        var marked = _repository.Orders["O1"];
        Assert.Equal(1, first.AlreadyPrinted);
        Assert.Equal(PrintedSource.SellerNote, marked.PrintedSource);
        Assert.Equal(_time.Now, marked.PrintedAt);
        Assert.Null(marked.PrintedBatchId);

        _time.Now = _time.Now.AddHours(1);
        var second = await ImportAsync(ShopA, ImportMode.Partial, Line("O1", "S1", sellerNote: "ĐÃ IN IVAN mini"));
        Assert.Equal(1, second.AlreadyPrinted);
        Assert.Equal(marked.PrintedAt, _repository.Orders["O1"].PrintedAt);

        var third = await ImportAsync(ShopA, ImportMode.Partial, Line("O1", "S1", sellerNote: "dã có qhung"));
        var cleared = _repository.Orders["O1"];
        Assert.Equal(0, third.AlreadyPrinted);
        Assert.Null(cleared.PrintedSource);
        Assert.Null(cleared.PrintedAt);
        Assert.Equal("dã có qhung", cleared.SellerNote);
    }

    [Fact]
    public async Task Seller_note_marker_is_ignored_when_setting_is_off()
    {
        _settings.TikTok.SellerNoteMarksPrinted = false;

        var report = await ImportAsync(ShopA, ImportMode.Partial, Line("O1", "S1", sellerNote: "ĐÃ IN"));

        Assert.Equal(0, report.AlreadyPrinted);
        Assert.Null(_repository.Orders["O1"].PrintedSource);
    }

    [Fact]
    public async Task Update_keeps_batch_print_marks_and_reviewed_hash()
    {
        await ImportAsync(ShopA, ImportMode.Partial, Line("O1", "S1", 2, buyerMessage: "cho em ảnh khác"));
        await PrintAsync("O1", ("S1", 2));
        await _repository.SetNoteReviewedAsync("O1", "HASH", CancellationToken.None);

        var report = await ImportAsync(ShopA, ImportMode.Partial, Line("O1", "S1", 2, buyerMessage: "đổi ý"));

        var order = _repository.Orders["O1"];
        Assert.Equal(1, report.AlreadyPrinted);
        Assert.Equal("BATCH-1", order.PrintedBatchId);
        Assert.Equal(PrintedSource.Batch, order.PrintedSource);
        Assert.Equal("HASH", order.NoteReviewedHash);
        Assert.Equal("đổi ý", order.BuyerMessage);
        Assert.False(order.ChangedAfterPrint);
    }

    [Fact]
    public async Task Changed_quantity_after_print_sets_sticky_flag()
    {
        await ImportAsync(ShopA, ImportMode.Partial, Line("O1", "S1", 2));
        await PrintAsync("O1", ("S1", 2));

        await ImportAsync(ShopA, ImportMode.Partial, Line("O1", "S1", 3));
        Assert.True(_repository.Orders["O1"].ChangedAfterPrint);

        await ImportAsync(ShopA, ImportMode.Partial, Line("O1", "S1", 2));
        Assert.True(_repository.Orders["O1"].ChangedAfterPrint);
    }

    [Fact]
    public async Task Removed_printed_sku_or_added_sku_sets_changed_after_print()
    {
        await ImportAsync(ShopA, ImportMode.Partial, Line("O1", "S1"), Line("O1", "KEYCHAIN"), Line("O2", "S1"));
        await PrintAsync("O1", ("S1", 1));
        await PrintAsync("O2", ("S1", 1));

        await ImportAsync(ShopA, ImportMode.Partial, Line("O1", "S1"), Line("O1", "KEYCHAIN"), Line("O2", "S1"));
        Assert.False(_repository.Orders["O1"].ChangedAfterPrint);
        Assert.False(_repository.Orders["O2"].ChangedAfterPrint);

        await ImportAsync(ShopA, ImportMode.Partial, Line("O1", "KEYCHAIN"), Line("O2", "S1"), Line("O2", "S9"));
        Assert.True(_repository.Orders["O1"].ChangedAfterPrint);
        Assert.True(_repository.Orders["O2"].ChangedAfterPrint);
    }

    [Fact]
    public async Task New_sku_is_suggested_once_and_existing_skus_are_not_touched()
    {
        var confirmed = new SkuConfig(
            "KNOWN", "P", "V", SkuProcessType.Ignore, SkuConfigStatus.Confirmed, null, null, Created, Created);
        await _repository.SaveSkusAsync([confirmed], CancellationToken.None);

        var report = await ImportAsync(
            ShopA,
            ImportMode.Partial,
            Line("O1", "NEW"),
            Line("O2", "NEW"),
            Line("O2", "KNOWN"),
            Line("O3", "OTHER", status: "Đã giao"));

        Assert.Equal(1, report.NewSkus);
        Assert.Single(_suggester.Calls);
        var sku = _repository.Skus["NEW"];
        Assert.Equal(SkuConfigStatus.Unconfigured, sku.ConfigStatus);
        Assert.Equal(SkuProcessType.Auto, sku.ProcessType);
        Assert.Equal("IVAN", sku.Idol);
        Assert.Equal(20, sku.CardCount);
        Assert.Equal(_time.Now, sku.FirstSeenAt);
        Assert.Equal(_time.Now, sku.UpdatedAt);
        Assert.Same(confirmed, _repository.Skus["KNOWN"]);
        Assert.False(_repository.Skus.ContainsKey("OTHER"));

        var again = await ImportAsync(ShopA, ImportMode.Partial, Line("O1", "NEW"));
        Assert.Equal(0, again.NewSkus);
    }

    [Fact]
    public async Task Order_from_another_shop_keeps_its_shop_and_warns()
    {
        await ImportAsync(ShopA, ImportMode.Full, Line("O1", "S1"));

        var report = await ImportAsync(ShopB, ImportMode.Full, Line("O1", "S1", 4));

        Assert.Equal(1, report.Updated);
        Assert.Contains(report.Warnings, warning => warning.Contains("shop khác", StringComparison.Ordinal));
        var order = _repository.Orders["O1"];
        Assert.Equal(ShopA, order.ShopId);
        Assert.Equal(4, order.Lines[0].Quantity);
        Assert.True(order.IsActive);
    }

    [Fact]
    public async Task Full_import_keeps_orders_whose_rows_the_source_rejected()
    {
        await ImportAsync(ShopA, ImportMode.Full, Line("O1", "S1"), Line("O2", "S1"), Line("O3", "S1"));

        // O2's only row had a bad date in the new export: the source dropped it and reported the order id.
        var report = await _service.ImportAsync(
            ShopA,
            "orders.csv",
            ImportMode.Full,
            [Line("O1", "S1")],
            ["O2"],
            CancellationToken.None);

        Assert.Equal(1, report.Deactivated);
        Assert.True(_repository.Orders["O2"].IsActive);
        Assert.False(_repository.Orders["O3"].IsActive);
        Assert.Contains("Đơn O2 có dòng không hợp lệ trong file nên được giữ nguyên, không ẩn.", report.Warnings);
    }

    [Fact]
    public async Task Corrupt_settings_file_falls_back_to_defaults_with_a_warning()
    {
        var service = new OrderImportService(_repository, _suggester, new ThrowingSettingsService(), _time);

        var report = await service.ImportAsync(ShopA, "orders.csv", ImportMode.Partial, [Line("O1", "S1")], CancellationToken.None);

        Assert.Equal(1, report.Inserted);
        Assert.Contains(TikTokSettingsLoader.CorruptNotice, report.Warnings);
    }

    private sealed class ThrowingSettingsService : PDFPageComposer.App.Interfaces.IAppSettingsService
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromException<AppSettings>(new System.Text.Json.JsonException("bad json"));

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public async Task Sample_file_imports_and_reimports_idempotently()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "tiktok", "orders-sample.csv");
        OrderSourceResult source;
        using (var reader = new StreamReader(path, Encoding.UTF8))
        {
            source = SellerCenterCsvOrderSource.Parse(reader);
        }

        var first = await _service.ImportAsync(ShopA, "orders-sample.csv", ImportMode.Full, source.Lines, CancellationToken.None);
        var second = await _service.ImportAsync(ShopA, "orders-sample.csv", ImportMode.Full, source.Lines, CancellationToken.None);

        Assert.Equal(_repository.Orders.Count, first.Inserted);
        Assert.Equal(33, first.Inserted + CountSkippedOrders(source));
        Assert.Equal(0, second.Inserted);
        Assert.Equal(first.Inserted, second.Updated);
        Assert.Equal(0, second.Deactivated);
        Assert.Equal(first.AlreadyPrinted, second.AlreadyPrinted);
        Assert.True(first.AlreadyPrinted >= 1);
        Assert.Equal(PrintedSource.SellerNote, _repository.Orders["586605624104421255"].PrintedSource);
    }

    private static int CountSkippedOrders(OrderSourceResult source)
    {
        var accepted = new AppSettings().TikTok.AcceptedStatuses
            .Select(TextNormalizer.Normalize)
            .ToHashSet();
        var acceptedOrders = source.Lines
            .Where(line => accepted.Contains(TextNormalizer.Normalize(line.StatusText)))
            .Select(line => line.OrderId)
            .ToHashSet();
        return source.Lines.Select(line => line.OrderId).Distinct().Count(id => !acceptedOrders.Contains(id));
    }

    private async Task PrintAsync(string orderId, params (string SkuId, int Quantity)[] items)
    {
        var existingBatches = await _repository.GetPrintBatchesAsync(CancellationToken.None);
        var batchId = $"BATCH-{existingBatches.Count + 1}";
        var batch = new PrintBatchRecord(batchId, _time.Now, "out.pdf", SeparatorMode.None, 1, items.Length, "Completed");
        var records = items
            .Select((item, index) => new PrintBatchItemRecord(batchId, index + 1, orderId, item.SkuId, 1, item.Quantity, "x.pdf", null))
            .ToArray();
        await _repository.CommitPrintBatchAsync(batch, records, [orderId], CancellationToken.None);
    }
}
