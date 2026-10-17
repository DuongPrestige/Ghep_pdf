using PDFPageComposer.App.TikTok.Matching;
using PDFPageComposer.App.TikTok.Models;
using static PDFPageComposer.Tests.TikTok.Matching.MatchingTestData;

namespace PDFPageComposer.Tests.TikTok.Matching;

public sealed class PrintPlanBuilderTests
{
    private static readonly PdfAsset Ivan1510 = Asset("IVAN", 3, 15);
    private static readonly PdfAsset Ivan1010 = Asset("IVAN", 3, 10);
    private static readonly PdfAsset Cody1410 = Asset("CODY", 4, 14);

    private static readonly IReadOnlyList<PdfAsset> Library = [Ivan1010, Cody1410, Ivan1510];

    private static readonly IReadOnlyDictionary<string, SkuConfig> PrdSkus = Skus(
        Auto("CODY30", "CODY", 30, "30 CARD, 1 Mặt"),
        Auto("IVAN20", "IVAN", 20, "20 CARD, 1 Mặt"),
        Manual("IVAN2M"),
        Ignore("KEYCHAIN_SPOTIFY"),
        Ignore("KEYCHAIN_ALBUM"),
        Ignore("LABEL_CORTIS"));

    [Fact]
    public void Idol_aliases_map_confirmed_sku_idols_to_library_files()
    {
        var skus = Skus(Auto("CODY20", "CODY NAM VO", 20));
        var orders = new[] { Order("O1", 0, [("CODY20", 1)]) };
        var library = new[] { Asset("CODY", 3, 14) };

        var withoutAlias = CreateBuilder().Build(orders, skus, library, SeparatorMode.None, new ClassificationOptions());
        var withAlias = CreateBuilder().Build(orders, skus, library, SeparatorMode.None,
            new ClassificationOptions(IdolAliases: [new IdolAlias("CODYNAMVO", "CODY")]));

        Assert.Equal(OrderCategory.MissingFile, Assert.Single(withoutAlias.Rejected).Category);
        var copy = Assert.Single(Assert.Single(withAlias.Orders).Copies);
        Assert.Equal(library[0], copy.Asset);
    }

    [Fact]
    public void Missing_page_count_is_composed_from_the_newest_files_and_every_order_restarts_at_the_newest()
    {
        var skus = Skus(Auto("JACK50", "JACK", 50));
        var orders = new[] { Order("O1", 0, [("JACK50", 1)]), Order("O2", 1, [("JACK50", 1)]) };
        var library = new[]
        {
            Asset("JACK", 9, 18, 4), Asset("JACK", 4, 12, 5), Asset("JACK", 1, 6, 9),
            Asset("JACK", 4, 31, 8), Asset("JACK", 1, 13, 9),
        };

        var off = CreateBuilder().Build(orders, skus, library, SeparatorMode.None, new ClassificationOptions());
        var on = CreateBuilder().Build(orders, skus, library, SeparatorMode.None, new ClassificationOptions(AlwaysUseNewestFiles: true));

        Assert.Equal(2, off.Rejected.Count);
        Assert.All(off.Rejected, rejected => Assert.Equal(OrderCategory.MissingFile, rejected.Category));
        Assert.Equal(2, on.Orders.Count);
        foreach (var planOrder in on.Orders)
        {
            Assert.Equal(
                [(library[4], 1), (library[2], 1), (library[3], 4)],
                planOrder.Copies.Select(copy => (copy.Asset, copy.PageCount)));
            Assert.All(planOrder.Copies, copy => Assert.Equal(1, copy.CopyIndex));
            Assert.All(planOrder.Copies, copy => Assert.False(copy.DuplicateImage));
            Assert.Equal(6, planOrder.SheetCount);
        }
    }

