using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PdfLibraryNormalizer;

public sealed class Rules
{
    public Dictionary<string, List<string>> Idols { get; set; } = [];

    public List<CategoryRule> Categories { get; set; } = [];

    /// <summary>Words that are never part of an idol name (FILE, NEW, OANH…).</summary>
    public List<string> NoiseWords { get; set; } = [];

    /// <summary>Multi-word noise ("DA GOP", "BAN SAO CUA").</summary>
    public List<string> NoisePhrases { get; set; } = [];

    public List<string> Junk { get; set; } = [];

    /// <summary>Folder categories that win over keywords found in the file name (except STICKER).</summary>
    public List<string> StrongFolderCategories { get; set; } = [];

    /// <summary>Card files with more pages are probably merged print batches and go to manual review.</summary>
    public int MaxCardPages { get; set; } = 30;

    public List<FileOverride> Overrides { get; set; } = [];
}

/// <summary>Per-file decision that rules cannot express. Source = relative path in the source folder.</summary>
public sealed class FileOverride
{
    public string Source { get; set; } = "";

    public string Group { get; set; } = "";

    public string? Category { get; set; }

    public string? Idol { get; set; }

    public string? Date { get; set; }

    public string Reason { get; set; } = "";
}

public sealed class CategoryRule
{
    public string Name { get; set; } = "";

    public List<string> Keywords { get; set; } = [];

    public string? Regex { get; set; }
}

public enum Group
{
    Card,
    Khac,
    ChuaPhanTich,
}

public sealed record Classification(
    Group Group,
    string? Category,
    string? Idol,
    int? Day,
    int? Month,
    string Confidence,
    string Note);

/// <summary>Turns a messy file name (plus its folder chain) into idol + date, or a non-card category.</summary>
public sealed partial class NameClassifier
{
    private readonly Dictionary<string, string> aliasByKey;
    private readonly List<(string Phrase, string Canonical)> aliasPhrases;
    private readonly HashSet<string> noise;
    private readonly List<string> noisePhrases;
    private readonly HashSet<string> junkKeys;
    private readonly HashSet<string> strongFolderCategories;
    private readonly List<(string Name, string[] Phrases, Regex? Pattern)> categories;

    public NameClassifier(Rules rules)
    {
        var all = rules.Idols
            .SelectMany(pair => pair.Value.Append(pair.Key).Select(alias => (Phrase: Normalize(alias), Canonical: pair.Key)))
            .Where(alias => alias.Phrase.Length > 0)
            .ToList();
        aliasByKey = all.GroupBy(a => Key(a.Phrase)).ToDictionary(g => g.Key, g => g.First().Canonical);
        aliasPhrases = all.DistinctBy(a => a.Phrase).OrderByDescending(a => a.Phrase.Length).ToList();
        noise = rules.NoiseWords.Select(Normalize).ToHashSet();
        noisePhrases = rules.NoisePhrases.Select(Normalize).OrderByDescending(p => p.Length).ToList();
        junkKeys = rules.Junk.Select(Key).ToHashSet();
        strongFolderCategories = rules.StrongFolderCategories.ToHashSet();
        categories = rules.Categories
            .Select(c => (c.Name, c.Keywords.Select(k => $" {Normalize(k)} ").ToArray(), c.Regex is null ? null : new Regex(c.Regex)))
            .ToList();
    }

    public Classification Classify(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        var folders = Path.GetDirectoryName(relativePath)?
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries) ?? [];

        var name = Clean(fileName);
        var (day, month, noDates) = ExtractDate(name);

        var nameCategory = FindCategory(name) ?? FindCategory(noDates);
        var strongFolder = folders.Reverse()
            .Select(folder => (Folder: folder, Category: FindCategory(Clean(folder))))
            .FirstOrDefault(f => f.Category is not null && strongFolderCategories.Contains(f.Category));
        if (strongFolder.Category is not null && nameCategory != "STICKER")
        {
            return new Classification(Group.Khac, strongFolder.Category, null, null, null, "CAO", $"Nằm trong thư mục \"{strongFolder.Folder}\"");
        }

        if (nameCategory is not null)
        {
            return new Classification(Group.Khac, nameCategory, null, null, null, "CAO", $"Tên chứa từ khóa loại \"{nameCategory}\"");
        }

        foreach (var folder in folders.Reverse())
        {
            var folderCategory = FindCategory(Clean(folder));
            if (folderCategory is not null)
            {
                return new Classification(Group.Khac, folderCategory, null, null, null, "CAO", $"Nằm trong thư mục \"{folder}\"");
            }
        }

        // raw keeps numbers that can belong to a name ("24 AT"); stripped drops counts, numbers and noise.
        var raw = Spaces().Replace(CountPhrase().Replace(noDates, " "), " ").Trim();
        var stripped = $" {raw} ";
        foreach (var phrase in noisePhrases)
        {
            stripped = stripped.Replace($" {phrase} ", " ", StringComparison.Ordinal);
        }

