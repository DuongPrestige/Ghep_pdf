using PDFPageComposer.App.Models;
using PDFPageComposer.App.TikTok.Models;

namespace PDFPageComposer.App.TikTok.Import;

/// <summary>Builds the inserted or updated order row for one order of the file (TT-09, TT-10).</summary>
internal static class OrderImportMerger
{
    public static TikTokOrder Create(
        long shopId,
        ImportedOrderLine firstLine,
        IReadOnlyList<TikTokOrderLine> lines,
        TikTokSettings settings,
        DateTimeOffset importedAt)
    {
        var markedPrinted = IsMarkedPrinted(firstLine.SellerNote, settings);
        return new TikTokOrder(
            OrderId: firstLine.OrderId,
            ShopId: shopId,
            StatusText: firstLine.StatusText,
            CreatedAt: firstLine.CreatedAt,
            BuyerUsername: firstLine.BuyerUsername,
            BuyerMessage: firstLine.BuyerMessage,
            SellerNote: firstLine.SellerNote,
            IsActive: true,
            FirstImportId: 0,
            LastSeenImportId: 0,
            PrintedBatchId: null,
            PrintedAt: markedPrinted ? importedAt : null,
            PrintedSource: markedPrinted ? PrintedSource.SellerNote : null,
            ChangedAfterPrint: false,
            NoteReviewedHash: null,
            Lines: lines,
            SourceRow: firstLine.SourceRowNumber);
    }

    /// <summary>
    /// Keeps identity, print marks and the reviewed-note hash; refreshes status, notes and lines from the file.
    /// The stored ShopId is kept even when the file was imported for another shop.
    /// </summary>
    public static TikTokOrder Update(
        TikTokOrder existing,
        ImportedOrderLine firstLine,
        IReadOnlyList<TikTokOrderLine> lines,
        IReadOnlyList<PrintBatchItemRecord>? printedSnapshot,
        TikTokSettings settings,
        DateTimeOffset importedAt)
    {
        var printedSource = existing.PrintedSource;
        var printedAt = existing.PrintedAt;
        var changedAfterPrint = existing.ChangedAfterPrint;

        if (existing.PrintedBatchId is null)
        {
            if (IsMarkedPrinted(firstLine.SellerNote, settings))
            {
                printedSource = PrintedSource.SellerNote;
                printedAt = existing.PrintedAt ?? importedAt;
            }
            else if (existing.PrintedSource == PrintedSource.SellerNote)
            {
                printedSource = null;
                printedAt = null;
            }
        }
        else
        {
            changedAfterPrint = existing.ChangedAfterPrint ||
                DiffersFromSnapshot(lines, printedSnapshot) ||
                !SameSkuSet(lines, existing.Lines);
        }

        return existing with
        {
            StatusText = firstLine.StatusText,
            BuyerUsername = firstLine.BuyerUsername,
            BuyerMessage = firstLine.BuyerMessage,
            SellerNote = firstLine.SellerNote,
            IsActive = true,
            PrintedSource = printedSource,
            PrintedAt = printedAt,
            ChangedAfterPrint = changedAfterPrint,
            Lines = lines,
            SourceRow = firstLine.SourceRowNumber,
        };
    }

    private static bool IsMarkedPrinted(string? sellerNote, TikTokSettings settings) =>
        settings.SellerNoteMarksPrinted && OrderNotes.IsMarkedPrinted(sellerNote);

    /// <summary>True when a printed SKU disappeared or its quantity differs from the quantity printed.</summary>
    private static bool DiffersFromSnapshot(
        IReadOnlyList<TikTokOrderLine> lines,
        IReadOnlyList<PrintBatchItemRecord>? printedSnapshot)
    {
        if (printedSnapshot is null || printedSnapshot.Count == 0)
        {
            return false;
        }

        var quantities = lines.ToDictionary(line => line.SkuId, line => line.Quantity, StringComparer.Ordinal);
        foreach (var item in printedSnapshot)
        {
            if (!quantities.TryGetValue(item.SkuId, out var quantity) || quantity != item.Quantity)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameSkuSet(IReadOnlyList<TikTokOrderLine> lines, IReadOnlyList<TikTokOrderLine>? storedLines)
    {
        var newSkus = lines.Select(line => line.SkuId).ToHashSet(StringComparer.Ordinal);
        return newSkus.SetEquals((storedLines ?? []).Select(line => line.SkuId));
    }
}
