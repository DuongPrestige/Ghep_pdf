using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Matching;

/// <summary>Builds the print plan of one batch (PRD_TIKTOK TT-04 → TT-07, TECH_SPEC_TIKTOK 5.3).</summary>
public sealed class PrintPlanBuilder : IPrintPlanBuilder
{
    /// <summary>Name shown in place of the idol for a manual line whose SKU has no idol.</summary>
    public const string ManualIdol = "THỦ CÔNG";

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

        // Each order restarts at the newest file (TT-04); the cursors only live inside one order.
        var candidateCache = new Dictionary<(string IdolKey, int Pages), IReadOnlyList<PdfAsset>>();
        var assetsByPath = AssetsByPath(assets);
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

            var planOrder = TryPlanOrder(order, skus, assets, assetsByPath, separatorMode, options, candidateCache, skipUnplannableLines: false);
            if (planOrder is null)
            {
                rejected.Add(new OrderClassification(
                    order.OrderId,
                    OrderCategory.MissingFile,
                    ["Không tìm thấy file kho phù hợp cho đơn này (hoặc file đã chọn tay không còn trong kho)."]));
                continue;
            }

            planned.Add(planOrder);
        }

        return new PrintPlan(planned, rejected, separatorMode);
    }

    /// <summary>
    /// Files one order would print now, without classifying it: lines that cannot be planned yet (a manual line
    /// without chosen files, an idol without library files) are left out. Used by the "Xem / chọn file" dialog.
    /// </summary>
    public IReadOnlyList<PrintPlanCopy> PlanOrderCopies(
        TikTokOrder order,
        IReadOnlyDictionary<string, SkuConfig> skus,
        IReadOnlyList<PdfAsset> assets,
        ClassificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(skus);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(options);

        var plan = TryPlanOrder(
            order,
            skus,
            assets,
            AssetsByPath(assets),
            SeparatorMode.None,
            options,
            new Dictionary<(string IdolKey, int Pages), IReadOnlyList<PdfAsset>>(),
            skipUnplannableLines: true);
        return plan?.Copies ?? [];
    }

    /// <summary>A PDF added from outside the library in the "Xem / chọn file" dialog.</summary>
    public static PdfAsset ExternalAsset(string filePath, string idol, int pageCount) =>
        new(filePath, idol, pageCount, DateOnly.MinValue, pageCount, default, IsValid: true, Error: null);

    /// <summary>A confirmed manual SKU: printed only from files chosen by hand (2026-11-08).</summary>
    public static bool IsManual(SkuConfig sku)
    {
        ArgumentNullException.ThrowIfNull(sku);
        return sku.ConfigStatus == SkuConfigStatus.Confirmed && sku.ProcessType == SkuProcessType.Manual;
    }

    private static Dictionary<string, PdfAsset> AssetsByPath(IReadOnlyList<PdfAsset> assets) =>
        assets
            .Where(asset => asset.IsValid)
            .GroupBy(asset => asset.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Plans every automatic line and every manual line of the order. Returns null when a line cannot be planned,
    /// unless <paramref name="skipUnplannableLines"/> leaves that line out instead.
    /// </summary>
    private static PrintPlanOrder? TryPlanOrder(
        TikTokOrder order,
        IReadOnlyDictionary<string, SkuConfig> skus,
        IReadOnlyList<PdfAsset> assets,
        IReadOnlyDictionary<string, PdfAsset> assetsByPath,
        SeparatorMode separatorMode,
        ClassificationOptions options,
        Dictionary<(string IdolKey, int Pages), IReadOnlyList<PdfAsset>> candidateCache,
        bool skipUnplannableLines)
    {
        var printLines = order.Lines
            .OrderBy(line => line.LineNo)
            .Select(line => (Line: line, Sku: skus.TryGetValue(line.SkuId, out var sku) ? sku : null))
            .Where(entry => entry.Sku is not null && (PdfAssetMatcher.IsUsableAuto(entry.Sku) || IsManual(entry.Sku)))
            .Select(entry => (entry.Line, Sku: entry.Sku!))
            .ToList();

        // Every order starts from the newest file (TT-04, 2026-10-17); only copies inside the same order
        // (quantity > 1, or two lines of the same idol and page count) move on to the next file.
        var cursors = new Dictionary<(string IdolKey, int Pages), int>();
        var copies = new List<PrintPlanCopy>();
        var manualDone = false;
        foreach (var (line, sku) in printLines)
        {
            List<PrintPlanCopy>? lineCopies;
            if (IsManual(sku))
            {
                // Every manual line of the order shares one set (the first manual line, copy 1), printed once.
                if (manualDone)
                {
                    continue;
                }

                manualDone = true;
                lineCopies = PlanManualSet(order, line, sku, assetsByPath, options);
            }
            else
            {
                lineCopies = PlanAutoLine(order, line, sku, assets, assetsByPath, options, cursors, candidateCache);
            }

            if (lineCopies is null)
            {
                if (skipUnplannableLines)
                {
                    continue;
                }

                return null;
            }

            copies.AddRange(lineCopies);
        }

        if (copies.Count == 0)
        {
            return null;
        }

        // Only automatic lines make an order complex: manual products never add a separator (2026-11-08).
        var autoLines = printLines.Where(entry => !IsManual(entry.Sku)).ToList();
        var isComplex = autoLines.Count >= 2 || autoLines.Any(entry => entry.Line.Quantity > 1);
        var includeSeparator = separatorMode switch
        {
            SeparatorMode.All => true,
            SeparatorMode.ComplexOnly => isComplex,
            _ => false,
        };

        return new PrintPlanOrder(order, includeSeparator, copies);
    }

    /// <summary>
    /// The manual products of an order (A4/A5 cards, photobooth, mini album…, any quantity) print the files chosen by
    /// hand once; they are stored on the first manual line, copy 1 (2026-11-08).
    /// </summary>
    private static List<PrintPlanCopy>? PlanManualSet(
        TikTokOrder order,
        TikTokOrderLine firstManualLine,
        SkuConfig sku,
        IReadOnlyDictionary<string, PdfAsset> assetsByPath,
        ClassificationOptions options)
    {
        var idol = string.IsNullOrWhiteSpace(sku.Idol) ? ManualIdol : PdfAssetMatcher.ResolveIdol(sku.Idol, options.IdolAliases);
        var chosen = FileOverrides.For(options.FileOverrides, order.OrderId, firstManualLine.SkuId, 1);
        var copies = new List<PrintPlanCopy>();
        return chosen.Count > 0 && AddChosenFiles(order, firstManualLine, 1, idol, chosen, assetsByPath, copies)
            ? copies.Select(copy => copy with { IsManualProduct = true }).ToList()
            : null;
    }

    /// <summary>Adds the files chosen by hand for one set; false when a chosen library file is no longer in the library.</summary>
    private static bool AddChosenFiles(
        TikTokOrder order,
        TikTokOrderLine line,
        int copyIndex,
        string idol,
        IReadOnlyList<FileOverride> chosen,
        IReadOnlyDictionary<string, PdfAsset> assetsByPath,
        List<PrintPlanCopy> lineCopies)
    {
        foreach (var choice in chosen)
        {
            if (!assetsByPath.TryGetValue(choice.PdfAssetPath, out var chosenAsset))
            {
                if (choice.FilePageCount is not int filePages || filePages <= 0)
                {
                    return false;
                }

                chosenAsset = ExternalAsset(choice.PdfAssetPath, idol, filePages);
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

        return true;
    }

    /// <summary>Assigns library files to every set of an automatic line; null when the idol has no usable file.</summary>
    private static List<PrintPlanCopy>? PlanAutoLine(
        TikTokOrder order,
        TikTokOrderLine line,
        SkuConfig sku,
        IReadOnlyList<PdfAsset> assets,
        IReadOnlyDictionary<string, PdfAsset> assetsByPath,
        ClassificationOptions options,
        Dictionary<(string IdolKey, int Pages), int> cursors,
        Dictionary<(string IdolKey, int Pages), IReadOnlyList<PdfAsset>> candidateCache)
    {
        var idol = PdfAssetMatcher.ResolveIdol(sku.Idol!, options.IdolAliases);
        var pages = PdfAssetMatcher.PagesForCards(sku.CardCount!.Value);
        // AlwaysUseNewestFiles: each set is composed from the idol's newest files, whatever their page count
        // (page count 0 in the key = any file). Otherwise only files with the exact page count are used.
        var compose = options.AlwaysUseNewestFiles;
        var key = (TextNormalizer.IdolKey(idol), compose ? 0 : pages);
        IReadOnlyList<PdfAsset>? candidates = null;

        var cursor = cursors.TryGetValue(key, out var current) ? current : 0;
        var firstCursor = cursor;
        var lineCopies = new List<PrintPlanCopy>();
        for (var copyIndex = 1; copyIndex <= line.Quantity; copyIndex++)
        {
            // Files chosen by hand replace the automatic choice for this set and do not move the cursor.
            var chosen = FileOverrides.For(options.FileOverrides, order.OrderId, line.SkuId, copyIndex);
            if (chosen.Count > 0)
            {
                if (!AddChosenFiles(order, line, copyIndex, idol, chosen, assetsByPath, lineCopies))
                {
                    return null;
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

        cursors[key] = cursor;

        // A file used twice inside one order means the same images are printed twice.
        var duplicateImage = candidates is not null && cursor - firstCursor > candidates.Count;
        return duplicateImage
            ? lineCopies.Select(copy => copy.IsManualChoice ? copy : copy with { DuplicateImage = true }).ToList()
            : lineCopies;
    }
}
