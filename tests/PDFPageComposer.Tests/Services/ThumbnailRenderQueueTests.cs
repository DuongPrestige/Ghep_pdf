using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Models;
using PDFPageComposer.App.Services;

namespace PDFPageComposer.Tests.Services;

public sealed class ThumbnailRenderQueueTests
{
    [Fact]
    public async Task RenderAsync_returns_cached_result_without_rendering_again()
    {
        var render = new ImmediateRenderService();
        var cache = new ThumbnailCacheService();
        var queue = new ThumbnailRenderQueue(render, cache, maxConcurrency: 1);
        var request = CreateRequest("a", 1);

        await queue.RenderAsync(request, CancellationToken.None);
        await queue.RenderAsync(request, CancellationToken.None);

        Assert.Equal(1, render.CallCount);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task RenderAsync_limits_concurrent_renders()
    {
        var render = new BlockingRenderService();
        var queue = new ThumbnailRenderQueue(render, new ThumbnailCacheService(), maxConcurrency: 2);

        var tasks = Enumerable.Range(1, 5)
            .Select(index => queue.RenderAsync(CreateRequest(index.ToString(), index), CancellationToken.None))
            .ToList();

        await render.WaitForStartedAsync(2);
        Assert.Equal(2, render.MaxConcurrentCalls);

        render.ReleaseAll();
        await Task.WhenAll(tasks);

        Assert.Equal(2, render.MaxConcurrentCalls);
        Assert.Equal(5, render.CallCount);
    }

    [Fact]
    public async Task Cancel_cancels_queued_or_running_render()
    {
        var render = new BlockingRenderService();
        var queue = new ThumbnailRenderQueue(render, new ThumbnailCacheService(), maxConcurrency: 1);
        var request = CreateRequest("a", 1);

        var renderTask = queue.RenderAsync(request, CancellationToken.None);
        await render.WaitForStartedAsync(1);

        queue.Cancel(request.CacheKey);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => renderTask);
    }

    private static ThumbnailRenderRequest CreateRequest(string key, int pageNumber)
    {
        return new ThumbnailRenderRequest(key, @"D:\Docs\a.pdf", "fingerprint", pageNumber, 100);
    }

    private sealed class BlockingRenderService : IPdfRenderService
    {
        private readonly object gate = new();
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int currentCalls;
        private int startedCalls;

        public int CallCount { get; private set; }

        public int MaxConcurrentCalls { get; private set; }

        public async Task<PdfPageRenderResult> RenderPageAsync(
            string filePath,
            int pageNumber,
            int pixelWidth,
            CancellationToken cancellationToken)
        {
            lock (gate)
            {
                CallCount++;
                currentCalls++;
                startedCalls++;
                MaxConcurrentCalls = Math.Max(MaxConcurrentCalls, currentCalls);
                started.TrySetResult();
            }

            try
            {
                await release.Task.WaitAsync(cancellationToken);
                return new PdfPageRenderResult(pixelWidth, pixelWidth, pixelWidth * 4, new byte[pixelWidth * pixelWidth * 4]);
            }
            finally
            {
                lock (gate)
                {
                    currentCalls--;
                }
            }
        }

        public async Task WaitForStartedAsync(int expectedCount)
        {
            while (true)
            {
                lock (gate)
                {
                    if (startedCalls >= expectedCount)
                    {
                        return;
                    }
                }

                await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        public void ReleaseAll()
        {
            release.SetResult();
        }
    }

    private sealed class ImmediateRenderService : IPdfRenderService
    {
        public int CallCount { get; private set; }

        public Task<PdfPageRenderResult> RenderPageAsync(
            string filePath,
            int pageNumber,
            int pixelWidth,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new PdfPageRenderResult(pixelWidth, pixelWidth, pixelWidth * 4, new byte[pixelWidth * pixelWidth * 4]));
        }
    }
}
