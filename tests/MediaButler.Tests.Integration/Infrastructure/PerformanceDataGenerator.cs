using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data;
using MediaButler.Tests.Unit.Builders;

namespace MediaButler.Tests.Integration.Infrastructure;

/// <summary>
/// Generates large volumes of realistic test data for performance testing.
/// Optimized for ARM32 environments with memory-conscious batch processing.
/// Follows "Simple Made Easy" principles with predictable, reproducible data patterns.
/// </summary>
public static class PerformanceDataGenerator
{
    // Predefined series for realistic data generation
    private static readonly string[] TVSeries = new[]
    {
        "Breaking Bad", "The Office", "Game of Thrones", "Friends", "Lost", 
        "Stranger Things", "The Walking Dead", "House", "Sherlock", "Dexter",
        "True Detective", "Westworld", "Black Mirror", "The Sopranos", "Mad Men",
        "Better Call Saul", "The Wire", "Succession", "Ozark", "Narcos"
    };

    private static readonly string[] Qualities = new[] { "720p", "1080p", "2160p", "4K" };
    private static readonly string[] Sources = new[] { "WEBRip", "BluRay", "HDTV", "WEB-DL" };
    private static readonly string[] ReleaseGroups = new[] { "[RARBG]", "[YTS]", "[EZTV]", "[TGx]", "[Trash]" };

