namespace PDFPageComposer.App.Models;

public sealed class AppUpdateManifest
{
    public string Version { get; set; } = string.Empty;

    public string DownloadUrl { get; set; } = string.Empty;

    public string? Sha256 { get; set; }

    public string? Notes { get; set; }
}
