using System.IO;
using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.TikTok.Models;
using Serilog;

namespace PDFPageComposer.App.TikTok.Export;

/// <summary>
/// Builds one print PDF from a print plan (TASK-065): one output group per order, an optional separator page,
/// then every page of each copy's library file. History is committed only after the export succeeded.
/// </summary>
public sealed class PrintBatchExporter : IPrintBatchExporter
{
    public const string CompletedStatus = "Completed";
    public const string DuplicateImageWarning = "DuplicateImage";

    private const string GroupColorHex = "#2563EB";

    private readonly IPdfMetadataService metadataService;
    private readonly IPdfExportService exportService;
    private readonly ISeparatorPageRenderer separatorRenderer;
    private readonly ITikTokRepository repository;
    private readonly TimeProvider timeProvider;

    public PrintBatchExporter(
        IPdfMetadataService metadataService,
        IPdfExportService exportService,
        ISeparatorPageRenderer separatorRenderer,
        ITikTokRepository repository,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(metadataService);
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(separatorRenderer);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.metadataService = metadataService;
        this.exportService = exportService;
        this.separatorRenderer = separatorRenderer;
        this.repository = repository;
        this.timeProvider = timeProvider;
    }

    /// <summary>Root folder of the per-batch temporary separator directories.</summary>
    public static string SeparatorTempRoot => Path.Combine(Path.GetTempPath(), "PDFPageComposer", "separators");

    public async Task<PrintBatchResult> ExportAsync(
        PrintPlan plan,
        IReadOnlyDictionary<long, string> shopNames,
        string outputPath,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(shopNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (plan.Orders.Count == 0)
        {
            throw new InvalidOperationException("Không có đơn nào để tạo file in.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var createdAt = timeProvider.GetLocalNow();
        var batchId = $"{createdAt:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..24];
        var tempDirectory = Path.Combine(SeparatorTempRoot, batchId);

        try
        {
            var layout = await BuildLayoutAsync(plan, shopNames, batchId, tempDirectory, cancellationToken)
                .ConfigureAwait(false);

            await exportService.ExportAsync(layout.Sources, layout.Groups, outputPath, progress, cancellationToken)
                .ConfigureAwait(false);

            var batch = new PrintBatchRecord(
                batchId,
                createdAt,
                outputPath,
                plan.SeparatorMode,
                plan.Orders.Count,
                layout.PageCount,
                CompletedStatus);
            var orderIds = plan.Orders.Select(order => order.Order.OrderId).ToList();

            await CommitOrDeleteOutputAsync(batch, layout.Items, orderIds, outputPath).ConfigureAwait(false);

            Log.Information(
                "TikTok print batch {BatchId} exported: {OrderCount} orders, {PageCount} pages",
                batchId,
                plan.Orders.Count,
                layout.PageCount);

            return new PrintBatchResult(batchId, outputPath, layout.PageCount, plan.Orders.Count);
        }
        finally
        {
            DeleteTempDirectory(tempDirectory, batchId);
        }
    }

    private async Task<BatchLayout> BuildLayoutAsync(
        PrintPlan plan,
        IReadOnlyDictionary<long, string> shopNames,
        string batchId,
        string tempDirectory,
        CancellationToken cancellationToken)
    {
        var assetSources = await ReadAssetSourcesAsync(plan, cancellationToken).ConfigureAwait(false);
        var sources = new List<SourcePdfFile>(assetSources.Values);
        var groups = new List<OutputGroup>(plan.Orders.Count);
        var items = new List<PrintBatchItemRecord>();
        var pageCount = 0;

        foreach (var planOrder in plan.Orders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var groupId = Guid.NewGuid();
            var pages = new List<OutputPageItem>();

            if (planOrder.IncludeSeparator)
            {
                var separatorPath = await separatorRenderer
                    .RenderAsync(CreateSeparatorInfo(planOrder, shopNames), tempDirectory, cancellationToken)
                    .ConfigureAwait(false);
                var separator = await metadataService.ReadAsync(separatorPath, cancellationToken).ConfigureAwait(false);
                sources.Add(separator);
                pages.Add(new OutputPageItem(Guid.NewGuid(), groupId, separator.Id, 1));
            }

            foreach (var copy in planOrder.Copies)
            {
                var source = assetSources[copy.Asset.FilePath];
                for (var pageNumber = copy.FirstPage; pageNumber <= copy.LastPage; pageNumber++)
                {
                    pages.Add(new OutputPageItem(Guid.NewGuid(), groupId, source.Id, pageNumber));
                }

                items.Add(new PrintBatchItemRecord(
                    batchId,
                    items.Count + 1,
                    copy.OrderId,
                    copy.SkuId,
                    copy.CopyIndex,
                    copy.Quantity,
                    copy.Asset.FilePath,
                    copy.DuplicateImage ? DuplicateImageWarning : null,
                    copy.UsesPartOfFile ? copy.PageCount : null,
                    copy.FirstPage > 1 ? copy.FirstPage : null));
            }

            pageCount += pages.Count;
            groups.Add(new OutputGroup(groupId, planOrder.Order.OrderId, timeProvider.GetUtcNow(), pages, GroupColorHex));
        }

        return new BatchLayout(sources, groups, items, pageCount);
    }

    private async Task<Dictionary<string, SourcePdfFile>> ReadAssetSourcesAsync(
        PrintPlan plan,
        CancellationToken cancellationToken)
    {
        var sources = new Dictionary<string, SourcePdfFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var copy in plan.Orders.SelectMany(order => order.Copies))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sources.TryGetValue(copy.Asset.FilePath, out var cached))
            {
                EnsurePageCountMatches(cached, copy);
                continue;
            }

            var source = await metadataService.ReadAsync(copy.Asset.FilePath, cancellationToken).ConfigureAwait(false);
            EnsurePageCountMatches(source, copy);
            sources.Add(copy.Asset.FilePath, source);
        }

        return sources;
    }

