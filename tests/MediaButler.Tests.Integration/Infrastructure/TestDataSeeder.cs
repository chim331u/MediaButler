using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data;
using MediaButler.Tests.Unit.Builders;

namespace MediaButler.Tests.Integration.Infrastructure;

/// <summary>
/// Database seeding utilities for integration tests.
/// Provides pre-configured test data scenarios following "Simple Made Easy" principles.
/// Each seeding method creates a complete, isolated scenario for testing.
/// </summary>
public static class TestDataSeeder
{
    /// <summary>
    /// Seeds the database with a typical file processing workflow scenario.
    /// Creates files in various states for comprehensive workflow testing.
    /// </summary>
    public static async Task SeedWorkflowScenarioAsync(MediaButlerDbContext context)
    {
        // Create files representing a complete processing pipeline with unique hashes
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        var newFiles = new[]
        {
            new TrackedFileBuilder().AsTVEpisode("Breaking Bad", 1, 1).WithHash($"workflownew01{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build(),
            new TrackedFileBuilder().AsTVEpisode("Breaking Bad", 1, 2).WithHash($"workflownew02{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build(),
            new TrackedFileBuilder().AsTVEpisode("Breaking Bad", 1, 3).WithHash($"workflownew03{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build()
        };

        var classifiedFiles = new[]
        {
            new TrackedFileBuilder().AsTVEpisode("The Office", 1, 1).AsClassified("THE OFFICE", 0.9m).WithHash($"workflowcla01{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build(),
            new TrackedFileBuilder().AsTVEpisode("The Office", 1, 2).AsClassified("THE OFFICE", 0.9m).WithHash($"workflowcla02{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build()
        };

        var confirmedFiles = new[]
        {
            new TrackedFileBuilder().AsTVEpisode("Game of Thrones", 1, 1).AsConfirmed("GAME OF THRONES").WithHash($"workflowcon01{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build(),
            new TrackedFileBuilder().AsTVEpisode("Game of Thrones", 1, 2).AsConfirmed("GAME OF THRONES").WithHash($"workflowcon02{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build()
        };

        var movedFiles = new[]
        {
            new TrackedFileBuilder().AsTVEpisode("Friends", 1, 1).AsMoved("FRIENDS", "/library/FRIENDS/Friends.S01E01.1080p.mkv").WithHash($"workflowmov01{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build()
        };

        var errorFiles = new[]
        {
            new TrackedFileBuilder().AsTVEpisode("Lost", 1, 1).AsError("Classification timeout", 2).WithHash($"workflowerr01{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build()
        };

        // Add configuration settings
        var configurations = new[]
        {
            new ConfigurationSetting
            {
                Key = "MediaButler.Paths.WatchFolder",
                Value = "/test/watch",
                Section = "Paths",
                DataType = ConfigurationDataType.String
            },
            new ConfigurationSetting
            {
                Key = "MediaButler.ML.ConfidenceThreshold",
                Value = "0.85",
                Section = "ML",
                DataType = ConfigurationDataType.String
            },
            new ConfigurationSetting
            {
                Key = "MediaButler.Butler.AutoOrganizeEnabled",
                Value = "true",
                Section = "Butler",
                DataType = ConfigurationDataType.Boolean
            }
        };

        // Add to context
        await context.TrackedFiles.AddRangeAsync(newFiles);
        await context.TrackedFiles.AddRangeAsync(classifiedFiles);
        await context.TrackedFiles.AddRangeAsync(confirmedFiles);
        await context.TrackedFiles.AddRangeAsync(movedFiles);
        await context.TrackedFiles.AddRangeAsync(errorFiles);
        await context.ConfigurationSettings.AddRangeAsync(configurations);

        // Add processing logs for classified files
        var logs = classifiedFiles.Select(file => ProcessingLog.Info(
            file.Hash,
            "ML",
            "ML Classification",
            $"Classified as {file.SuggestedCategory} with confidence {file.Confidence}")
        ).ToArray();

        await context.ProcessingLogs.AddRangeAsync(logs);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds the database with performance testing data.
    /// Creates large volumes of files for load testing and performance validation.
    /// </summary>
    public static async Task SeedPerformanceTestDataAsync(MediaButlerDbContext context, int fileCount = 1000)
    {
        const int batchSize = 100;
        var series = new[] { "Breaking Bad", "The Office", "Game of Thrones", "Friends", "Lost", "Stranger Things" };
        
        for (int batch = 0; batch < fileCount / batchSize; batch++)
        {
            var files = new List<TrackedFile>();
            
            for (int i = 0; i < batchSize; i++)
            {
                var fileIndex = batch * batchSize + i;
                var selectedSeries = series[fileIndex % series.Length];
                var season = (fileIndex % 10) + 1;
                var episode = (fileIndex % 24) + 1;
                
                var file = new TrackedFileBuilder()
                    .AsTVEpisode(selectedSeries, season, episode)
                    .WithStatus((FileStatus)(fileIndex % 5)) // Distribute across all statuses
                    .WithTimestamps(
                        DateTime.UtcNow.AddDays(-Random.Shared.Next(0, 365)), // Random creation date within year
                        DateTime.UtcNow.AddDays(-Random.Shared.Next(0, 30))   // Random update date within month
                    )
                    .Build();
                    
                files.Add(file);
            }
            
            await context.TrackedFiles.AddRangeAsync(files);
            await context.SaveChangesAsync(); // Save in batches to avoid memory issues
        }
    }

    /// <summary>
    /// Seeds the database with soft delete testing scenarios.
    /// Creates active and soft-deleted files for testing query filters.
    /// </summary>
    public static async Task SeedSoftDeleteScenarioAsync(MediaButlerDbContext context)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        // Create active files with unique hashes
        var activeFiles = new[]
        {
            new TrackedFileBuilder().AsTVEpisode("Active Series", 1, 1).WithHash($"active01{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build(),
            new TrackedFileBuilder().AsTVEpisode("Active Series", 1, 2).WithHash($"active02{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build(),
            new TrackedFileBuilder().AsTVEpisode("Active Series", 1, 3).WithHash($"active03{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build(),
            new TrackedFileBuilder().AsTVEpisode("Active Series", 1, 4).WithHash($"active04{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build(),
            new TrackedFileBuilder().AsTVEpisode("Active Series", 1, 5).WithHash($"active05{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build()
        };

        // Create files to soft delete
        var filesToDelete = new[]
        {
            new TrackedFileBuilder().AsTVEpisode("Deleted Series", 1, 1).WithHash($"delete01{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build(),
            new TrackedFileBuilder().AsTVEpisode("Deleted Series", 1, 2).WithHash($"delete02{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build(),
            new TrackedFileBuilder().AsTVEpisode("Deleted Series", 1, 3).WithHash($"delete03{timestamp:X}".PadRight(64, '0').Substring(0, 64)).Build()
        };

        await context.TrackedFiles.AddRangeAsync(activeFiles);
        await context.TrackedFiles.AddRangeAsync(filesToDelete);
        await context.SaveChangesAsync();

        // Soft delete some files
        foreach (var file in filesToDelete)
        {
            file.SoftDelete("Integration test soft delete");
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds the database with classification accuracy testing data.
    /// Creates files with known series patterns for ML testing.
    /// </summary>
    public static async Task SeedClassificationTestDataAsync(MediaButlerDbContext context)
    {
        var testScenarios = new[]
        {
            // Clear matches - should classify with high confidence
            ("Breaking.Bad.S01E01.1080p.mkv", "BREAKING BAD", 0.95m),
            ("The.Office.US.S02E05.720p.mkv", "THE OFFICE", 0.90m),
            ("Game.of.Thrones.S08E06.FINAL.4K.mkv", "GAME OF THRONES", 0.92m),
            
            // Ambiguous matches - should classify with medium confidence  
            ("Friends.1994.S01E01.mkv", "FRIENDS", 0.75m),
            ("Lost.S06E18.The.End.mkv", "LOST", 0.70m),
            
            // Difficult matches - should classify with low confidence or fail
            ("[Trash].One.Piece.1089.1080p.mkv", "ONE PIECE", 0.60m),
            ("Strange.Series.Name.2024.E01.mkv", "STRANGE SERIES NAME", 0.45m)
        };

        var files = testScenarios.Select((scenario, index) => 
            new TrackedFileBuilder()
                .WithFileName(scenario.Item1)
                .WithHash($"classification{index:D3}".PadRight(64, '0').Substring(0, 64))
                .WithOriginalPath($"/test/classification/{scenario.Item1}")
                .AsClassified(scenario.Item2, scenario.Item3)
                .Build()
        ).ToArray();

        await context.TrackedFiles.AddRangeAsync(files);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds the database with error handling test scenarios.
    /// Creates files with various error conditions for resilience testing.
    /// </summary>
    public static async Task SeedErrorScenarioAsync(MediaButlerDbContext context)
    {
        var errorScenarios = new[]
        {
            ("Network timeout during classification", 1),
            ("File access denied", 2),
            ("Disk space insufficient", 3),
            ("ML model unavailable", 1),
            ("File corrupted during processing", 2)
        };

        var files = errorScenarios.Select((scenario, index) =>
            new TrackedFileBuilder()
                .AsTVEpisode($"Error Series {index + 1}", 1, 1)
                .AsError(scenario.Item1, scenario.Item2)
                .Build()
        ).ToArray();

        await context.TrackedFiles.AddRangeAsync(files);

        // Add error logs
        var errorLogs = files.Select(file => ProcessingLog.Error(
            file.Hash,
            "ErrorTest",
            "Error Scenario",
            exception: null,
            details: file.LastError ?? "Unknown error")
        ).ToArray();

        await context.ProcessingLogs.AddRangeAsync(errorLogs);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds the database with a minimal test scenario.
    /// Creates just essential data for simple integration tests.
    /// </summary>
    public static async Task SeedMinimalScenarioAsync(MediaButlerDbContext context)
    {
        var file = new TrackedFileBuilder()
            .WithFileName("Minimal.Test.S01E01.mkv")
            .WithHash("minimal123456789012345678901234567890123456789012345678901")
            .WithOriginalPath("/test/Minimal.Test.S01E01.mkv")
            .Build();

        var config = new ConfigurationSetting
        {
            Key = "MediaButler.Test.MinimalSetting",
            Value = "test",
            Section = "Test",
            DataType = ConfigurationDataType.String
        };

        await context.TrackedFiles.AddAsync(file);
        await context.ConfigurationSettings.AddAsync(config);
        await context.SaveChangesAsync();
    }
}