    [Fact]
    public void Composed_set_takes_only_the_first_pages_of_its_last_file_and_the_next_copy_moves_on()
    {
        var skus = Skus(Auto("JACK30", "JACK", 30));
        var orders = new[] { Order("O1", 0, [("JACK30", 2)]) };
        var library = new[] { Asset("JACK", 1, 13, 9), Asset("JACK", 9, 31, 8), Asset("JACK", 2, 12, 5) };

        var plan = CreateBuilder().Build(orders, skus, library, SeparatorMode.None, new ClassificationOptions(AlwaysUseNewestFiles: true));

        var copies = Assert.Single(plan.Orders).Copies;
        Assert.Equal(
            [(library[0], 1, 1), (library[1], 3, 1), (library[2], 2, 2), (library[0], 1, 2), (library[1], 1, 2)],
            copies.Select(copy => (copy.Asset, copy.PageCount, copy.CopyIndex)));
        Assert.True(copies[1].UsesPartOfFile);
        Assert.False(copies[0].UsesPartOfFile);
        Assert.All(copies, copy => Assert.True(copy.DuplicateImage));
    }

    [Fact]
    public void Newest_file_wins_over_an_older_exact_page_count_file()
    {
        // DINH BAC 20 card = 3 pages: the first 3 pages of the newest 5-page file, not the older 3-page file.
        var skus = Skus(Auto("DB20", "DINH BAC", 20));
        var orders = new[] { Order("O1", 0, [("DB20", 1)]) };
        var library = new[] { Asset("DINH BAC", 3, 31, 7), Asset("DINH BAC", 5, 24, 9) };

        var newest = CreateBuilder().Build(orders, skus, library, SeparatorMode.None, new ClassificationOptions(AlwaysUseNewestFiles: true));
        var exactOnly = CreateBuilder().Build(orders, skus, library, SeparatorMode.None, new ClassificationOptions());

        var copy = Assert.Single(Assert.Single(newest.Orders).Copies);
        Assert.Equal(library[1], copy.Asset);
        Assert.Equal(3, copy.PageCount);
        Assert.True(copy.UsesPartOfFile);
        Assert.Equal(library[0], Assert.Single(Assert.Single(exactOnly.Orders).Copies).Asset);
    }

    private static PrintPlanBuilder CreateBuilder() => new(new OrderClassifier());

    [Fact]
    public void Build_matches_prd_section_7_example()
    {
        var orders = new[]
        {
            Order("O6", 5, [("IVAN20", 1)], sellerNote: "ĐÃ IN IVAN mini"),
            Order("O3", 2, [("IVAN2M", 1), ("KEYCHAIN_ALBUM", 1)]),
            Order("O1", 0, [("CODY30", 1)]),
            Order("O5", 4, [("CODY30", 1)], buyerMessage: "cho em ảnh khác nhé"),
            Order("O2", 1, [("IVAN20", 2), ("KEYCHAIN_SPOTIFY", 1)]),
            Order("O4", 3, [("LABEL_CORTIS", 1)]),
        };

        var plan = CreateBuilder().Build(orders, PrdSkus, Library, SeparatorMode.ComplexOnly, new ClassificationOptions());

        Assert.Equal(["O1", "O2"], plan.Orders.Select(order => order.Order.OrderId));

        var o1 = plan.Orders[0];
        Assert.False(o1.IncludeSeparator);
        Assert.Equal([Cody1410], o1.Copies.Select(copy => copy.Asset));
        Assert.Equal(4, o1.SheetCount);

        var o2 = plan.Orders[1];
        Assert.True(o2.IncludeSeparator);
        Assert.Equal([Ivan1510, Ivan1010], o2.Copies.Select(copy => copy.Asset));
        Assert.Equal([1, 2], o2.Copies.Select(copy => copy.CopyIndex));
        Assert.All(o2.Copies, copy => Assert.Equal("IVAN20", copy.SkuId));
        Assert.All(o2.Copies, copy => Assert.False(copy.DuplicateImage));
        Assert.Equal(6, o2.SheetCount);

        Assert.Equal(11, plan.TotalPages);
        Assert.Equal(SeparatorMode.ComplexOnly, plan.SeparatorMode);

        var rejected = plan.Rejected.ToDictionary(item => item.OrderId, item => item.Category);
        Assert.Equal(["O3", "O4", "O5", "O6"], plan.Rejected.Select(item => item.OrderId));
        Assert.Equal(OrderCategory.Manual, rejected["O3"]);
        Assert.Equal(OrderCategory.NoCardNeeded, rejected["O4"]);
        Assert.Equal(OrderCategory.HasNote, rejected["O5"]);
        Assert.Equal(OrderCategory.Printed, rejected["O6"]);
    }

