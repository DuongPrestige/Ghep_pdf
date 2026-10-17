using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace PdfLibraryNormalizer;

/// <summary>
/// Copies files according to an approved plan.json. Never touches the source folder, never overwrites:
/// an existing target with the same size is treated as already copied (safe to re-run after an interruption).
/// </summary>
public static class Applier
{
    public static void Run(string planFile, string destination)
    {
        var plan = JsonSerializer.Deserialize<Plan>(File.ReadAllText(planFile))!;
        var destinationRoot = Path.GetFullPath(destination);
        if (!string.Equals(destinationRoot.TrimEnd('\\'), plan.Destination.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Kế hoạch được lập cho {plan.Destination}, không phải {destinationRoot}.");
        }

        if (destinationRoot.StartsWith(Path.GetFullPath(plan.SourceRoot), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Thư mục đích không được nằm trong thư mục gốc.");
        }

        var toCopy = plan.Entries.Where(e => e.TargetRelative is not null).ToList();
        var needed = toCopy.Sum(e => e.Size);
        var drive = new DriveInfo(Path.GetPathRoot(destinationRoot)!);
        if (drive.AvailableFreeSpace < needed + (1L << 30))
        {
            throw new InvalidOperationException($"Không đủ dung lượng: cần {needed / 1073741824.0:0.0} GB, còn {drive.AvailableFreeSpace / 1073741824.0:0.0} GB.");
        }

        var log = new StringBuilder("STT,KetQua,DuongDanGoc,DuongDanMoi,GhiChu\n");
        int copied = 0, skipped = 0, failed = 0;
        var stopwatch = Stopwatch.StartNew();

        foreach (var entry in toCopy)
        {
            var source = Path.Combine(plan.SourceRoot, entry.SourceRelative);
            var target = Path.Combine(destinationRoot, entry.TargetRelative!);
            string result;
            string note = "";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (File.Exists(target))
                {
                    if (new FileInfo(target).Length == entry.Size)
                    {
                        result = "DA_CO";
                        skipped++;
                    }
                    else
                    {
                        result = "LOI";
                        note = "File đích đã tồn tại với dung lượng khác, không ghi đè";
                        failed++;
                    }
                }
                else
                {
                    File.Copy(source, target, overwrite: false);
                    if (new FileInfo(target).Length != entry.Size)
                    {
                        throw new IOException("Dung lượng sau khi copy không khớp");
                    }

                    result = "OK";
                    copied++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result = "LOI";
                note = ex.Message;
                failed++;
            }

            log.AppendLine($"{entry.No},{result},\"{entry.SourceRelative}\",\"{entry.TargetRelative}\",\"{note.Replace("\"", "'")}\"");
            var done = copied + skipped + failed;
            if (done % 50 == 0)
            {
                Console.WriteLine($"  {done}/{toCopy.Count} ({stopwatch.Elapsed:mm\\:ss})");
            }
        }

        var logPath = Path.Combine(destinationRoot, Planner.PlanFolder, $"apply-log-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        File.WriteAllText(logPath, log.ToString(), new UTF8Encoding(true));
        Console.WriteLine($"Xong trong {stopwatch.Elapsed:mm\\:ss}: copy {copied}, đã có sẵn {skipped}, lỗi {failed}. Nhật ký: {logPath}");
        Environment.ExitCode = failed > 0 ? 2 : 0;
    }
}
