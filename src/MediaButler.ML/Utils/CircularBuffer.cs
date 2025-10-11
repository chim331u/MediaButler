namespace MediaButler.ML.Utils;

/// <summary>
/// Fixed-size circular buffer with zero-allocation after initialization.
/// Optimized for ARM32 deployment with predictable memory footprint.
/// Following "Simple Made Easy" - values over state with immutable capacity.
/// </summary>
/// <typeparam name="T">The type of items stored in the buffer</typeparam>
public sealed class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private readonly object _lock = new();
    private int _head;
    private int _count;

    /// <summary>
    /// Creates a new circular buffer with the specified capacity.
    /// Memory footprint is fixed at initialization: capacity * sizeof(T).
    /// </summary>
    /// <param name="capacity">Maximum number of items the buffer can hold</param>
    /// <exception cref="ArgumentException">Thrown when capacity is less than 1</exception>
    public CircularBuffer(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentException("Capacity must be at least 1", nameof(capacity));

        _buffer = new T[capacity];
        _head = 0;
        _count = 0;
    }

    /// <summary>
    /// Gets the maximum capacity of the buffer.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Gets the current number of items in the buffer.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Adds an item to the buffer. If the buffer is full, the oldest item is overwritten.
    /// Thread-safe operation with minimal lock contention.
    /// </summary>
    /// <param name="item">The item to add</param>
    public void Add(T item)
    {
        lock (_lock)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;

            if (_count < _buffer.Length)
                _count++;
        }
    }

    /// <summary>
    /// Gets a snapshot of all items currently in the buffer.
    /// Items are returned in insertion order (oldest to newest).
    /// </summary>
    /// <returns>Array containing all current items</returns>
    public T[] GetItems()
    {
        lock (_lock)
        {
            if (_count == 0)
                return Array.Empty<T>();

            var result = new T[_count];

            if (_count < _buffer.Length)
            {
                // Buffer not yet full - items are from index 0 to _count-1
                Array.Copy(_buffer, 0, result, 0, _count);
            }
            else
            {
                // Buffer is full - items wrap around
                // Copy from _head to end
                var itemsAfterHead = _buffer.Length - _head;
                Array.Copy(_buffer, _head, result, 0, itemsAfterHead);

                // Copy from start to _head
                if (_head > 0)
                    Array.Copy(_buffer, 0, result, itemsAfterHead, _head);
            }

            return result;
        }
    }

    /// <summary>
    /// Clears all items from the buffer.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _count = 0;
        }
    }
}
