using System.Collections.Concurrent;
using System.IO;
using PdfSharp.Fonts;

namespace PDFPageComposer.App.TikTok.Export;

/// <summary>
/// Resolves Arial (and Segoe UI as a fallback) from the Windows fonts folder. Both fonts contain the Vietnamese
/// glyphs needed by separator pages. Any requested family is mapped to these fonts.
/// </summary>
internal sealed class WindowsFontResolver : IFontResolver
{
    public static readonly WindowsFontResolver Instance = new();

    private const string RegularFace = "tiktok-sans-regular";
    private const string BoldFace = "tiktok-sans-bold";

    private static readonly string[] RegularFiles = ["arial.ttf", "segoeui.ttf"];
    private static readonly string[] BoldFiles = ["arialbd.ttf", "segoeuib.ttf"];

    private readonly ConcurrentDictionary<string, byte[]> cache = new(StringComparer.Ordinal);

    private WindowsFontResolver()
    {
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? BoldFace : RegularFace, mustSimulateBold: false, mustSimulateItalic: isItalic);

    public byte[] GetFont(string faceName) =>
        cache.GetOrAdd(faceName, static face => LoadFont(face == BoldFace ? BoldFiles : RegularFiles));

    private static byte[] LoadFont(IReadOnlyList<string> candidates)
    {
        var fontsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var candidate in candidates)
        {
            var path = Path.Combine(fontsFolder, candidate);
            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }
        }

        throw new FileNotFoundException(
            $"Không tìm thấy font Arial hoặc Segoe UI trong \"{fontsFolder}\" để tạo trang phân cách.");
    }
}
