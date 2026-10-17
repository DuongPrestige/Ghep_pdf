using PDFPageComposer.App.TikTok.Export;
using PDFPageComposer.App.TikTok.Matching;
using PDFPageComposer.App.TikTok.Models;
using PDFPageComposer.App.ViewModels.TikTok;
using static PDFPageComposer.Tests.TikTok.Matching.MatchingTestData;

namespace PDFPageComposer.Tests.TikTok.Export;

public sealed class OrderListExportBuilderTests
{
    private static readonly TimeZoneInfo Vietnam =
        TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "UTC+07", "UTC+07");

    private static readonly IReadOnlyDictionary<string, SkuConfig> Catalog = Skus(
        Auto("IVAN20", "IVAN", 20),
        Auto("CODY30", "CODY", 30),
        Auto("JACK30", "JACK", 30),
        Ignore("KEY"));

    private static readonly PdfAsset Ivan3 = Asset("IVAN", 3, 15);
    private static readonly PdfAsset Ivan9 = Asset("IVAN", 9, 14);

    private readonly OrderListExportBuilder builder = new(new OrderClassifier());

    [Fact]
    public void All_imported_lists_active_orders_with_the_printed_mark_and_the_orders_tab_label()
    {
        var ready = Order("READY", 2, [("IVAN20", 1)]);
        var printed = Order("PRINTED", 1, [("IVAN20", 1)], printedBatchId: "B1", printedSource: PrintedSource.Batch);
        var gone = Order("GONE", 3, [("IVAN20", 1)], isActive: false);

        var rows = Build(OrderListExportScope.AllImported, [ready, printed, gone]);

        Assert.Equal(["PRINTED", "READY"], rows.Select(row => row.OrderId));
        var printedRow = rows[0];
        Assert.Equal("Có", printedRow.Printed);
        Assert.Equal("Lô in", printedRow.MarkSource);
        Assert.Equal("B1", printedRow.BatchId);
        Assert.Equal("16/10/2026 08:00:00", printedRow.PrintedAt);
        Assert.Equal("Đã tạo file in", printedRow.Status);
        Assert.Equal("Đã in (lô B1)", printedRow.Reasons);
        Assert.Equal("Có", printedRow.AwaitingShipment);
        Assert.Equal("Shop A", printedRow.ShopName);
        Assert.Equal("16/10/2026 08:01:00", printedRow.CreatedAt);

        var readyRow = rows[1];
        Assert.Equal("Không", readyRow.Printed);
        Assert.Equal(string.Empty, readyRow.MarkSource);
        Assert.Equal(string.Empty, readyRow.BatchId);
        Assert.Equal(string.Empty, readyRow.PrintedAt);
        Assert.Equal("Sẵn sàng", readyRow.Status);
        Assert.Equal(string.Empty, readyRow.Reasons);
        Assert.Equal(string.Empty, readyRow.FilesUsed);
    }

    [Fact]
    public void Not_printed_lists_only_active_orders_without_a_print_mark()
    {
        var ready = Order("READY", 2, [("IVAN20", 1)]);
        var printed = Order("PRINTED", 1, [("IVAN20", 1)], printedBatchId: "B1", printedSource: PrintedSource.Batch);
        var note = Order("NOTE", 3, [("IVAN20", 1)], sellerNote: "đã in rồi");
        var gone = Order("GONE", 4, [("IVAN20", 1)], isActive: false);

        var rows = Build(OrderListExportScope.NotPrinted, [ready, printed, note, gone]);

        var row = Assert.Single(rows);
        Assert.Equal("READY", row.OrderId);
        Assert.Equal("Không", row.Printed);
    }

    [Fact]
    public void Printed_only_includes_seller_note_marks_and_orders_no_longer_awaiting_shipment()
    {
        var batch = Order("BATCH", 1, [("IVAN20", 1)], isActive: false, printedBatchId: "B1", printedSource: PrintedSource.Batch);
        var note = Order("NOTE", 2, [("IVAN20", 1)], sellerNote: "đã in rồi");
        var ready = Order("READY", 3, [("IVAN20", 1)]);

        var rows = Build(OrderListExportScope.PrintedOnly, [ready, note, batch]);

        Assert.Equal(["BATCH", "NOTE"], rows.Select(row => row.OrderId));
        Assert.Equal("Không", rows[0].AwaitingShipment);
        Assert.Equal("Seller Note", rows[1].MarkSource);
        Assert.Equal(string.Empty, rows[1].BatchId);
        Assert.Equal("Đã in theo Seller Note", rows[1].Reasons);
        Assert.Equal("đã in rồi", rows[1].SellerNote);
    }

    [Fact]
    public void Seller_note_mark_does_not_count_when_the_option_is_off()
    {
        var note = Order("NOTE", 2, [("IVAN20", 1)], sellerNote: "ĐÃ IN");
        var options = new ClassificationOptions(SellerNoteMarksPrinted: false, BlockOrdersWithNotes: false);

        var rows = builder.Build(
            new OrderListExportInput(OrderListExportScope.AllImported, [note], NoItems()),
            Context(options));

        Assert.Equal("Không", Assert.Single(rows).Printed);
        Assert.Empty(builder.Build(new OrderListExportInput(OrderListExportScope.PrintedOnly, [note], NoItems()), Context(options)));
    }

    [Fact]
    public void Latest_import_keeps_orders_last_seen_by_the_newest_import()
    {
        var old = Order("OLD", 1, [("IVAN20", 1)]) with { LastSeenImportId = 4 };
        var latest = Order("NEW", 2, [("IVAN20", 1)]) with { LastSeenImportId = 5 };

        var rows = Build(OrderListExportScope.LatestImport, [old, latest], latestImportId: 5);

        Assert.Equal(["NEW"], rows.Select(row => row.OrderId));
        Assert.Empty(Build(OrderListExportScope.LatestImport, [old, latest], latestImportId: null));
    }

    [Fact]
    public void Print_batch_keeps_only_the_orders_of_the_batch()
    {
        var a = Order("A", 1, [("IVAN20", 1)], printedBatchId: "B1", printedSource: PrintedSource.Batch);
        var b = Order("B", 2, [("IVAN20", 1)], isActive: false, printedBatchId: "B1", printedSource: PrintedSource.Batch);
        var c = Order("C", 3, [("IVAN20", 1)]);

        var rows = Build(OrderListExportScope.PrintBatch, [a, b, c], batchOrderIds: ["B", "A"]);

        Assert.Equal(["A", "B"], rows.Select(row => row.OrderId));
    }

    [Fact]
    public void Files_used_list_the_printed_items_of_each_line_with_partial_page_text()
    {
        var order = Order("P", 1, [("IVAN20", 2), ("CODY30", 1)], printedBatchId: "B1", printedSource: PrintedSource.Batch);
        var items = new Dictionary<string, IReadOnlyList<PrintBatchItemRecord>>(StringComparer.Ordinal)
        {
            ["P"] =
            [
                new("B1", 3, "P", "CODY30", 1, 1, @"C:\library\CODY - 9 PAGE - 01.10.pdf", null, 4),
                new("B1", 2, "P", "IVAN20", 2, 2, Ivan9.FilePath, null, 3),
                new("B1", 1, "P", "IVAN20", 1, 2, Ivan3.FilePath, null),
            ],
        };

        var rows = builder.Build(new OrderListExportInput(OrderListExportScope.AllImported, [order], items), Context());

        Assert.Equal(2, rows.Count);
        Assert.Equal("IVAN - 3 PAGE - 15.10.pdf; IVAN - 9 PAGE - 14.10.pdf (lấy 3/9 trang đầu)", rows[0].FilesUsed);
        // The CODY file is no longer in the library: the page total comes from its file name.
        Assert.Equal("CODY - 9 PAGE - 01.10.pdf (lấy 4/9 trang đầu)", rows[1].FilesUsed);
    }

    [Fact]
    public void Files_used_drop_the_page_total_only_when_a_removed_file_name_cannot_be_parsed()
    {
        var order = Order("P", 1, [("CODY30", 1)], printedBatchId: "B1", printedSource: PrintedSource.Batch);
        var items = new Dictionary<string, IReadOnlyList<PrintBatchItemRecord>>(StringComparer.Ordinal)
        {
            ["P"] = [new("B1", 1, "P", "CODY30", 1, 1, @"D:\old\cody ban cu.pdf", null, 4)],
        };

        var row = Assert.Single(builder.Build(new OrderListExportInput(OrderListExportScope.AllImported, [order], items), Context()));

        Assert.Equal("cody ban cu.pdf (lấy 4 trang đầu)", row.FilesUsed);
    }

    [Fact]
    public void Print_batch_uses_the_selected_batch_for_an_order_that_was_unmarked_later()
    {
        var unmarked = Order("U", 1, [("IVAN20", 1)]);
        var items = new Dictionary<string, IReadOnlyList<PrintBatchItemRecord>>(StringComparer.Ordinal)
        {
            ["U"] = [new("B1", 1, "U", "IVAN20", 1, 1, Ivan9.FilePath, null, 3)],
        };

        var row = Assert.Single(BuildBatch([unmarked], items, "B1"));

        Assert.Equal("Có", row.Printed);
        Assert.Equal("Lô in", row.MarkSource);
        Assert.Equal("B1", row.BatchId);
        Assert.Equal("15/10/2026 12:30:00", row.PrintedAt);
        Assert.Equal("IVAN - 9 PAGE - 14.10.pdf (lấy 3/9 trang đầu)", row.FilesUsed);
        // Status and reasons keep the current classification.
        Assert.Equal("Sẵn sàng", row.Status);
    }

    [Fact]
    public void Print_batch_uses_the_selected_batch_for_an_order_reprinted_in_a_later_batch()
    {
        var reprinted = Order("R", 1, [("IVAN20", 1)], printedBatchId: "B2", printedSource: PrintedSource.Batch);
        var items = new Dictionary<string, IReadOnlyList<PrintBatchItemRecord>>(StringComparer.Ordinal)
        {
            ["R"] = [new("B1", 1, "R", "IVAN20", 1, 1, Ivan3.FilePath, null)],
        };

        var row = Assert.Single(BuildBatch([reprinted], items, "B1"));

        Assert.Equal("Có", row.Printed);
        Assert.Equal("B1", row.BatchId);
        Assert.Equal("15/10/2026 12:30:00", row.PrintedAt);
        Assert.Equal("IVAN - 3 PAGE - 15.10.pdf", row.FilesUsed);
        Assert.Equal("Đã tạo file in", row.Status);
        Assert.Equal("Đã in (lô B2)", row.Reasons);
    }

    private IReadOnlyList<OrderListExportRow> BuildBatch(
        IReadOnlyList<TikTokOrder> orders,
        IReadOnlyDictionary<string, IReadOnlyList<PrintBatchItemRecord>> items,
        string batchId) =>
        builder.Build(
            new OrderListExportInput(
                OrderListExportScope.PrintBatch,
                orders,
                items,
                BatchOrderIds: items.Keys.ToList(),
                Batch: new OrderListExportBatch(batchId, new DateTimeOffset(2026, 10, 15, 5, 30, 0, TimeSpan.Zero))),
            Context());

    [Fact]
    public void Files_are_not_listed_for_orders_that_are_not_printed()
    {
        var order = Order("P", 1, [("IVAN20", 1)]);
        var items = new Dictionary<string, IReadOnlyList<PrintBatchItemRecord>>(StringComparer.Ordinal)
        {
            ["P"] = [new("B1", 1, "P", "IVAN20", 1, 1, Ivan3.FilePath, null)],
        };

        var row = Assert.Single(builder.Build(new OrderListExportInput(OrderListExportScope.AllImported, [order], items), Context()));

        Assert.Equal(string.Empty, row.FilesUsed);
    }

    [Fact]
    public void Reasons_are_joined_and_changed_after_print_is_flagged()
    {
        var missing = Order("MISS", 1, [("CODY30", 1), ("JACK30", 1)]);
        var changed = Order("CHG", 2, [("IVAN20", 1)], printedBatchId: "B1", printedSource: PrintedSource.Batch) with
        {
            ChangedAfterPrint = true,
        };

        var rows = Build(OrderListExportScope.AllImported, [missing, changed]);

        Assert.Equal("Thiếu file", rows[0].Status);
        Assert.Equal("Thiếu file kho CODY - 4 PAGE | Thiếu file kho JACK - 4 PAGE", rows[0].Reasons);
        Assert.All(rows.Where(row => row.OrderId == "MISS"), row => Assert.Equal(string.Empty, row.ChangedAfterPrint));
        Assert.Equal("Có", rows.Single(row => row.OrderId == "CHG").ChangedAfterPrint);
    }

    [Fact]
    public void One_row_per_line_sorted_by_created_at_order_id_and_line_number()
    {
        var late = Order("A-LATE", 5, [("IVAN20", 1)]);
        var tieB = Order("B", 1, [("IVAN20", 1)]);
        var tieA = Order("A", 1, [("IVAN20", 1)]) with
        {
            Lines =
            [
                new TikTokOrderLine("A", "KEY", 2, "Móc khóa", "Đỏ", 1),
                new TikTokOrderLine("A", "IVAN20", 1, "Card", "20 CARD", 3),
            ],
        };

        var rows = Build(OrderListExportScope.AllImported, [late, tieB, tieA]);

        Assert.Equal(["A", "A", "B", "A-LATE"], rows.Select(row => row.OrderId));
        Assert.Equal(["IVAN20", "KEY"], rows.Take(2).Select(row => row.SkuId));
        Assert.Equal("3", rows[0].Quantity);
        Assert.Equal("Card", rows[0].ProductName);
        Assert.Equal("20 CARD", rows[0].VariationName);
        Assert.Equal(rows[0].Status, rows[1].Status);
        Assert.Equal(rows[0].BuyerUsername, rows[1].BuyerUsername);
    }

    [Fact]
    public void Cells_follow_the_headers_and_ids_end_with_a_tab()
    {
        var order = Order("586621080534091589", 1, [("1729384756", 1)]);
        var row = Assert.Single(Build(OrderListExportScope.AllImported, [order]));

        var cells = row.ToCells();

        Assert.Equal(OrderListExportRow.Headers.Count, cells.Count);
        Assert.Equal(19, cells.Count);
        Assert.Equal("586621080534091589\t", cells[0]);
        Assert.Equal("1729384756\t", cells[9]);
        Assert.Equal("Order ID", OrderListExportRow.Headers[0]);
        Assert.Equal("Seller Note", OrderListExportRow.Headers[^1]);
        Assert.DoesNotContain(OrderListExportRow.Headers, header => header.Contains("Recipient", StringComparison.OrdinalIgnoreCase) || header.Contains("Phone", StringComparison.OrdinalIgnoreCase) || header.Contains("Địa chỉ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Order_without_lines_still_gets_one_row_without_a_sku_tab()
    {
        var order = Order("EMPTY", 1, []);

        var row = Assert.Single(Build(OrderListExportScope.AllImported, [order]));

        Assert.Equal(string.Empty, row.ToCells()[9]);
    }

    private IReadOnlyList<OrderListExportRow> Build(
        OrderListExportScope scope,
        IReadOnlyList<TikTokOrder> orders,
        long? latestImportId = null,
        IReadOnlyCollection<string>? batchOrderIds = null) =>
        builder.Build(new OrderListExportInput(scope, orders, NoItems(), latestImportId, batchOrderIds), Context());

    private static Dictionary<string, IReadOnlyList<PrintBatchItemRecord>> NoItems() => new(StringComparer.Ordinal);

    private static OrderListExportContext Context(ClassificationOptions? options = null) =>
        new(
            new Dictionary<long, string> { [1] = "Shop A" },
            Catalog,
            [Ivan3, Ivan9],
            options ?? new ClassificationOptions(),
            TikTokLabels.Of,
            Vietnam);
}
