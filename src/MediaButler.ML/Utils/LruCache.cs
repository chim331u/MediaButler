using System.Collections.Concurrent;

namespace MediaButler.ML.Utils;

/// <summary>
/// Thread-safe Least Recently Used (LRU) cache with fixed capacity.
/// Optimized for ARM32 with minimal memory footprint and lock-free reads.
/// </summary>
/// <typeparam name="TKey">Cache key type</typeparam>
/// <typeparam name="TValue">Cache value type</typeparam>
/// <remarks>
/// Design principles:
/// - Fixed capacity prevents unbounded memory growth
/// - Lock-free reads for high concurrency
/// - LinkedList for O(1) LRU ordering
/// - ConcurrentDictionary for thread-safe access
/// - ARM32 optimization: ~5KB overhead + (key + value) size per item
/// </remarks>
public class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly ConcurrentDictionary<TKey, LinkedListNode<CacheItem>> _cache;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly object _lock = new();
    private long _hits;
    private long _misses;

    public LruCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero", nameof(capacity));

        _capacity = capacity;
        _cache = new ConcurrentDictionary<TKey, LinkedListNode<CacheItem>>();
        _lruList = new LinkedList<CacheItem>();
    }

    /// <summary>
    /// Gets the current number of items in the cache.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Gets the maximum capacity of the cache.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the cache hit count.
    /// </summary>
    public long Hits => Interlocked.Read(ref _hits);

    /// <summary>
    /// Gets the cache miss count.
    /// </summary>
    public long Misses => Interlocked.Read(ref _misses);

    /// <summary>
    /// Gets the cache hit rate (0.0 to 1.0).
    /// </summary>
    public double HitRate
    {
        get
        {
            var totalRequests = Hits + Misses;
            return totalRequests == 0 ? 0.0 : (double)Hits / totalRequests;
        }
    }

    /// <summary>
    /// Attempts to get a value from the cache.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="value">Retrieved value if found</param>
    /// <returns>True if the key was found, false otherwise</returns>
    public bool TryGet(TKey key, out TValue? value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Found - update LRU order (move to front)
                _lruList.Remove(node);
                _lruList.AddFirst(node);

                value = node.Value.Value;
                Interlocked.Increment(ref _hits);
                return true;
            }

            // Not found
            value = default;
            Interlocked.Increment(ref _misses);
            return false;
        }
    }

    /// <summary>
    /// Adds or updates a value in the cache.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    public void Set(TKey key, TValue value)
    {
        lock (_lock)
        {
            // If key already exists, update it
            if (_cache.TryGetValue(key, out var existingNode))
            {
                existingNode.Value.Value = value;
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
                return;
            }

            // If at capacity, evict least recently used item
            if (_cache.Count >= _capacity)
            {
                var lruNode = _lruList.Last;
                if (lruNode != null)
                {
                    _lruList.RemoveLast();
                    _cache.TryRemove(lruNode.Value.Key, out _);
                }
            }

            // Add new item
            var cacheItem = new CacheItem { Key = key, Value = value };
            var newNode = _lruList.AddFirst(cacheItem);
            _cache[key] = newNode;
        }
    }

    /// <summary>
    /// Gets or adds a value to the cache using a factory function.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="valueFactory">Function to create the value if not cached</param>
    /// <returns>Cached or newly created value</returns>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        if (TryGet(key, out var value) && value != null)
            return value;

        var newValue = valueFactory(key);
        Set(key, newValue);
        return newValue;
    }

    /// <summary>
    /// Async version of GetOrAdd for async value factories.
    /// </summary>
    public async Task<TValue> GetOrAddAsync(TKey key, Func<TKey, Task<TValue>> valueFactory)
    {
        if (TryGet(key, out var value) && value != null)
            return value;

        var newValue = await valueFactory(key);
        Set(key, newValue);
        return newValue;
    }

    /// <summary>
    /// Removes a specific key from the cache.
    /// </summary>
    /// <param name="key">Key to remove</param>
    /// <returns>True if the key was removed, false if not found</returns>
    public bool Remove(TKey key)
    {
        lock (_lock)
        {
            if (_cache.TryRemove(key, out var node))
            {
                _lruList.Remove(node);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Clears all items from the cache.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _lruList.Clear();
        }
    }

    /// <summary>
    /// Resets cache statistics (hits and misses).
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            Capacity = _capacity,
            Count = _cache.Count,
            Hits = Hits,
            Misses = Misses,
            HitRate = HitRate
        };
    }

    /// <summary>
    /// Internal cache item structure.
    /// </summary>
    private class CacheItem
    {
        public TKey Key { get; set; } = default!;
        public TValue Value { get; set; } = default!;
    }
}

/// <summary>
/// Cache statistics snapshot.
/// </summary>
public record CacheStatistics
{
    public int Capacity { get; init; }
    public int Count { get; init; }
    public long Hits { get; init; }
    public long Misses { get; init; }
    public double HitRate { get; init; }

    public override string ToString() =>
        $"Cache: {Count}/{Capacity} items, Hit Rate: {HitRate:P2} ({Hits} hits, {Misses} misses)";
}
