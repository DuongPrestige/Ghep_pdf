using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Services;

/// <summary>
/// Rules of the "Ghép 2 mặt" mode: side 2 pages, export check and the output order.
/// The exported file holds side 1 of every pair, then side 2 of every pair in the same order, so with N sheets
/// page N + k is printed on the back of page k.
/// </summary>
public static class DuplexLayout
{
    /// <summary>
    /// Items for side 2 of a pair. A single page (usually the signature page) is repeated to match side 1;
    /// otherwise the pages are used as chosen.
    /// </summary>
    public static IReadOnlyList<OutputPageItem> CreateBackItems(
        Guid pairId,
        IReadOnlyList<SourcePdfPage> pages,
        int frontPageCount)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var chosen = pages.Count == 1 && frontPageCount > 1
            ? Enumerable.Repeat(pages[0], frontPageCount)
            : pages;
        return CreateItems(pairId, chosen);
    }

    public static IReadOnlyList<OutputPageItem> CreateItems(Guid pairId, IEnumerable<SourcePdfPage> pages)
    {
        return pages
            .Select(page => new OutputPageItem(Guid.NewGuid(), pairId, page.SourceFileId, page.PageNumber))
            .ToList();
    }

    /// <summary>Why the pairs cannot be exported yet, or null when every pair is complete.</summary>
    public static string? DescribeProblem(IReadOnlyList<DuplexPair> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        if (pairs.Count == 0)
        {
            return "Khay 2 mặt đang trống";
        }

        var missingBack = PairNumbers(pairs, pair => pair.FrontItems.Count > 0 && !pair.HasBack);
        var missingFront = PairNumbers(pairs, pair => pair.FrontItems.Count == 0);
        var mismatched = PairNumbers(pairs, pair => pair.FrontItems.Count > 0 && pair.HasBack && !pair.IsComplete);

        var problems = new List<string>();
        if (missingFront.Count > 0)
        {
            problems.Add($"thiếu mặt 1: cặp {string.Join(", ", missingFront)}");
        }

        if (missingBack.Count > 0)
        {
            problems.Add($"thiếu mặt 2: cặp {string.Join(", ", missingBack)}");
        }

        if (mismatched.Count > 0)
        {
            problems.Add($"lệch số trang: cặp {string.Join(", ", mismatched)}");
        }

        return problems.Count == 0
            ? null
            : "Chưa xuất được: " + string.Join("; ", problems);
    }

    /// <summary>Status shown on a pair row.</summary>
    public static string DescribePair(DuplexPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);

        if (pair.FrontItems.Count == 0)
        {
            return "Thiếu mặt 1";
        }

        if (!pair.HasBack)
        {
            return "Chờ mặt 2";
        }

        return pair.IsComplete
            ? "Khớp"
            : $"Lệch: mặt 1 có {pair.FrontItems.Count} trang, mặt 2 có {pair.BackItems.Count} trang";
    }

    /// <summary>One output group per pair holding the pages of one side, in pair order.</summary>
    public static IReadOnlyList<OutputGroup> ToSideGroups(IEnumerable<DuplexPair> pairs, DuplexSide side)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        return pairs
            .Select((pair, index) => new OutputGroup(
                side == DuplexSide.Front ? pair.Id : Guid.NewGuid(),
                $"Cặp {index + 1} · mặt {(int)side}",
                DateTimeOffset.UtcNow,
                side == DuplexSide.Front ? pair.FrontItems : pair.BackItems,
                pair.ColorHex))
            .ToList();
    }

    /// <summary>Groups in export order: side 1 of every pair, then side 2 of every pair.</summary>
    public static IReadOnlyList<OutputGroup> ToExportGroups(IReadOnlyList<DuplexPair> pairs)
    {
        return [.. ToSideGroups(pairs, DuplexSide.Front), .. ToSideGroups(pairs, DuplexSide.Back)];
    }

    private static List<int> PairNumbers(IReadOnlyList<DuplexPair> pairs, Func<DuplexPair, bool> predicate)
    {
        return pairs
            .Select((pair, index) => (pair, number: index + 1))
            .Where(entry => predicate(entry.pair))
            .Select(entry => entry.number)
            .ToList();
    }
}
