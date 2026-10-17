using System.Diagnostics;
using System.Text.Json;
using PDFiumCore;

namespace PdfLibraryNormalizer;

public sealed record ScannedFile(string RelativePath, long Size, DateTime LastWriteTime, int? Pages, string? Error);

/// <summary>Read-only scan: page count (PDFium), size and last write time of every PDF under a folder.</summary>
public static class Scanner
{
    public static void Run(string sourceFolder, string scanFile)
    {
        var root = Path.GetFullPath(sourceFolder);
        var files = Directory.EnumerateFiles(root, "*.pdf", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                MatchCasing = MatchCasing.CaseInsensitive,
                IgnoreInaccessible = true,
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine($"Quét {files.Count} file PDF trong {root}");
        var results = new List<ScannedFile>(files.Count);
        var stopwatch = Stopwatch.StartNew();

        fpdfview.FPDF_InitLibrary();
        try
        {
            foreach (var path in files)
            {
                var info = new FileInfo(path);
                var (pages, error) = CountPages(path);
                results.Add(new ScannedFile(Path.GetRelativePath(root, path), info.Length, info.LastWriteTime, pages, error));
                if (results.Count % 100 == 0)
                {
                    Console.WriteLine($"  {results.Count}/{files.Count} ({stopwatch.Elapsed:mm\\:ss})");
                }
            }
        }
        finally
        {
            fpdfview.FPDF_DestroyLibrary();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(scanFile))!);
        File.WriteAllText(scanFile, JsonSerializer.Serialize(new { Root = root, Files = results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Xong {results.Count} file trong {stopwatch.Elapsed:mm\\:ss}. Lỗi đọc: {results.Count(r => r.Error is not null)}. Kết quả: {scanFile}");
    }

    private static (int? Pages, string? Error) CountPages(string path)
    {
        var document = fpdfview.FPDF_LoadDocument(path, null);
        if (document == null)
        {
            var code = fpdfview.FPDF_GetLastError();
            return (null, code switch
            {
                4 => "PDF có mật khẩu",
                2 => "Không mở được file",
                3 => "File PDF hỏng hoặc sai định dạng",
                _ => $"Lỗi PDFium {code}",
            });
        }

        try
        {
            return (fpdfview.FPDF_GetPageCount(document), null);
        }
        finally
        {
            fpdfview.FPDF_CloseDocument(document);
        }
    }
}
