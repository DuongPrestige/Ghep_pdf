using System.IO;
using Microsoft.Data.Sqlite;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.TikTok;
using PDFPageComposer.App.TikTok.Data;
using PDFPageComposer.App.TikTok.Export;
using PDFPageComposer.App.TikTok.Import;
using PDFPageComposer.App.TikTok.Matching;
using PDFPageComposer.App.TikTok.Models;
using PDFPageComposer.App.ViewModels.TikTok;

namespace PDFPageComposer.Tests.ViewModels.TikTok;

/// <summary>
/// Real SQLite repository on a temp file plus the real pure services (classifier, plan builder, suggester, import);
/// dialogs, Foxit, clipboard, library scanner and exporter are fakes.
/// </summary>
internal sealed class TikTokUiHarness : IDisposable
{
    public static readonly DateTimeOffset LocalNow = new(2026, 10, 16, 9, 30, 0, TimeSpan.FromHours(7));

    private readonly string tempDirectory;

    public TikTokUiHarness()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "gpdf-ui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        Repository = new SqliteTikTokRepository(Path.Combine(tempDirectory, "tiktok.db"));
        Time = new FixedTimeProvider(LocalNow);
        Exporter = new FakeExporter(Repository, Time, failure: null);
        Services = new TikTokServices(
            Repository,
            Settings,
            Time,
            new OrderImportService(Repository, new SkuConfigSuggester(), Settings, Time),
            path => new SellerCenterCsvOrderSource(path),
            new SkuConfigSuggester(),
            Scanner,
            new OrderClassifier(),
            new PrintPlanBuilder(new OrderClassifier()),
            Exporter,
            Foxit,
            Dialogs,
            Clipboard,
            new OrderListExportService(Repository, new OrderClassifier()));
    }

    public string TempDirectory => tempDirectory;

    public SqliteTikTokRepository Repository { get; }

    public FixedTimeProvider Time { get; }

    public FakeSettingsService Settings { get; } = new();

    public FakeFileDialogs Dialogs { get; } = new();

    public FakeFoxit Foxit { get; } = new();

    public FakeClipboard Clipboard { get; } = new();

    public FakeScanner Scanner { get; } = new();

    public FakeExporter Exporter { get; }

    public TikTokServices Services { get; }

    public TikTokOrdersViewModel CreateViewModel() => new(Services);

    public async Task<long> AddShopAsync(string name = "Shop A")
    {
        await Repository.InitializeAsync(CancellationToken.None);
        return (await Repository.AddShopAsync(name, CancellationToken.None)).ShopId;
    }

    public Task SeedOrdersAsync(long shopId, params TikTokOrder[] orders) =>
        Repository.ApplyImportAsync(
            new ImportChangeSet(shopId, "seed.csv", ImportMode.Partial, LocalNow, orders, [], [], [], 0, 0),
            CancellationToken.None);

    public string WriteTempFile(string name, string content)
    {
        var path = Path.Combine(tempDirectory, name);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
        catch (IOException exception)
        {
            Console.WriteLine($"Temp cleanup failed: {exception.Message}");
        }
    }

    // ------------------------------------------------------------------ test data

    public const string Cody30 = "SKU-CODY30";
    public const string Ivan20 = "SKU-IVAN20";
    public const string Ivan20TwoSided = "SKU-IVAN20-2M";
    public const string KeyChain = "SKU-KEY";
    public const string Label = "SKU-LABEL";

    public static PdfAsset Asset(string idol, int pages, int day, int month = 10) =>
        new(
            $@"D:\Kho\{idol} - {pages} PAGE - {day:00}.{month:00}.pdf",
            idol,
            pages,
            new DateOnly(2026, month, day),
            pages,
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            true,
            null);

    public static PdfAsset[] ExampleAssets() => [Asset("IVAN", 3, 15), Asset("IVAN", 3, 10), Asset("CODY", 4, 14)];

    public static SkuConfig Sku(
        string skuId,
        SkuProcessType type,
        string? idol,
        int? cards,
        SkuConfigStatus status = SkuConfigStatus.Confirmed,
        string? product = null,
        string? variation = null) =>
        new(
            skuId,
            product ?? $"[CARD BO GÓC {idol}] {skuId}",
            variation ?? (cards is null ? "Mặc định" : $"{cards} CARD, 1 Mặt"),
            type,
            status,
            idol,
            cards,
            LocalNow,
            LocalNow);

    public static SkuConfig[] ExampleSkus() =>
    [
        Sku(Cody30, SkuProcessType.Auto, "CODY", 30),
        Sku(Ivan20, SkuProcessType.Auto, "IVAN", 20),
        Sku(Ivan20TwoSided, SkuProcessType.Manual, "IVAN", 20, variation: "20 CARD, 2 MẶT"),
        Sku(KeyChain, SkuProcessType.Ignore, null, null, product: "[MÓC KHÓA] Spotify"),
        Sku(Label, SkuProcessType.Ignore, null, null, product: "[NHÃN VỞ CORTIS]"),
    ];

    public static TikTokOrder Order(
        string orderId,
        long shopId,
        int minute,
        (string SkuId, int Quantity)[] lines,
        string? buyerMessage = null,
        string? sellerNote = null) =>
        new(
            orderId,
            shopId,
            "Chờ vận chuyển",
            new DateTimeOffset(2026, 10, 15, 8, minute, 0, TimeSpan.FromHours(7)),
            "b***r",
            buyerMessage,
            sellerNote,
            true,
            0,
            0,
            null,
            null,
            null,
            false,
            null,
            lines.Select((line, index) => new TikTokOrderLine(orderId, line.SkuId, index + 1, $"Product {line.SkuId}", $"Variation {line.SkuId}", line.Quantity))
                .ToList());

    /// <summary>The six orders of PRD_TIKTOK section 7.</summary>
    public static TikTokOrder[] ExampleOrders(long shopId) =>
    [
        Order("O1", shopId, 1, [(Cody30, 1)]),
        Order("O2", shopId, 2, [(Ivan20, 2), (KeyChain, 1)]),
        Order("O3", shopId, 3, [(Ivan20TwoSided, 1), (KeyChain, 1)]),
        Order("O4", shopId, 4, [(Label, 1)]),
        Order("O5", shopId, 5, [(Cody30, 1)], buyerMessage: "cho em ảnh khác nhé"),
        Order("O6", shopId, 6, [(Ivan20, 1)], sellerNote: "ĐÃ IN IVAN mini"),
    ];

    /// <summary>Seeds shop, SKUs, library and the PRD example orders; returns the shop id.</summary>
    public async Task<long> SeedExampleAsync()
    {
        var shopId = await AddShopAsync();
        await Repository.SaveSkusAsync(ExampleSkus(), CancellationToken.None);
        await Repository.ReplacePdfAssetsAsync(ExampleAssets(), CancellationToken.None);
        await SeedOrdersAsync(shopId, ExampleOrders(shopId));
        return shopId;
    }


    // ------------------------------------------------------------------ fakes

    internal sealed class FixedTimeProvider : TimeProvider
    {
        private static readonly TimeZoneInfo Vietnam =
            TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "UTC+07", "UTC+07");

        private readonly DateTimeOffset utcNow;

        public FixedTimeProvider(DateTimeOffset now)
        {
            utcNow = now.ToUniversalTime();
        }

        public override TimeZoneInfo LocalTimeZone => Vietnam;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    internal sealed class FakeSettingsService : IAppSettingsService
    {
        public AppSettings Current { get; private set; } = new();

        public int SaveCount { get; private set; }

        public Exception? SaveFailure { get; set; }

        public Exception? LoadFailure { get; set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken) =>
            LoadFailure is not null ? Task.FromException<AppSettings>(LoadFailure) : Task.FromResult(Clone(Current));

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            if (SaveFailure is not null)
            {
                return Task.FromException(SaveFailure);
            }

            Current = Clone(settings);
            SaveCount++;
            return Task.CompletedTask;
        }

        private static AppSettings Clone(AppSettings settings) =>
            System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(settings))!;
    }

    internal sealed class FakeFileDialogs : IFileDialogService
    {
        public IReadOnlyList<string> CsvFiles { get; set; } = [];

        public string? Folder { get; set; }

        public string? OutputPdf { get; set; }

        public string? LastSuggestedFileName { get; private set; }

        public IReadOnlyList<string> PickPdfFiles() => [];

        public string? PickOutputPdfFile() => OutputPdf;

        public IReadOnlyList<string> PickCsvFiles() => CsvFiles;

        public string? PickFolder(string? initialFolder) => Folder;

        public string? PickOutputPdfFile(string suggestedFileName, string? initialFolder)
        {
            LastSuggestedFileName = suggestedFileName;
            return OutputPdf;
        }

        public string? SaveCsv { get; set; }

        public string? LastSuggestedCsvName { get; private set; }

        public string? LastCsvInitialFolder { get; private set; }

        public int SaveCsvCalls { get; private set; }

        public string? PickSaveCsvFile(string suggestedFileName, string? initialFolder)
        {
            SaveCsvCalls++;
            LastSuggestedCsvName = suggestedFileName;
            LastCsvInitialFolder = initialFolder;
            return SaveCsv;
        }

        public bool ConfirmResult { get; set; } = true;

        public string? LastConfirmMessage { get; private set; }

        public bool Confirm(string message, string title)
        {
            LastConfirmMessage = message;
            return ConfirmResult;
        }
    }

    internal sealed class FakeFoxit : IFoxitLauncherService
    {
        public List<string> OpenedFiles { get; } = [];

        public Exception? Failure { get; set; }

        public Task<string?> DiscoverFoxitExecutableAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public bool IsValidExecutable(string? executablePath) => false;

        public Task OpenPdfAsync(string pdfPath, CancellationToken cancellationToken)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            OpenedFiles.Add(pdfPath);
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeClipboard : IClipboardService
    {
        public string? Text { get; private set; }

        public bool Busy { get; set; }

        public void SetText(string text)
        {
            if (Busy)
            {
                throw new InvalidOperationException("Không copy được vào clipboard (đang bị ứng dụng khác giữ). Vui lòng thử lại.");
            }

            Text = text;
        }
    }

    internal sealed class FakeScanner : IPdfLibraryScanner
    {
        public IReadOnlyList<PdfAsset> Result { get; set; } = [];

        public string? ScannedFolder { get; private set; }

        public IReadOnlyList<PdfAsset>? Previous { get; private set; }

        public DateOnly? Today { get; private set; }

        public Task<IReadOnlyList<PdfAsset>> ScanAsync(
            string folder,
            IReadOnlyList<PdfAsset> previous,
            DateOnly today,
            CancellationToken cancellationToken)
        {
            ScannedFolder = folder;
            Previous = previous;
            Today = today;
            return Task.FromResult(Result);
        }
    }
}

