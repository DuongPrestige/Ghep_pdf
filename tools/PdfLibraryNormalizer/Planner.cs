using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PdfLibraryNormalizer;

public sealed record PlanEntry(
    int No,
    string Group,
    string? Category,
    string SourceRelative,
    string? TargetRelative,
    string? Idol,
    int? Pages,
    string? Date,
    string DateSource,
    string Confidence,
    string Note,
    long Size,
    string? DuplicateOf);

public sealed record Plan(string SourceRoot, string Destination, List<PlanEntry> Entries);

/// <summary>Builds the copy plan (no file is copied here). Writes plan.csv for review and plan.json for apply.</summary>
public static class Planner
{
    public const string CardFolder = "CARD";
    public const string OtherFolder = "KHAC";
    public const string UnparsedFolder = "_CHUA_PHAN_TICH";
    public const string PlanFolder = "_KE_HOACH";

    public static void Run(string scanFile, string rulesFile, string destination)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var scan = JsonSerializer.Deserialize<ScanResult>(File.ReadAllText(scanFile), options)!;
        var rules = JsonSerializer.Deserialize<Rules>(File.ReadAllText(rulesFile), options)!;
        var classifier = new NameClassifier(rules);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var overrides = rules.Overrides.ToDictionary(o => o.Source, StringComparer.OrdinalIgnoreCase);
        var items = scan.Files
            .Select(file => (File: file, Class: overrides.TryGetValue(file.RelativePath, out var o) ? FromOverride(o) : classifier.Classify(file.RelativePath)))
            .ToList();

