using System.Threading.Channels;
using MediaButler.Core.Entities;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.Background;

/// <summary>
/// Thread-safe file processing queue implementation using .NET Channels for efficient
/// producer-consumer scenarios with priority support and ARM32 memory optimization.
/// </summary>
public class FileProcessingQueue : IFileProcessingQueue
{
    private readonly Channel<TrackedFile> _normalPriorityChannel;
    private readonly Channel<TrackedFile> _highPriorityChannel;
    private readonly ChannelWriter<TrackedFile> _normalPriorityWriter;
    private readonly ChannelReader<TrackedFile> _normalPriorityReader;
    private readonly ChannelWriter<TrackedFile> _highPriorityWriter;
    private readonly ChannelReader<TrackedFile> _highPriorityReader;
    private readonly ILogger<FileProcessingQueue> _logger;
    private readonly SemaphoreSlim _semaphore;
    
    // ARM32 optimization: limit queue size to prevent memory issues
    private const int MaxQueueSize = 1000;
    private const int MaxHighPrioritySize = 100;

    public FileProcessingQueue(ILogger<FileProcessingQueue> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Configure bounded channels for ARM32 memory constraints
        var normalOptions = new BoundedChannelOptions(MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        var highPriorityOptions = new BoundedChannelOptions(MaxHighPrioritySize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        _normalPriorityChannel = Channel.CreateBounded<TrackedFile>(normalOptions);
        _highPriorityChannel = Channel.CreateBounded<TrackedFile>(highPriorityOptions);
        
        _normalPriorityWriter = _normalPriorityChannel.Writer;
        _normalPriorityReader = _normalPriorityChannel.Reader;
        _highPriorityWriter = _highPriorityChannel.Writer;
        _highPriorityReader = _highPriorityChannel.Reader;
        
        // Semaphore for ARM32 resource management
        _semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(TrackedFile file, CancellationToken cancellationToken = default)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));

        try
        {
            await _normalPriorityWriter.WriteAsync(file, cancellationToken);
            
            _logger.LogDebug(
                "File {FileHash} ({FileName}) enqueued for processing. Queue size: {QueueSize}",
                file.Hash, file.FileName, Count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("File enqueue operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to enqueue file {FileHash} ({FileName}) for processing",
                file.Hash, file.FileName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task EnqueueHighPriorityAsync(TrackedFile file, CancellationToken cancellationToken = default)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));

        try
        {
            await _highPriorityWriter.WriteAsync(file, cancellationToken);
            
            _logger.LogDebug(
                "File {FileHash} ({FileName}) enqueued for high priority processing. High priority queue size: {HighPriorityQueueSize}",
                file.Hash, file.FileName, HighPriorityCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("High priority file enqueue operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to enqueue file {FileHash} ({FileName}) for high priority processing",
                file.Hash, file.FileName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<TrackedFile?> DequeueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            
            try
            {
                // Priority handling: check high priority queue first
                if (_highPriorityReader.TryRead(out var highPriorityFile))
                {
                    _logger.LogDebug(
                        "Dequeued high priority file {FileHash} ({FileName}) for processing",
                        highPriorityFile.Hash, highPriorityFile.FileName);
                    return highPriorityFile;
                }

                // Check normal priority queue
                if (_normalPriorityReader.TryRead(out var normalFile))
                {
                    _logger.LogDebug(
                        "Dequeued file {FileHash} ({FileName}) for processing",
                        normalFile.Hash, normalFile.FileName);
                    return normalFile;
                }

                // Wait for next available item from either queue
                var highPriorityTask = _highPriorityReader.WaitToReadAsync(cancellationToken).AsTask();
                var normalPriorityTask = _normalPriorityReader.WaitToReadAsync(cancellationToken).AsTask();
                
                var completedTask = await Task.WhenAny(highPriorityTask, normalPriorityTask);
                
                if (completedTask == highPriorityTask && await highPriorityTask)
                {
                    if (_highPriorityReader.TryRead(out var file))
                    {
                        _logger.LogDebug(
                            "Dequeued high priority file {FileHash} ({FileName}) for processing after wait",
                            file.Hash, file.FileName);
                        return file;
                    }
                }
                else if (completedTask == normalPriorityTask && await normalPriorityTask)
                {
                    if (_normalPriorityReader.TryRead(out var file))
                    {
                        _logger.LogDebug(
                            "Dequeued file {FileHash} ({FileName}) for processing after wait",
                            file.Hash, file.FileName);
                        return file;
                    }
                }

                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("File dequeue operation was cancelled");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dequeue file for processing");
            throw;
        }
    }

    /// <inheritdoc />
    public int Count => 
        _normalPriorityReader.TryPeek(out _) ? 
        GetApproximateCount(_normalPriorityReader) : 0;

    /// <inheritdoc />
    public int HighPriorityCount => 
        _highPriorityReader.TryPeek(out _) ? 
        GetApproximateCount(_highPriorityReader) : 0;

    /// <inheritdoc />
    public void Clear()
    {
        try
        {
            // Complete writers to prevent new items
            _normalPriorityWriter.TryComplete();
            _highPriorityWriter.TryComplete();
            
            // Drain existing items
            while (_normalPriorityReader.TryRead(out _)) { }
            while (_highPriorityReader.TryRead(out _)) { }
            
            _logger.LogInformation("File processing queue cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear file processing queue");
            throw;
        }
    }

    /// <summary>
    /// Gets an approximate count of items in the channel reader.
    /// Note: This is an approximation as Channel doesn't expose exact count.
    /// </summary>
    private static int GetApproximateCount(ChannelReader<TrackedFile> reader)
    {
        var count = 0;
        while (reader.TryPeek(out _) && count < 10000) // Safety limit
        {
            count++;
        }
        return count;
    }

    public void Dispose()
    {
        Clear();
        _semaphore?.Dispose();
    }
}