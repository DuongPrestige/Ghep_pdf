namespace PDFPageComposer.App.Models;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    public string? FoxitExecutablePath { get; set; }

    public int ThumbnailCacheLimit { get; set; } = 128 * 1024 * 1024;

    public string? LastOpenDirectory { get; set; }

    public string? UpdateManifestUrl { get; set; }
}