        var duplicateOf = FindContentDuplicates(scan.Root, items, overrides.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase));

        var entries = new List<PlanEntry>();
        var usedTargets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ordered = items
            .OrderBy(item => item.Class.Group)
            .ThenBy(item => item.Class.Idol, StringComparer.Ordinal)
            .ThenBy(item => item.File.RelativePath, StringComparer.OrdinalIgnoreCase);

        foreach (var (file, classified) in ordered)
        {
            var c = classified;
            var date = ResolveDate(c, file, today, out var dateSource);
            var dateText = date.ToString("dd.MM");

            if (duplicateOf.TryGetValue(file.RelativePath, out var original))
            {
                entries.Add(new PlanEntry(0, "TRUNG_LAP", c.Category, file.RelativePath, null, c.Idol, file.Pages, dateText, dateSource,
                    c.Confidence, "Nội dung giống hệt file khác, không copy", file.Size, original));
                continue;
            }

            string target;
            string group;
            switch (c.Group)
            {
                case Group.Card when file.Pages > rules.MaxCardPages:
                    group = "CHUA_PHAN_TICH";
                    target = Path.Combine(UnparsedFolder, Path.GetFileName(file.RelativePath));
                    c = c with { Note = $"{file.Pages} trang (> {rules.MaxCardPages}): có thể là file gộp đơn, không phải bộ card" };
                    break;
                case Group.Card when file.Pages is > 0:
                    group = "CARD";
                    var safeIdol = SafeName(c.Idol!);
                    target = Path.Combine(CardFolder, safeIdol, $"{safeIdol} - {file.Pages} PAGE - {dateText}.pdf");
                    break;
                case Group.Card:
                    group = "CHUA_PHAN_TICH";
                    target = Path.Combine(UnparsedFolder, Path.GetFileName(file.RelativePath));
                    c = c with { Note = "Không đọc được số trang PDF" };
                    break;
                case Group.Khac:
                    group = "KHAC";
                    target = Path.Combine(OtherFolder, SafeName(c.Category!), Path.GetFileName(file.RelativePath));
                    break;
                default:
                    group = "CHUA_PHAN_TICH";
                    target = Path.Combine(UnparsedFolder, Path.GetFileName(file.RelativePath));
                    break;
            }

            if (group == "CARD" && usedTargets.ContainsKey(target))
            {
                group = "CHUA_PHAN_TICH";
                c = c with { Note = $"Trùng tên đích \"{Path.GetFileName(target)}\" với file khác (nội dung khác nhau), cần chọn tay" };
                target = Path.Combine(UnparsedFolder, Path.GetFileName(file.RelativePath));
            }

            target = MakeUnique(target, usedTargets);
            entries.Add(new PlanEntry(0, group, c.Category, file.RelativePath, target, group == "CARD" ? c.Idol : null,
                file.Pages, group == "CARD" ? dateText : null, group == "CARD" ? dateSource : "", c.Confidence, c.Note, file.Size, null));
        }

        entries = entries.Select((entry, index) => entry with { No = index + 1 }).ToList();

        var planDirectory = Path.Combine(destination, PlanFolder);
        Directory.CreateDirectory(planDirectory);
        var plan = new Plan(scan.Root, Path.GetFullPath(destination), entries);
        File.WriteAllText(Path.Combine(planDirectory, "plan.json"), JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        WriteCsv(Path.Combine(planDirectory, "plan.csv"), entries);
        WriteCsv(Path.Combine(planDirectory, "chua-phan-tich.csv"), entries.Where(e => e.Group == "CHUA_PHAN_TICH").ToList());
        WriteCsv(Path.Combine(planDirectory, "can-xem-lai.csv"), entries.Where(e => e.Group == "CARD" && e.Confidence != "CAO").ToList());
        PrintSummary(entries);
    }

    private static Classification FromOverride(FileOverride o)
    {
        var group = o.Group.ToUpperInvariant() switch
        {
            "CARD" => Group.Card,
            "KHAC" => Group.Khac,
            _ => Group.ChuaPhanTich,
        };
        int? day = null;
        int? month = null;
        if (o.Date?.Split('.') is [var d, var m] && int.TryParse(d, out var dd) && int.TryParse(m, out var mm))
        {
            day = dd;
            month = mm;
        }

        return new Classification(group, o.Category, o.Idol, day, month, group == Group.ChuaPhanTich ? "" : "CAO", $"Quyết định khi rà soát: {o.Reason}");
    }

    private static DateOnly ResolveDate(Classification c, ScannedFile file, DateOnly today, out string source)
    {
        if (c.Day is int day && c.Month is int month)
        {
            source = "tên file";
            var year = today.Year;
            if (day > DateTime.DaysInMonth(year, month) || new DateOnly(year, month, Math.Min(day, DateTime.DaysInMonth(year, month))) > today)
            {
                year--;
            }

            return new DateOnly(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
        }

        source = "ngày sửa file";
        return DateOnly.FromDateTime(file.LastWriteTime);
    }

    /// <summary>Same size + same SHA-256 ⇒ duplicate. Keeps the best copy (card with high confidence, then outside nested copies, then shortest path).</summary>
    private static Dictionary<string, string> FindContentDuplicates(string root, List<(ScannedFile File, Classification Class)> items, HashSet<string> overridden)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sameSize in items.GroupBy(item => item.File.Size).Where(g => g.Count() > 1))
        {
            foreach (var sameHash in sameSize.GroupBy(item => Hash(Path.Combine(root, item.File.RelativePath))).Where(g => g.Count() > 1))
            {
                var keep = sameHash
                    .OrderBy(item => overridden.Contains(item.File.RelativePath) ? 0 : 1)
                    .ThenBy(item => item.Class.Group == Group.Card && item.Class.Confidence == "CAO" ? 0 : item.Class.Group == Group.Card ? 1 : item.Class.Group == Group.Khac ? 2 : 3)
                    .ThenBy(item => item.File.RelativePath.Count(ch => ch == Path.DirectorySeparatorChar))
                    .ThenBy(item => item.File.RelativePath.Length)
                    .First();
                foreach (var duplicate in sameHash.Where(item => !ReferenceEquals(item.File, keep.File)))
                {
                    result[duplicate.File.RelativePath] = keep.File.RelativePath;
                }
            }
        }

        return result;
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    /// <summary>Adds " (2)", " (3)"… before .pdf when two different files would get the same name.</summary>
    private static string MakeUnique(string target, Dictionary<string, int> used)
    {
        if (used.TryAdd(target, 1))
        {
            return target;
        }

        var directory = Path.GetDirectoryName(target)!;
        var stem = Path.GetFileNameWithoutExtension(target);
        for (var n = used[target] + 1; ; n++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({n}).pdf");
            if (used.TryAdd(candidate, 1))
            {
                used[target] = n;
                return candidate;
            }
        }
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? ' ' : ch).ToArray()).Trim();
    }

    private static void WriteCsv(string path, List<PlanEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("STT,Nhom,Loai,DuongDanGoc,DuongDanMoi,Idol,SoTrang,Ngay,NguonNgay,DoTinCay,GhiChu,TrungVoi,MB");
        foreach (var e in entries)
        {
            builder.AppendLine(string.Join(',',
                e.No, e.Group, Csv(e.Category), Csv(e.SourceRelative), Csv(e.TargetRelative), Csv(e.Idol), e.Pages,
                Csv(e.Date), Csv(e.DateSource), Csv(e.Confidence), Csv(e.Note), Csv(e.DuplicateOf), (e.Size / 1048576.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Csv(string? value) =>
        string.IsNullOrEmpty(value) ? "" : value.IndexOfAny([',', '"', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static void PrintSummary(List<PlanEntry> entries)
    {
        Console.WriteLine($"Tổng: {entries.Count} file");
        foreach (var group in entries.GroupBy(e => e.Group))
        {
            Console.WriteLine($"  {group.Key}: {group.Count()} file, {group.Sum(e => e.Size) / 1073741824.0:0.0} GB");
        }

        foreach (var category in entries.Where(e => e.Group == "KHAC").GroupBy(e => e.Category).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"    KHAC/{category.Key}: {category.Count()}");
        }

        var cards = entries.Where(e => e.Group == "CARD").ToList();
        Console.WriteLine($"  CARD theo độ tin cậy: {string.Join(", ", cards.GroupBy(e => e.Confidence).Select(g => $"{g.Key}={g.Count()}"))}");
        Console.WriteLine($"  Số idol: {cards.Select(e => e.Idol).Distinct().Count()}");
    }

    private sealed record ScanResult(string Root, List<ScannedFile> Files);
}
