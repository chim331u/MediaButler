using System;
using System.Collections.Generic;
using System.Data.SQLite;
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
        var sourcePath = "../../temp/Import/FileCat.db";
        var targetPath = "../../temp/mediabutler.dev.db";
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

        using var connection = new SQLiteConnection($"Data Source={_sourceDatabasePath};Version=3;Read Only=True;");
        await connection.OpenAsync();

        var query = @"
            SELECT Id, Name, Path, FileSize, LastUpdateFile, FileCategory,
                   IsToCategorize, IsNew, IsDeleted, IsNotToMove,
                   CreatedDate, LastUpdatedDate, IsActive, Note
            FROM FilesDetail
            WHERE Name IS NOT NULL AND Name != ''
            ORDER BY Id";

        using var command = new SQLiteCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            records.Add(new FileCatRecord
            {
                Id = reader.GetInt32("Id"),
                Name = reader.IsDBNull("Name") ? null : reader.GetString("Name"),
                Path = reader.IsDBNull("Path") ? null : reader.GetString("Path"),
                FileSize = reader.GetDouble("FileSize"),
                LastUpdateFile = reader.GetString("LastUpdateFile"),
                FileCategory = reader.IsDBNull("FileCategory") ? null : reader.GetString("FileCategory"),
                IsToCategorize = reader.GetBoolean("IsToCategorize"),
                IsNew = reader.GetBoolean("IsNew"),
                IsDeleted = reader.GetBoolean("IsDeleted"),
                IsNotToMove = reader.GetBoolean("IsNotToMove"),
                CreatedDate = DateTime.Parse(reader.GetString("CreatedDate")),
                LastUpdatedDate = DateTime.Parse(reader.GetString("LastUpdatedDate")),
                IsActive = reader.GetBoolean("IsActive"),
                Note = reader.IsDBNull("Note") ? null : reader.GetString("Note")
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
        using var connection = new SQLiteConnection($"Data Source={_targetDatabasePath};Version=3;");
        await connection.OpenAsync();

        using var command = new SQLiteCommand("SELECT COUNT(*) FROM TrackedFiles", connection);
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
                Console.WriteLine($"Would migrate: {trackedFile.FileName} -> Status: {trackedFile.Status}, Category: {trackedFile.Category ?? "None"}");
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

        using var connection = new SQLiteConnection($"Data Source={_targetDatabasePath};Version=3;");
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            var insertCommand = new SQLiteCommand(@"
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
                    insertCommand.Parameters.AddWithValue("@TargetPath", DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@ClassifiedAt", (object)trackedFile.ClassifiedAt ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@MovedAt", DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@LastError", (object)trackedFile.LastError ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@LastErrorAt", (object)trackedFile.LastErrorAt ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@RetryCount", 0);
                    insertCommand.Parameters.AddWithValue("@CreatedDate", trackedFile.CreatedDate);
                    insertCommand.Parameters.AddWithValue("@LastUpdateDate", trackedFile.LastUpdateDate);
                    insertCommand.Parameters.AddWithValue("@Note", (object)trackedFile.Note ?? DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@IsActive", trackedFile.IsActive);
                    insertCommand.Parameters.AddWithValue("@MovedToPath", DBNull.Value);

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

        // Determine status based on flags
        var status = DetermineStatus(source);

        return new TrackedFileRecord
        {
            Hash = hash,
            FileName = source.Name ?? "",
            OriginalPath = source.Path ?? "",
            FileSize = (long)source.FileSize,
            Status = status,
            SuggestedCategory = !string.IsNullOrEmpty(source.FileCategory) ? source.FileCategory : null,
            Confidence = !string.IsNullOrEmpty(source.FileCategory) ? 0.95m : 0.0m,
            Category = !string.IsNullOrEmpty(source.FileCategory) && !source.IsNotToMove ? source.FileCategory : null,
            ClassifiedAt = !string.IsNullOrEmpty(source.FileCategory) ? source.LastUpdatedDate : null,
            LastError = source.IsDeleted ? "File marked as deleted in source system" : null,
            LastErrorAt = source.IsDeleted ? source.LastUpdatedDate : null,
            CreatedDate = source.CreatedDate,
            LastUpdateDate = source.LastUpdatedDate,
            Note = source.Note,
            IsActive = source.IsActive && !source.IsDeleted
        };
    }

    private int DetermineStatus(FileCatRecord source)
    {
        // MediaButler Status values:
        // 0 = New, 1 = Discovered, 2 = Classified, 3 = ConfirmedCategory, 4 = Queued, 5 = Moved, 6 = Error, 7 = Failed

        if (source.IsDeleted) return 6; // Error
        if (source.IsNotToMove) return 3; // ConfirmedCategory (but not to move)
        if (!string.IsNullOrEmpty(source.FileCategory)) return 3; // ConfirmedCategory
        if (source.IsToCategorize) return 1; // Discovered
        return 1; // Default: Discovered
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
    public DateTime? ClassifiedAt { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastUpdateDate { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; }
}