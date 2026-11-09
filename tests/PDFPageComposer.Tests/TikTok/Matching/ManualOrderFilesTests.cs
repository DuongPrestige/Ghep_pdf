using System.IO;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Matching;
using PDFPageComposer.App.TikTok.Models;
using static PDFPageComposer.Tests.TikTok.Matching.MatchingTestData;

namespace PDFPageComposer.Tests.TikTok.Matching;

/// <summary>Manual products (A4/A5 cards, photobooth, mini album…) printed from files chosen by hand (2026-11-08).</summary>
public sealed class ManualOrderFilesTests : IDisposable
{
    private static readonly PdfAsset Ivan3 = Asset("IVAN", 3, 15);
    private static readonly IReadOnlyList<PdfAsset> Library = [Ivan3];

    private static readonly IReadOnlyDictionary<string, SkuConfig> SkuMap = Skus(
        Auto("IVAN20", "IVAN", 20),
        Sku("A4", SkuProcessType.Manual, SkuConfigStatus.Confirmed, null, null, "1 mặt, A4"),
        Manual("IVAN2M"),
        Sku("A5DRAFT", SkuProcessType.Manual, SkuConfigStatus.Unconfigured, null, null, "1 mặt, A5"));

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"gpdf-manual-store-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static FileOverride External(string orderId, string skuId, int copy, string path, int pages) =>
        new(orderId, skuId, copy, 1, path, pages, 1, FilePageCount: pages);

    private static PrintPlanBuilder CreateBuilder() => new(new OrderClassifier());

    [Fact]
    public void Manual_order_becomes_ready_once_its_single_manual_set_has_a_file_whatever_the_quantities()
    {
        var order = Order("O1", 0, [("A4", 2), ("IVAN2M", 3)]);
        var chosen = new ClassificationOptions(FileOverrides: [External("O1", "A4", 1, @"D:\ngoai\a4.pdf", 5)]);

        var none = new OrderClassifier().Classify(order, SkuMap, Library, new ClassificationOptions());
        var ready = new OrderClassifier().Classify(order, SkuMap, Library, chosen);

        Assert.Equal(OrderCategory.Manual, none.Category);
        Assert.Equal(2, none.Reasons.Count);
        Assert.Equal(OrderCategory.Ready, ready.Category);
    }

    [Fact]
    public void Unconfirmed_manual_sku_still_needs_configuration_even_with_files()
    {
        var options = new ClassificationOptions(FileOverrides: [External("O1", "A5DRAFT", 1, @"D:\ngoai\a5.pdf", 1)]);

        var result = new OrderClassifier().Classify(Order("O1", 0, [("A5DRAFT", 1)]), SkuMap, Library, options);

        Assert.Equal(OrderCategory.NeedsConfig, result.Category);
    }

    [Fact]
    public void Plan_prints_the_manual_set_once_in_line_order_and_never_adds_a_separator_for_it()
    {
        var order = Order("O1", 0, [("IVAN20", 1), ("A4", 2), ("IVAN2M", 1)]);
        var options = new ClassificationOptions(FileOverrides:
        [
            new FileOverride("O1", "A4", 1, 1, @"D:\ngoai\a4.pdf", 2, 1, FilePageCount: 2),
            new FileOverride("O1", "A4", 1, 2, @"D:\ngoai\hai-mat.pdf", 1, 3, FilePageCount: 4),
        ]);

        var plan = CreateBuilder().Build([order], SkuMap, Library, SeparatorMode.ComplexOnly, options);

        var planOrder = Assert.Single(plan.Orders);
        Assert.False(planOrder.IncludeSeparator);
        Assert.Equal(
            [("IVAN20", Ivan3.FilePath, 1, 3), ("A4", @"D:\ngoai\a4.pdf", 1, 2), ("A4", @"D:\ngoai\hai-mat.pdf", 3, 1)],
            planOrder.Copies.Select(copy => (copy.SkuId, copy.Asset.FilePath, copy.FirstPage, copy.PageCount)));
        Assert.All(planOrder.Copies.Skip(1), copy =>
        {
            Assert.True(copy.IsManualChoice);
            Assert.True(copy.IsManualProduct);
            Assert.False(copy.ShowsCopyNumber);
            Assert.Equal(2, copy.Quantity);
            Assert.Equal(PrintPlanBuilder.ManualIdol, copy.Idol);
        });
        Assert.Equal(6, planOrder.SheetCount);
    }

    [Fact]
    public void Plan_order_copies_leaves_out_a_manual_line_without_files_but_keeps_the_auto_line()
    {
        var order = Order("O1", 0, [("IVAN20", 1), ("IVAN2M", 1)]);

        var copies = CreateBuilder().PlanOrderCopies(order, SkuMap, Library, new ClassificationOptions());
        var batch = CreateBuilder().Build([order], SkuMap, Library, SeparatorMode.None, new ClassificationOptions());

        var copy = Assert.Single(copies);
        Assert.Equal("IVAN20", copy.SkuId);
        Assert.Empty(batch.Orders);
        Assert.Equal(OrderCategory.Manual, Assert.Single(batch.Rejected).Category);
    }

    [Fact]
    public void Store_copies_an_outside_file_once_and_never_overwrites_a_different_file_with_the_same_name()
    {
        var root = Path.Combine(directory, "kho", ManualFileStore.FolderName);
        var source = Path.Combine(directory, "ngoai", "khach.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "first");

        var stored = ManualFileStore.Import(root, "587035179774936214", source);
        var again = ManualFileStore.Import(root, "587035179774936214", source);
        var fromStore = ManualFileStore.Import(root, "587035179774936214", stored);
        File.WriteAllText(source, "second, longer");
        var changed = ManualFileStore.Import(root, "587035179774936214", source);

        Assert.Equal(Path.Combine(root, "587035179774936214", "khach.pdf"), stored);
        Assert.Equal(stored, again);
        Assert.Equal(stored, fromStore);
        Assert.Equal(Path.Combine(root, "587035179774936214", "khach (2).pdf"), changed);
        Assert.Equal("first", File.ReadAllText(stored));
        Assert.Equal("second, longer", File.ReadAllText(source));
        Assert.True(ManualFileStore.IsInStore(Path.Combine(directory, "kho"), changed));
        Assert.False(ManualFileStore.IsInStore(Path.Combine(directory, "kho"), source));
    }

    [Fact]
    public void Store_root_is_inside_the_library_folder()
    {
        Assert.Equal(Path.Combine(@"D:\Kho", ManualFileStore.FolderName), ManualFileStore.RootFor(@"D:\Kho"));
        Assert.EndsWith(Path.Combine("PDFPageComposer", ManualFileStore.FolderName), ManualFileStore.RootFor(null));
    }
}
