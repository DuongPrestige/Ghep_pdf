using System.Globalization;
using System.Text;

namespace PDFPageComposer.App.TikTok;

/// <summary>Shared text normalization for matching Vietnamese product, variation, idol and note text.</summary>
public static class TextNormalizer
{
    /// <summary>Upper-case, strip diacritics (Đ → D), turn tabs/newlines into spaces, collapse whitespace, trim.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var mapped = character switch
            {
                'đ' or 'Đ' => 'D',
                _ => char.ToUpperInvariant(character),
            };

            if (char.IsWhiteSpace(mapped))
            {
                if (!previousWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                previousWasSpace = true;
                continue;
            }

            builder.Append(mapped);
            previousWasSpace = false;
        }

        return builder.ToString().TrimEnd().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Key used to compare idol codes: Normalize, then keep only letters and digits ("SƠN.K" → "SONK").</summary>
    public static string IdolKey(string? value)
    {
        var normalized = Normalize(value);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    /// <summary>Trim spaces and tabs (Seller Center pads numeric cells with a trailing tab); empty becomes null.</summary>
    public static string? TrimCell(string? value)
    {
        var trimmed = value?.Trim(' ', '\t', '\r', '\n', ' ');
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
