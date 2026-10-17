namespace PDFPageComposer.App.TikTok.Models;

/// <summary>
/// One library file chosen by hand for one printed set (copy CopyIndex of line SkuId) of an order (TT-04, 2026-10-17).
/// A set is the rows in Seq order; each row prints PagesUsed pages of its file starting at FirstPage.
/// When a set has chosen files, the automatic newest-file choice is skipped for that set.
/// </summary>
public sealed record FileOverride(
    string OrderId,
    string SkuId,
    int CopyIndex,
    int Seq,
    string PdfAssetPath,
    int PagesUsed,
    int FirstPage = 1);

public static class FileOverrides
{
    /// <summary>The chosen files of one set in Seq order; empty when the set uses the automatic choice.</summary>
    public static IReadOnlyList<FileOverride> For(
        IReadOnlyList<FileOverride>? overrides,
        string orderId,
        string skuId,
        int copyIndex) =>
        overrides is null || overrides.Count == 0
            ? []
            : overrides
                .Where(choice => choice.CopyIndex == copyIndex
                    && string.Equals(choice.OrderId, orderId, StringComparison.Ordinal)
                    && string.Equals(choice.SkuId, skuId, StringComparison.Ordinal))
                .OrderBy(choice => choice.Seq)
                .ToList();
}