    /// <summary>
    /// Generates a large dataset optimized for performance testing on ARM32.
    /// Uses batch processing to avoid memory exhaustion.
    /// </summary>
    public static async Task GenerateLargeDatasetAsync(
        MediaButlerDbContext context, 
        int totalFiles, 
        int batchSize = 500)
    {
        var random = new Random(12345); // Fixed seed for reproducible results
        var processedFiles = 0;

        while (processedFiles < totalFiles)
        {
            var remainingFiles = Math.Min(batchSize, totalFiles - processedFiles);
            var batchFiles = new List<TrackedFile>(remainingFiles);

            for (int i = 0; i < remainingFiles; i++)
            {
                var fileIndex = processedFiles + i;
                var file = GenerateRealisticFile(fileIndex, random);
                batchFiles.Add(file);
            }

            await context.TrackedFiles.AddRangeAsync(batchFiles);
            await context.SaveChangesAsync();

            processedFiles += remainingFiles;

            // Force garbage collection every 5 batches to manage ARM32 memory
            if (processedFiles % (batchSize * 5) == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }

    /// <summary>
    /// Generates files with realistic distribution across different statuses.
    /// Mimics real-world usage patterns for accurate performance testing.
    /// </summary>
    public static async Task GenerateRealisticDistributionAsync(
        MediaButlerDbContext context,
        int totalFiles)
    {
        var statusDistribution = new Dictionary<FileStatus, double>
        {
            { FileStatus.New, 0.20 },           // 20% new files
            { FileStatus.Classified, 0.30 },    // 30% classified awaiting confirmation  
            { FileStatus.ReadyToMove, 0.15 },   // 15% confirmed, ready to move
            { FileStatus.Moved, 0.30 },         // 30% successfully moved
            { FileStatus.Error, 0.05 }          // 5% in error state
        };

        var random = new Random(54321);
        var files = new List<TrackedFile>();

        foreach (var (status, percentage) in statusDistribution)
        {
            var fileCount = (int)(totalFiles * percentage);
            
            for (int i = 0; i < fileCount; i++)
            {
                var file = GenerateFileWithStatus(status, files.Count, random);
                files.Add(file);
            }
        }

        // Shuffle to avoid sequential patterns
        for (int i = files.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (files[i], files[j]) = (files[j], files[i]);
        }

        const int batchSize = 250;
        for (int i = 0; i < files.Count; i += batchSize)
        {
            var batch = files.Skip(i).Take(batchSize);
            await context.TrackedFiles.AddRangeAsync(batch);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Generates time-series data for testing temporal queries and trends.
    /// Files spread across different time periods for realistic testing.
    /// </summary>
    public static async Task GenerateTimeSeriesDataAsync(
        MediaButlerDbContext context,
        int filesPerDay,
        int dayCount)
    {
        var baseDate = DateTime.UtcNow.AddDays(-dayCount);
        var random = new Random(11111);

        for (int day = 0; day < dayCount; day++)
        {
            var dayFiles = new List<TrackedFile>();
            var currentDate = baseDate.AddDays(day);

            for (int file = 0; file < filesPerDay; file++)
            {
                var fileIndex = day * filesPerDay + file;
                var trackedFile = GenerateRealisticFile(fileIndex, random);
                
                // Set realistic timestamps for the day
                var createdTime = currentDate.AddHours(random.NextDouble() * 24);
                var updatedTime = createdTime.AddMinutes(random.Next(1, 1440)); // 1 minute to 24 hours later

                SetTimestamps(trackedFile, createdTime, updatedTime);
                dayFiles.Add(trackedFile);
            }

            await context.TrackedFiles.AddRangeAsync(dayFiles);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Generates files with various error scenarios for resilience testing.
    /// Creates realistic error patterns and retry scenarios.
    /// </summary>
    public static async Task GenerateErrorScenariosAsync(
        MediaButlerDbContext context,
        int errorFileCount)
    {
        var errorScenarios = new[]
        {
            ("Network timeout during classification", 1, 0.3),
            ("File access denied", 2, 0.2),
            ("Disk space insufficient", 3, 0.1),
            ("ML model unavailable", 1, 0.2),
            ("File corrupted during processing", 2, 0.1),
            ("Database connection failed", 1, 0.05),
            ("Classification service overloaded", 2, 0.05)
        };

        var random = new Random(99999);
        var files = new List<TrackedFile>();
        var logs = new List<ProcessingLog>();

        for (int i = 0; i < errorFileCount; i++)
        {
            // Select error scenario based on probability weights
            var scenario = SelectWeightedError(errorScenarios, random);
            var file = new TrackedFileBuilder()
                .AsTVEpisode(TVSeries[i % TVSeries.Length], 1, (i % 24) + 1)
                .AsError(scenario.Item1, scenario.Item2)
                .Build();

            files.Add(file);

            // Add corresponding error log
            var errorLog = ProcessingLog.Error(
                file.Hash,
                "PerformanceTest",
                "Error Scenario",
                exception: null,
                details: scenario.Item1);

            logs.Add(errorLog);
        }

        await context.TrackedFiles.AddRangeAsync(files);
        await context.ProcessingLogs.AddRangeAsync(logs);
        await context.SaveChangesAsync();
    }

    private static TrackedFile GenerateRealisticFile(int index, Random random)
    {
        var series = TVSeries[index % TVSeries.Length];
        var season = (index % 10) + 1;
        var episode = (index % 24) + 1;
        var quality = Qualities[random.Next(Qualities.Length)];
        var source = Sources[random.Next(Sources.Length)];
        var releaseGroup = ReleaseGroups[random.Next(ReleaseGroups.Length)];

        var fileName = $"{series}.S{season:D2}E{episode:D2}.{source}.{quality}.{releaseGroup}.mkv";
        var hash = $"perf{index:D10}".PadRight(64, '0').Substring(0, 64);

        return new TrackedFileBuilder()
            .WithFileName(fileName)
            .WithHash(hash)
            .WithOriginalPath($"/downloads/{fileName}")
            .WithFileSize(random.NextInt64(300_000_000, 3_000_000_000)) // 300MB - 3GB
            .WithTimestamps(
                DateTime.UtcNow.AddDays(-random.Next(0, 365)),
                DateTime.UtcNow.AddDays(-random.Next(0, 30))
            )
            .Build();
    }

    private static TrackedFile GenerateFileWithStatus(FileStatus status, int index, Random random)
    {
        var file = GenerateRealisticFile(index, random);
        
        return status switch
        {
            FileStatus.Classified => new TrackedFileBuilder()
                .WithHash(file.Hash)
                .WithFileName(file.FileName)
                .WithOriginalPath(file.OriginalPath)
                .WithFileSize(file.FileSize)
                .AsClassified(ExtractSeriesName(file.FileName), (decimal)(0.6 + random.NextDouble() * 0.35))
                .Build(),
                
            FileStatus.ReadyToMove => new TrackedFileBuilder()
                .WithHash(file.Hash)
                .WithFileName(file.FileName)
                .WithOriginalPath(file.OriginalPath)
                .WithFileSize(file.FileSize)
                .AsConfirmed(ExtractSeriesName(file.FileName))
                .Build(),
                
            FileStatus.Moved => new TrackedFileBuilder()
                .WithHash(file.Hash)
                .WithFileName(file.FileName)
                .WithOriginalPath(file.OriginalPath)
                .WithFileSize(file.FileSize)
                .AsMoved(ExtractSeriesName(file.FileName), $"/library/{ExtractSeriesName(file.FileName)}/{file.FileName}")
                .Build(),
                
            FileStatus.Error => new TrackedFileBuilder()
                .WithHash(file.Hash)
                .WithFileName(file.FileName)
                .WithOriginalPath(file.OriginalPath)
                .WithFileSize(file.FileSize)
                .AsError("Performance test error", random.Next(1, 4))
                .Build(),
                
            _ => file
        };
    }

    private static (string, int, double) SelectWeightedError((string, int, double)[] scenarios, Random random)
    {
        var totalWeight = scenarios.Sum(s => s.Item3);
        var randomValue = random.NextDouble() * totalWeight;
        
        double currentWeight = 0;
        foreach (var scenario in scenarios)
        {
            currentWeight += scenario.Item3;
            if (randomValue <= currentWeight)
                return scenario;
        }
        
        return scenarios[0]; // Fallback
    }

    private static string ExtractSeriesName(string fileName)
    {
        var parts = fileName.Split('.');
        if (parts.Length > 2)
        {
            // Take first two parts as series name (e.g., "Breaking.Bad" from "Breaking.Bad.S01E01.1080p.mkv")
            return string.Join(" ", parts.Take(2)).ToUpperInvariant();
        }
        return parts[0].ToUpperInvariant();
    }

    private static void SetTimestamps(TrackedFile file, DateTime createdDate, DateTime updatedDate)
    {
        var baseEntityType = typeof(TrackedFile).BaseType;
        baseEntityType?.GetProperty("CreatedDate")?.SetValue(file, createdDate);
        baseEntityType?.GetProperty("LastUpdateDate")?.SetValue(file, updatedDate);
    }
}