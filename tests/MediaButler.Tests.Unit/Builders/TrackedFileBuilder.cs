using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.Tests.Unit.Builders;

/// <summary>
/// Test data builder for TrackedFile entities.
/// Follows the Builder pattern for creating test data with sane defaults.
/// Uses method chaining for fluent configuration while maintaining simplicity.
/// </summary>
public class TrackedFileBuilder
{
    private string _hash = "abc123def456789012345678901234567890123456789012345678901234";
    private string _fileName = "Breaking.Bad.S01E01.mkv";
    private string _originalPath = "/downloads/Breaking.Bad.S01E01.mkv";
    private long _fileSize = 734003200; // ~700MB
    private FileStatus _status = FileStatus.New;
    private string? _category = null;
    private string? _suggestedCategory = null;
    private decimal _confidence = 0m;
    private string? _targetPath = null;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;
    private string? _lastError = null;
    private int _retryCount = 0;

    /// <summary>
    /// Sets the SHA256 hash for the file.
    /// </summary>
    public TrackedFileBuilder WithHash(string hash)
    {
        _hash = hash;
        return this;
    }

    /// <summary>
    /// Sets the file name.
    /// </summary>
    public TrackedFileBuilder WithFileName(string fileName)
    {
        _fileName = fileName;
        return this;
    }

    /// <summary>
    /// Sets the original file path.
    /// </summary>
    public TrackedFileBuilder WithOriginalPath(string originalPath)
    {
        _originalPath = originalPath;
        return this;
    }

    /// <summary>
    /// Sets the file size in bytes.
    /// </summary>
    public TrackedFileBuilder WithFileSize(long fileSize)
    {
        _fileSize = fileSize;
        return this;
    }

    /// <summary>
    /// Sets the file status.
    /// </summary>
    public TrackedFileBuilder WithStatus(FileStatus status)
    {
        _status = status;
        return this;
    }

    /// <summary>
    /// Sets the confirmed category.
    /// </summary>
    public TrackedFileBuilder WithCategory(string category)
    {
        _category = category;
        return this;
    }

    /// <summary>
    /// Sets the ML suggested category and confidence.
    /// </summary>
    public TrackedFileBuilder WithSuggestion(string suggestedCategory, decimal confidence)
    {
        _suggestedCategory = suggestedCategory;
        _confidence = confidence;
        return this;
    }

    /// <summary>
    /// Sets the target path for file movement.
    /// </summary>
    public TrackedFileBuilder WithTargetPath(string targetPath)
    {
        _targetPath = targetPath;
        return this;
    }

    /// <summary>
    /// Sets creation and update timestamps.
    /// </summary>
    public TrackedFileBuilder WithTimestamps(DateTime createdAt, DateTime updatedAt)
    {
        _createdAt = createdAt;
        _updatedAt = updatedAt;
        return this;
    }

    /// <summary>
    /// Sets error information.
    /// </summary>
    public TrackedFileBuilder WithError(string error, int retryCount = 1)
    {
        _lastError = error;
        _retryCount = retryCount;
        return this;
    }

    /// <summary>
    /// Creates a file in pending state with ML suggestion.
    /// Common scenario: File classified by ML, awaiting user confirmation.
    /// </summary>
    public TrackedFileBuilder AsClassified(string suggestedCategory, decimal confidence = 0.85m)
    {
        return WithStatus(FileStatus.Classified)
               .WithSuggestion(suggestedCategory, confidence);
    }

    /// <summary>
    /// Creates a confirmed file ready for moving.
    /// Common scenario: User confirmed the category assignment.
    /// </summary>
    public TrackedFileBuilder AsConfirmed(string category)
    {
        return WithStatus(FileStatus.ReadyToMove)
               .WithCategory(category)
               .WithSuggestion(category, 1.0m); // High confidence after confirmation
    }