    [Theory]
    [InlineData(SeparatorMode.None, 10)]
    [InlineData(SeparatorMode.ComplexOnly, 11)]
    [InlineData(SeparatorMode.All, 12)]
    public void Build_applies_separator_mode(SeparatorMode mode, int expectedPages)
    {
        var orders = new[]
        {
            Order("O1", 0, [("CODY30", 1)]),
            Order("O2", 1, [("IVAN20", 2)]),
        };

        var plan = CreateBuilder().Build(orders, PrdSkus, Library, mode, new ClassificationOptions());

        Assert.Equal(expectedPages, plan.TotalPages);
    }

    [Fact]
    public void Build_marks_order_with_two_auto_lines_as_complex()
    {
        var plan = CreateBuilder().Build(
            [Order("O1", 0, [("CODY30", 1), ("IVAN20", 1)])],
            PrdSkus,
            Library,
            SeparatorMode.ComplexOnly,
            new ClassificationOptions());

        var order = Assert.Single(plan.Orders);
        Assert.True(order.IncludeSeparator);
        Assert.Equal([1, 2], order.Copies.Select(copy => copy.LineNo));
    }

    [Fact]
    public void Build_gives_every_order_the_newest_file()
    {
        var orders = new[]
        {
            Order("A", 0, [("IVAN20", 1)]),
            Order("B", 1, [("IVAN20", 1)]),
            Order("C", 2, [("IVAN20", 1)]),
        };

        var plan = CreateBuilder().Build(orders, PrdSkus, Library, SeparatorMode.None, new ClassificationOptions());

        Assert.Equal(
            [Ivan1510, Ivan1510, Ivan1510],
            plan.Orders.SelectMany(order => order.Copies).Select(copy => copy.Asset));
    }

    [Fact]
    public void Build_copies_inside_one_order_still_use_different_files()
    {
        var plan = CreateBuilder().Build(
            [Order("A", 0, [("IVAN20", 2)]), Order("B", 1, [("IVAN20", 1)])],
            PrdSkus,
            Library,
            SeparatorMode.None,
            new ClassificationOptions());

        Assert.Equal([Ivan1510, Ivan1010], plan.Orders[0].Copies.Select(copy => copy.Asset));
        Assert.Equal(Ivan1510, Assert.Single(plan.Orders[1].Copies).Asset);
    }

    [Fact]
    public void Build_sorts_by_created_at_then_order_id()
    {
        var orders = new[]
        {
            Order("B", 0, [("IVAN20", 1)]),
            Order("A", 0, [("IVAN20", 1)]),
            Order("0", 5, [("IVAN20", 1)]),
        };

        var plan = CreateBuilder().Build(orders, PrdSkus, Library, SeparatorMode.None, new ClassificationOptions());

        Assert.Equal(["A", "B", "0"], plan.Orders.Select(order => order.Order.OrderId));
        Assert.Equal(Ivan1510, plan.Orders[0].Copies[0].Asset);
    }

    [Fact]
    public void Build_flags_duplicate_image_when_quantity_exceeds_files()
    {
        var plan = CreateBuilder().Build(
            [Order("O1", 0, [("IVAN20", 3)])],
            PrdSkus,
            Library,
            SeparatorMode.None,
            new ClassificationOptions());

        var copies = Assert.Single(plan.Orders).Copies;
        Assert.Equal([Ivan1510, Ivan1010, Ivan1510], copies.Select(copy => copy.Asset));
        Assert.All(copies, copy => Assert.True(copy.DuplicateImage));
        Assert.All(copies, copy => Assert.Equal(3, copy.Quantity));
    }

