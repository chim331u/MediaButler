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
}