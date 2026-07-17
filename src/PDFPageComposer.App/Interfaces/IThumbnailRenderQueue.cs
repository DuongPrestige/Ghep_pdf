using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Interfaces;

public interface IThumbnailRenderQueue
{
    Task<PdfPageRenderResult> RenderAsync(ThumbnailRenderRequest request, CancellationToken cancellationToken);

    void Cancel(string cacheKey);

    void CancelAll();
}
