using System.Security.Cryptography;
using System.Text;

namespace PDFPageComposer.App.TikTok;

/// <summary>Buyer Message / Seller Note rules from PRD_TIKTOK TT-10.</summary>
public static class OrderNotes
{
    private const string PrintedMarker = "DA IN";

    public static bool HasNote(string? buyerMessage, string? sellerNote) =>
        TextNormalizer.TrimCell(buyerMessage) is not null || TextNormalizer.TrimCell(sellerNote) is not null;

    /// <summary>True when the seller note contains "ĐÃ IN" as whole words, ignoring case and diacritics.</summary>
    public static bool IsMarkedPrinted(string? sellerNote)
    {
        var normalized = TextNormalizer.Normalize(sellerNote);
        if (normalized.Length == 0)
        {
            return false;
        }

        var index = normalized.IndexOf(PrintedMarker, StringComparison.Ordinal);
        while (index >= 0)
        {
            var startsWord = index == 0 || !char.IsLetterOrDigit(normalized[index - 1]);
            var end = index + PrintedMarker.Length;
            var endsWord = end == normalized.Length || !char.IsLetterOrDigit(normalized[end]);
            if (startsWord && endsWord)
            {
                return true;
            }

            index = normalized.IndexOf(PrintedMarker, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>Stable hash of the current notes, stored when the user reviews a noted order.</summary>
    public static string ComputeHash(string? buyerMessage, string? sellerNote)
    {
        var text = $"{TextNormalizer.TrimCell(buyerMessage)}\n{TextNormalizer.TrimCell(sellerNote)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
