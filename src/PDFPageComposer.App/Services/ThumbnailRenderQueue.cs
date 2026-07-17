using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;

namespace PDFPageComposer.App.Services;

public sealed class ThumbnailRenderQueue : IThumbnailRenderQueue, IDisposable
{
    private readonly IPdfRenderService pdfRenderService;
    private readonly IThumbnailCacheService cache;
    private readonly SemaphoreSlim concurrencyGate;
    private readonly Dictionary<string, CancellationTokenSource> runningRequests = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private bool disposed;

    public ThumbnailRenderQueue(IPdfRenderService pdfRenderService, IThumbnailCacheService cache)
        : this(pdfRenderService, cache, Math.Clamp(Environment.ProcessorCount / 2, 1, 2))
    {
    }

    public ThumbnailRenderQueue(IPdfRenderService pdfRenderService, IThumbnailCacheService cache, int maxConcurrency)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);
        this.pdfRenderService = pdfRenderService;
        this.cache = cache;
        concurrencyGate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public async Task<PdfPageRenderResult> RenderAsync(ThumbnailRenderRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (cache.TryGet(request.CacheKey, out var cached))
        {
            return cached;
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (gate)
        {
            if (runningRequests.Remove(request.CacheKey, out var previous))
            {
                previous.Cancel();
            }

            runningRequests[request.CacheKey] = linkedCancellation;
        }

        try
        {
            await concurrencyGate.WaitAsync(linkedCancellation.Token).ConfigureAwait(false);
            try
            {
                linkedCancellation.Token.ThrowIfCancellationRequested();
                if (cache.TryGet(request.CacheKey, out cached))
                {
                    return cached;
                }

                var result = await pdfRenderService
                    .RenderPageAsync(request.FilePath, request.PageNumber, request.PixelWidth, linkedCancellation.Token)
                    .ConfigureAwait(false);
                linkedCancellation.Token.ThrowIfCancellationRequested();
                cache.Set(request.CacheKey, result);
                return result;
            }
            finally
            {
                concurrencyGate.Release();
            }
        }
        finally
        {
            lock (gate)
            {
                if (runningRequests.TryGetValue(request.CacheKey, out var current) && ReferenceEquals(current, linkedCancellation))
                {
                    runningRequests.Remove(request.CacheKey);
                }
            }
        }
    }

    public void Cancel(string cacheKey)
    {
        lock (gate)
        {
            if (!runningRequests.Remove(cacheKey, out var cancellation))
            {
                return;
            }

            cancellation.Cancel();
        }
    }

    public void CancelAll()
    {
        List<CancellationTokenSource> cancellations;
        lock (gate)
        {
            cancellations = runningRequests.Values.ToList();
            runningRequests.Clear();
        }

        foreach (var cancellation in cancellations)
        {
            cancellation.Cancel();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancelAll();
        concurrencyGate.Dispose();
    }
}
