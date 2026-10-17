using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Matching;

/// <summary>Builds the print plan of one batch (PRD_TIKTOK TT-04 → TT-07, TECH_SPEC_TIKTOK 5.3).</summary>
public sealed class PrintPlanBuilder : IPrintPlanBuilder
{
    private readonly IOrderClassifier _classifier;

    public PrintPlanBuilder(IOrderClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        _classifier = classifier;
    }

    public PrintPlan Build(
        IReadOnlyList<TikTokOrder> orders,
        IReadOnlyDictionary<string, SkuConfig> skus,
        IReadOnlyList<PdfAsset> assets,
        SeparatorMode separatorMode,
        ClassificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(skus);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(options);

        // Same order as the Seller Center CSV, so the print file matches the order list (2026-10-17).
        var sortedOrders = TikTokOrderOrdering.InFileOrder(orders.Where(order => order.IsActive)).ToList();

        // Cursor per (idol key, page count) inside one order only; each order restarts at the newest file (TT-04).
        var cursors = new Dictionary<(string IdolKey, int Pages), int>();
        var candidateCache = new Dictionary<(string IdolKey, int Pages), IReadOnlyList<PdfAsset>>();
        var assetsByPath = assets
            .Where(asset => asset.IsValid)
            .GroupBy(asset => asset.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var planned = new List<PrintPlanOrder>();
        var rejected = new List<OrderClassification>();

        foreach (var order in sortedOrders)
        {
            var classification = _classifier.Classify(order, skus, assets, options);
            if (classification.Category != OrderCategory.Ready)
            {
                rejected.Add(classification);
                continue;
            }

            var pendingCursors = new Dictionary<(string IdolKey, int Pages), int>();
            var planOrder = TryPlanOrder(order, skus, assets, assetsByPath, separatorMode, options, cursors, pendingCursors, candidateCache);
            if (planOrder is null)
            {
                rejected.Add(new OrderClassification(
                    order.OrderId,
                    OrderCategory.MissingFile,
                    ["Không tìm thấy file kho phù hợp cho đơn này (hoặc file đã chọn tay không còn trong kho)."]));
                continue;
            }

            foreach (var (key, value) in pendingCursors)
            {
                cursors[key] = value;
            }

            planned.Add(planOrder);
        }

        return new PrintPlan(planned, rejected, separatorMode);
    }

    /// <summary>Assigns files using pendingCursors only; the caller commits them when the whole order succeeds.</summary>
    private static PrintPlanOrder? TryPlanOrder(
        TikTokOrder order,
        IReadOnlyDictionary<string, SkuConfig> skus,
        IReadOnlyList<PdfAsset> assets,
        IReadOnlyDictionary<string, PdfAsset> assetsByPath,
        SeparatorMode separatorMode,
        ClassificationOptions options,
        IReadOnlyDictionary<(string IdolKey, int Pages), int> cursors,
        Dictionary<(string IdolKey, int Pages), int> pendingCursors,
        Dictionary<(string IdolKey, int Pages), IReadOnlyList<PdfAsset>> candidateCache)
    {
        var autoLines = order.Lines
            .OrderBy(line => line.LineNo)
            .Select(line => (Line: line, Sku: skus.TryGetValue(line.SkuId, out var sku) ? sku : null))
            .Where(entry => entry.Sku is not null && PdfAssetMatcher.IsUsableAuto(entry.Sku))
            .ToList();

        var copies = new List<PrintPlanCopy>();
        foreach (var (line, sku) in autoLines)
        {
            var idol = PdfAssetMatcher.ResolveIdol(sku!.Idol!, options.IdolAliases);
            var pages = PdfAssetMatcher.PagesForCards(sku.CardCount!.Value);
            // AlwaysUseNewestFiles: each set is composed from the idol's newest files, whatever their page count
            // (page count 0 in the key = any file). Otherwise only files with the exact page count are used.
            var compose = options.AlwaysUseNewestFiles;
            var key = (TextNormalizer.IdolKey(idol), compose ? 0 : pages);
            IReadOnlyList<PdfAsset>? candidates = null;

            // Every order starts from the newest file (TT-04, 2026-10-17); only copies inside the same order
            // (quantity > 1, or two lines of the same idol and page count) move on to the next file.
            var cursor = pendingCursors.TryGetValue(key, out var pending) ? pending : 0;
            var firstCursor = cursor;
            var lineCopies = new List<PrintPlanCopy>();
            for (var copyIndex = 1; copyIndex <= line.Quantity; copyIndex++)
            {
                // Files chosen by hand replace the automatic choice for this set and do not move the cursor.
                var chosen = FileOverrides.For(options.FileOverrides, order.OrderId, line.SkuId, copyIndex);
                if (chosen.Count > 0)
                {
                    foreach (var choice in chosen)
                    {
                        if (!assetsByPath.TryGetValue(choice.PdfAssetPath, out var chosenAsset))
                        {
                            return null;
                        }

                        var firstPage = Math.Clamp(choice.FirstPage, 1, chosenAsset.PageCount);
                        lineCopies.Add(new PrintPlanCopy(
                            order.OrderId,
                            line.SkuId,
                            line.LineNo,
                            copyIndex,
                            line.Quantity,
                            idol,
                            Math.Clamp(choice.PagesUsed, 1, chosenAsset.PageCount - firstPage + 1),
                            chosenAsset,
                            DuplicateImage: false,
                            IsManualChoice: true,
                            FirstPage: firstPage));
                    }

                    continue;
                }

                if (candidates is null)
                {
                    if (!candidateCache.TryGetValue(key, out candidates))
                    {
                        candidates = compose ? PdfAssetMatcher.FindIdolFiles(assets, idol) : PdfAssetMatcher.FindCandidates(assets, idol, pages);
                        candidateCache[key] = candidates;
                    }

                    if (candidates.Count == 0)
                    {
                        return null;
                    }
                }

                var remaining = pages;
                while (remaining > 0)
                {
                    var asset = candidates[cursor % candidates.Count];
                    cursor++;
                    var used = compose ? Math.Min(remaining, asset.PageCount) : pages;
                    remaining -= used;
                    lineCopies.Add(new PrintPlanCopy(
                        order.OrderId,
                        line.SkuId,
                        line.LineNo,
                        copyIndex,
                        line.Quantity,
                        idol,
                        used,
                        asset,
                        DuplicateImage: false));
                }
            }

            // A file used twice inside one order means the same images are printed twice.
            var duplicateImage = candidates is not null && cursor - firstCursor > candidates.Count;
            copies.AddRange(duplicateImage
                ? lineCopies.Select(copy => copy.IsManualChoice ? copy : copy with { DuplicateImage = true })
                : lineCopies);
            pendingCursors[key] = cursor;
        }

        if (copies.Count == 0)
        {
            return null;
        }

        var isComplex = autoLines.Count >= 2 || autoLines.Any(entry => entry.Line.Quantity > 1);
        var includeSeparator = separatorMode switch
        {
            SeparatorMode.All => true,
            SeparatorMode.ComplexOnly => isComplex,
            _ => false,
        };

        return new PrintPlanOrder(order, includeSeparator, copies);
    }
}
