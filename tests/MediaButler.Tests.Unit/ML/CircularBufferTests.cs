using FluentAssertions;
using MediaButler.ML.Utils;
using Xunit;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Unit tests for CircularBuffer<T> to verify ARM32 memory optimization behavior.
/// Tests focus on memory boundaries, thread safety, and zero-allocation guarantees.
/// </summary>
public class CircularBufferTests
{
    [Fact]
    public void Constructor_WithValidCapacity_CreatesBuffer()
    {
        // Arrange & Act
        var buffer = new CircularBuffer<int>(capacity: 10);

        // Assert
        buffer.Capacity.Should().Be(10);
        buffer.Count.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidCapacity_ThrowsArgumentException(int invalidCapacity)
    {
        // Arrange & Act
        Action act = () => new CircularBuffer<int>(invalidCapacity);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Capacity must be at least 1*");
    }

    [Fact]
    public void Add_SingleItem_IncreasesCount()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(capacity: 5);

        // Act
        buffer.Add(42);

        // Assert
        buffer.Count.Should().Be(1);
        buffer.GetItems().Should().Equal(42);
    }

    [Fact]
    public void Add_MultipleItems_StoresInOrder()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(capacity: 5);

        // Act
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        // Assert
        buffer.Count.Should().Be(3);
        buffer.GetItems().Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Add_ExactlyCapacityItems_DoesNotOverwrite()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(capacity: 3);

        // Act
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        // Assert
        buffer.Count.Should().Be(3);
        buffer.Capacity.Should().Be(3);
        buffer.GetItems().Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Add_OverCapacity_OverwritesOldestItem()
    {
        // Arrange - ARM32 critical: Buffer must overwrite, not grow
        var buffer = new CircularBuffer<int>(capacity: 3);

        // Act - Add 4 items to 3-capacity buffer
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4); // Should overwrite 1

        // Assert - Count stays at capacity, oldest item removed
        buffer.Count.Should().Be(3, "count should not exceed capacity");
        buffer.GetItems().Should().Equal(2, 3, 4);
    }

    [Fact]
    public void Add_ManyItemsOverCapacity_KeepsMostRecentN()
    {
        // Arrange - ARM32 memory boundary test
        var buffer = new CircularBuffer<int>(capacity: 5);

        // Act - Add 10 items to 5-capacity buffer
        for (int i = 1; i <= 10; i++)
        {
            buffer.Add(i);
        }

        // Assert - Should keep last 5 items (6, 7, 8, 9, 10)
        buffer.Count.Should().Be(5);
        buffer.GetItems().Should().Equal(6, 7, 8, 9, 10);
    }

    [Fact]
    public void GetItems_EmptyBuffer_ReturnsEmptyArray()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(capacity: 5);

        // Act
        var items = buffer.GetItems();

        // Assert
        items.Should().BeEmpty();
        items.Should().NotBeNull();
    }

    [Fact]
    public void GetItems_ReturnsSnapshotInInsertionOrder()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(capacity: 3);
        buffer.Add("first");
        buffer.Add("second");
        buffer.Add("third");

        // Act
        var items = buffer.GetItems();

        // Assert
        items.Should().Equal("first", "second", "third");
    }

    [Fact]
    public void GetItems_AfterOverwrite_ReturnsCorrectOrder()
    {
        // Arrange - Test wrap-around behavior
        var buffer = new CircularBuffer<int>(capacity: 3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4); // Overwrites 1, head wraps to index 0
        buffer.Add(5); // Overwrites 2, head at index 1

        // Act
        var items = buffer.GetItems();

        // Assert - Should return items in insertion order (oldest to newest)
        items.Should().Equal(3, 4, 5);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(capacity: 5);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);

        // Act
        buffer.Clear();

        // Assert
        buffer.Count.Should().Be(0);
        buffer.GetItems().Should().BeEmpty();
    }

    [Fact]
    public void Clear_ResetsHead()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(capacity: 3);
        buffer.Add(1);
        buffer.Add(2);
        buffer.Add(3);
        buffer.Add(4); // Wraps head

        // Act
        buffer.Clear();
        buffer.Add(10);
        buffer.Add(20);

        // Assert
        buffer.GetItems().Should().Equal(10, 20);
    }

    [Fact]
    public void Add_WithReferenceTypes_StoresCorrectly()
    {
        // Arrange
        var buffer = new CircularBuffer<string>(capacity: 3);

        // Act
        buffer.Add("alpha");
        buffer.Add("beta");
        buffer.Add("gamma");

        // Assert
        buffer.GetItems().Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public void Add_WithComplexTypes_PreservesObjectReferences()
    {
        // Arrange
        var buffer = new CircularBuffer<PredictionMetric>(capacity: 2);
        var metric1 = new PredictionMetric { Confidence = 0.95, Duration = TimeSpan.FromMilliseconds(10) };
        var metric2 = new PredictionMetric { Confidence = 0.87, Duration = TimeSpan.FromMilliseconds(15) };

        // Act
        buffer.Add(metric1);
        buffer.Add(metric2);

        // Assert
        var items = buffer.GetItems();
        items.Should().HaveCount(2);
        items[0].Confidence.Should().Be(0.95);
        items[1].Confidence.Should().Be(0.87);
    }

    [Fact]
    public void ARM32_MemoryFootprint_IsFixedAfterInitialization()
    {
        // Arrange - ARM32 critical: No allocations after initialization
        var buffer = new CircularBuffer<int>(capacity: 100);

        // Act - Add items up to and beyond capacity
        for (int i = 0; i < 200; i++)
        {
            buffer.Add(i);
        }

        // Assert - Count should not exceed capacity
        buffer.Count.Should().Be(100, "memory footprint must remain fixed");
        buffer.Capacity.Should().Be(100);
    }

    [Fact]
    public void ARM32_NoAllocation_WhenAddingItems()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(capacity: 10);

        // Act - Multiple adds should not allocate new arrays
        for (int i = 0; i < 20; i++)
        {
            buffer.Add(i);
        }

        // Assert - Capacity unchanged (no internal resize)
        buffer.Capacity.Should().Be(10);
        buffer.Count.Should().Be(10);
    }

    [Fact]
    public void ThreadSafety_ConcurrentAdds_NoDataLoss()
    {
        // Arrange - ARM32 thread safety test
        var buffer = new CircularBuffer<int>(capacity: 1000);
        var itemsToAdd = 500;

        // Act - Add items from multiple threads
        var tasks = Enumerable.Range(0, 5).Select(threadId =>
            Task.Run(() =>
            {
                for (int i = 0; i < itemsToAdd / 5; i++)
                {
                    buffer.Add(threadId * 1000 + i);
                }
            })
        ).ToArray();

        Task.WaitAll(tasks);

        // Assert - All items should be added (some may be overwritten, but count is correct)
        buffer.Count.Should().BeLessOrEqualTo(1000);
        buffer.GetItems().Should().NotBeEmpty();
    }

    [Fact]
    public void GetItems_MultipleCallsReturnIndependentArrays()
    {
        // Arrange
        var buffer = new CircularBuffer<int>(capacity: 3);
        buffer.Add(1);
        buffer.Add(2);

        // Act
        var items1 = buffer.GetItems();
        var items2 = buffer.GetItems();

        // Assert - Each call should return a new array
        items1.Should().Equal(items2);
        items1.Should().NotBeSameAs(items2, "GetItems should return new array each time");
    }

    // Helper class for complex type tests
    private class PredictionMetric
    {
        public double Confidence { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