    private static void EnsurePageCountMatches(SourcePdfFile source, PrintPlanCopy copy)
    {
        if (source.PageCount != copy.Asset.PageCount || copy.FirstPage < 1 || copy.LastPage > source.PageCount)
        {
            throw new InvalidOperationException(
                $"File \"{Path.GetFileName(copy.Asset.FilePath)}\" có {source.PageCount} trang nhưng kho ghi {copy.Asset.PageCount} trang. " +
                "File đã thay đổi sau lần quét kho, hãy quét lại kho rồi tạo file in.");
        }
    }

    private static SeparatorPageInfo CreateSeparatorInfo(
        PrintPlanOrder planOrder,
        IReadOnlyDictionary<long, string> shopNames)
    {
        var order = planOrder.Order;
        var shopName = shopNames.TryGetValue(order.ShopId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : $"Shop {order.ShopId}";
        var itemLines = planOrder.Copies.Select(FormatItemLine).ToList();

        return new SeparatorPageInfo(order.OrderId, shopName, order.BuyerUsername, order.CreatedAt, itemLines, planOrder.SheetCount);
    }

    private static string FormatItemLine(PrintPlanCopy copy)
    {
        var line = $"{copy.Idol} · bộ {copy.CopyIndex}/{copy.Quantity} · {Path.GetFileName(copy.Asset.FilePath)}";
        if (copy.UsesPartOfFile)
        {
            line += copy.FirstPage == 1
                ? $" · lấy {copy.PageCount}/{copy.Asset.PageCount} trang đầu"
                : $" · lấy trang {copy.FirstPage}-{copy.LastPage}";
        }
        return copy.DuplicateImage ? $"{line} · TRÙNG HÌNH" : line;
    }

    private async Task CommitOrDeleteOutputAsync(
        PrintBatchRecord batch,
        IReadOnlyList<PrintBatchItemRecord> items,
        IReadOnlyCollection<string> orderIds,
        string outputPath)
    {
        try
        {
            // The PDF already exists: do not let a late cancellation leave printed but untracked orders.
            await repository.CommitPrintBatchAsync(batch, items, orderIds, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception commitException)
        {
            Log.Error(commitException, "Could not save TikTok print batch {BatchId}; deleting its output file", batch.BatchId);
            DeleteOutputFile(outputPath, batch.BatchId);
            throw;
        }
    }

    private static void DeleteOutputFile(string outputPath, string batchId)
    {
        try
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not delete output file of uncommitted print batch {BatchId}", batchId);
        }
    }

    private static void DeleteTempDirectory(string tempDirectory, string batchId)
    {
        try
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Could not delete separator temp directory of print batch {BatchId}", batchId);
        }
    }

    private sealed record BatchLayout(
        IReadOnlyList<SourcePdfFile> Sources,
        IReadOnlyList<OutputGroup> Groups,
        IReadOnlyList<PrintBatchItemRecord> Items,
        int PageCount);
}
