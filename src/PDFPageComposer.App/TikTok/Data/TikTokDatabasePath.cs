using System.IO;

namespace PDFPageComposer.App.TikTok.Data;

/// <summary>Location of the TikTok module database (TECH_SPEC_TIKTOK section 4).</summary>
public static class TikTokDatabasePath
{
    public const string FileName = "tiktok.db";

    /// <summary>%LOCALAPPDATA%\PDFPageComposer\tiktok.db</summary>
    public static string Default => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PDFPageComposer",
        FileName);
}
