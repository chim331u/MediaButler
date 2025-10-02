using System.Diagnostics;
using FluentAssertions;
using MediaButler.API;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MediaButler.Tests.Integration.API;

/// <summary>
/// Performance tests for API endpoints with ARM32 optimization validation.
/// Validates the fix for Priority 4 (v1.0.7) - Performance Degradation.
/// </summary>
public class ApiPerformanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const int PerformanceThresholdMs = 1000; // 1 second threshold

    public ApiPerformanceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace DbContext with in-memory database for testing
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<MediaButlerDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<MediaButlerDbContext>(options =>
                {
                    options.UseInMemoryDatabase($"PerformanceTest_{Guid.NewGuid()}");
                });
            });
        });
    }

    [Fact]
    public async Task GetFilesByStatus_WithLargeDataset_ShouldCompleteWithinThreshold()
    {
        // Arrange - Seed database with 500 files
        await SeedDatabaseWithFiles(500);

        var client = _factory.CreateClient();
        var stopwatch = Stopwatch.StartNew();

        // Act - Query files by single status
        var response = await client.GetAsync("/api/files?status=Classified&skip=0&take=100");

        stopwatch.Stop();

        // Assert - Performance within threshold
        response.IsSuccessStatusCode.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(PerformanceThresholdMs,
            $"GET /api/files should complete within {PerformanceThresholdMs}ms (took {stopwatch.ElapsedMilliseconds}ms)");
    }

    [Fact]
    public async Task GetFilesByStatuses_MultipleStatuses_ShouldCompleteWithinThreshold()
    {
        // Arrange - Seed database with 500 files
        await SeedDatabaseWithFiles(500);

        var client = _factory.CreateClient();
        var stopwatch = Stopwatch.StartNew();

        // Act - Query files by multiple statuses (uses new IX_TrackedFiles_MultiStatus_Query index)
        var response = await client.GetAsync(
            "/api/files/by-statuses?statuses=New&statuses=Classified&statuses=ReadyToMove&skip=0&take=100");

        stopwatch.Stop();

        // Assert - Performance within threshold with new index optimization
        response.IsSuccessStatusCode.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(PerformanceThresholdMs,
            $"GET /api/files/by-statuses should complete within {PerformanceThresholdMs}ms (took {stopwatch.ElapsedMilliseconds}ms)");
    }

    [Fact]
    public async Task GetStats_WithLargeDataset_ShouldCompleteWithinThreshold()
    {
        // Arrange - Seed database with 1000 files across all statuses
        await SeedDatabaseWithFiles(1000, distributeAcrossStatuses: true);

        var client = _factory.CreateClient();
        var stopwatch = Stopwatch.StartNew();

        // Act - Get statistics (aggregation query)
        var response = await client.GetAsync("/api/stats");

        stopwatch.Stop();

        // Assert - Performance within threshold
        response.IsSuccessStatusCode.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(PerformanceThresholdMs,
            $"GET /api/stats should complete within {PerformanceThresholdMs}ms (took {stopwatch.ElapsedMilliseconds}ms)");
    }

    [Fact]
    public async Task GetFilesByCategory_WithLargeDataset_ShouldCompleteWithinThreshold()
    {
        // Arrange - Seed database with 500 files
        await SeedDatabaseWithFiles(500);

        var client = _factory.CreateClient();
        var stopwatch = Stopwatch.StartNew();

        // Act - Query files by category (uses IX_TrackedFiles_Category_Stats index)
        var response = await client.GetAsync("/api/files/by-statuses?statuses=Moved&category=BREAKING%20BAD&skip=0&take=100");

        stopwatch.Stop();

        // Assert - Performance within threshold
        response.IsSuccessStatusCode.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(PerformanceThresholdMs,
            $"GET /api/files with category filter should complete within {PerformanceThresholdMs}ms (took {stopwatch.ElapsedMilliseconds}ms)");
    }

    [Theory]
    [InlineData(100)]  // Small dataset
    [InlineData(500)]  // Medium dataset
    [InlineData(1000)] // Large dataset (ARM32 stress test)
    public async Task GetFilesByStatuses_ScalabilityTest_ShouldMaintainPerformance(int fileCount)
    {
        // Arrange - Seed database with varying dataset sizes
        await SeedDatabaseWithFiles(fileCount);

        var client = _factory.CreateClient();
        var stopwatch = Stopwatch.StartNew();

        // Act - Query with multiple statuses
        var response = await client.GetAsync(
            "/api/files/by-statuses?statuses=New&statuses=Classified&statuses=ReadyToMove&statuses=Moved&skip=0&take=100");

        stopwatch.Stop();

        // Assert - Performance scales linearly with AsNoTracking and index optimization
        response.IsSuccessStatusCode.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(PerformanceThresholdMs,
            $"Performance should remain consistent with {fileCount} files (took {stopwatch.ElapsedMilliseconds}ms)");
    }

    [Fact]
    public async Task MemoryUsage_UnderLoad_ShouldRemainBelowARM32Threshold()
    {
        // Arrange - Seed large dataset
        await SeedDatabaseWithFiles(1000);

        var client = _factory.CreateClient();
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

        // Act - Execute multiple concurrent requests
        var tasks = Enumerable.Range(0, 20).Select(async i =>
        {
            await client.GetAsync("/api/files/by-statuses?statuses=New&statuses=Classified&skip=0&take=100");
        }).ToList();

        await Task.WhenAll(tasks);

        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var memoryIncreaseMB = (memoryAfter - memoryBefore) / 1024.0 / 1024.0;

        // Assert - Memory increase should be minimal with AsNoTracking
        memoryIncreaseMB.Should().BeLessThan(50,
            "Memory increase should be <50MB with AsNoTracking optimization for read-only queries");
    }

    [Fact]
    public async Task PaginationPerformance_LargeSkip_ShouldUseEfficientQuery()
    {
        // Arrange - Seed database with 1000 files
        await SeedDatabaseWithFiles(1000);

        var client = _factory.CreateClient();
        var stopwatch = Stopwatch.StartNew();

        // Act - Query with large skip offset (tests index efficiency)
        var response = await client.GetAsync("/api/files/by-statuses?statuses=Classified&skip=900&take=50");

        stopwatch.Stop();

        // Assert - Should remain performant even with large offset
        response.IsSuccessStatusCode.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(PerformanceThresholdMs,
            $"Pagination with large offset should use efficient index (took {stopwatch.ElapsedMilliseconds}ms)");
    }

    /// <summary>
    /// Seeds the in-memory database with test files for performance testing.
    /// </summary>
    private async Task SeedDatabaseWithFiles(int count, bool distributeAcrossStatuses = false)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MediaButlerDbContext>();

        var statuses = distributeAcrossStatuses
            ? Enum.GetValues<FileStatus>()
            : new[] { FileStatus.Classified };

        var categories = new[] { "BREAKING BAD", "THE OFFICE", "GAME OF THRONES", "STRANGER THINGS", "THE WALKING DEAD" };

        var files = Enumerable.Range(1, count).Select(i =>
        {
            var status = distributeAcrossStatuses
                ? statuses[i % statuses.Length]
                : FileStatus.Classified;

            var category = categories[i % categories.Length];

            return new TrackedFile
            {
                Hash = $"hash_{i:D10}",
                FileName = $"{category.Replace(" ", ".")}.S{(i % 10) + 1:D2}E{(i % 24) + 1:D2}.mkv",
                OriginalPath = $"/watch/{category}/{category.Replace(" ", ".")}.S{(i % 10) + 1:D2}E{(i % 24) + 1:D2}.mkv",
                FileSize = 1024 * 1024 * (i % 2000 + 100), // 100-2100 MB
                Status = status,
                Category = category,
                SuggestedCategory = category,
                Confidence = 0.85m + (i % 15) / 100.0m,
                ClassifiedAt = status >= FileStatus.Classified ? DateTime.UtcNow.AddDays(-i % 30) : null,
                MovedAt = status == FileStatus.Moved ? DateTime.UtcNow.AddDays(-i % 30) : null,
                CreatedDate = DateTime.UtcNow.AddDays(-i % 60),
                LastUpdateDate = DateTime.UtcNow.AddDays(-i % 30),
                IsActive = true
            };
        }).ToList();

        await context.TrackedFiles.AddRangeAsync(files);
        await context.SaveChangesAsync();
    }
}
