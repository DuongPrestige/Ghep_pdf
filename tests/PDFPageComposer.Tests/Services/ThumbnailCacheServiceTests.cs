using PDFPageComposer.App.Interfaces;
using PDFPageComposer.App.Services;

namespace PDFPageComposer.Tests.Services;

public sealed class ThumbnailCacheServiceTests
{
    [Fact]
    public void TryGet_returns_cached_thumbnail_and_updates_lru()
    {
        var cache = new ThumbnailCacheService(memoryBudgetBytes: 1_000);
        var result = CreateResult(10);

        cache.Set("a", result);

        Assert.True(cache.TryGet("a", out var cached));
        Assert.Same(result.Bgra32Pixels, cached.Bgra32Pixels);
        Assert.Equal(1, cache.Count);
        Assert.True(cache.EstimatedBytes > 0);
    }

    [Fact]
    public void Set_evicts_least_recently_used_entries_when_budget_is_exceeded()
    {
        var cache = new ThumbnailCacheService(memoryBudgetBytes: 900);
        cache.Set("a", CreateResult(10));
        cache.Set("b", CreateResult(10));

        Assert.True(cache.TryGet("a", out _));

        cache.Set("c", CreateResult(10));

        Assert.True(cache.TryGet("a", out _));
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
        Assert.True(cache.EstimatedBytes <= 900);
    }

    [Fact]
    public void Clear_releases_all_cached_entries()
    {
        var cache = new ThumbnailCacheService(memoryBudgetBytes: 1_000);
        cache.Set("a", CreateResult(10));

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.EstimatedBytes);
        Assert.False(cache.TryGet("a", out _));
    }

    private static PdfPageRenderResult CreateResult(int side)
    {
        return new PdfPageRenderResult(side, side, side * 4, new byte[side * side * 4]);
    }
}
