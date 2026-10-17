using Microsoft.Data.Sqlite;
using PDFPageComposer.App.TikTok.Models;
using static PDFPageComposer.Tests.TikTok.Data.TikTokDatabaseFixture;

namespace PDFPageComposer.Tests.TikTok.Data;

public sealed class SqliteTikTokRepositoryOrderTests
{
    private static readonly TikTokOrderLine[] NoLines = [];

    [Fact]
    public async Task ApplyImportAsync_inserts_orders_and_round_trips_every_field()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var shop = await repository.AddShopAsync("Shop Bê", CancellationToken.None);
        var full = new TikTokOrder(
            "586610736328377928",
            shop.ShopId,
            "Chờ vận chuyển",
            VnTime(16, 8, 30),
            "n***h",
            "Gói kỹ giúp em ạ, cảm ơn shop!",
            "ĐÃ IN IVAN mini",
            true,
            FirstImportId: 777,
            LastSeenImportId: 778,
            "batch-old",
            new DateTimeOffset(2026, 10, 15, 21, 0, 0, TimeSpan.FromHours(-5)),
            PrintedSource.SellerNote,
            ChangedAfterPrint: true,
            "HASH-1",
            [Line("586610736328377928", "SKU-B", 2, 3), Line("586610736328377928", "SKU-A", 1)]);
        var earlier = Order("100", shop.ShopId, new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero), Line("100", "SKU-A", 1));

        var batch = await repository.ApplyImportAsync(
            ChangeSet(shop.ShopId, insert: [full, earlier], newSkus: [Sku("SKU-A")]),
            CancellationToken.None);

        Assert.True(batch.ImportId > 0);
        Assert.Equal(new ImportBatchRecord(batch.ImportId, shop.ShopId, "Chờ vận chuyển đơn hàng.csv", ImportMode.Full, VnTime(16, 8, 38), 2, 0, 2, 3, 0), batch);
        Assert.Equal([batch], await repository.GetImportBatchesAsync(CancellationToken.None));

        var orders = await repository.GetOrdersAsync(includeInactive: false, CancellationToken.None);

        // 16/10 08:30 +07:00 is 01:30 UTC, after 16/10 00:00 UTC.
        Assert.Equal(["100", "586610736328377928"], orders.Select(order => order.OrderId));
        var stored = orders[1];
        Assert.Equal(full with { FirstImportId = batch.ImportId, LastSeenImportId = batch.ImportId, Lines = NoLines }, stored with { Lines = NoLines });
        Assert.Equal(VietnamOffset, stored.CreatedAt.Offset);
        Assert.Equal(TimeSpan.FromHours(-5), stored.PrintedAt!.Value.Offset);
        Assert.Equal([full.Lines[1], full.Lines[0]], stored.Lines);
        Assert.Equal([Sku("SKU-A")], await repository.GetSkusAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ApplyImportAsync_updates_keeps_first_import_replaces_lines_and_deactivates()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var shop = await repository.AddShopAsync("Shop A", CancellationToken.None);
        var existingSku = Sku("SKU-A") with { ConfigStatus = SkuConfigStatus.Confirmed, Idol = "HIEUTHUHAI" };
        await repository.SaveSkusAsync([existingSku], CancellationToken.None);
        var first = await repository.ApplyImportAsync(
            ChangeSet(shop.ShopId, insert:
            [
                Order("1", shop.ShopId, VnTime(15, 9), Line("1", "SKU-A", 1), Line("1", "SKU-B", 2)),
                Order("2", shop.ShopId, VnTime(15, 10), Line("2", "SKU-A", 1)),
            ]),
            CancellationToken.None);

        var changed = Order("1", shop.ShopId, VnTime(15, 9), Line("1", "SKU-C", 1, 5)) with
        {
            StatusText = "Cần vận chuyển",
            SellerNote = "dã có qhung",
            FirstImportId = 999,
            ChangedAfterPrint = true,
        };
        var second = await repository.ApplyImportAsync(
            ChangeSet(shop.ShopId, update: [changed], deactivate: ["2"], newSkus: [Sku("SKU-A", SkuProcessType.Ignore), Sku("SKU-C")]),
            CancellationToken.None);

        Assert.True(second.ImportId > first.ImportId);
        Assert.Equal((0, 1, 1), (second.Inserted, second.Updated, second.Deactivated));
        Assert.Equal([second.ImportId, first.ImportId], (await repository.GetImportBatchesAsync(CancellationToken.None)).Select(batch => batch.ImportId));

        var all = await repository.GetOrdersAsync(includeInactive: true, CancellationToken.None);
        // File order: import by import, so order 2 (last seen in the first import) comes before order 1.
        Assert.Equal(["2", "1"], all.Select(order => order.OrderId));
        var updated = all[1];
        // ChangedAfterPrint is only raised for the batch the stored row is printed in; this row is not printed.
        Assert.Equal(
            changed with { FirstImportId = first.ImportId, LastSeenImportId = second.ImportId, ChangedAfterPrint = false, Lines = NoLines },
            updated with { Lines = NoLines });
        Assert.Equal(changed.Lines, updated.Lines);
        Assert.False(all[0].IsActive);
        Assert.Equal(first.ImportId, all[0].LastSeenImportId);

        Assert.Equal(["1"], (await repository.GetOrdersAsync(includeInactive: false, CancellationToken.None)).Select(order => order.OrderId));
        Assert.Equal(1, await fixture.CountAsync("OrderLine WHERE OrderId = '1'"));
        Assert.Equal([existingSku, Sku("SKU-C")], await repository.GetSkusAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ApplyImportAsync_rolls_back_everything_on_failure()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var shop = await repository.AddShopAsync("Shop A", CancellationToken.None);

        await Assert.ThrowsAsync<SqliteException>(() => repository.ApplyImportAsync(
            ChangeSet(shop.ShopId, insert:
            [
                Order("1", shop.ShopId, VnTime(15, 9), Line("1", "SKU-A", 1)),
                Order("1", shop.ShopId, VnTime(15, 9), Line("1", "SKU-B", 1)),
            ], newSkus: [Sku("SKU-A")]),
            CancellationToken.None));

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ApplyImportAsync(
            ChangeSet(shop.ShopId, insert: [Order("2", shop.ShopId, VnTime(15, 9))], update: [Order("missing", shop.ShopId, VnTime(15, 9))]),
            CancellationToken.None));

        foreach (var table in new[] { "ImportBatch", "\"Order\"", "OrderLine", "Sku" })
        {
            Assert.Equal(0, await fixture.CountAsync(table));
        }
    }

    [Fact]
    public async Task Order_queries_filter_by_shop_ids_and_activity()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var shopA = await repository.AddShopAsync("Shop A", CancellationToken.None);
        var shopB = await repository.AddShopAsync("Shop B", CancellationToken.None);
        var manyOrders = Enumerable.Range(0, 600)
            .Select(index => Order($"A{index:D4}", shopA.ShopId, VnTime(10, 8).AddMinutes(index), Line($"A{index:D4}", "SKU-A", 1)))
            .ToList();
        await repository.ApplyImportAsync(ChangeSet(shopA.ShopId, insert: manyOrders, deactivate: []), CancellationToken.None);
        await repository.ApplyImportAsync(
            ChangeSet(shopB.ShopId, insert: [Order("B1", shopB.ShopId, VnTime(9, 8))], deactivate: ["A0000"]),
            CancellationToken.None);

        var activeA = await repository.GetActiveOrdersForShopAsync(shopA.ShopId, CancellationToken.None);
        Assert.Equal(599, activeA.Count);
        Assert.Equal("A0001", activeA[0].OrderId);
        Assert.Equal(["B1"], (await repository.GetActiveOrdersForShopAsync(shopB.ShopId, CancellationToken.None)).Select(order => order.OrderId));

        var ids = manyOrders.Select(order => order.OrderId).Reverse().Append("B1").Append("A0005").Append("unknown").ToList();
        var byIds = await repository.GetOrdersByIdsAsync(ids, CancellationToken.None);
        Assert.Equal(601, byIds.Count);
        // File order: the shop A import first (then CreatedAt inside it), the later shop B import last.
        Assert.Equal("A0000", byIds[0].OrderId);
        Assert.False(byIds[0].IsActive);
        Assert.Equal("B1", byIds[^1].OrderId);
        Assert.All(byIds.Take(600), order => Assert.Single(order.Lines));
        Assert.Empty(await repository.GetOrdersByIdsAsync([], CancellationToken.None));
    }

    [Fact]
    public async Task CommitPrintBatchAsync_marks_orders_and_printed_items_follow_current_batch()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var shop = await repository.AddShopAsync("Shop A", CancellationToken.None);
        await repository.ApplyImportAsync(
            ChangeSet(shop.ShopId, insert:
            [
                Order("1", shop.ShopId, VnTime(15, 9), Line("1", "SKU-A", 1, 2)),
                Order("2", shop.ShopId, VnTime(15, 10), Line("2", "SKU-B", 1)),
                Order("3", shop.ShopId, VnTime(15, 11), Line("3", "SKU-C", 1)),
            ]),
            CancellationToken.None);

        var batch1 = new PrintBatchRecord("B-1", VnTime(16, 9), @"D:\Xuất\lô 1.pdf", SeparatorMode.ComplexOnly, 2, 12, "Exported");
        PrintBatchItemRecord[] items1 =
        [
            new("B-1", 2, "1", "SKU-A", 2, 2, @"D:\Thư viện\IVAN - 4 PAGE - 15.10.pdf", "Trùng ảnh"),
            new("B-1", 1, "1", "SKU-A", 1, 2, @"D:\Thư viện\IVAN - 4 PAGE - 15.10.pdf", null),
            new("B-1", 3, "2", "SKU-B", 1, 1, @"D:\Thư viện\HTH - 4 PAGE - 15.10.pdf", null),
        ];
        await repository.CommitPrintBatchAsync(batch1, items1, ["1", "2"], CancellationToken.None);
        var batch2 = new PrintBatchRecord("B-2", VnTime(16, 10), @"D:\Xuất\lô 2.pdf", SeparatorMode.All, 1, 4, "Exported");
        PrintBatchItemRecord[] items2 = [new("B-2", 1, "1", "SKU-A", 1, 2, @"D:\Thư viện\IVAN - 4 PAGE - 15.10.pdf", null)];
        await repository.CommitPrintBatchAsync(batch2, items2, ["1"], CancellationToken.None);

        var orders = await repository.GetOrdersByIdsAsync(["1", "2", "3"], CancellationToken.None);
        Assert.Equal(((string?)"B-2", (DateTimeOffset?)batch2.CreatedAt, (PrintedSource?)PrintedSource.Batch, false),(orders[0].PrintedBatchId, orders[0].PrintedAt, orders[0].PrintedSource, orders[0].ChangedAfterPrint));
        Assert.Equal(VietnamOffset, orders[0].PrintedAt!.Value.Offset);
        Assert.Equal("B-1", orders[1].PrintedBatchId);
        Assert.Null(orders[2].PrintedBatchId);

        Assert.Equal([batch2, batch1], await repository.GetPrintBatchesAsync(CancellationToken.None));
        Assert.Equal([items1[1], items1[0], items1[2]], await repository.GetPrintBatchItemsAsync("B-1", CancellationToken.None));

        var printed = await repository.GetPrintedItemsForOrdersAsync(["1", "2", "3", "unknown"], CancellationToken.None);
        Assert.Equal(["1", "2"], printed.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(items2, printed["1"]);
        Assert.Equal([items1[2]], printed["2"]);

        await repository.UnmarkPrintedAsync("1", CancellationToken.None);
        var unmarked = (await repository.GetOrdersByIdsAsync(["1"], CancellationToken.None))[0];
        Assert.Equal(((string?)null, (DateTimeOffset?)null, (PrintedSource?)null, false),(unmarked.PrintedBatchId, unmarked.PrintedAt, unmarked.PrintedSource, unmarked.ChangedAfterPrint));
        Assert.False((await repository.GetPrintedItemsForOrdersAsync(["1"], CancellationToken.None)).ContainsKey("1"));
        Assert.Equal(4, await fixture.CountAsync("PrintBatchItem"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UnmarkPrintedAsync("unknown", CancellationToken.None));
    }

    [Fact]
    public async Task CommitPrintBatchAsync_rolls_back_when_an_order_is_missing()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var shop = await repository.AddShopAsync("Shop A", CancellationToken.None);
        await repository.ApplyImportAsync(ChangeSet(shop.ShopId, insert: [Order("1", shop.ShopId, VnTime(15, 9))]), CancellationToken.None);
        var batch = new PrintBatchRecord("B-1", VnTime(16, 9), @"D:\out.pdf", SeparatorMode.None, 2, 2, "Exported");

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CommitPrintBatchAsync(
            batch,
            [new PrintBatchItemRecord("B-1", 1, "1", "SKU-A", 1, 1, @"D:\a.pdf", null)],
            ["1", "missing"],
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CommitPrintBatchAsync(
            batch,
            [new PrintBatchItemRecord("OTHER", 1, "1", "SKU-A", 1, 1, @"D:\a.pdf", null)],
            ["1"],
            CancellationToken.None));

        Assert.Equal(0, await fixture.CountAsync("PrintBatch"));
        Assert.Equal(0, await fixture.CountAsync("PrintBatchItem"));
        Assert.Null((await repository.GetOrdersByIdsAsync(["1"], CancellationToken.None))[0].PrintedBatchId);
    }

    [Fact]
    public async Task SetNoteReviewedAsync_sets_and_clears_hash()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var shop = await repository.AddShopAsync("Shop A", CancellationToken.None);
        await repository.ApplyImportAsync(ChangeSet(shop.ShopId, insert: [Order("1", shop.ShopId, VnTime(15, 9))]), CancellationToken.None);

        await repository.SetNoteReviewedAsync("1", "ABC123", CancellationToken.None);
        Assert.Equal("ABC123", (await repository.GetOrdersByIdsAsync(["1"], CancellationToken.None))[0].NoteReviewedHash);

        await repository.SetNoteReviewedAsync("1", null, CancellationToken.None);
        Assert.Null((await repository.GetOrdersByIdsAsync(["1"], CancellationToken.None))[0].NoteReviewedHash);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SetNoteReviewedAsync("missing", "X", CancellationToken.None));
    }

    [Fact]
    public async Task CommitPrintBatchAsync_replay_of_outdated_quantities_keeps_changed_after_print()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var shop = await repository.AddShopAsync("Shop A", CancellationToken.None);
        var order = Order("1", shop.ShopId, VnTime(15, 9), Line("1", "SKU-A", 1, 1));
        await repository.ApplyImportAsync(ChangeSet(shop.ShopId, insert: [order]), CancellationToken.None);
        await repository.CommitPrintBatchAsync(
            new PrintBatchRecord("A", VnTime(16, 9), @"D:\a.pdf", SeparatorMode.None, 1, 3, "Completed"),
            [new PrintBatchItemRecord("A", 1, "1", "SKU-A", 1, 1, @"D:\lib.pdf", null)],
            ["1"],
            CancellationToken.None);
        Assert.False((await repository.GetOrdersByIdsAsync(["1"], CancellationToken.None))[0].ChangedAfterPrint);

        // The buyer changed the quantity to 2; the import flags the order.
        var stored = (await repository.GetOrdersByIdsAsync(["1"], CancellationToken.None))[0];
        await repository.ApplyImportAsync(
            ChangeSet(shop.ShopId, update: [stored with { ChangedAfterPrint = true, Lines = [Line("1", "SKU-A", 1, 2)] }]),
            CancellationToken.None);
        Assert.True((await repository.GetOrdersByIdsAsync(["1"], CancellationToken.None))[0].ChangedAfterPrint);

        // "Tạo lại" replays batch A with its quantity 1.
        await repository.CommitPrintBatchAsync(
            new PrintBatchRecord("B", VnTime(16, 10), @"D:\b.pdf", SeparatorMode.None, 1, 3, "Completed"),
            [new PrintBatchItemRecord("B", 1, "1", "SKU-A", 1, 1, @"D:\lib.pdf", null)],
            ["1"],
            CancellationToken.None);
        var replayed = (await repository.GetOrdersByIdsAsync(["1"], CancellationToken.None))[0];
        Assert.Equal("B", replayed.PrintedBatchId);
        Assert.True(replayed.ChangedAfterPrint);

        // A normal batch built from the current lines clears the warning.
        await repository.CommitPrintBatchAsync(
            new PrintBatchRecord("C", VnTime(16, 11), @"D:\c.pdf", SeparatorMode.None, 1, 6, "Completed"),
            [
                new PrintBatchItemRecord("C", 1, "1", "SKU-A", 1, 2, @"D:\lib.pdf", null),
                new PrintBatchItemRecord("C", 2, "1", "SKU-A", 2, 2, @"D:\lib.pdf", null),
            ],
            ["1"],
            CancellationToken.None);
        Assert.False((await repository.GetOrdersByIdsAsync(["1"], CancellationToken.None))[0].ChangedAfterPrint);
    }

    [Fact]
    public async Task ApplyImportAsync_does_not_revert_print_or_review_changes_made_after_the_order_was_read()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var shop = await repository.AddShopAsync("Shop A", CancellationToken.None);
        await repository.ApplyImportAsync(
            ChangeSet(shop.ShopId, insert: [Order("1", shop.ShopId, VnTime(15, 9), Line("1", "SKU-A", 1))]),
            CancellationToken.None);

        // The import reads the order while it is still unprinted...
        var snapshot = (await repository.GetOrdersByIdsAsync(["1"], CancellationToken.None))[0];

        // ...meanwhile another window prints it and approves its note.
        await repository.CommitPrintBatchAsync(
            new PrintBatchRecord("B1", VnTime(16, 9), @"D:\b1.pdf", SeparatorMode.None, 1, 3, "Completed"),
            [new PrintBatchItemRecord("B1", 1, "1", "SKU-A", 1, 1, @"D:\lib.pdf", null)],
            ["1"],
            CancellationToken.None);
        await repository.SetNoteReviewedAsync("1", "HASH", CancellationToken.None);

        await repository.ApplyImportAsync(
            ChangeSet(shop.ShopId, update: [snapshot with { StatusText = "Cần vận chuyển" }]),
            CancellationToken.None);

        var stored = (await repository.GetOrdersByIdsAsync(["1"], CancellationToken.None))[0];
        Assert.Equal("Cần vận chuyển", stored.StatusText);
        Assert.Equal("B1", stored.PrintedBatchId);
        Assert.Equal(PrintedSource.Batch, stored.PrintedSource);
        Assert.NotNull(stored.PrintedAt);
        Assert.Equal("HASH", stored.NoteReviewedHash);
        Assert.False(stored.ChangedAfterPrint);
    }

    [Fact]
    public async Task DeleteImportAsync_removes_orders_seen_only_in_that_import_and_DeleteAllOrdersAsync_clears_everything()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var shop = await repository.AddShopAsync("Shop A", CancellationToken.None);
        var first = await repository.ApplyImportAsync(
            ChangeSet(shop.ShopId, insert:
            [
                Order("1", shop.ShopId, VnTime(15, 9), Line("1", "SKU-A", 1)),
                Order("2", shop.ShopId, VnTime(15, 10), Line("2", "SKU-A", 1)),
            ]),
            CancellationToken.None);
        var second = await repository.ApplyImportAsync(
            ChangeSet(
                shop.ShopId,
                insert: [Order("3", shop.ShopId, VnTime(16, 9), Line("3", "SKU-A", 1))],
                update: [Order("2", shop.ShopId, VnTime(15, 10), Line("2", "SKU-A", 1))]),
            CancellationToken.None);

        Assert.Equal(1, await repository.DeleteImportAsync(second.ImportId, CancellationToken.None));

        var orders = await repository.GetOrdersAsync(includeInactive: true, CancellationToken.None);
        Assert.Equal(["1", "2"], orders.Select(order => order.OrderId));
        Assert.All(orders, order => Assert.Equal(first.ImportId, order.LastSeenImportId));
        Assert.All(orders, order => Assert.Single(order.Lines));
        Assert.Equal([first.ImportId], (await repository.GetImportBatchesAsync(CancellationToken.None)).Select(batch => batch.ImportId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.DeleteImportAsync(second.ImportId, CancellationToken.None));

        Assert.Equal(2, await repository.DeleteAllOrdersAsync(CancellationToken.None));
        Assert.Empty(await repository.GetOrdersAsync(includeInactive: true, CancellationToken.None));
        Assert.Empty(await repository.GetImportBatchesAsync(CancellationToken.None));
        Assert.Single(await repository.GetShopsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Orders_of_one_import_keep_the_csv_row_order_not_the_created_time()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var shop = await repository.AddShopAsync("Shop A", CancellationToken.None);
        await repository.ApplyImportAsync(
            ChangeSet(shop.ShopId, insert:
            [
                Order("late", shop.ShopId, VnTime(16, 22, 7)) with { SourceRow = 2 },
                Order("early", shop.ShopId, VnTime(16, 21, 0)) with { SourceRow = 4 },
                Order("first", shop.ShopId, VnTime(16, 23, 0)) with { SourceRow = 1 },
            ]),
            CancellationToken.None);

        var orders = await repository.GetOrdersAsync(includeInactive: false, CancellationToken.None);

        Assert.Equal(["first", "late", "early"], orders.Select(order => order.OrderId));
        Assert.Equal([1, 2, 4], orders.Select(order => order.SourceRow));
    }

    [Fact]
    public async Task File_overrides_are_replaced_per_order_and_deleted_with_the_orders()
    {
        using var fixture = await CreateAsync();
        var repository = fixture.Repository;
        var shop = await repository.AddShopAsync("Shop A", CancellationToken.None);
        await repository.ApplyImportAsync(
            ChangeSet(shop.ShopId, insert:
            [
                Order("1", shop.ShopId, VnTime(15, 9), Line("1", "SKU-A", 1)),
                Order("2", shop.ShopId, VnTime(15, 10), Line("2", "SKU-A", 1)),
            ]),
            CancellationToken.None);
        var first = new FileOverride("1", "SKU-A", 1, 1, @"C:\lib\A.pdf", 2);
        var second = new FileOverride("1", "SKU-A", 1, 2, @"C:\lib\B.pdf", 1, FirstPage: 3);
        var other = new FileOverride("2", "SKU-A", 1, 1, @"C:\lib\C.pdf", 3);

        await repository.SaveFileOverridesAsync("1", [second, first], CancellationToken.None);
        await repository.SaveFileOverridesAsync("2", [other], CancellationToken.None);
        Assert.Equal([first, second, other], await repository.GetFileOverridesAsync(CancellationToken.None));

        await repository.SaveFileOverridesAsync("1", [], CancellationToken.None);
        Assert.Equal([other], await repository.GetFileOverridesAsync(CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveFileOverridesAsync("1", [other], CancellationToken.None));

        await repository.DeleteAllOrdersAsync(CancellationToken.None);
        Assert.Empty(await repository.GetFileOverridesAsync(CancellationToken.None));
    }
}
