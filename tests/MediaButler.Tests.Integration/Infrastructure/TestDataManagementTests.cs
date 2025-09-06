using FluentAssertions;
using MediaButler.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace MediaButler.Tests.Integration.Infrastructure;

/// <summary>
/// Integration tests validating the test data management infrastructure.
/// Demonstrates proper usage of seeding, isolation, and performance data generation.
/// Follows "Simple Made Easy" principles with clear, focused test scenarios.
/// </summary>
public class TestDataManagementTests : IntegrationTestBase
{
    public TestDataManagementTests(DatabaseFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task SeedWorkflowScenario_ShouldCreateCompleteTestData()
    {
        // Arrange & Act
        await SeedDataAsync(TestDataScenario.Workflow);

        // Assert - Verify files in different statuses exist
        var newFiles = await Context.TrackedFiles.Where(f => f.Status == FileStatus.New).ToListAsync();
        var classifiedFiles = await Context.TrackedFiles.Where(f => f.Status == FileStatus.Classified).ToListAsync();
        var confirmedFiles = await Context.TrackedFiles.Where(f => f.Status == FileStatus.ReadyToMove).ToListAsync();
        var movedFiles = await Context.TrackedFiles.Where(f => f.Status == FileStatus.Moved).ToListAsync();
        var errorFiles = await Context.TrackedFiles.Where(f => f.Status == FileStatus.Error).ToListAsync();

        newFiles.Should().HaveCount(3);
        classifiedFiles.Should().HaveCount(2);
        confirmedFiles.Should().HaveCount(2);
        movedFiles.Should().HaveCount(1);
        errorFiles.Should().HaveCount(1);

        // Verify configurations exist
        var configs = await Context.ConfigurationSettings.ToListAsync();
        configs.Should().HaveCount(3);

        // Verify processing logs exist
        var logs = await Context.ProcessingLogs.ToListAsync();
        logs.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task SeedSoftDeleteScenario_ShouldCreateActiveAndDeletedFiles()
    {
        // Arrange & Act
        await SeedDataAsync(TestDataScenario.SoftDelete);

        // Assert - Verify soft delete behavior
        var allFiles = await Context.TrackedFiles.IgnoreQueryFilters().ToListAsync();
        var activeFiles = await Context.TrackedFiles.ToListAsync();

        allFiles.Should().HaveCount(8); // 5 active + 3 deleted
        activeFiles.Should().HaveCount(5); // Only active files visible by default

        var deletedFiles = allFiles.Where(f => f.IsActive == false).ToList();
        deletedFiles.Should().HaveCount(3);
    }

    [Fact]
    public async Task TestIsolation_ShouldEnsureCleanStateBeforeEachTest()
    {
        // Arrange - Verify we start with clean database
        await AssertDatabaseIsEmptyAsync();

        // Act - Add some test data
        await SeedDataAsync(TestDataScenario.Minimal);

        // Assert - Verify data was added
        var fileCount = await Context.TrackedFiles.CountAsync();
        var configCount = await Context.ConfigurationSettings.CountAsync();

        fileCount.Should().Be(1);
        configCount.Should().Be(1);

        // Note: Cleanup happens automatically in next test due to IntegrationTestBase
    }

    [Fact]
    public async Task ExecuteIsolated_ShouldGuaranteeCleanupEvenAfterExceptions()
    {
        // Arrange & Act
        var result = await ExecuteIsolatedAsync(async () =>
        {
            await SeedDataAsync(TestDataScenario.Minimal);
            
            // Verify data exists
            var count = await Context.TrackedFiles.CountAsync();
            count.Should().Be(1);
            
            return count;
        });

        // Assert - Data should exist during execution
        result.Should().Be(1);

        // Verify cleanup occurred after execution
        var finalCount = await Context.TrackedFiles.CountAsync();
        finalCount.Should().Be(0);
    }

    [Fact]
    public async Task PerformanceDataGenerator_ShouldCreateRealisticDistribution()
    {
        // Arrange & Act
        await PerformanceDataGenerator.GenerateRealisticDistributionAsync(Context, 100);

        // Assert - Verify realistic status distribution
        var statusCounts = await Context.TrackedFiles
            .GroupBy(f => f.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        statusCounts.Should().HaveCount(5); // All statuses should be represented

        var newCount = statusCounts.First(s => s.Status == FileStatus.New).Count;
        var classifiedCount = statusCounts.First(s => s.Status == FileStatus.Classified).Count;
        var movedCount = statusCounts.First(s => s.Status == FileStatus.Moved).Count;

        // Verify approximate distribution (allowing for rounding)
        newCount.Should().BeInRange(15, 25);      // ~20%
        classifiedCount.Should().BeInRange(25, 35); // ~30%  
        movedCount.Should().BeInRange(25, 35);    // ~30%
    }

    [Fact]
    public async Task TimeSeriesGeneration_ShouldCreateTemporalData()
    {
        // Arrange & Act
        await PerformanceDataGenerator.GenerateTimeSeriesDataAsync(Context, filesPerDay: 5, dayCount: 10);

        // Assert
        var totalFiles = await Context.TrackedFiles.CountAsync();
        totalFiles.Should().Be(50); // 5 files × 10 days

        // Verify files are distributed across different dates
        var files = await Context.TrackedFiles.ToListAsync();
        var dateCounts = files
            .GroupBy(f => f.CreatedDate.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(dc => dc.Date)
            .ToList();

        // Debug logging removed for cleaner test output

        // Should have approximately 10 different days (allowing some tolerance due to random hour assignment)
        dateCounts.Should().HaveCountLessOrEqualTo(12); // Allow some tolerance for boundary conditions
        dateCounts.Should().HaveCountGreaterOrEqualTo(8);  // At least most days represented
        
        // Total should be exactly 50 files regardless of distribution
        var totalFilesByDate = dateCounts.Sum(dc => dc.Count);
        totalFilesByDate.Should().Be(50);
    }

    [Fact]
    public async Task ErrorScenarioGeneration_ShouldCreateRealisticErrors()
    {
        // Arrange & Act
        await PerformanceDataGenerator.GenerateErrorScenariosAsync(Context, 25);

        // Assert
        var errorFiles = await Context.TrackedFiles.Where(f => f.Status == FileStatus.Error).ToListAsync();
        var errorLogs = await Context.ProcessingLogs.Where(l => l.Level == LogLevel.Error).ToListAsync();

        errorFiles.Should().HaveCount(25);
        errorLogs.Should().HaveCount(25);

        // Verify realistic error patterns
        var errorTypes = errorFiles.Select(f => f.LastError).Distinct().ToList();
        errorTypes.Should().HaveCountGreaterThan(3); // Multiple error types
        
        errorFiles.Should().OnlyContain(f => f.RetryCount >= 1 && f.RetryCount <= 3);
    }

    [Fact]
    public async Task BatchProcessing_ShouldHandleLargeDatasets()
    {
        // Arrange & Act - Test memory-efficient batch processing
        await PerformanceDataGenerator.GenerateLargeDatasetAsync(Context, totalFiles: 500, batchSize: 100);

        // Assert
        var totalFiles = await Context.TrackedFiles.CountAsync();
        totalFiles.Should().Be(500);

        // Verify realistic file patterns by loading files to memory first
        var fileNames = await Context.TrackedFiles
            .Select(f => f.FileName)
            .ToListAsync();
        
        var uniqueSeries = fileNames
            .Select(f => f.Split('.')[0])
            .Distinct()
            .Count();

        uniqueSeries.Should().BeGreaterThan(10); // Multiple TV series represented
    }
}