using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MediaButler.Core.Services;
using MediaButler.Core.Models;
using MediaButler.Core.Enums;
using MediaButler.Core.Entities;
using MediaButler.Tests.Integration.Infrastructure;
using System.Text.Json;

namespace MediaButler.Tests.Integration.Services;

/// <summary>
/// Integration tests for ErrorClassificationService with real database.
/// Tests error classification, recovery determination, and statistics tracking.
/// </summary>
public class ErrorClassificationServiceIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _databaseFixture;

    public ErrorClassificationServiceIntegrationTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    [Fact]
    public async Task ClassifyErrorAsync_WithPermissionError_ReturnsCorrectClassification()
    {
        // Given
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();
        
        var errorContext = new ErrorContext
        {
            Exception = new UnauthorizedAccessException("Access to the path '/protected/file.mkv' is denied."),
            OperationType = "MOVE",
            SourcePath = "/source/test.mkv",
            TargetPath = "/protected/file.mkv",
            FileSize = 1024 * 1024 * 100, // 100MB
            FileHash = "test-hash-123",
            RetryAttempts = 0
        };

        // When
        var result = await service.ClassifyErrorAsync(errorContext);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ErrorType.Should().Be(FileOperationErrorType.PermissionError);
        result.Value.CanRetry.Should().BeFalse();
        result.Value.RequiresUserIntervention.Should().BeTrue();
        result.Value.ClassificationConfidence.Should().BeGreaterOrEqualTo(0.85);
        result.Value.UserMessage.Should().Contain("Permission denied");
        result.Value.ResolutionSteps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ClassifyErrorAsync_WithSpaceError_ReturnsCorrectClassification()
    {
        // Given
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();
        
        var errorContext = new ErrorContext
        {
            Exception = new IOException("There is not enough space on the disk."),
            OperationType = "COPY",
            SourcePath = "/source/largefile.mkv",
            TargetPath = "/target/largefile.mkv",
            FileSize = 1024L * 1024L * 1024L * 5L, // 5GB
            AvailableSpace = 1024L * 1024L * 1024L * 2L, // 2GB available
            FileHash = "large-file-hash",
            RetryAttempts = 1
        };

        // When
        var result = await service.ClassifyErrorAsync(errorContext);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ErrorType.Should().Be(FileOperationErrorType.SpaceError);
        result.Value.CanRetry.Should().BeFalse();
        result.Value.RequiresUserIntervention.Should().BeTrue();
        result.Value.UserMessage.Should().Contain("Insufficient disk space");
        result.Value.ResolutionSteps.Should().Contain(step => step.Contains("Free up disk space"));
    }

    [Fact]
    public async Task ClassifyErrorAsync_WithTransientError_AllowsRetry()
    {
        // Given
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();
        
        var errorContext = new ErrorContext
        {
            Exception = new IOException("The process cannot access the file because it is being used by another process."),
            OperationType = "MOVE",
            SourcePath = "/source/locked.mkv",
            TargetPath = "/target/locked.mkv",
            FileSize = 1024 * 1024 * 50, // 50MB
            FileHash = "locked-file-hash",
            RetryAttempts = 1
        };

        // When
        var result = await service.ClassifyErrorAsync(errorContext);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ErrorType.Should().Be(FileOperationErrorType.TransientError);
        result.Value.CanRetry.Should().BeTrue();
        result.Value.RequiresUserIntervention.Should().BeFalse();
        result.Value.MaxRetryAttempts.Should().BeGreaterThan(0);
        result.Value.RecommendedRetryDelayMs.Should().BeGreaterThan(0);
        result.Value.UserMessage.Should().Contain("temporary error");
    }

    [Fact]
    public async Task ClassifyErrorAsync_WithPathError_ReturnsPathClassification()
    {
        // Given
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();
        
        var errorContext = new ErrorContext
        {
            Exception = new DirectoryNotFoundException("Could not find a part of the path '/nonexistent/path/file.mkv'."),
            OperationType = "MOVE",
            SourcePath = "/source/file.mkv",
            TargetPath = "/nonexistent/path/file.mkv",
            FileSize = 1024 * 1024 * 10, // 10MB
            FileHash = "path-error-hash",
            RetryAttempts = 0
        };

        // When
        var result = await service.ClassifyErrorAsync(errorContext);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ErrorType.Should().Be(FileOperationErrorType.PathError);
        result.Value.CanRetry.Should().BeFalse();
        result.Value.RequiresUserIntervention.Should().BeTrue();
        result.Value.ResolutionSteps.Should().Contain(step => step.Contains("path"));
    }

    [Fact]
    public async Task DetermineRecoveryActionAsync_WithTransientError_ReturnsAutomaticRetry()
    {
        // Given
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();
        
        var errorContext = new ErrorContext
        {
            Exception = new TimeoutException("Operation timed out"),
            OperationType = "COPY",
            SourcePath = "/source/timeout.mkv",
            FileSize = 1024 * 1024 * 25, // 25MB
            FileHash = "timeout-hash",
            RetryAttempts = 1
        };

        // When
        var result = await service.DetermineRecoveryActionAsync(errorContext);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ActionType.Should().Be(ErrorRecoveryType.AutomaticRetry);
        result.Value.Delay.Should().BeGreaterThan(TimeSpan.Zero);
        result.Value.Parameters.Should().ContainKey("RetryDelayMs");
        result.Value.Parameters.Should().ContainKey("MaxRetries");
        result.Value.Description.Should().Contain("retry");
    }

    [Fact]
    public async Task DetermineRecoveryActionAsync_WithPermissionError_ReturnsWaitForUser()
    {
        // Given
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();
        
        var errorContext = new ErrorContext
        {
            Exception = new UnauthorizedAccessException("Access denied"),
            OperationType = "MOVE",
            SourcePath = "/source/protected.mkv",
            FileSize = 1024 * 1024 * 15, // 15MB
            FileHash = "protected-hash",
            RetryAttempts = 0
        };

        // When
        var result = await service.DetermineRecoveryActionAsync(errorContext);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ActionType.Should().Be(ErrorRecoveryType.WaitForUserIntervention);
        result.Value.Delay.Should().Be(TimeSpan.Zero);
        result.Value.Description.Should().Contain("user");
    }

    [Fact]
    public async Task RecordErrorOutcomeAsync_WithValidData_SavesToDatabase()
    {
        // Given
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();
        
        var errorContext = new ErrorContext
        {
            Exception = new IOException("Test error"),
            OperationType = "TEST_OPERATION",
            SourcePath = "/test/source.mkv",
            FileSize = 1024 * 1024, // 1MB
            FileHash = "record-test-hash",
            RetryAttempts = 2
        };

        var classification = ErrorClassificationResult.TransientError(
            "Test transient error", 
            "Test technical details");

        var recoveryAction = new ErrorRecoveryAction
        {
            ActionType = ErrorRecoveryType.AutomaticRetry,
            Delay = TimeSpan.FromSeconds(1),
            Description = "Test recovery action"
        };

        // When
        var result = await service.RecordErrorOutcomeAsync(errorContext, classification, recoveryAction, true);

        // Then
        result.IsSuccess.Should().BeTrue();

        // Verify data was saved
        var logs = await _databaseFixture.Context.ProcessingLogs
            .Where(log => log.FileHash == "record-test-hash" && log.Category == "ERROR_TEST_OPERATION")
            .ToListAsync();

        logs.Should().HaveCount(1);
        var log = logs.First();
        log.Message.Should().Be("Error recovered successfully");
        log.Level.Should().Be(MediaButler.Core.Enums.LogLevel.Information);
        log.Details.Should().NotBeNullOrEmpty();

        var details = JsonSerializer.Deserialize<JsonElement>(log.Details);
        details.GetProperty("ErrorType").GetString().Should().Be("TransientError");
        details.GetProperty("WasSuccessful").GetBoolean().Should().BeTrue();
        details.GetProperty("RetryAttempts").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task GetErrorStatisticsAsync_WithErrorHistory_ReturnsAccurateStats()
    {
        // Given
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();
        
        // Create required TrackedFiles first to satisfy foreign key constraints
        var trackedFiles = new[]
        {
            new TrackedFile
            {
                Hash = "stats-hash-1",
                FileName = "file1.mkv",
                OriginalPath = "/test/file1.mkv",
                FileSize = 1024,
                Status = FileStatus.New,
                SuggestedCategory = "TEST",
                Confidence = 0.8m
            },
            new TrackedFile
            {
                Hash = "stats-hash-2",
                FileName = "file2.mkv", 
                OriginalPath = "/test/file2.mkv",
                FileSize = 1024,
                Status = FileStatus.New,
                SuggestedCategory = "TEST",
                Confidence = 0.8m
            },
            new TrackedFile
            {
                Hash = "stats-hash-3",
                FileName = "file3.mkv",
                OriginalPath = "/test/file3.mkv",
                FileSize = 1024,
                Status = FileStatus.New,
                SuggestedCategory = "TEST",
                Confidence = 0.8m
            }
        };

        _databaseFixture.Context.TrackedFiles.AddRange(trackedFiles);
        await _databaseFixture.Context.SaveChangesAsync();
        
        // Create sample error logs
        var errorLogs = new[]
        {
            new ProcessingLog
            {
                FileHash = "stats-hash-1",
                Level = MediaButler.Core.Enums.LogLevel.Information,
                Category = "ERROR_MOVE",
                Message = "Error recovered successfully",
                Details = JsonSerializer.Serialize(new { ErrorType = "TransientError", WasSuccessful = true })
            },
            new ProcessingLog
            {
                FileHash = "stats-hash-2", 
                Level = MediaButler.Core.Enums.LogLevel.Error,
                Category = "ERROR_MOVE",
                Message = "Error recovery failed",
                Details = JsonSerializer.Serialize(new { ErrorType = "PermissionError", WasSuccessful = false })
            },
            new ProcessingLog
            {
                FileHash = "stats-hash-3",
                Level = MediaButler.Core.Enums.LogLevel.Information,
                Category = "ERROR_COPY", 
                Message = "Error recovered successfully",
                Details = JsonSerializer.Serialize(new { ErrorType = "TransientError", WasSuccessful = true })
            }
        };

        _databaseFixture.Context.ProcessingLogs.AddRange(errorLogs);
        await _databaseFixture.Context.SaveChangesAsync();

        // When
        var result = await service.GetErrorStatisticsAsync(TimeSpan.FromHours(6));

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TotalErrors.Should().Be(3);
        result.Value.ErrorsByType.Should().ContainKey("TransientError");
        result.Value.ErrorsByType.Should().ContainKey("PermissionError");
        result.Value.ErrorsByType["TransientError"].Should().Be(2);
        result.Value.ErrorsByType["PermissionError"].Should().Be(1);
        result.Value.RetrySuccessRates.Should().ContainKey("TransientError");
        result.Value.RetrySuccessRates["TransientError"].Should().Be(1.0); // 100% success
    }

    [Fact]
    public async Task ValidateSystemStateAsync_WithValidPaths_ReturnsHealthyState()
    {
        // Given
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();
        
        var tempDir = Path.GetTempPath();
        var tempFile = Path.Combine(tempDir, "test-validation.txt");
        await File.WriteAllTextAsync(tempFile, "test content");
        
        try
        {
            var errorContext = new ErrorContext
            {
                Exception = new Exception("Test exception"),
                OperationType = "VALIDATE_TEST",
                SourcePath = tempFile,
                TargetPath = Path.Combine(tempDir, "target-validation.txt"),
                FileSize = 1024,
                AvailableSpace = 1024 * 1024 * 1024, // 1GB available
                FileHash = "validate-hash"
            };

            // When
            var result = await service.ValidateSystemStateAsync(errorContext);

            // Then
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.IsHealthy.Should().BeTrue();
            result.Value.PotentialIssues.Should().BeEmpty();
            result.Value.SystemInfo.Should().ContainKey("AvailableSpaceBytes");
            result.Value.SystemInfo.Should().ContainKey("SourceFileAccessible");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ValidateSystemStateAsync_WithInsufficientSpace_IdentifiesIssue()
    {
        // Given
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();
        
        var errorContext = new ErrorContext
        {
            Exception = new Exception("Test exception"),
            OperationType = "SPACE_TEST",
            SourcePath = "/source/large.mkv",
            TargetPath = "/target/large.mkv",
            FileSize = 1024L * 1024L * 1024L * 5L, // 5GB file
            AvailableSpace = 1024L * 1024L * 1024L * 1L, // 1GB available (insufficient)
            FileHash = "space-validate-hash"
        };

        // When
        var result = await service.ValidateSystemStateAsync(errorContext);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsHealthy.Should().BeFalse();
        result.Value.PotentialIssues.Should().Contain(issue => issue.Contains("Low disk space"));
        result.Value.PreventiveActions.Should().Contain(action => action.Contains("Free up disk space"));
        result.Value.SystemInfo.Should().ContainKey("RequiredSpaceBytes");
    }

    [Fact]
    public async Task ValidateSystemStateAsync_WithLongPath_IdentifiesPathIssue()
    {
        // Given
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();
        
        var longPath = string.Join("/", Enumerable.Repeat("verylongfoldername", 20)); // Create very long path
        
        var errorContext = new ErrorContext
        {
            Exception = new Exception("Test exception"),
            OperationType = "PATH_TEST",
            SourcePath = "/source/normal.mkv",
            TargetPath = longPath,
            FileSize = 1024 * 1024, // 1MB
            FileHash = "path-validate-hash"
        };

        // When
        var result = await service.ValidateSystemStateAsync(errorContext);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsHealthy.Should().BeFalse();
        result.Value.PotentialIssues.Should().Contain(issue => issue.Contains("Target path too long"));
        result.Value.PreventiveActions.Should().Contain(action => action.Contains("shorter"));
        result.Value.SystemInfo.Should().ContainKey("TargetPathLength");
    }

    [Fact]
    public async Task ErrorClassificationWorkflow_EndToEnd_WorksCorrectly()
    {
        // Given - Comprehensive end-to-end test
        await _databaseFixture.CleanupAsync();
        
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();
        
        var errorContext = new ErrorContext
        {
            Exception = new IOException("File is locked by another process"),
            OperationType = "END_TO_END_TEST",
            SourcePath = "/source/workflow.mkv",
            TargetPath = "/target/workflow.mkv", 
            FileSize = 1024 * 1024 * 50, // 50MB
            FileHash = "workflow-hash",
            RetryAttempts = 0,
            AdditionalContext = new Dictionary<string, object>
            {
                ["TestContext"] = "End-to-end workflow test"
            }
        };

        // When - Execute full workflow
        // 1. Classify the error
        var classificationResult = await service.ClassifyErrorAsync(errorContext);
        classificationResult.IsSuccess.Should().BeTrue();
        var classification = classificationResult.Value;

        // 2. Determine recovery action
        var recoveryResult = await service.DetermineRecoveryActionAsync(errorContext, classification);
        recoveryResult.IsSuccess.Should().BeTrue();
        var recovery = recoveryResult.Value;

        // 3. Record successful outcome
        var recordResult = await service.RecordErrorOutcomeAsync(errorContext, classification, recovery, true);
        recordResult.IsSuccess.Should().BeTrue();

        // 4. Validate system state
        var validationResult = await service.ValidateSystemStateAsync(errorContext);
        validationResult.IsSuccess.Should().BeTrue();

        // 5. Get statistics
        var statsResult = await service.GetErrorStatisticsAsync(TimeSpan.FromHours(1));
        statsResult.IsSuccess.Should().BeTrue();

        // Then - Verify complete workflow
        classification.ErrorType.Should().Be(FileOperationErrorType.TransientError);
        recovery.ActionType.Should().Be(ErrorRecoveryType.AutomaticRetry);
        
        var stats = statsResult.Value;
        stats.TotalErrors.Should().Be(1);
        stats.ErrorsByType.Should().ContainKey("TransientError");
        stats.RetrySuccessRates.Should().ContainKey("TransientError");

        // Verify database record
        var logs = await _databaseFixture.Context.ProcessingLogs
            .Where(log => log.FileHash == "workflow-hash")
            .ToListAsync();
        
        logs.Should().HaveCount(1);
        logs.First().Message.Should().Be("Error recovered successfully");
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), FileOperationErrorType.PermissionError)]
    [InlineData(typeof(DirectoryNotFoundException), FileOperationErrorType.PathError)] 
    [InlineData(typeof(FileNotFoundException), FileOperationErrorType.PathError)]
    [InlineData(typeof(PathTooLongException), FileOperationErrorType.PathError)]
    [InlineData(typeof(IOException), FileOperationErrorType.TransientError)]
    [InlineData(typeof(TimeoutException), FileOperationErrorType.TransientError)]
    public async Task ClassifyErrorAsync_WithVariousExceptionTypes_ReturnsCorrectClassification(
        Type exceptionType, FileOperationErrorType expectedErrorType)
    {
        // Given
        using var scope = _databaseFixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();
        
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test error message")!;
        
        var errorContext = new ErrorContext
        {
            Exception = exception,
            OperationType = "CLASSIFICATION_TEST",
            SourcePath = "/test/source.mkv",
            TargetPath = "/test/target.mkv",
            FileSize = 1024 * 1024, // 1MB
            FileHash = $"classification-{exceptionType.Name.ToLower()}",
            RetryAttempts = 0
        };

        // When
        var result = await service.ClassifyErrorAsync(errorContext);

        // Then
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ErrorType.Should().Be(expectedErrorType);
        result.Value.ClassificationConfidence.Should().BeGreaterThan(0.5);
    }
}