        var words = stripped
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !noise.Contains(word) && !NumberToken().IsMatch(word))
            .ToList();
        stripped = string.Join(' ', words);

        if (Key(stripped).Length == 0)
        {
            var folderIdol = folders.Reverse().Select(f => ExactAlias(Clean(f))).FirstOrDefault(i => i is not null);
            return folderIdol is null
                ? Unparsed("Tên không có tên idol (chỉ có ngày/số)")
                : new Classification(Group.Card, null, folderIdol, day, month, "TRUNG BINH", $"Idol lấy từ tên thư mục \"{folders[^1]}\"");
        }

        if (junkKeys.Contains(Key(raw)) || junkKeys.Contains(Key(stripped)))
        {
            return Unparsed($"Tên không mang thông tin idol: \"{stripped}\"");
        }

        var exact = ExactAlias(raw) ?? ExactAlias(stripped);
        if (exact is not null)
        {
            return new Classification(Group.Card, null, exact, day, month, "CAO", "");
        }

        if (words.Contains("VA") || fileName.Contains('+') || fileName.Contains('&'))
        {
            return Unparsed($"Có vẻ nhiều idol trong một file: \"{stripped}\"");
        }

        var found = FindWholeWordAliases(raw);
        if (found.Count > 1)
        {
            return Unparsed($"Nhiều idol trong một file: {string.Join(", ", found)}");
        }

        if (found.Count == 1)
        {
            return new Classification(Group.Card, null, found[0], day, month, "TRUNG BINH", $"Tên còn chữ thừa: \"{stripped}\"");
        }

        if (Key(stripped).Length < 4 || words.Count > 4)
        {
            return Unparsed($"Không nhận ra idol: \"{stripped}\"");
        }

        return new Classification(Group.Card, null, stripped, day, month, "THAP", "Idol chưa có trong bảng alias (tên mới)");

        Classification Unparsed(string note) => new(Group.ChuaPhanTich, null, null, day, month, "", note);
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var builder = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(character is 'đ' or 'Đ' ? 'D' : char.ToUpperInvariant(character));
        }

        return Spaces().Replace(builder.ToString(), " ").Trim();
    }

    public static string Key(string? value) => new(Normalize(value).Where(char.IsLetterOrDigit).ToArray());

    /// <summary>Removes ".pdf" fragments and punctuation, keeps dots between digits (dates), turns "20,4" into "20.4".</summary>
    private static string Clean(string value)
    {
        var text = PdfFragment().Replace(value, " ");
        text = Normalize(text);
        text = CommaDate().Replace(text, "$1.$2");
        text = NonWord().Replace(text, " ");
        text = StrayDot().Replace(text, " ");
        return Spaces().Replace(text, " ").Trim();
    }

    private string? FindCategory(string cleaned)
    {
        var padded = $" {cleaned} ";
        foreach (var (name, phrases, pattern) in categories)
        {
            if (phrases.Any(padded.Contains) || (pattern?.IsMatch(cleaned) ?? false))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>Last valid d.M date in the text (also glued to letters: "JAYSONLEI27.5"); removes every date-like token.</summary>
    private static (int? Day, int? Month, string Remainder) ExtractDate(string text)
    {
        int? day = null;
        int? month = null;
        foreach (Match match in DateToken().Matches(text))
        {
            var d = int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture);
            var m = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
            if (m is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(2024, m))
            {
                day = d;
                month = m;
            }
        }

        var rest = DateLike().Replace(text, " ");
        return (day, month, Spaces().Replace(rest, " ").Trim());
    }

    private string? ExactAlias(string text) => aliasByKey.GetValueOrDefault(Key(text));

    /// <summary>Canonical idols whose alias phrase occurs as whole words (longest first, non-overlapping).</summary>
    private List<string> FindWholeWordAliases(string text)
    {
        var padded = $" {text} ";
        var found = new List<string>();
        foreach (var (phrase, canonical) in aliasPhrases)
        {
            var needle = $" {phrase} ";
            if (!padded.Contains(needle, StringComparison.Ordinal))
            {
                continue;
            }

            padded = padded.Replace(needle, " | ", StringComparison.Ordinal);
            if (!found.Contains(canonical))
            {
                found.Add(canonical);
            }
        }

        return found;
    }

    [GeneratedRegex(@"\.pdf", RegexOptions.IgnoreCase)]
    private static partial Regex PdfFragment();

    [GeneratedRegex(@"(\d),(\d)")]
    private static partial Regex CommaDate();

    [GeneratedRegex(@"[^A-Z0-9\. ]")]
    private static partial Regex NonWord();

    [GeneratedRegex(@"(?<!\d)\.|\.(?!\d)")]
    private static partial Regex StrayDot();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"(?<!\d)(?<d>\d{1,2})\.(?<m>\d{1,2})(?!\d)")]
    private static partial Regex DateToken();

    [GeneratedRegex(@"\d+(\.\d+)+")]
    private static partial Regex DateLike();

    [GeneratedRegex(@"\b\d+\s*(CARD|CARDS|CAI|ANH|TO|TRANG|PAGE|PAGES|STICKER|DON)\b")]
    private static partial Regex CountPhrase();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex NumberToken();
}