    [Fact]
    public void Build_rejected_orders_do_not_change_files_of_other_orders()
    {
        var orders = new[]
        {
            Order("A", 0, [("IVAN20", 1)]),
            Order("B", 1, [("IVAN20", 1)], buyerMessage: "ghi chú"),
            Order("C", 2, [("IVAN20", 1), ("CODY30", 1)], sellerNote: "giao trước"),
            Order("D", 3, [("IVAN20", 1)]),
        };

        var plan = CreateBuilder().Build(orders, PrdSkus, [Ivan1510, Ivan1010], SeparatorMode.None, new ClassificationOptions());

        Assert.Equal(["A", "D"], plan.Orders.Select(order => order.Order.OrderId));
        Assert.Equal([Ivan1510, Ivan1510], plan.Orders.SelectMany(order => order.Copies).Select(copy => copy.Asset));
        Assert.Equal(["B", "C"], plan.Rejected.Select(item => item.OrderId));
    }

    [Fact]
    public void Build_missing_file_order_does_not_shift_cursor()
    {
        var orders = new[]
        {
            Order("A", 0, [("IVAN20", 1), ("CODY30", 1)]),
            Order("B", 1, [("IVAN20", 1)]),
        };

        var plan = CreateBuilder().Build(orders, PrdSkus, [Ivan1510, Ivan1010], SeparatorMode.None, new ClassificationOptions());

        var planned = Assert.Single(plan.Orders);
        Assert.Equal("B", planned.Order.OrderId);
        Assert.Equal(Ivan1510, Assert.Single(planned.Copies).Asset);
        Assert.Equal(OrderCategory.MissingFile, Assert.Single(plan.Rejected).Category);
    }

    [Fact]
    public void Build_ignores_inactive_orders_entirely()
    {
        var orders = new[]
        {
            Order("A", 0, [("IVAN20", 1)], isActive: false),
            Order("B", 1, [("IVAN20", 1)]),
        };

        var plan = CreateBuilder().Build(orders, PrdSkus, Library, SeparatorMode.None, new ClassificationOptions());

        Assert.Equal(["B"], plan.Orders.Select(order => order.Order.OrderId));
        Assert.Equal(Ivan1510, plan.Orders[0].Copies[0].Asset);
        Assert.Empty(plan.Rejected);
    }

    [Fact]
    public void Build_cursor_resets_for_each_batch()
    {
        var builder = CreateBuilder();
        var orders = new[] { Order("A", 0, [("IVAN20", 1)]) };

        var first = builder.Build(orders, PrdSkus, Library, SeparatorMode.None, new ClassificationOptions());
        var second = builder.Build(orders, PrdSkus, Library, SeparatorMode.None, new ClassificationOptions());

        Assert.Equal(Ivan1510, first.Orders[0].Copies[0].Asset);
        Assert.Equal(Ivan1510, second.Orders[0].Copies[0].Asset);
    }

    [Fact]
    public void Build_reprint_option_includes_printed_orders()
    {
        var orders = new[]
        {
            Order("A", 0, [("IVAN20", 1)], printedBatchId: "B1", printedSource: PrintedSource.Batch),
            Order("B", 1, [("IVAN20", 1)], sellerNote: "ĐÃ IN"),
        };

        var normal = CreateBuilder().Build(orders, PrdSkus, Library, SeparatorMode.None, new ClassificationOptions());
        var reprint = CreateBuilder().Build(orders, PrdSkus, Library, SeparatorMode.None, new ClassificationOptions(AllowReprint: true));

        Assert.Empty(normal.Orders);
        Assert.All(normal.Rejected, item => Assert.Equal(OrderCategory.Printed, item.Category));
        // "In lại" on a printed order counts as having read its notes (e.g. the "ĐÃ IN" written after printing).
        Assert.Equal(["A", "B"], reprint.Orders.Select(order => order.Order.OrderId));
        Assert.Empty(reprint.Rejected);
    }

