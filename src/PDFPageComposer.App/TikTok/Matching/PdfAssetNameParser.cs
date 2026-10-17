using System.IO;
using System.Text.RegularExpressions;

namespace PDFPageComposer.App.TikTok.Matching;

/// <summary>Parses library file names "&lt;IDOL&gt; - &lt;N&gt; PAGE - &lt;dd.MM&gt;.pdf" (PRD_TIKTOK TT-01).</summary>
public static partial class PdfAssetNameParser
{
    public const string InvalidNameError = "Tên file không đúng quy ước <IDOL> - <N> PAGE - <dd.MM>.pdf";

    /// <summary>
    /// Returns false when the name does not follow the convention, the date does not exist or N is below 1.
    /// The year is today's year, or the previous year when that date would be after today.
    /// </summary>
    public static bool TryParse(string fileName, DateOnly today, out string idol, out int pageCount, out DateOnly fileDate)
    {
        idol = string.Empty;
        pageCount = 0;
        fileDate = default;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var match = NamePattern().Match(Path.GetFileName(fileName.Trim()));
        if (!match.Success)
        {
            return false;
        }

        var normalizedIdol = TextNormalizer.Normalize(match.Groups["idol"].Value);
        if (normalizedIdol.Length == 0 || TextNormalizer.IdolKey(normalizedIdol).Length == 0)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["pages"].Value, out var pages) || pages < 1)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["d"].Value, out var day) || !int.TryParse(match.Groups["m"].Value, out var month))
        {
            return false;
        }

        if (!TryResolveDate(day, month, today, out var resolved))
        {
            return false;
        }

        idol = normalizedIdol;
        pageCount = pages;
        fileDate = resolved;
        return true;
    }

    private static bool TryResolveDate(int day, int month, DateOnly today, out DateOnly date)
    {
        date = default;
        if (month is < 1 or > 12 || day < 1)
        {
            return false;
        }

        // 29.02 exists only in leap years, so try the current year first, then the previous one.
        if (day <= DateTime.DaysInMonth(today.Year, month))
        {
            var candidate = new DateOnly(today.Year, month, day);
            if (candidate <= today)
            {
                date = candidate;
                return true;
            }
        }

        var previousYear = today.Year - 1;
        if (previousYear < 1 || day > DateTime.DaysInMonth(previousYear, month))
        {
            return false;
        }

        date = new DateOnly(previousYear, month, day);
        return true;
    }

    [GeneratedRegex(
        @"^(?<idol>.+?)\s*-\s*(?<pages>\d+)\s*PAGES?\s*-\s*(?<d>\d{1,2})\.(?<m>\d{1,2})(?:\s*\(\d+\))?\.pdf$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