    /// <summary>
    /// Creates a moved file.
    /// Common scenario: File successfully moved to library.
    /// </summary>
    public TrackedFileBuilder AsMoved(string category, string targetPath)
    {
        return WithStatus(FileStatus.Moved)
               .WithCategory(category)
               .WithTargetPath(targetPath);
    }

    /// <summary>
    /// Creates a file with error.
    /// Common scenario: Processing failed with retries.
    /// </summary>
    public TrackedFileBuilder AsError(string error, int retryCount = 3)
    {
        return WithStatus(FileStatus.Error)
               .WithError(error, retryCount);
    }

    /// <summary>
    /// Creates multiple files with unique hashes for batch testing.
    /// Useful for performance testing and bulk operation validation.
    /// </summary>
    public TrackedFileBuilder WithUniqueHash(int sequence)
    {
        _hash = $"batch{sequence:D10}".PadRight(64, '0').Substring(0, 64);
        _fileName = $"{Path.GetFileNameWithoutExtension(_fileName)}.{sequence:D3}{Path.GetExtension(_fileName)}";
        _originalPath = Path.Combine(Path.GetDirectoryName(_originalPath) ?? "/test", _fileName);
        return this;
    }

    /// <summary>
    /// Sets the file as soft deleted for testing deletion scenarios.
    /// </summary>
    public TrackedFileBuilder AsSoftDeleted(string deleteReason = "Test deletion")
    {
        return WithError(deleteReason, 0);
    }

    /// <summary>
    /// Creates a realistic TV series file with episode information.
    /// </summary>
    public TrackedFileBuilder AsTVEpisode(string series, int season, int episode, string quality = "1080p")
    {
        var fileName = $"{series}.S{season:D2}E{episode:D2}.{quality}.mkv";
        var hash = $"tv{series.Replace(" ", "").ToLower()}{season:D2}{episode:D2}".PadRight(64, '0').Substring(0, 64);
        
        return WithFileName(fileName)
               .WithOriginalPath($"/downloads/{fileName}")
               .WithHash(hash)
               .WithFileSize(Random.Shared.NextInt64(500_000_000, 2_000_000_000)); // 500MB - 2GB
    }

    /// <summary>
    /// Builds the TrackedFile instance with configured values.
    /// </summary>
    public TrackedFile Build()
    {
        var file = new TrackedFile
        {
            Hash = _hash,
            FileName = _fileName,
            OriginalPath = _originalPath,
            FileSize = _fileSize,
            Status = _status,
            Category = _category,
            SuggestedCategory = _suggestedCategory,
            Confidence = _confidence,
            TargetPath = _targetPath,
            LastError = _lastError,
            RetryCount = _retryCount
        };
        
        // Set BaseEntity properties via reflection for test data
        var baseEntityType = typeof(TrackedFile).BaseType;
        baseEntityType?.GetProperty("CreatedDate")?.SetValue(file, _createdAt);
        baseEntityType?.GetProperty("LastUpdateDate")?.SetValue(file, _updatedAt);
        baseEntityType?.GetProperty("IsActive")?.SetValue(file, true);
        
        return file;
    }

    /// <summary>
    /// Builds multiple TrackedFile instances with incremental variations.
    /// Useful for bulk testing scenarios and performance validation.
    /// </summary>
    public IEnumerable<TrackedFile> BuildMany(int count)
    {
        for (int i = 0; i < count; i++)
        {
            // Create a new builder instance for each file to avoid state sharing
            var builder = new TrackedFileBuilder()
                .WithHash(_hash)
                .WithFileName(_fileName)
                .WithOriginalPath(_originalPath)
                .WithFileSize(_fileSize)
                .WithStatus(_status)
                .WithTimestamps(_createdAt, _updatedAt);
                
            if (_category != null) builder.WithCategory(_category);
            if (_suggestedCategory != null) builder.WithSuggestion(_suggestedCategory, _confidence);
            if (_targetPath != null) builder.WithTargetPath(_targetPath);
            if (_lastError != null) builder.WithError(_lastError, _retryCount);
                
            yield return builder.WithUniqueHash(i + 1).Build();
        }
    }
}