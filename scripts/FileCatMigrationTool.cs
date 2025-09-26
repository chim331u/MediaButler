using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Migration tool to import data from FileCat.db to MediaButler database
/// Usage: dotnet run FileCatMigrationTool.cs
/// </summary>
public class FileCatMigrationTool
{
    private readonly string _sourceDatabasePath;
    private readonly string _targetDatabasePath;
    private readonly bool _dryRun;

    public FileCatMigrationTool(string sourcePath, string targetPath, bool dryRun = true)
    {
        _sourceDatabasePath = sourcePath;
        _targetDatabasePath = targetPath;
        _dryRun = dryRun;
    }

    public static async Task Main(string[] args)
    {
        var sourcePath = "/Users/luca/GitHub/mediabutler/MediaButler/temp/Import/FileCat.db";
        var targetPath = "/Users/luca/GitHub/mediabutler/MediaButler/temp/mediabutler.dev.db";
        var dryRun = args.Length > 0 && args[0].ToLower() == "--dry-run";

        var migrator = new FileCatMigrationTool(sourcePath, targetPath, dryRun);
        await migrator.MigrateAsync();
    }

    public async Task MigrateAsync()
    {
        Console.WriteLine("=== FileCat to MediaButler Migration Tool ===");
        Console.WriteLine($"Source: {_sourceDatabasePath}");
        Console.WriteLine($"Target: {_targetDatabasePath}");
        Console.WriteLine($"Mode: {(_dryRun ? "DRY RUN" : "LIVE MIGRATION")}");
        Console.WriteLine();

        try
        {
            // Validate source database
            if (!File.Exists(_sourceDatabasePath))
            {
                throw new FileNotFoundException($"Source database not found: {_sourceDatabasePath}");
            }

            // Validate target database
            if (!File.Exists(_targetDatabasePath))
            {
                throw new FileNotFoundException($"Target database not found: {_targetDatabasePath}");
            }

            // Load source data
            var sourceRecords = await LoadSourceDataAsync();
            Console.WriteLine($"Found {sourceRecords.Count} records in source database");

            // Analyze source data
            await AnalyzeSourceDataAsync(sourceRecords);

            // Check for existing data in target
            var existingCount = await GetExistingRecordCountAsync();
            Console.WriteLine($"Target database currently has {existingCount} tracked files");

            if (!_dryRun && existingCount > 0)
            {
                Console.Write("Target database has existing data. Continue? (y/N): ");
                var response = Console.ReadLine();
                if (response?.ToLower() != "y")
                {
                    Console.WriteLine("Migration cancelled.");
                    return;
                }
            }

            // Perform migration
            if (_dryRun)
            {
                await SimulateMigrationAsync(sourceRecords);
            }
            else
            {
                await PerformMigrationAsync(sourceRecords);
            }

            Console.WriteLine("\nMigration completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Migration failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private async Task<List<FileCatRecord>> LoadSourceDataAsync()
    {
        var records = new List<FileCatRecord>();

        using var connection = new SqliteConnection($"Data Source={_sourceDatabasePath};Mode=ReadOnly;");
        await connection.OpenAsync();

        var query = @"
            SELECT Id, Name, Path, FileSize, LastUpdateFile, FileCategory,
                   IsToCategorize, IsNew, IsDeleted, IsNotToMove,
                   CreatedDate, LastUpdatedDate, IsActive, Note
            FROM FilesDetail
            WHERE Name IS NOT NULL AND Name != '' AND IsActive = 1
            ORDER BY Id";

        using var command = new SqliteCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            records.Add(new FileCatRecord
            {
                Id = Convert.ToInt32(reader["Id"]),
                Name = reader.IsDBNull(reader.GetOrdinal("Name")) ? null : reader["Name"].ToString(),
                Path = reader.IsDBNull(reader.GetOrdinal("Path")) ? null : reader["Path"].ToString(),
                FileSize = Convert.ToDouble(reader["FileSize"]),
                LastUpdateFile = reader["LastUpdateFile"].ToString() ?? "",
                FileCategory = reader.IsDBNull(reader.GetOrdinal("FileCategory")) ? null : reader["FileCategory"].ToString(),
                IsToCategorize = Convert.ToBoolean(reader["IsToCategorize"]),
                IsNew = Convert.ToBoolean(reader["IsNew"]),
                IsDeleted = Convert.ToBoolean(reader["IsDeleted"]),
                IsNotToMove = Convert.ToBoolean(reader["IsNotToMove"]),
                CreatedDate = DateTime.Parse(reader["CreatedDate"].ToString() ?? DateTime.Now.ToString()),
                LastUpdatedDate = DateTime.Parse(reader["LastUpdatedDate"].ToString() ?? DateTime.Now.ToString()),
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                Note = reader.IsDBNull(reader.GetOrdinal("Note")) ? null : reader["Note"].ToString()
            });
        }

        return records;
    }

    private async Task AnalyzeSourceDataAsync(List<FileCatRecord> records)
    {
        var withCategory = records.Count(r => !string.IsNullOrEmpty(r.FileCategory));
        var deleted = records.Count(r => r.IsDeleted);
        var toCategorize = records.Count(r => r.IsToCategorize);
        var notToMove = records.Count(r => r.IsNotToMove);
        var active = records.Count(r => r.IsActive);

        Console.WriteLine("\n=== Source Data Analysis ===");
        Console.WriteLine($"Total records: {records.Count}");
        Console.WriteLine($"With category: {withCategory}");
        Console.WriteLine($"Deleted: {deleted}");
        Console.WriteLine($"To categorize: {toCategorize}");
        Console.WriteLine($"Not to move: {notToMove}");
        Console.WriteLine($"Active: {active}");

        // Show category distribution
        var categories = records
            .Where(r => !string.IsNullOrEmpty(r.FileCategory))
            .GroupBy(r => r.FileCategory)
            .OrderByDescending(g => g.Count())
            .Take(10);

        Console.WriteLine("\nTop 10 Categories:");
        foreach (var category in categories)
        {
            Console.WriteLine($"  {category.Key}: {category.Count()} files");
        }
    }

    private async Task<int> GetExistingRecordCountAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_targetDatabasePath};");
        await connection.OpenAsync();

        using var command = new SqliteCommand("SELECT COUNT(*) FROM TrackedFiles", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task SimulateMigrationAsync(List<FileCatRecord> records)
    {
        Console.WriteLine("\n=== DRY RUN - Simulating Migration ===");

        var migratedCount = 0;
        var errorCount = 0;

        foreach (var record in records.Take(10)) // Show first 10 for demo
        {
            try
            {
                var trackedFile = MapToTrackedFile(record);
                Console.WriteLine($"Would migrate: {trackedFile.FileName} -> Status: {trackedFile.Status}, Category: {trackedFile.Category ?? "None"}, MovedToPath: {trackedFile.MovedToPath ?? "None"}");
                migratedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error mapping record {record.Id}: {ex.Message}");
                errorCount++;
            }
        }

        Console.WriteLine($"\nDry run completed. Would migrate {migratedCount} records with {errorCount} errors.");
        Console.WriteLine("Run with --live to perform actual migration.");
    }

    private async Task PerformMigrationAsync(List<FileCatRecord> records)
    {
        Console.WriteLine("\n=== LIVE MIGRATION ===");

        using var connection = new SqliteConnection($"Data Source={_targetDatabasePath};");
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            var insertCommand = new SqliteCommand(@"
                INSERT INTO TrackedFiles (
                    Hash, FileName, OriginalPath, FileSize, Status, SuggestedCategory,
                    Confidence, Category, TargetPath, ClassifiedAt, MovedAt, LastError,
                    LastErrorAt, RetryCount, CreatedDate, LastUpdateDate, Note, IsActive, MovedToPath
                ) VALUES (
                    @Hash, @FileName, @OriginalPath, @FileSize, @Status, @SuggestedCategory,
                    @Confidence, @Category, @TargetPath, @ClassifiedAt, @MovedAt, @LastError,
                    @LastErrorAt, @RetryCount, @CreatedDate, @LastUpdateDate, @Note, @IsActive, @MovedToPath
                )", connection, transaction);

            var migratedCount = 0;
            var errorCount = 0;

            foreach (var record in records)
            {
                try
                {
                    var trackedFile = MapToTrackedFile(record);

                    // Set parameters
                    insertCommand.Parameters.Clear();
                    insertCommand.Parameters.AddWithValue("@Hash", trackedFile.Hash);
                    insertCommand.Parameters.AddWithValue("@FileName", trackedFile.FileName);
                    insertCommand.Parameters.AddWithValue("@OriginalPath", trackedFile.OriginalPath);
                    insertCommand.Parameters.AddWithValue("@FileSize", trackedFile.FileSize);
                    insertCommand.Parameters.AddWithValue("@Status", trackedFile.Status);
                    insertCommand.Parameters.AddWithValue("@SuggestedCategory", (object)trackedFile.SuggestedCategory ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@Confidence", trackedFile.Confidence);
                    insertCommand.Parameters.AddWithValue("@Category", (object)trackedFile.Category ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@TargetPath", (object)trackedFile.TargetPath ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@ClassifiedAt", (object)trackedFile.ClassifiedAt ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@MovedAt", DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@LastError", (object)trackedFile.LastError ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@LastErrorAt", (object)trackedFile.LastErrorAt ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@RetryCount", 0);
                    insertCommand.Parameters.AddWithValue("@CreatedDate", trackedFile.CreatedDate);
                    insertCommand.Parameters.AddWithValue("@LastUpdateDate", trackedFile.LastUpdateDate);
                    insertCommand.Parameters.AddWithValue("@Note", (object)trackedFile.Note ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@IsActive", trackedFile.IsActive);
                    insertCommand.Parameters.AddWithValue("@MovedToPath", (object)trackedFile.MovedToPath ?? DBNull.Value);

                    await insertCommand.ExecuteNonQueryAsync();
                    migratedCount++;

                    if (migratedCount % 100 == 0)
                    {
                        Console.WriteLine($"Migrated {migratedCount} records...");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error migrating record {record.Id}: {ex.Message}");
                    errorCount++;
                }
            }

            transaction.Commit();
            Console.WriteLine($"Migration completed: {migratedCount} records migrated, {errorCount} errors");
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    private TrackedFileRecord MapToTrackedFile(FileCatRecord source)
    {
        // Generate hash from file path + name (since we don't have actual file content)
        var hashInput = $"{source.Path ?? ""}/{source.Name ?? ""}";
        var hash = GenerateHash(hashInput);

        // Refactor filename using watch folder method
        var refactoredFileName = RefactorFileName(source.Name ?? "");

        // Determine status based on flags
        var status = DetermineStatus(source);

        // Map target path from source path
        var targetPath = !string.IsNullOrEmpty(source.Path) && !string.IsNullOrEmpty(source.FileCategory)
            ? Path.Combine(source.FileCategory.ToUpperInvariant(), refactoredFileName)
            : null;

        return new TrackedFileRecord
        {
            Hash = hash,
            FileName = refactoredFileName,
            OriginalPath = "", // Keep empty as this represents the watch folder path
            FileSize = (long)source.FileSize, // Migrate filesize as is
            Status = status,
            SuggestedCategory = !string.IsNullOrEmpty(source.FileCategory) ? source.FileCategory.ToUpperInvariant() : null,
            Confidence = !string.IsNullOrEmpty(source.FileCategory) ? 0.95m : 0.0m,
            Category = !string.IsNullOrEmpty(source.FileCategory) && !source.IsNotToMove ? source.FileCategory.ToUpperInvariant() : null,
            TargetPath = targetPath,
            ClassifiedAt = !string.IsNullOrEmpty(source.FileCategory) ? source.LastUpdatedDate : null,
            LastError = source.IsDeleted ? "File marked as deleted in source system" : null,
            LastErrorAt = source.IsDeleted ? source.LastUpdatedDate : null,
            CreatedDate = source.CreatedDate,
            LastUpdateDate = source.LastUpdatedDate, // Migrate lastupdate date in lastupdated
            Note = source.Note,
            IsActive = source.IsActive && !source.IsDeleted,
            MovedToPath = source.Path // Migrate FileCat Path to MovedToPath
        };
    }

    private int DetermineStatus(FileCatRecord source)
    {
        // MediaButler Status values:
        // 0 = New, 1 = Processing, 2 = Classified, 3 = ReadyToMove, 4 = Moving, 5 = Moved, 6 = Error, 7 = Retry, 8 = Ignored

        // migrate records with IsNotToMove = 1 in status = 8 (Ignored)
        if (source.IsNotToMove) return 8; // Ignored

        // migrate records with isToCategorize = 0 with in Status = 2 (Classified)
        if (!source.IsToCategorize && !string.IsNullOrEmpty(source.FileCategory)) return 2; // Classified

        // migrate all other records in status = 5 (Moved)
        return 5; // Moved
    }

    /// <summary>
    /// Refactor filename using the method used when a new file is found in watch folder.
    /// This normalizes the filename for consistent processing.
    /// </summary>
    private string RefactorFileName(string originalName)
    {
        if (string.IsNullOrWhiteSpace(originalName))
            return string.Empty;

        var fileName = originalName.Trim();

        // Remove any path separators that might be in the name
        fileName = Path.GetFileName(fileName);

        // Normalize separators (dots, underscores to spaces)
        fileName = fileName.Replace('.', ' ').Replace('_', ' ');

        // Remove multiple spaces
        while (fileName.Contains("  "))
            fileName = fileName.Replace("  ", " ");

        // Remove common file prefixes/suffixes that don't add value
        var prefixesToRemove = new[] { "[", "]", "(", ")", "{", "}" };
        foreach (var prefix in prefixesToRemove)
        {
            fileName = fileName.Replace(prefix, " ");
        }

        // Normalize case - keep original extension case
        var extension = Path.GetExtension(originalName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // Clean up the name part but preserve the original extension
        nameWithoutExt = nameWithoutExt.Trim();

        // Reconstruct with cleaned name and original extension
        fileName = string.IsNullOrEmpty(extension) ? nameWithoutExt : $"{nameWithoutExt}{extension}";

        return fileName.Trim();
    }

    private string GenerateHash(string input)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLower();
    }
}

public class FileCatRecord
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Path { get; set; }
    public double FileSize { get; set; }
    public string LastUpdateFile { get; set; } = "";
    public string? FileCategory { get; set; }
    public bool IsToCategorize { get; set; }
    public bool IsNew { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsNotToMove { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastUpdatedDate { get; set; }
    public bool IsActive { get; set; }
    public string? Note { get; set; }
}

public class TrackedFileRecord
{
    public string Hash { get; set; } = "";
    public string FileName { get; set; } = "";
    public string OriginalPath { get; set; } = "";
    public long FileSize { get; set; }
    public int Status { get; set; }
    public string? SuggestedCategory { get; set; }
    public decimal Confidence { get; set; }
    public string? Category { get; set; }
    public string? TargetPath { get; set; }
    public DateTime? ClassifiedAt { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastUpdateDate { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; }
    public string? MovedToPath { get; set; }
}