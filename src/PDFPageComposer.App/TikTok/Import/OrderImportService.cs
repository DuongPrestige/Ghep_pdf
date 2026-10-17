using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Import;

/// <summary>
/// Idempotent order import with de-duplication (PRD_TIKTOK TT-09, TT-10; TECH_SPEC_TIKTOK 3.3).
/// Computes one ImportChangeSet and lets the repository apply it in a single transaction.
/// </summary>
public sealed class OrderImportService : IOrderImportService
{
    private readonly ITikTokRepository _repository;
    private readonly ISkuConfigSuggester _suggester;
    private readonly IAppSettingsService _settingsService;
    private readonly TimeProvider _timeProvider;

    public OrderImportService(
        ITikTokRepository repository,
        ISkuConfigSuggester suggester,
        IAppSettingsService settingsService,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(suggester);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _repository = repository;
        _suggester = suggester;
        _settingsService = settingsService;
        _timeProvider = timeProvider;
    }

    public Task<ImportReport> ImportAsync(
        long shopId,
        string sourceName,
        ImportMode mode,
        IReadOnlyList<ImportedOrderLine> lines,
        CancellationToken cancellationToken) =>
        ImportAsync(shopId, sourceName, mode, lines, [], cancellationToken);

    public async Task<ImportReport> ImportAsync(
        long shopId,
        string sourceName,
        ImportMode mode,
        IReadOnlyList<ImportedOrderLine> lines,
        IReadOnlyCollection<string> protectedOrderIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(protectedOrderIds);

        var (appSettings, settingsCorrupt) = await TikTokSettingsLoader.LoadAsync(_settingsService, cancellationToken)
            .ConfigureAwait(false);
        var settings = appSettings.TikTok ?? new TikTokSettings();
        var importedAt = _timeProvider.GetUtcNow();

        var grouping = ImportedOrderGrouper.Group(lines, settings.AcceptedStatuses ?? []);
        var warnings = new List<string>(grouping.Warnings);
        if (settingsCorrupt)
        {
            warnings.Add(TikTokSettingsLoader.CorruptNotice);
        }
        var orderIds = grouping.Orders.Select(order => order.OrderId).ToArray();

        var existingOrders = orderIds.Length == 0
            ? new Dictionary<string, TikTokOrder>(StringComparer.Ordinal)
            : (await _repository.GetOrdersByIdsAsync(orderIds, cancellationToken).ConfigureAwait(false))
                .ToDictionary(order => order.OrderId, StringComparer.Ordinal);

        var printedOrderIds = existingOrders.Values
            .Where(order => order.PrintedBatchId is not null)
            .Select(order => order.OrderId)
            .ToArray();
        var printedItems = printedOrderIds.Length == 0
            ? new Dictionary<string, IReadOnlyList<PrintBatchItemRecord>>(StringComparer.Ordinal)
            : await _repository.GetPrintedItemsForOrdersAsync(printedOrderIds, cancellationToken).ConfigureAwait(false);

        var toInsert = new List<TikTokOrder>();
        var toUpdate = new List<TikTokOrder>();
        var alreadyPrinted = 0;

        foreach (var group in grouping.Orders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var orderLines = group.Lines
                .Select((line, index) => new TikTokOrderLine(
                    group.OrderId,
                    line.SkuId,
                    index + 1,
                    line.ProductName,
                    line.VariationName,
                    line.Quantity))
                .ToArray();

            TikTokOrder order;
            if (existingOrders.TryGetValue(group.OrderId, out var existing))
            {
                if (existing.ShopId != shopId)
                {
                    warnings.Add(
                        $"Đơn {group.OrderId} đã được import trước đó cho một shop khác; vẫn cập nhật nhưng giữ shop cũ.");
                }

                printedItems.TryGetValue(group.OrderId, out var snapshot);
                order = OrderImportMerger.Update(existing, group.FirstLine, orderLines, snapshot, settings, importedAt);
                toUpdate.Add(order);
            }
            else
            {
                order = OrderImportMerger.Create(shopId, group.FirstLine, orderLines, settings, importedAt);
                toInsert.Add(order);
            }

            if (order.PrintedBatchId is not null || order.PrintedSource is not null)
            {
                alreadyPrinted++;
            }
        }

        var toDeactivate = await GetOrdersToDeactivateAsync(shopId, mode, orderIds, protectedOrderIds, warnings, cancellationToken)
            .ConfigureAwait(false);
        var newSkus = await CreateNewSkusAsync(grouping.Orders, importedAt, cancellationToken).ConfigureAwait(false);

        var changeSet = new ImportChangeSet(
            shopId,
            sourceName,
            mode,
            importedAt,
            toInsert,
            toUpdate,
            toDeactivate,
            newSkus,
            alreadyPrinted,
            grouping.Skipped);
        var batch = await _repository.ApplyImportAsync(changeSet, cancellationToken).ConfigureAwait(false);

        return new ImportReport(
            batch.ImportId,
            toInsert.Count,
            toUpdate.Count,
            alreadyPrinted,
            grouping.Skipped,
            toDeactivate.Count,
            newSkus.Count,
            warnings);
    }

