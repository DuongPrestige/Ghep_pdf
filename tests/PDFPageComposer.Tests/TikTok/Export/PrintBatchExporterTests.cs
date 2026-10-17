using System.IO;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Export;
using PDFPageComposer.App.TikTok.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using static PDFPageComposer.Tests.TikTok.Export.ExportTestHelpers;

namespace PDFPageComposer.Tests.TikTok.Export;

public sealed class PrintBatchExporterTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 14, 30, 15, TimeSpan.Zero);

    private readonly string directory = Path.Combine(Path.GetTempPath(), $"gpdf-batch-export-test-{Guid.NewGuid():N}");
    private readonly PdfiumLibrary pdfium = new();
    private readonly RecordingTikTokRepository repository = new();
    private readonly CountingMetadataService metadata;
    private readonly PdfAsset assetA;
    private readonly PdfAsset assetB;

    public PrintBatchExporterTests()
    {
        Directory.CreateDirectory(directory);
        metadata = new CountingMetadataService(new PdfMetadataService(pdfium));

        var pathA = Path.Combine(directory, "IVE - 2 PAGE - 15.10.pdf");
        var pathB = Path.Combine(directory, "NEWJEANS - 3 PAGE - 15.10.pdf");
        CreatePdf(pathA, 301, 302);
        CreatePdf(pathB, 401, 402, 403);
        assetA = Asset(pathA, "IVE", 2);
        assetB = Asset(pathB, "NEWJEANS", 3);
    }

    private string OutputPath => Path.Combine(directory, "out", "TIKTOK_20261016_1430_2don.pdf");

    [Fact]
    public async Task ExportAsync_writes_pages_in_plan_order_with_separators_and_commits_history()
    {
        var plan = CreatePlan();
        var exporter = CreateExporter();

        var result = await exporter.ExportAsync(plan, ShopNames(), OutputPath, null, CancellationToken.None);

        using (var output = PdfReader.Open(OutputPath, PdfDocumentOpenMode.Import))
        {
            Assert.Equal(
                [595, 301, 302, 301, 302, 401, 402, 403],
                output.Pages.Cast<PdfPage>().Select(page => (int)Math.Round(page.Width.Point)));
        }

        Assert.Equal(8, result.PageCount);
        Assert.Equal(plan.TotalPages, result.PageCount);
        Assert.Equal(2, result.OrderCount);
        Assert.Equal(OutputPath, result.OutputPath);
        Assert.StartsWith("20261016-143015-", result.BatchId);

        var commit = Assert.Single(repository.Commits);
        Assert.Equal(
            new PrintBatchRecord(result.BatchId, Now, OutputPath, SeparatorMode.ComplexOnly, 2, 8, "Completed"),
            commit.Batch);
        Assert.Equal(["O1", "O2"], commit.OrderIds);
        Assert.Equal(
            [
                new PrintBatchItemRecord(result.BatchId, 1, "O1", "SKU-A", 1, 2, assetA.FilePath, null),
                new PrintBatchItemRecord(result.BatchId, 2, "O1", "SKU-A", 2, 2, assetA.FilePath, "DuplicateImage"),
                new PrintBatchItemRecord(result.BatchId, 3, "O2", "SKU-B", 1, 1, assetB.FilePath, null),
            ],
            commit.Items);
    }

    [Fact]
    public async Task ExportAsync_reads_an_asset_used_twice_only_once()
    {
        await CreateExporter().ExportAsync(CreatePlan(), ShopNames(), OutputPath, null, CancellationToken.None);

        Assert.Equal(1, metadata.Calls[assetA.FilePath]);
        Assert.Equal(1, metadata.Calls[assetB.FilePath]);
    }

    [Fact]
    public async Task ExportAsync_removes_separator_temp_directory_after_success()
    {
        var result = await CreateExporter().ExportAsync(CreatePlan(), ShopNames(), OutputPath, null, CancellationToken.None);

        Assert.False(Directory.Exists(Path.Combine(PrintBatchExporter.SeparatorTempRoot, result.BatchId)));
    }

    [Fact]
    public async Task ExportAsync_passes_separator_info_with_shop_fallback_and_item_lines()
    {
        var renderer = new RecordingSeparatorRenderer(new SeparatorPageRenderer());
        var plan = CreatePlan();
        var exporter = CreateExporter(separatorRenderer: renderer);

        await exporter.ExportAsync(plan, new Dictionary<long, string>(), OutputPath, null, CancellationToken.None);

        var info = Assert.Single(renderer.Infos);
        Assert.Equal("O1", info.OrderId);
        Assert.Equal("Shop 7", info.ShopName);
        Assert.Equal("ng***ễn", info.BuyerUsername);
        Assert.Equal(OrderTime, info.CreatedAt);
        Assert.Equal(4, info.SheetCount);
        Assert.Equal(
            [
                "IVE · bộ 1/2 · IVE - 2 PAGE - 15.10.pdf",
                "IVE · bộ 2/2 · IVE - 2 PAGE - 15.10.pdf · TRÙNG HÌNH",
            ],
            info.ItemLines);
    }

    [Fact]
    public async Task ExportAsync_rejects_empty_plan()
    {
        var plan = new PrintPlan([], [], SeparatorMode.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateExporter().ExportAsync(plan, ShopNames(), OutputPath, null, CancellationToken.None));

        Assert.Empty(repository.Commits);
    }

    [Fact]
    public async Task ExportAsync_prints_only_the_first_pages_of_a_larger_file_and_records_them()
    {
        var order = Order("O9", 7);
        var partial = new PrintPlanCopy("O9", "SKU-B", 1, 1, 1, "NEWJEANS", 2, assetB, false);
        var plan = new PrintPlan([new PrintPlanOrder(order, false, [partial])], [], SeparatorMode.None);

        var result = await CreateExporter().ExportAsync(plan, ShopNames(), OutputPath, null, CancellationToken.None);

        using (var output = PdfReader.Open(OutputPath, PdfDocumentOpenMode.Import))
        {
            Assert.Equal([401, 402], output.Pages.Cast<PdfPage>().Select(page => (int)Math.Round(page.Width.Point)));
        }

        Assert.Equal(2, result.PageCount);
        Assert.Equal(2, Assert.Single(Assert.Single(repository.Commits).Items).PagesUsed);
    }

    [Fact]
    public async Task ExportAsync_prints_a_page_range_chosen_by_hand_and_records_its_first_page()
    {
        var order = Order("O9", 7);
        var range = new PrintPlanCopy("O9", "SKU-B", 1, 1, 1, "NEWJEANS", 2, assetB, false, IsManualChoice: true, FirstPage: 2);
        var plan = new PrintPlan([new PrintPlanOrder(order, false, [range])], [], SeparatorMode.None);

        await CreateExporter().ExportAsync(plan, ShopNames(), OutputPath, null, CancellationToken.None);

        using (var output = PdfReader.Open(OutputPath, PdfDocumentOpenMode.Import))
        {
            Assert.Equal([402, 403], output.Pages.Cast<PdfPage>().Select(page => (int)Math.Round(page.Width.Point)));
        }

        var item = Assert.Single(Assert.Single(repository.Commits).Items);
        Assert.Equal((2, 2), (item.PagesUsed, item.FirstPage));
    }

    [Fact]
    public async Task ExportAsync_rejects_asset_whose_page_count_changed()
    {
        var staleAsset = assetB with { PageCount = 4 };
        var order = Order("O9", 7);
        var plan = new PrintPlan(
            [new PrintPlanOrder(order, false, [Copy("O9", "SKU-B", 1, 1, staleAsset)])],
            [],
            SeparatorMode.None);
        var before = SnapshotTempRoot();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateExporter().ExportAsync(plan, ShopNames(), OutputPath, null, CancellationToken.None));

        Assert.Empty(repository.Commits);
        Assert.False(File.Exists(OutputPath));
        Assert.Equal(before, SnapshotTempRoot());
    }

    [Fact]
    public async Task ExportAsync_does_not_commit_when_export_fails()
    {
        var before = SnapshotTempRoot();
        var exporter = CreateExporter(exportService: new ThrowingExportService(new IOException("disk full")));

        await Assert.ThrowsAsync<IOException>(
            () => exporter.ExportAsync(CreatePlan(), ShopNames(), OutputPath, null, CancellationToken.None));

        Assert.Empty(repository.Commits);
        Assert.Equal(before, SnapshotTempRoot());
    }

    [Fact]
    public async Task ExportAsync_does_not_commit_when_cancelled_during_export()
    {
        var before = SnapshotTempRoot();
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateExporter().ExportAsync(
                CreatePlan(),
                ShopNames(),
                OutputPath,
                new CancelAfterFirstProgress(cancellation),
                cancellation.Token));

        Assert.Empty(repository.Commits);
        Assert.False(File.Exists(OutputPath));
        Assert.Equal(before, SnapshotTempRoot());
    }

    [Fact]
    public async Task ExportAsync_does_not_commit_when_cancelled_before_start()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateExporter().ExportAsync(CreatePlan(), ShopNames(), OutputPath, null, cancellation.Token));

        Assert.Empty(repository.Commits);
        Assert.False(File.Exists(OutputPath));
    }

    [Fact]
    public async Task ExportAsync_deletes_output_file_when_commit_fails()
    {
        var before = SnapshotTempRoot();
        repository.CommitException = new InvalidOperationException("database locked");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateExporter().ExportAsync(CreatePlan(), ShopNames(), OutputPath, null, CancellationToken.None));

        Assert.Equal("database locked", exception.Message);
        Assert.False(File.Exists(OutputPath));
        Assert.Equal(before, SnapshotTempRoot());
    }

    public void Dispose()
    {
        pdfium.Dispose();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private PrintBatchExporter CreateExporter(
        IPdfExportService? exportService = null,
        ISeparatorPageRenderer? separatorRenderer = null) =>
        new(
            metadata,
            exportService ?? new PdfExportService(),
            separatorRenderer ?? new SeparatorPageRenderer(),
            repository,
            new FixedTimeProvider(Now));

    private PrintPlan CreatePlan()
    {
        var order1 = Order("O1", 7, new TikTokOrderLine("O1", "SKU-A", 1, "Card IVE", "IVE", 2));
        var order2 = Order("O2", 8, new TikTokOrderLine("O2", "SKU-B", 1, "Card NewJeans", "NEWJEANS", 1));
        return new PrintPlan(
            [
                new PrintPlanOrder(
                    order1,
                    true,
                    [Copy("O1", "SKU-A", 1, 2, assetA), Copy("O1", "SKU-A", 2, 2, assetA, duplicateImage: true)]),
                new PrintPlanOrder(order2, false, [Copy("O2", "SKU-B", 1, 1, assetB)]),
            ],
            [],
            SeparatorMode.ComplexOnly);
    }

    private static Dictionary<long, string> ShopNames() => new() { [7] = "Shop Hàn", [8] = "Shop Nhật" };

    private static string[] SnapshotTempRoot() =>
        Directory.Exists(PrintBatchExporter.SeparatorTempRoot)
            ? Directory.GetDirectories(PrintBatchExporter.SeparatorTempRoot).Order(StringComparer.Ordinal).ToArray()
            : [];

    private sealed class RecordingSeparatorRenderer(ISeparatorPageRenderer inner) : ISeparatorPageRenderer
    {
        public List<SeparatorPageInfo> Infos { get; } = [];

        public Task<string> RenderAsync(SeparatorPageInfo info, string tempDirectory, CancellationToken cancellationToken)
        {
            Infos.Add(info);
            return inner.RenderAsync(info, tempDirectory, cancellationToken);
        }
    }
}
