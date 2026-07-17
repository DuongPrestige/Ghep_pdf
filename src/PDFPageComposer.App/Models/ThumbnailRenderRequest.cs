namespace PDFPageComposer.App.Models;

public sealed record ThumbnailRenderRequest(
    string CacheKey,
    string FilePath,
    string Fingerprint,
    int PageNumber,
    int PixelWidth);