    private async Task<IReadOnlyList<string>> GetOrdersToDeactivateAsync(
        long shopId,
        ImportMode mode,
        IReadOnlyCollection<string> orderIdsInFile,
        IReadOnlyCollection<string> protectedOrderIds,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (mode != ImportMode.Full)
        {
            return [];
        }

        if (orderIdsInFile.Count == 0)
        {
            // An empty or fully filtered file would otherwise hide every pending order of the shop.
            warnings.Add("File không có đơn chờ vận chuyển hợp lệ nào nên không ẩn đơn nào của shop.");
            return [];
        }

        var inFile = orderIdsInFile.ToHashSet(StringComparer.Ordinal);
        var isProtected = protectedOrderIds.ToHashSet(StringComparer.Ordinal);
        var active = await _repository.GetActiveOrdersForShopAsync(shopId, cancellationToken).ConfigureAwait(false);
        var toDeactivate = new List<string>();
        foreach (var order in active.Where(order => order.ShopId == shopId && !inFile.Contains(order.OrderId)))
        {
            if (isProtected.Contains(order.OrderId))
            {
                // Rows of this order are still in the export but could not be read: keep it as it is.
                warnings.Add($"Đơn {order.OrderId} có dòng không hợp lệ trong file nên được giữ nguyên, không ẩn.");
                continue;
            }

            toDeactivate.Add(order.OrderId);
        }

        return toDeactivate;
    }

    private async Task<IReadOnlyList<SkuConfig>> CreateNewSkusAsync(
        IReadOnlyList<ImportedOrderGroup> orders,
        DateTimeOffset importedAt,
        CancellationToken cancellationToken)
    {
        if (orders.Count == 0)
        {
            return [];
        }

        var stored = (await _repository.GetSkusAsync(cancellationToken).ConfigureAwait(false))
            .Select(sku => sku.SkuId)
            .ToHashSet(StringComparer.Ordinal);
        var aliases = await _repository.GetIdolAliasesAsync(cancellationToken).ConfigureAwait(false);

        var newSkus = new List<SkuConfig>();
        foreach (var line in orders.SelectMany(order => order.Lines))
        {
            if (!stored.Add(line.SkuId))
            {
                continue;
            }

            var suggestion = _suggester.Suggest(line.ProductName, line.VariationName, aliases);
            newSkus.Add(new SkuConfig(
                line.SkuId,
                line.ProductName,
                line.VariationName,
                suggestion.ProcessType,
                SkuConfigStatus.Unconfigured,
                suggestion.Idol,
                suggestion.CardCount,
                importedAt,
                importedAt));
        }

        return newSkus;
    }
}