    [Fact]
    public void Build_reprint_plans_printed_order_whose_seller_note_says_printed()
    {
        var order = Order("X", 0, [("IVAN20", 1)], sellerNote: "ĐÃ IN", printedBatchId: "B1", printedSource: PrintedSource.Batch);

        var plan = CreateBuilder().Build([order], PrdSkus, Library, SeparatorMode.None, new ClassificationOptions(AllowReprint: true));

        Assert.Equal(["X"], plan.Orders.Select(planned => planned.Order.OrderId));
    }

    [Fact]
    public void Build_reprint_option_still_blocks_unprinted_orders_with_notes()
    {
        var orders = new[]
        {
            Order("A", 0, [("IVAN20", 1)], printedBatchId: "B1", printedSource: PrintedSource.Batch),
            Order("N", 1, [("IVAN20", 1)], buyerMessage: "cho em ảnh khác"),
        };

        var plan = CreateBuilder().Build(orders, PrdSkus, Library, SeparatorMode.None, new ClassificationOptions(AllowReprint: true));

        Assert.Equal(["A"], plan.Orders.Select(planned => planned.Order.OrderId));
        Assert.Equal(OrderCategory.HasNote, Assert.Single(plan.Rejected).Category);
    }

    [Fact]
    public void Build_uses_file_path_as_tie_breaker_for_same_date()
    {
        var second = Asset("IVAN", 3, 15) with { FilePath = @"C:\library\b\IVAN - 3 PAGE - 15.10.pdf" };
        var first = Asset("IVAN", 3, 15) with { FilePath = @"C:\library\A\IVAN - 3 PAGE - 15.10.pdf" };

        var plan = CreateBuilder().Build(
            [Order("A", 0, [("IVAN20", 2)])],
            PrdSkus,
            [second, first],
            SeparatorMode.None,
            new ClassificationOptions());

        Assert.Equal([first, second], plan.Orders[0].Copies.Select(copy => copy.Asset));
    }

    [Fact]
    public void Files_chosen_by_hand_replace_the_automatic_choice()
    {
        var skus = Skus(Auto("CODY20", "CODY", 20), Auto("NQC40", "NHA QUE CA", 40));
        var library = new[] { Asset("CODY", 1, 6, 10), Asset("CODY", 9, 1, 1) };
        var orders = new[] { Order("O1", 0, [("CODY20", 1)]), Order("O2", 1, [("NQC40", 1)]) };
        var chosen = new ClassificationOptions(
            AlwaysUseNewestFiles: true,
            FileOverrides:
            [
                new FileOverride("O1", "CODY20", 1, 2, library[0].FilePath, 1),
                new FileOverride("O1", "CODY20", 1, 1, library[1].FilePath, 2, FirstPage: 5),
                new FileOverride("O2", "NQC40", 1, 1, library[1].FilePath, 5),
            ]);

        var plan = CreateBuilder().Build(orders, skus, library, SeparatorMode.None, chosen);

        Assert.Empty(plan.Rejected);
        var o1 = plan.Orders[0].Copies;
        Assert.Equal([(library[1], 5, 2), (library[0], 1, 1)], o1.Select(copy => (copy.Asset, copy.FirstPage, copy.PageCount)));
        Assert.All(o1, copy => Assert.True(copy.IsManualChoice));
        // No NHA QUE CA file exists, but the file chosen by hand is enough.
        Assert.Equal(5, Assert.Single(plan.Orders[1].Copies).PageCount);

        var gone = chosen with { FileOverrides = [new FileOverride("O2", "NQC40", 1, 1, @"C:\library\gone.pdf", 5)] };
        var rejected = CreateBuilder().Build([orders[1]], skus, library, SeparatorMode.None, gone);
        Assert.Equal(OrderCategory.MissingFile, Assert.Single(rejected.Rejected).Category);
    }
}
