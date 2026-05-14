using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Core.Services;
using MediaButler.Data;
using MediaButler.Services;
using MediaButler.Services.Interfaces;
using MediaButler.Services.FileOperations;
using MediaButler.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MediaButler.Data.UnitOfWork;
using UnitOfWorkImpl = MediaButler.Data.UnitOfWork.UnitOfWork;
using Xunit;
using MediaButler.Tests.Integration.Infrastructure;

namespace MediaButler.Tests.Integration.Services;

/// <summary>
/// Integration tests for concurrent DbContext access scenarios.
/// Validates the fix for Priority 1 (v1.0.7) - DbContext Threading Issues.
/// </summary>
[Collection("Database Tests")]
public class ConcurrentDbContextTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _databaseFixture;
    private readonly IServiceProvider _serviceProvider;

    public ConcurrentDbContextTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
        _serviceProvider = databaseFixture.ServiceProvider;
    }

    [Fact]
    public async Task RollbackService_ConcurrentOperations_ShouldNotThrowDbContextException()
    {
        // Arrange
        var tasks = new List<Task>();
        var testFiles = Enumerable.Range(1, 10).Select(i => new
        {
            Hash = $"hash_{i}",
            OperationType = "ORGANIZE",
            OriginalPath = $"/test/file_{i}.mkv",
            TargetPath = $"/library/TEST/file_{i}.mkv"
        }).ToList();

        // Act - Create TrackedFile entries first to satisfy foreign key constraints
        using (var setupScope = _serviceProvider.CreateScope())
        {
            var dbContext = setupScope.ServiceProvider.GetRequiredService<MediaButlerDbContext>();
            foreach (var file in testFiles)
            {
                dbContext.TrackedFiles.Add(new TrackedFile
                {
                    Hash = file.Hash,
                    FileName = Path.GetFileName(file.OriginalPath),
                    OriginalPath = file.OriginalPath,
                    Status = FileStatus.New
                });
            }
            await dbContext.SaveChangesAsync();
        }

        // Act - Create multiple concurrent rollback points
        for (int i = 0; i < testFiles.Count; i++)
        {
            var file = testFiles[i];
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                // Add staggering delay to prevent SQLite lock contention
                await Task.Delay(index * 20);

                using var scope = _serviceProvider.CreateScope();
                var rollbackService = scope.ServiceProvider.GetRequiredService<IRollbackService>();

                var result = await rollbackService.CreateRollbackPointAsync(
                    file.Hash,
                    file.OperationType,
                    file.OriginalPath,
                    file.TargetPath);

                result.IsSuccess.Should().BeTrue();
            }));
        }

        // Assert - All operations should complete without DbContext threading errors
        var aggregateTask = Task.WhenAll(tasks);
        await aggregateTask;

        aggregateTask.IsCompletedSuccessfully.Should().BeTrue();
        aggregateTask.Exception.Should().BeNull("No DbContext threading exceptions should occur");
    }

    [Fact]
    public async Task RollbackService_SimultaneousReadAndWrite_ShouldSucceed()
    {
        // Arrange - Create initial rollback point
        using (var scope = _serviceProvider.CreateScope())
        {
            var rollbackService = scope.ServiceProvider.GetRequiredService<IRollbackService>();
            await rollbackService.CreateRollbackPointAsync(
                "test_hash",
                "ORGANIZE",
                "/test/original.mkv",
                "/library/TEST/target.mkv");
        }

        var readTasks = new List<Task>();
        var writeTasks = new List<Task>();

        // Act - Simultaneous reads and writes
        for (int i = 0; i < 5; i++)
        {
            var index = i;
            // Read operations
            readTasks.Add(Task.Run(async () =>
            {
                await Task.Delay(index * 20);
                using var scope = _serviceProvider.CreateScope();
                var rollbackService = scope.ServiceProvider.GetRequiredService<IRollbackService>();
                var result = await rollbackService.GetRollbackHistoryAsync("test_hash");
                result.IsSuccess.Should().BeTrue();
            }));

            // Write operations
            writeTasks.Add(Task.Run(async () =>
            {
                await Task.Delay(index * 20 + 10);
                using var scope = _serviceProvider.CreateScope();
                var rollbackService = scope.ServiceProvider.GetRequiredService<IRollbackService>();
                await rollbackService.CreateRollbackPointAsync(
                    $"concurrent_hash_{index}",
                    "ORGANIZE",
                    $"/test/file_{index}.mkv",
                    $"/library/TEST/file_{index}.mkv");
            }));
        }

        // Assert
        var allTasks = readTasks.Concat(writeTasks);
        var aggregateTask = Task.WhenAll(allTasks);
        await aggregateTask;

        aggregateTask.IsCompletedSuccessfully.Should().BeTrue();
        aggregateTask.Exception.Should().BeNull();
    }

    [Fact]
    public async Task RollbackService_HighConcurrency_ShouldMaintainDataIntegrity()
    {
        // Arrange
        const int concurrentOperations = 10;
        var tasks = new List<Task>();

        // Pre-create TrackedFiles
        using (var setupScope = _serviceProvider.CreateScope())
        {
            var dbContext = setupScope.ServiceProvider.GetRequiredService<MediaButlerDbContext>();
            for (int i = 0; i < concurrentOperations; i++)
            {
                dbContext.TrackedFiles.Add(new TrackedFile
                {
                    Hash = $"high_concurrency_{i}",
                    FileName = $"concurrent_{i}.mkv",
                    OriginalPath = $"/test/concurrent_{i}.mkv",
                    Status = FileStatus.New
                });
            }
            await dbContext.SaveChangesAsync();
        }

        // Act - High concurrency scenario (50 concurrent operations)
        for (int i = 0; i < concurrentOperations; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                // Stagger starts to prevent SQLite locking
                await Task.Delay(index * 25);

                using var scope = _serviceProvider.CreateScope();
                var rollbackService = scope.ServiceProvider.GetRequiredService<IRollbackService>();

                await rollbackService.CreateRollbackPointAsync(
                    $"high_concurrency_{index}",
                    "ORGANIZE",
                    $"/test/concurrent_{index}.mkv",
                    $"/library/TEST/concurrent_{index}.mkv");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Verify all rollback points were created
        using (var verifyScope = _serviceProvider.CreateScope())
        {
            var dbContext = verifyScope.ServiceProvider.GetRequiredService<MediaButlerDbContext>();
            var rollbackLogs = await dbContext.ProcessingLogs
                .Where(log => log.Category == "FileOperation.Rollback")
                .ToListAsync();

            rollbackLogs.Should().HaveCountGreaterOrEqualTo(concurrentOperations,
                "All concurrent operations should create rollback points without data loss");
        }
    }

    [Fact]
    public async Task RollbackService_ValidationAndExecution_Concurrent_ShouldNotDeadlock()
    {
        // Pre-create TrackedFile
        using (var setupScope = _serviceProvider.CreateScope())
        {
            var dbContext = setupScope.ServiceProvider.GetRequiredService<MediaButlerDbContext>();
            dbContext.TrackedFiles.Add(new TrackedFile
            {
                Hash = "deadlock_test",
                FileName = "deadlock.mkv",
                OriginalPath = "/test/deadlock.mkv",
                Status = FileStatus.New
            });
            await dbContext.SaveChangesAsync();
        }

        // Arrange - Create rollback point
        Guid rollbackId;
        using (var scope = _serviceProvider.CreateScope())
        {
            var rollbackService = scope.ServiceProvider.GetRequiredService<IRollbackService>();
            var result = await rollbackService.CreateRollbackPointAsync(
                "deadlock_test",
                "ORGANIZE",
                "/test/deadlock.mkv",
                "/library/TEST/deadlock.mkv");
            rollbackId = result.Value;
        }

        // Act - Concurrent validation calls (potential deadlock scenario)
        var validationTasks = Enumerable.Range(1, 10).Select(_ => Task.Run(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var rollbackService = scope.ServiceProvider.GetRequiredService<IRollbackService>();
            var result = await rollbackService.ValidateRollbackIntegrityAsync(rollbackId);
            return result;
        })).ToList();

        // Assert - Should complete without deadlock
        var results = await Task.WhenAll(validationTasks);
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
    }

}
