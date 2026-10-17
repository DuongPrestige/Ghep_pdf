using System.Text.RegularExpressions;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Matching;

/// <summary>Suggests a configuration for a newly seen SKU (PRD_TIKTOK TT-03, TECH_SPEC_TIKTOK 5.2).</summary>
public sealed partial class SkuConfigSuggester : ISkuConfigSuggester
{
    private static readonly HashSet<string> ProductStopWords = new(StringComparer.Ordinal)
    {
        "CARD", "BO", "GOC", "COMBO", "THE", "ANH", "MOI", "TANG", "CHU", "KY", "PHOTOCARD",
    };

    public SkuSuggestion Suggest(string productName, string variationName, IReadOnlyList<IdolAlias> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);

        var product = TextNormalizer.Normalize(productName);
        var variation = TextNormalizer.Normalize(variationName);

        if (!IsCardProduct(product))
        {
            return new SkuSuggestion(SkuProcessType.Ignore, null, null, null);
        }

        var cardCount = ReadNumber(CardCountPattern(), variation);
        var sides = ReadNumber(SidesPattern(), variation);
        var processType = SuggestProcessType(product, variation, cardCount, sides);
        var idol = SuggestIdol(product, variation, aliases);

        return new SkuSuggestion(processType, idol, cardCount, sides);
    }

    /// <summary>Rounded photo cards: "BO GÓC", "THẺ GÓC" or "PHOTOCARD" in the product title.</summary>
    private static bool IsCardProduct(string product) =>
        product.Contains("BO GOC", StringComparison.Ordinal)
        || product.Contains("THE GOC", StringComparison.Ordinal)
        || product.Contains("PHOTOCARD", StringComparison.Ordinal);

    private static SkuProcessType SuggestProcessType(string product, string variation, int? cardCount, int? sides)
    {
        if (cardCount is null)
        {
            return SkuProcessType.Manual;
        }

        // Custom orders: "IN THEO YÊU CẦU", "Ảnh Yêu cầu (gửi cho shop)", or a product sold only as a custom print.
        if (sides == 2
            || variation.Contains("YEU CAU", StringComparison.Ordinal)
            || product.StartsWith("IN THEO YEU CAU", StringComparison.Ordinal))
        {
            return SkuProcessType.Manual;
        }

        // "20 CARD" without a side count is the plain one-sided set.
        return sides is null or 1 ? SkuProcessType.Auto : SkuProcessType.Manual;
    }

    private static string? SuggestIdol(string product, string variation, IReadOnlyList<IdolAlias> aliases)
    {
        var candidate = FindVariationCandidate(variation) ?? FindProductCandidate(product);
        if (candidate is null)
        {
            return null;
        }

        var key = TextNormalizer.IdolKey(candidate);
        if (key.Length == 0)
        {
            return null;
        }

        foreach (var alias in aliases)
        {
            if (string.Equals(TextNormalizer.IdolKey(alias.Alias), key, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(alias.Idol))
            {
                return TextNormalizer.Normalize(alias.Idol);
            }
        }

        var normalized = TextNormalizer.Normalize(candidate);
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? FindVariationCandidate(string variation)
    {
        var last = variation.Split(',')[^1].Trim();
        if (last.Length == 0 || NonIdolWordsPattern().IsMatch(last))
        {
            return null;
        }

        return last;
    }

    private static string? FindProductCandidate(string product)
    {
        foreach (var segment in ProductSeparatorPattern().Split(product))
        {
            if (!IsCardProduct(segment))
            {
                continue;
            }

            var words = segment
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(word => !ProductStopWords.Contains(word));
            var candidate = string.Join(' ', words);
            return candidate.Length == 0 ? null : candidate;
        }

        return null;
    }

    private static int? ReadNumber(Regex pattern, string text)
    {
        var match = pattern.Match(text);
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }

    [GeneratedRegex(@"\b(\d+)\s*CARD\b", RegexOptions.CultureInvariant)]
    private static partial Regex CardCountPattern();

    [GeneratedRegex(@"\b(\d+)\s*MAT\b", RegexOptions.CultureInvariant)]
    private static partial Regex SidesPattern();

    [GeneratedRegex(@"\b(CARD|MAT|IN)\b|YEU CAU", RegexOptions.CultureInvariant)]
    private static partial Regex NonIdolWordsPattern();

    [GeneratedRegex(@"\[|\]|\s+-\s*|\s*-\s+", RegexOptions.CultureInvariant)]
    private static partial Regex ProductSeparatorPattern();
}
