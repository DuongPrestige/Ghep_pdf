using System.IO;
using Microsoft.Data.Sqlite;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;
using PDFPageComposer.App.TikTok.Data;
using PDFPageComposer.App.TikTok.Export;
using PDFPageComposer.App.TikTok.Import;
using PDFPageComposer.App.TikTok.Matching;
using PDFPageComposer.App.TikTok.Models;
using PDFPageComposer.Tests.TikTok.Import;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PDFPageComposer.Tests.TikTok;

/// <summary>
/// The whole TikTok pipeline with real components: CSV → import (SQLite) → SKU confirmation → library scan →
/// classification → print plan → PDF export → history, then re-import (PRD_TIKTOK AC-T01..AC-T06).
/// </summary>
public sealed class EndToEndTests : IDisposable
{
    private const string PrintedBySellerNote = "586605624104421255";
    private const string KeychainsWithNote = "586604186668729713";
    private const string TwoSidedCody = "586605483998676255";
    private const string CustomCard = "586603293419734271";
    private const string PolaroidOnly = "586607961666520863";
    private const string CodyThirtyCards = "586610253562020879";
    private const string IvanTwentyCards = "586604252850128721";

    private static readonly DateOnly Today = new(2026, 10, 16);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "gpdf-tiktok-e2e-" + Guid.NewGuid().ToString("N"));
    private readonly string _library;
    private readonly PdfiumLibrary _pdfium = new();
    private readonly SqliteTikTokRepository _repository;
    private readonly OrderImportService _importService;
    private readonly ClassificationOptions _options = new();

    public EndToEndTests()
    {
        _library = Path.Combine(_root, "library");
        Directory.CreateDirectory(_library);
        _repository = new SqliteTikTokRepository(Path.Combine(_root, "tiktok.db"));
        _importService = new OrderImportService(
            _repository,
            new SkuConfigSuggester(),
            new StubSettingsService(new AppSettings()),
            TimeProvider.System);
    }

    private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "TestData", "tiktok", "orders-sample.csv");

    [Fact]
    public async Task Sample_file_flows_from_import_to_printed_pdf_and_survives_reimport()
    {
        var ct = CancellationToken.None;
        await _repository.InitializeAsync(ct);
        var shop = await _repository.AddShopAsync("Shop mẫu", ct);
        var source = await new SellerCenterCsvOrderSource(SamplePath).ReadAsync(ct);
        var distinctSkus = source.Lines.Select(line => line.SkuId).Distinct(StringComparer.Ordinal).Count();

        // Import (TT-09/TT-10).
        var first = await _importService.ImportAsync(shop.ShopId, "orders-sample.csv", ImportMode.Full, source.Lines, ct);
        Assert.Equal(33, first.Inserted);
        Assert.Equal(0, first.Updated);
        Assert.Equal(0, first.Deactivated);
        Assert.Equal(distinctSkus, first.NewSkus);
        Assert.Equal(34, distinctSkus);
        Assert.Equal(1, first.AlreadyPrinted);

        // The user confirms every suggestion (TT-03).
        var suggested = await _repository.GetSkusAsync(ct);
        Assert.Equal(distinctSkus, suggested.Count);
        Assert.All(suggested, sku => Assert.Equal(SkuConfigStatus.Unconfigured, sku.ConfigStatus));
        await _repository.SaveSkusAsync(
            suggested.Select(sku => sku with { ConfigStatus = SkuConfigStatus.Confirmed }).ToList(),
            ct);

        // Library scan (TT-01).
        CreateLibraryPdf("CODY NAM VO - 4 PAGE - 14.10.pdf", 4);
        CreateLibraryPdf("CODY NAM VO - 3 PAGE - 14.10.pdf", 3);
        CreateLibraryPdf("IVAN - 3 PAGE - 15.10.pdf", 3);
        CreateLibraryPdf("IVAN - 3 PAGE - 10.10.pdf", 3);
        CreateLibraryPdf("CONGB - 4 PAGE - 13.10.pdf", 4);
        CreateLibraryPdf("CONGB - 5 PAGE - 13.10.pdf", 5);
        CreateLibraryPdf("CONGB - 3 PAGE - 13.10.pdf", 3);
        var scanner = new PdfLibraryScanner(new PdfMetadataService(_pdfium));
        var assets = await scanner.ScanAsync(_library, await _repository.GetPdfAssetsAsync(ct), Today, ct);
        Assert.Equal(7, assets.Count);
        Assert.All(assets, asset => Assert.True(asset.IsValid, asset.Error));
        await _repository.ReplacePdfAssetsAsync(assets, ct);

        // Classification (TT-08).
        var skus = (await _repository.GetSkusAsync(ct)).ToDictionary(sku => sku.SkuId, StringComparer.Ordinal);
        var orders = await _repository.GetOrdersAsync(includeInactive: false, ct);
        Assert.Equal(33, orders.Count);
        var classifier = new OrderClassifier();
        var categories = orders.ToDictionary(
            order => order.OrderId,
            order => classifier.Classify(order, skus, assets, _options).Category,
            StringComparer.Ordinal);

        Assert.Equal(OrderCategory.Printed, categories[PrintedBySellerNote]);
        Assert.Equal(OrderCategory.NoCardNeeded, categories[KeychainsWithNote]);
        Assert.Equal(OrderCategory.Manual, categories[TwoSidedCody]);
        Assert.Equal(OrderCategory.Manual, categories[CustomCard]);
        Assert.Equal(OrderCategory.NoCardNeeded, categories[PolaroidOnly]);
        string[] expectedReady =
        [
            CodyThirtyCards, // 30 cards -> CODY NAM VO 4 PAGE
            "586604956131755578", // 20 cards -> CODY NAM VO 3 PAGE
            IvanTwentyCards, // 20 cards -> IVAN 3 PAGE (newest 15.10)
            "586604101020583821", // 30 cards -> CONGB 4 PAGE
            "586602657350714486", // 40 cards -> CONGB 5 PAGE
            "586600253334783642", // 20 cards -> CONGB 3 PAGE
        ];
        Assert.Equal(
            expectedReady.Order(StringComparer.Ordinal),
            categories.Where(pair => pair.Value == OrderCategory.Ready).Select(pair => pair.Key).Order(StringComparer.Ordinal));

        // Print plan (TT-04..TT-07) and export (TASK-065).
        var builder = new PrintPlanBuilder(classifier);
        var plan = builder.Build(orders, skus, assets, SeparatorMode.ComplexOnly, _options);
        var plannedIds = plan.Orders.Select(order => order.Order.OrderId).ToList();
        var readyIds = categories.Where(pair => pair.Value == OrderCategory.Ready).Select(pair => pair.Key).ToHashSet();
        Assert.Equal(readyIds.OrderBy(id => id), plannedIds.OrderBy(id => id));
        Assert.Contains(CodyThirtyCards, plannedIds);
        Assert.DoesNotContain(PrintedBySellerNote, plannedIds);
        Assert.Equal(4 + 3 + 3 + 4 + 5 + 3, plan.Orders.Sum(order => order.SheetCount));
        Assert.Equal(
            Path.Combine(_library, "IVAN - 3 PAGE - 15.10.pdf"),
            plan.Orders.Single(order => order.Order.OrderId == IvanTwentyCards).Copies.Single().Asset.FilePath);

        var exporter = new PrintBatchExporter(
            new PdfMetadataService(_pdfium),
            new PdfExportService(),
            new SeparatorPageRenderer(),
            _repository,
            TimeProvider.System);
        var outputPath = Path.Combine(_root, "out", $"TIKTOK_20261016_0900_{plan.Orders.Count}don.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var result = await exporter.ExportAsync(
            plan,
            new Dictionary<long, string> { [shop.ShopId] = shop.Name },
            outputPath,
            progress: null,
            ct);

        Assert.Equal(plan.TotalPages, result.PageCount);
        Assert.Equal(plan.Orders.Count, result.OrderCount);
        using (var output = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import))
        {
            Assert.Equal(plan.TotalPages, output.PageCount);
        }

        var afterPrint = (await _repository.GetOrdersByIdsAsync(plannedIds, ct)).ToList();
        Assert.Equal(plannedIds.Count, afterPrint.Count);
        Assert.All(afterPrint, order =>
        {
            Assert.Equal(result.BatchId, order.PrintedBatchId);
            Assert.Equal(PrintedSource.Batch, order.PrintedSource);
        });
        var batch = Assert.Single(await _repository.GetPrintBatchesAsync(ct));
        Assert.Equal(result.BatchId, batch.BatchId);
        Assert.Equal(plan.TotalPages, batch.PageCount);
        var items = await _repository.GetPrintBatchItemsAsync(result.BatchId, ct);
        Assert.Equal(plan.Orders.Sum(order => order.Copies.Count), items.Count);

        // Re-import of the same file is idempotent and keeps print marks (AC-T02).
        var second = await _importService.ImportAsync(shop.ShopId, "orders-sample.csv", ImportMode.Full, source.Lines, ct);
        Assert.Equal(0, second.Inserted);
        Assert.Equal(33, second.Updated);
        Assert.Equal(0, second.Deactivated);
        Assert.Equal(0, second.NewSkus);

        var reloaded = await _repository.GetOrdersAsync(includeInactive: false, ct);
        Assert.Equal(33, reloaded.Count);
        Assert.All(
            reloaded.Where(order => plannedIds.Contains(order.OrderId)),
            order =>
            {
                Assert.Equal(result.BatchId, order.PrintedBatchId);
                Assert.False(order.ChangedAfterPrint);
            });
        skus = (await _repository.GetSkusAsync(ct)).ToDictionary(sku => sku.SkuId, StringComparer.Ordinal);
        Assert.All(skus.Values, sku => Assert.Equal(SkuConfigStatus.Confirmed, sku.ConfigStatus));
        var replan = builder.Build(reloaded, skus, assets, SeparatorMode.ComplexOnly, _options);
        Assert.Empty(replan.Orders);
        Assert.All(
            plannedIds,
            id => Assert.Equal(OrderCategory.Printed, replan.Rejected.Single(rejected => rejected.OrderId == id).Category));

        // Full-mode import of a file without one order deactivates only that order (TT-09).
        var withoutPolaroid = source.Lines.Where(line => line.OrderId != PolaroidOnly).ToList();
        var third = await _importService.ImportAsync(shop.ShopId, "orders-sample-2.csv", ImportMode.Full, withoutPolaroid, ct);
        Assert.Equal(0, third.Inserted);
        Assert.Equal(32, third.Updated);
        Assert.Equal(1, third.Deactivated);
        var active = await _repository.GetOrdersAsync(includeInactive: false, ct);
        Assert.Equal(32, active.Count);
        Assert.DoesNotContain(active, order => order.OrderId == PolaroidOnly);
        var all = await _repository.GetOrdersAsync(includeInactive: true, ct);
        Assert.False(all.Single(order => order.OrderId == PolaroidOnly).IsActive);
        Assert.Equal(3, (await _repository.GetImportBatchesAsync(ct)).Count);
    }

    public void Dispose()
    {
        _pdfium.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void CreateLibraryPdf(string fileName, int pageCount)
    {
        using var document = new PdfDocument();
        for (var index = 0; index < pageCount; index++)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(595);
            page.Height = XUnit.FromPoint(842);
            using var graphics = XGraphics.FromPdfPage(page);
            graphics.DrawRectangle(XBrushes.LightGray, 20 + (index * 10), 20, 100, 100);
        }

        document.Save(Path.Combine(_library, fileName));
    }
}
