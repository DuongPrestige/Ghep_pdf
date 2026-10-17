using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Matching;

/// <summary>Shared card → page conversion (TT-02) and library candidate lookup (TT-04).</summary>
public static class PdfAssetMatcher
{
    public const int CardsPerPage = 9;

    /// <summary>ceil(cardCount / 9).</summary>
    public static int PagesForCards(int cardCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cardCount);
        return (cardCount + CardsPerPage - 1) / CardsPerPage;
    }

    /// <summary>Maps an idol through the alias table (compared by IdolKey); returns the idol unchanged when no alias matches.</summary>
    public static string ResolveIdol(string idol, IReadOnlyList<IdolAlias>? aliases)
    {
        ArgumentNullException.ThrowIfNull(idol);
        if (aliases is null || aliases.Count == 0)
        {
            return idol;
        }

        var key = TextNormalizer.IdolKey(idol);
        var alias = aliases.FirstOrDefault(a =>
            !string.IsNullOrWhiteSpace(a.Idol) && string.Equals(TextNormalizer.IdolKey(a.Alias), key, StringComparison.Ordinal));
        return alias?.Idol ?? idol;
    }

    /// <summary>Valid assets with the same idol key (after aliases) and page count, newest FileDate first, then FilePath.</summary>
    public static IReadOnlyList<PdfAsset> FindCandidates(
        IReadOnlyList<PdfAsset> assets,
        string idol,
        int pageCount,
        IReadOnlyList<IdolAlias>? aliases = null)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var idolKey = TextNormalizer.IdolKey(ResolveIdol(idol, aliases));
        if (idolKey.Length == 0)
        {
            return [];
        }

        return assets
            .Where(asset => asset.IsValid
                && asset.PageCount == pageCount
                && string.Equals(TextNormalizer.IdolKey(asset.Idol), idolKey, StringComparison.Ordinal))
            .OrderByDescending(asset => asset.FileDate)
            .ThenBy(asset => asset.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Every valid file of the idol (after aliases), whatever its page count, newest FileDate first, then FilePath.</summary>
    public static IReadOnlyList<PdfAsset> FindIdolFiles(
        IReadOnlyList<PdfAsset> assets,
        string idol,
        IReadOnlyList<IdolAlias>? aliases = null)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var idolKey = TextNormalizer.IdolKey(ResolveIdol(idol, aliases));
        if (idolKey.Length == 0)
        {
            return [];
        }

        return assets
            .Where(asset => asset.IsValid
                && asset.PageCount > 0
                && string.Equals(TextNormalizer.IdolKey(asset.Idol), idolKey, StringComparison.Ordinal))
            .OrderByDescending(asset => asset.FileDate)
            .ThenBy(asset => asset.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// True when a set of pageCount pages can be printed: composed from the idol's newest files (any file of the idol
    /// will do), or otherwise from an exact page-count file (TT-04).
    /// </summary>
    public static bool CanPrint(
        IReadOnlyList<PdfAsset> assets,
        string idol,
        int pageCount,
        IReadOnlyList<IdolAlias>? aliases,
        bool alwaysUseNewestFiles) =>
        alwaysUseNewestFiles
            ? FindIdolFiles(assets, idol, aliases).Count > 0
            : FindCandidates(assets, idol, pageCount, aliases).Count > 0;

    /// <summary>True when the SKU is a confirmed Auto SKU with an idol and a positive card count.</summary>
    public static bool IsUsableAuto(SkuConfig sku)
    {
        ArgumentNullException.ThrowIfNull(sku);
        return sku.ConfigStatus == SkuConfigStatus.Confirmed
            && sku.ProcessType == SkuProcessType.Auto
            && !string.IsNullOrWhiteSpace(sku.Idol)
            && sku.CardCount is > 0;
    }
}
