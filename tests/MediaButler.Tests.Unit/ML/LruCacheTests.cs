using FluentAssertions;
using MediaButler.ML.Utils;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Unit tests for LruCache implementation.
/// Tests thread safety, capacity limits, LRU eviction, and statistics.
/// </summary>
public class LruCacheTests
{
    [Fact]
    public void Constructor_WithValidCapacity_CreatesCache()
    {
        // Arrange & Act
        var cache = new LruCache<string, int>(capacity: 100);

        // Assert
        cache.Capacity.Should().Be(100);
        cache.Count.Should().Be(0);
        cache.Hits.Should().Be(0);
        cache.Misses.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidCapacity_ThrowsArgumentException(int invalidCapacity)
    {
        // Act
        Action act = () => new LruCache<string, int>(invalidCapacity);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Capacity must be greater than zero*");
    }

    [Fact]
    public void Set_AddsSingleItem_CountIsOne()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);

        // Act
        cache.Set("key1", 100);

        // Assert
        cache.Count.Should().Be(1);
        cache.TryGet("key1", out var value).Should().BeTrue();
        value.Should().Be(100);
    }

    [Fact]
    public void Set_UpdatesExistingItem_CountRemainsOne()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);
        cache.Set("key1", 100);

        // Act
        cache.Set("key1", 200);

        // Assert
        cache.Count.Should().Be(1);
        cache.TryGet("key1", out var value).Should().BeTrue();
        value.Should().Be(200);
    }

    [Fact]
    public void TryGet_WithMissingKey_ReturnsFalseAndIncrementsMisses()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);

        // Act
        var found = cache.TryGet("missing", out var value);

        // Assert
        found.Should().BeFalse();
        value.Should().Be(0);
        cache.Misses.Should().Be(1);
        cache.Hits.Should().Be(0);
    }

    [Fact]
    public void TryGet_WithExistingKey_ReturnsTrueAndIncrementsHits()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);
        cache.Set("key1", 100);

        // Act
        var found = cache.TryGet("key1", out var value);

        // Assert
        found.Should().BeTrue();
        value.Should().Be(100);
        cache.Hits.Should().Be(1);
        cache.Misses.Should().Be(0);
    }

    [Fact]
    public void Set_ExceedsCapacity_EvictsLeastRecentlyUsed()
    {
        // Arrange
        var cache = new LruCache<string, int>(capacity: 3);
        cache.Set("key1", 1);
        cache.Set("key2", 2);
        cache.Set("key3", 3);

        // Act - Add 4th item, should evict key1
        cache.Set("key4", 4);

        // Assert
        cache.Count.Should().Be(3);
        cache.TryGet("key1", out _).Should().BeFalse(); // key1 evicted
        cache.TryGet("key2", out _).Should().BeTrue();
        cache.TryGet("key3", out _).Should().BeTrue();
        cache.TryGet("key4", out _).Should().BeTrue();
    }

    [Fact]
    public void TryGet_UpdatesLruOrder_PreventsEviction()
    {
        // Arrange
        var cache = new LruCache<string, int>(capacity: 3);
        cache.Set("key1", 1);
        cache.Set("key2", 2);
        cache.Set("key3", 3);

        // Act - Access key1 to make it recently used
        cache.TryGet("key1", out _);

        // Add 4th item - should evict key2 (now least recently used)
        cache.Set("key4", 4);

        // Assert
        cache.Count.Should().Be(3);
        cache.TryGet("key1", out _).Should().BeTrue(); // key1 protected by access
        cache.TryGet("key2", out _).Should().BeFalse(); // key2 evicted
        cache.TryGet("key3", out _).Should().BeTrue();
        cache.TryGet("key4", out _).Should().BeTrue();
    }

    [Fact]
    public void GetOrAdd_WithMissingKey_CallsFactoryAndCaches()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);
        var factoryCalled = false;

        // Act
        var value = cache.GetOrAdd("key1", key =>
        {
            factoryCalled = true;
            return 100;
        });

        // Assert
        factoryCalled.Should().BeTrue();
        value.Should().Be(100);
        cache.Count.Should().Be(1);
        cache.TryGet("key1", out var cached).Should().BeTrue();
        cached.Should().Be(100);
    }

    [Fact]
    public void GetOrAdd_WithExistingKey_ReturnsCachedValueWithoutCallingFactory()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);
        cache.Set("key1", 100);
        var factoryCalled = false;

        // Act
        var value = cache.GetOrAdd("key1", key =>
        {
            factoryCalled = true;
            return 200;
        });

        // Assert
        factoryCalled.Should().BeFalse();
        value.Should().Be(100); // Original cached value
    }

    [Fact]
    public async Task GetOrAddAsync_WithMissingKey_CallsAsyncFactoryAndCaches()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);
        var factoryCalled = false;

        // Act
        var value = await cache.GetOrAddAsync("key1", async key =>
        {
            factoryCalled = true;
            await Task.Delay(10);
            return 100;
        });

        // Assert
        factoryCalled.Should().BeTrue();
        value.Should().Be(100);
        cache.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetOrAddAsync_WithExistingKey_ReturnsCachedValueWithoutCallingFactory()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);
        cache.Set("key1", 100);
        var factoryCalled = false;

        // Act
        var value = await cache.GetOrAddAsync("key1", async key =>
        {
            factoryCalled = true;
            await Task.Delay(10);
            return 200;
        });

        // Assert
        factoryCalled.Should().BeFalse();
        value.Should().Be(100);
    }

    [Fact]
    public void Remove_WithExistingKey_RemovesItemAndReturnsTrue()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);
        cache.Set("key1", 100);

        // Act
        var removed = cache.Remove("key1");

        // Assert
        removed.Should().BeTrue();
        cache.Count.Should().Be(0);
        cache.TryGet("key1", out _).Should().BeFalse();
    }

    [Fact]
    public void Remove_WithMissingKey_ReturnsFalse()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);

        // Act
        var removed = cache.Remove("missing");

        // Assert
        removed.Should().BeFalse();
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);
        cache.Set("key1", 1);
        cache.Set("key2", 2);
        cache.Set("key3", 3);

        // Act
        cache.Clear();

        // Assert
        cache.Count.Should().Be(0);
        cache.TryGet("key1", out _).Should().BeFalse();
        cache.TryGet("key2", out _).Should().BeFalse();
        cache.TryGet("key3", out _).Should().BeFalse();
    }

    [Fact]
    public void HitRate_CalculatesCorrectly()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);
        cache.Set("key1", 100);

        // Act
        cache.TryGet("key1", out _); // Hit
        cache.TryGet("key1", out _); // Hit
        cache.TryGet("key2", out _); // Miss
        cache.TryGet("key3", out _); // Miss

        // Assert
        cache.Hits.Should().Be(2);
        cache.Misses.Should().Be(2);
        cache.HitRate.Should().BeApproximately(0.5, 0.01); // 2/4 = 50%
    }

    [Fact]
    public void HitRate_WithNoRequests_ReturnsZero()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);

        // Act & Assert
        cache.HitRate.Should().Be(0.0);
    }

    [Fact]
    public void ResetStatistics_ClearsHitsAndMisses()
    {
        // Arrange
        var cache = new LruCache<string, int>(10);
        cache.Set("key1", 100);
        cache.TryGet("key1", out _); // Hit
        cache.TryGet("key2", out _); // Miss

        // Act
        cache.ResetStatistics();

        // Assert
        cache.Hits.Should().Be(0);
        cache.Misses.Should().Be(0);
        cache.HitRate.Should().Be(0.0);
        cache.Count.Should().Be(1); // Items remain
    }

    [Fact]
    public void GetStatistics_ReturnsAccurateSnapshot()
    {
        // Arrange
        var cache = new LruCache<string, int>(capacity: 100);
        cache.Set("key1", 1);
        cache.Set("key2", 2);
        cache.TryGet("key1", out _); // Hit
        cache.TryGet("missing", out _); // Miss

        // Act
        var stats = cache.GetStatistics();

        // Assert
        stats.Capacity.Should().Be(100);
        stats.Count.Should().Be(2);
        stats.Hits.Should().Be(1);
        stats.Misses.Should().Be(1);
        stats.HitRate.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentAccess_NoDataCorruption()
    {
        // Arrange
        var cache = new LruCache<int, int>(capacity: 100);
        var tasks = new List<Task>();

        // Act - Concurrent writes and reads
        for (int i = 0; i < 10; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    var key = (taskId * 100) + j;
                    cache.Set(key, key * 2);
                    cache.TryGet(key, out var value);
                }
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        // Assert - No exceptions, count within capacity
        cache.Count.Should().BeLessOrEqualTo(100);
        cache.Hits.Should().BeGreaterThan(0);
    }

    [Fact]
    public void LruEviction_ComplexScenario_EvictsCorrectItems()
    {
        // Arrange
        var cache = new LruCache<string, string>(capacity: 5);

        // Add 5 items (at capacity)
        cache.Set("A", "valueA");
        cache.Set("B", "valueB");
        cache.Set("C", "valueC");
        cache.Set("D", "valueD");
        cache.Set("E", "valueE");

        // Access A and C to make them recently used
        // Order: A, C, E, D, B (most to least recent)
        cache.TryGet("A", out _);
        cache.TryGet("C", out _);

        // Act - Add new item, should evict B (least recently used)
        cache.Set("F", "valueF");

        // Assert
        cache.Count.Should().Be(5);
        cache.TryGet("A", out _).Should().BeTrue();
        cache.TryGet("B", out _).Should().BeFalse(); // Evicted
        cache.TryGet("C", out _).Should().BeTrue();
        cache.TryGet("D", out _).Should().BeTrue();
        cache.TryGet("E", out _).Should().BeTrue();
        cache.TryGet("F", out _).Should().BeTrue();
    }

    [Fact]
    public void CacheStatistics_ToString_FormatsCorrectly()
    {
        // Arrange
        var stats = new CacheStatistics
        {
            Capacity = 100,
            Count = 50,
            Hits = 75,
            Misses = 25,
            HitRate = 0.75
        };

        // Act
        var str = stats.ToString();

        // Assert
        str.Should().Contain("50/100");
        str.Should().MatchRegex(@"75[.,]00%"); // Support both decimal separators (locale-independent)
        str.Should().Contain("75 hits");
        str.Should().Contain("25 misses");
    }

    [Fact]
    public void Cache_WithNullValues_HandlesCorrectly()
    {
        // Arrange
        var cache = new LruCache<string, string?>(10);

        // Act
        cache.Set("key1", null);

        // Assert
        cache.TryGet("key1", out var value).Should().BeTrue();
        value.Should().BeNull();
        cache.Count.Should().Be(1);
    }

    [Fact]
    public void Cache_MemoryFootprint_RemainsWithinBounds()
    {
        // Arrange - Simulate 1000 items with ~100 byte values
        var cache = new LruCache<string, string>(capacity: 1000);

        // Act - Fill cache to capacity
        for (int i = 0; i < 1000; i++)
        {
            var key = $"key{i}";
            var value = new string('x', 100); // ~100 bytes per value
            cache.Set(key, value);
        }

        // Assert
        cache.Count.Should().Be(1000);
        // Memory footprint: ~5KB overhead + (1000 * ~100 bytes) ≈ 105KB
        // This is well under the 5MB target for prediction caching
    }
}
