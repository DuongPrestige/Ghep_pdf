using System.IO;

namespace PDFPageComposer.App.TikTok;

/// <summary>
/// Keeps a copy of every PDF picked from outside the library in "Xem / chọn file" (2026-11-08), so the order still
/// prints (and "In lại" still works) after the original file is moved or deleted. Copies live in
/// <c>&lt;library&gt;\_ThuCong\&lt;order id&gt;\</c>; the library scan skips that folder. The source file is never changed.
/// </summary>
public static class ManualFileStore
{
    public const string FolderName = "_ThuCong";

    /// <summary>The store folder: inside the PDF library when one is set, otherwise next to the TikTok database.</summary>
    public static string RootFor(string? libraryFolder) =>
        string.IsNullOrWhiteSpace(libraryFolder)
            ? Path.Combine(Path.GetDirectoryName(Data.TikTokDatabasePath.Default)!, FolderName)
            : Path.Combine(libraryFolder.Trim(), FolderName);

    /// <summary>True when <paramref name="path"/> lies inside a "_ThuCong" folder directly under <paramref name="libraryFolder"/>.</summary>
    public static bool IsInStore(string libraryFolder, string path)
    {
        var root = Path.GetFullPath(Path.Combine(libraryFolder, FolderName)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copies <paramref name="sourcePath"/> into the order folder of <paramref name="root"/> and returns the copy's path.
    /// A file already inside the store is returned as is; an existing copy with the same name, size and time is reused;
    /// a different file with the same name gets a " (2)" suffix, so a copy used by a printed batch is never overwritten.
    /// </summary>
    public static string Import(string root, string orderId, string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullSource = Path.GetFullPath(sourcePath);
        if (fullSource.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return fullSource;
        }

        var folder = Path.Combine(fullRoot, SafeName(orderId));
        Directory.CreateDirectory(folder);
        var source = new FileInfo(fullSource);
        var baseName = Path.GetFileNameWithoutExtension(fullSource);
        var extension = Path.GetExtension(fullSource);
        for (var attempt = 1; ; attempt++)
        {
            var name = attempt == 1 ? baseName + extension : $"{baseName} ({attempt}){extension}";
            var target = new FileInfo(Path.Combine(folder, name));
            if (!target.Exists)
            {
                File.Copy(fullSource, target.FullName);
                return target.FullName;
            }

            if (target.Length == source.Length && target.LastWriteTimeUtc == source.LastWriteTimeUtc)
            {
                return target.FullName;
            }
        }
    }

    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Trim().Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }
}