/// <summary>Records the plan; on success commits the batch like PrintBatchExporter does.</summary>
internal sealed class FakeExporter : IPrintBatchExporter
{
    private readonly ITikTokRepository repository;
    private readonly TimeProvider time;

    public FakeExporter(ITikTokRepository repository, TimeProvider time, Exception? failure)
    {
        this.repository = repository;
        this.time = time;
        Failure = failure;
    }

    public Exception? Failure { get; set; }

    public List<(PrintPlan Plan, string OutputPath, IReadOnlyDictionary<long, string> ShopNames)> Calls { get; } = [];

    public async Task<PrintBatchResult> ExportAsync(
        PrintPlan plan,
        IReadOnlyDictionary<long, string> shopNames,
        string outputPath,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        Calls.Add((plan, outputPath, shopNames));
        progress?.Report(plan.TotalPages);
        if (Failure is not null)
        {
            throw Failure;
        }

        var batchId = $"B{Calls.Count}";
        var copies = plan.Orders.SelectMany(order => order.Copies).ToList();
        var items = copies
            .Select((copy, index) => new PrintBatchItemRecord(
                batchId,
                index + 1,
                copy.OrderId,
                copy.SkuId,
                copy.CopyIndex,
                copy.Quantity,
                copy.Asset.FilePath,
                copy.DuplicateImage ? "DuplicateImage" : null))
            .ToList();
        var batch = new PrintBatchRecord(
            batchId,
            time.GetUtcNow(),
            outputPath,
            plan.SeparatorMode,
            plan.Orders.Count,
            plan.TotalPages,
            "Completed");
        await repository.CommitPrintBatchAsync(
            batch,
            items,
            plan.Orders.Select(order => order.Order.OrderId).ToList(),
            CancellationToken.None);
        return new PrintBatchResult(batchId, outputPath, plan.TotalPages, plan.Orders.Count);
    }
}
