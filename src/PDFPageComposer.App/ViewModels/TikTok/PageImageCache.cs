using PDFPageComposer.App.Interfaces;

namespace PDFPageComposer.App.ViewModels.TikTok;

/// <summary>
/// Page thumbnails of the TikTok dialogs, rendered once per (file, page) with the composer's PDFium renderer.
/// The task is cached, so a page requested twice while rendering is rendered only once.
/// </summary>
public sealed class PageImageCache
{
    private const int ThumbnailPixelWidth = 200;

    private readonly IPdfRenderService? renderer;
    private readonly Dictionary<(string Path, int Page), Task<PdfPageRenderResult>> cache = [];

    public PageImageCache(IPdfRenderService? renderer)
    {
        this.renderer = renderer;
    }

    public bool CanRender => renderer is not null;

    /// <summary>The page image, or null when no renderer is available. A failed render throws to the caller.</summary>
    public async Task<PdfPageRenderResult?> GetAsync(string filePath, int pageNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (renderer is null)
        {
            return null;
        }

        var key = (filePath.ToUpperInvariant(), pageNumber);
        if (!cache.TryGetValue(key, out var task))
        {
            task = renderer.RenderPageAsync(filePath, pageNumber, ThumbnailPixelWidth, CancellationToken.None);
            cache[key] = task;
        }

        return await task;
    }
}
