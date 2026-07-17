using PDFPageComposer.App.Interfaces;

namespace PDFPageComposer.App.Services;

public sealed class ThumbnailCacheService : IThumbnailCacheService, IDisposable
{
    private readonly long memoryBudgetBytes;
    private readonly Dictionary<string, CacheEntry> entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> lruKeys = new();
    private readonly object gate = new();
    private long estimatedBytes;

    public ThumbnailCacheService()
        : this(64 * 1024 * 1024)
    {
    }

    public ThumbnailCacheService(long memoryBudgetBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(memoryBudgetBytes);
        this.memoryBudgetBytes = memoryBudgetBytes;
    }

    public int Count
    {
        get
        {
            lock (gate)
            {
                return entries.Count;
            }
        }
    }

    public long EstimatedBytes
    {
        get
        {
            lock (gate)
            {
                return estimatedBytes;
            }
        }
    }

    public bool TryGet(string key, out PdfPageRenderResult result)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                result = default!;
                return false;
            }

            lruKeys.Remove(entry.Node);
            lruKeys.AddFirst(entry.Node);
            result = entry.Result;
            return true;
        }
    }

    public void Set(string key, PdfPageRenderResult result)
    {
        lock (gate)
        {
            if (entries.TryGetValue(key, out var existing))
            {
                estimatedBytes -= existing.SizeBytes;
                lruKeys.Remove(existing.Node);
                entries.Remove(key);
            }

            var node = new LinkedListNode<string>(key);
            var sizeBytes = EstimateBytes(result);
            entries[key] = new CacheEntry(result, sizeBytes, node);
            lruKeys.AddFirst(node);
            estimatedBytes += sizeBytes;

            EvictOverBudget();
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            entries.Clear();
            lruKeys.Clear();
            estimatedBytes = 0;
        }
    }

    private void EvictOverBudget()
    {
        while (estimatedBytes > memoryBudgetBytes && lruKeys.Last is { } last)
        {
            var key = last.Value;
            if (entries.Remove(key, out var entry))
            {
                estimatedBytes -= entry.SizeBytes;
            }

            lruKeys.RemoveLast();
        }
    }

    private static long EstimateBytes(PdfPageRenderResult result)
    {
        return result.Bgra32Pixels.LongLength + (sizeof(int) * 3);
    }

    private sealed record CacheEntry(PdfPageRenderResult Result, long SizeBytes, LinkedListNode<string> Node);

    public void Dispose()
    {
        Clear();
    }
}
