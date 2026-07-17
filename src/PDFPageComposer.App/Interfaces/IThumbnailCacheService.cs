using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Interfaces;

public interface IThumbnailCacheService
{
    bool TryGet(string key, out PdfPageRenderResult result);

    void Set(string key, PdfPageRenderResult result);

    void Clear();

    int Count { get; }

    long EstimatedBytes { get; }
}
