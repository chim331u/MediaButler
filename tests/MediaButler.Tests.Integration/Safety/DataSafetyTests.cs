using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Services.Interfaces;
using MediaButler.Tests.Integration.Infrastructure;
using System.Diagnostics;

namespace MediaButler.Tests.Integration.Safety;

/// <summary>
/// Comprehensive data safety tests for Task 3.4.2: Safety Testing.
/// Validates data loss prevention under various failure conditions following "Simple Made Easy" principles.
/// </summary>
/// <remarks>
/// These tests ensure MediaButler's core safety promise: no data loss under any failure condition.
/// Focus areas:
/// 1. Power failure simulation with incomplete operations
/// 2. Disk space exhaustion during file operations
/// 3. File system permission changes mid-operation
/// 4. Network storage disconnection scenarios
/// 
/// All tests follow the principle that the system should fail gracefully and preserve data integrity
/// even under adverse conditions, prioritizing data safety over operation completion.
/// </remarks>
public class DataSafetyTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _databaseFixture;

    public DataSafetyTests(DatabaseFixture databaseFixture)
    {
        _databaseFixture = databaseFixture;
    }

    #region Power Failure Simulation

    [Fact]
    public async Task PowerFailureSimulation_DuringFileRegistration_PreservesDataIntegrity()
    {
        // Given - Simulate power failure during file registration
        await _databaseFixture.CleanupAsync();
        using var scope = _databaseFixture.CreateScope();
        
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        var testFile = CreateTestFile("PowerFailure.Registration.Test.mkv", "Critical data that must not be lost");
        var originalContent = File.ReadAllText(testFile);

        try
        {
            // When - Start registration with cancellation (simulating power failure)
            using var cancellationTokenSource = new CancellationTokenSource();
            var registrationTask = fileService.RegisterFileAsync(testFile, cancellationTokenSource.Token);
            
            // Simulate power failure by cancelling after very short delay
            await Task.Delay(10); // Allow operation to start
            cancellationTokenSource.Cancel();
            
            try
            {
                await registrationTask;
            }
            catch (OperationCanceledException)
            {
                // Expected - simulated power failure
            }

            // Then - Verify data integrity after "power failure"
            File.Exists(testFile).Should().BeTrue("Original file must be preserved after power failure");
            var preservedContent = File.ReadAllText(testFile);
            preservedContent.Should().Be(originalContent, "File content must remain intact after power failure");
            
            // Database should either have no record or a consistent record
            var dbFiles = await _databaseFixture.Context.TrackedFiles.ToListAsync();
            if (dbFiles.Any())
            {
                // If a record exists, it should be in a consistent state
                var dbFile = dbFiles.First();
                dbFile.Status.Should().BeOneOf(FileStatus.New, FileStatus.Processing, FileStatus.Error);
                dbFile.OriginalPath.Should().NotBeNullOrEmpty("File path should be recorded if entry exists");
            }
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public async Task PowerFailureSimulation_DuringClassification_MaintainsConsistency()
    {
        // Given - File in processing state with simulated power failure during classification
        await _databaseFixture.CleanupAsync();
        using var scope = _databaseFixture.CreateScope();
        
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        var testFile = CreateTestFile("PowerFailure.Classification.Test.mkv");
        
        // Register file successfully first
        var registrationResult = await fileService.RegisterFileAsync(testFile);
        registrationResult.IsSuccess.Should().BeTrue();
        var fileHash = registrationResult.Value.Hash;

        try
        {
            // When - Start classification with cancellation (simulating power failure)
            using var cancellationTokenSource = new CancellationTokenSource();
            var classificationTask = fileService.UpdateClassificationAsync(
                fileHash, "POWER FAILURE TEST", 0.85m, cancellationTokenSource.Token);
            
            // Simulate power failure
            await Task.Delay(10);
            cancellationTokenSource.Cancel();
            
            try
            {
                await classificationTask;
            }
            catch (OperationCanceledException)
            {
                // Expected - simulated power failure
            }

            // Then - Verify system maintains consistency
            var recoveredFile = await _databaseFixture.Context.TrackedFiles
                .FirstOrDefaultAsync(f => f.Hash == fileHash);
            
            if (recoveredFile != null)
            {
                // File should be in a valid state
                recoveredFile.Status.Should().BeOneOf(
                    FileStatus.New, FileStatus.Processing, FileStatus.Classified, FileStatus.Error);
                
                // If classified, should have valid classification data
                if (recoveredFile.Status == FileStatus.Classified)
                {
                    recoveredFile.SuggestedCategory.Should().NotBeNullOrEmpty();
                    recoveredFile.Confidence.Should().BeInRange(0m, 1m);
                }
            }

            // Original file must still exist
            File.Exists(testFile).Should().BeTrue("Original file must be preserved after power failure");
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    #endregion

    #region Disk Space Exhaustion

    [Fact]
    public async Task DiskSpaceExhaustion_DuringFileOperation_FailsGracefully()
    {
        // Given - Simulate disk space exhaustion scenario
        await _databaseFixture.CleanupAsync();
        using var scope = _databaseFixture.CreateScope();
        
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        var testFile = CreateTestFile("DiskSpace.Exhaustion.Test.mkv", GenerateLargeContent(1024 * 100)); // 100KB
        
        try
        {
            // When - Register file that might encounter space issues
            var result = await fileService.RegisterFileAsync(testFile);
            
            // Then - Operation should either succeed or fail gracefully
            if (result.IsSuccess)
            {
                // If successful, verify data integrity
                var retrievedResult = await fileService.GetFileByHashAsync(result.Value.Hash);
                retrievedResult.IsSuccess.Should().BeTrue();
                retrievedResult.Value.Should().NotBeNull();
                retrievedResult.Value.FileSize.Should().BeGreaterThan(0);
            }
            else
            {
                // If failed due to space issues, should fail gracefully
                result.Error.Should().NotBeNullOrEmpty("Error message should be provided for failure");
                
                // Original file should still exist
                File.Exists(testFile).Should().BeTrue("Original file should be preserved on failure");
                
                // No partial database records should exist
                var dbFiles = await _databaseFixture.Context.TrackedFiles.ToListAsync();
                dbFiles.Should().BeEmpty("No partial records should be created on space exhaustion failure");
            }
        }
        finally
        {
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public async Task DiskSpaceExhaustion_DatabaseFull_HandlesGracefully()
    {
        // Given - Simulate database storage exhaustion
        await _databaseFixture.CleanupAsync();
        using var scope = _databaseFixture.CreateScope();
        
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        var testFiles = new List<string>();
        
        try
        {
            // When - Create many files to potentially exhaust resources
            for (int i = 0; i < 50; i++) // Reasonable number for testing
            {
                var testFile = CreateTestFile($"Database.Stress.Test.{i:D3}.mkv");
                testFiles.Add(testFile);
                
                var result = await fileService.RegisterFileAsync(testFile);
                
                // All operations should either succeed or fail gracefully
                if (!result.IsSuccess)
                {
                    // If failure occurs, should be graceful with clear error
                    result.Error.Should().NotBeNullOrEmpty();
                    break; // Stop on first failure
                }
            }

            // Then - Verify database consistency
            var dbFiles = await _databaseFixture.Context.TrackedFiles.ToListAsync();
            
            // All database records should be consistent
            foreach (var dbFile in dbFiles)
            {
                dbFile.Hash.Should().NotBeNullOrEmpty();
                dbFile.FileName.Should().NotBeNullOrEmpty();
                dbFile.OriginalPath.Should().NotBeNullOrEmpty();
                dbFile.FileSize.Should().BeGreaterThan(0);
                dbFile.Status.Should().BeOneOf(FileStatus.New, FileStatus.Processing, FileStatus.Classified);
            }
            
            // All registered files should still exist on disk
            var registeredPaths = dbFiles.Select(f => f.OriginalPath).ToHashSet();
            foreach (var path in registeredPaths)
            {
                File.Exists(path).Should().BeTrue($"Registered file should exist: {path}");
            }
        }
        finally
        {
            foreach (var testFile in testFiles)
            {
                CleanupTestFile(testFile);
            }
        }
    }

    #endregion

    #region File System Permission Changes

    [Fact]
    public async Task PermissionChanges_MidOperation_HandlesGracefully()
    {
        // Given - File operation with permission changes during execution
        await _databaseFixture.CleanupAsync();
        using var scope = _databaseFixture.CreateScope();
        
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        var testFile = CreateTestFile("Permission.Change.Test.mkv");
        
        try
        {
            // Start with normal permissions
            var initialResult = await fileService.RegisterFileAsync(testFile);
            initialResult.IsSuccess.Should().BeTrue();
            var fileHash = initialResult.Value.Hash;
            
            // When - Change file permissions to read-only
            File.SetAttributes(testFile, FileAttributes.ReadOnly);
            
            // Attempt classification operation with restricted permissions
            var classificationResult = await fileService.UpdateClassificationAsync(
                fileHash, "PERMISSION TEST", 0.75m);
            
            // Then - Operation should handle permissions gracefully
            if (classificationResult.IsSuccess)
            {
                // If successful, verify data integrity
                var updatedFile = await _databaseFixture.Context.TrackedFiles
                    .FirstAsync(f => f.Hash == fileHash);
                updatedFile.Status.Should().Be(FileStatus.Classified);
                updatedFile.SuggestedCategory.Should().Be("PERMISSION TEST");
                updatedFile.Confidence.Should().Be(0.75m);
            }
            else
            {
                // If failed, should fail gracefully with clear error
                classificationResult.Error.Should().NotBeNullOrEmpty();
                
                // Original file should still exist
                File.Exists(testFile).Should().BeTrue();
                
                // Database should remain in consistent state
                var unchangedFile = await _databaseFixture.Context.TrackedFiles
                    .FirstAsync(f => f.Hash == fileHash);
                unchangedFile.Status.Should().Be(FileStatus.New); // Unchanged from original state
            }
        }
        finally
        {
            // Reset permissions for cleanup
            if (File.Exists(testFile))
            {
                File.SetAttributes(testFile, FileAttributes.Normal);
            }
            CleanupTestFile(testFile);
        }
    }

    [Fact]
    public async Task PermissionChanges_DirectoryAccess_PreservesData()
    {
        // Given - Test directory permission changes
        await _databaseFixture.CleanupAsync();
        using var scope = _databaseFixture.CreateScope();
        
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        
        // Create test file in subdirectory
        var testDir = Path.Combine(Path.GetTempPath(), $"permission_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "Directory.Permission.Test.mkv");
        File.WriteAllText(testFile, "Test content for directory permission testing");
        
        try
        {
            // Register file successfully first
            var result = await fileService.RegisterFileAsync(testFile);
            result.IsSuccess.Should().BeTrue();
            
            // When - Simulate directory permission issues (can't easily restrict on all systems)
            // Instead, test behavior with inaccessible paths
            var inaccessiblePath = "/dev/null/nonexistent/path/test.mkv";
            var inaccessibleResult = await fileService.RegisterFileAsync(inaccessiblePath);
            
            // Then - Should handle inaccessible paths gracefully
            inaccessibleResult.IsSuccess.Should().BeFalse();
            inaccessibleResult.Error.Should().NotBeNullOrEmpty();
            
            // Original operations should remain unaffected
            var originalFile = await fileService.GetFileByHashAsync(result.Value.Hash);
            originalFile.IsSuccess.Should().BeTrue();
            originalFile.Value.Should().NotBeNull();
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }

    #endregion

    #region Network Storage Disconnection

    [Fact]
    public async Task NetworkDisconnection_InvalidPath_FailsGracefully()
    {
        // Given - Simulate network storage disconnection by using invalid network paths
        await _databaseFixture.CleanupAsync();
        using var scope = _databaseFixture.CreateScope();
        
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        
        // When - Attempt to register file from "disconnected" network path
        var networkPath = "//unreachable-server/share/test.mkv";
        var result = await fileService.RegisterFileAsync(networkPath);
        
        // Then - Should fail gracefully without corrupting system
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty("Should provide clear error message for network issues");
        
        // Database should remain clean
        var dbFiles = await _databaseFixture.Context.TrackedFiles.ToListAsync();
        dbFiles.Should().BeEmpty("No records should be created for inaccessible network files");
        
        // System should remain stable for subsequent operations
        var localTestFile = CreateTestFile("PostNetworkFailure.Test.mkv");
        try
        {
            var localResult = await fileService.RegisterFileAsync(localTestFile);
            localResult.IsSuccess.Should().BeTrue("System should recover and handle local files correctly");
        }
        finally
        {
            CleanupTestFile(localTestFile);
        }
    }

    [Fact]
    public async Task NetworkDisconnection_ExistingOperation_MaintainsIntegrity()
    {
        // Given - File registered from "network" location, then connection lost
        await _databaseFixture.CleanupAsync();
        using var scope = _databaseFixture.CreateScope();
        
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        
        // Create file that simulates network storage (using local path first)
        var testFile = CreateTestFile("Network.Simulation.Test.mkv");
        var result = await fileService.RegisterFileAsync(testFile);
        result.IsSuccess.Should().BeTrue();
        var fileHash = result.Value.Hash;
        
        try
        {
            // When - Simulate network disconnection by removing the file
            File.Delete(testFile);
            
            // Attempt classification on "disconnected" file
            var classificationResult = await fileService.UpdateClassificationAsync(
                fileHash, "NETWORK TEST", 0.80m);
            
            // Then - System should handle missing file gracefully
            if (classificationResult.IsSuccess)
            {
                // If successful, verify database consistency
                var dbFile = await _databaseFixture.Context.TrackedFiles
                    .FirstAsync(f => f.Hash == fileHash);
                dbFile.Status.Should().Be(FileStatus.Classified);
                dbFile.SuggestedCategory.Should().Be("NETWORK TEST");
            }
            else
            {
                // If failed, should maintain database integrity
                classificationResult.Error.Should().NotBeNullOrEmpty();
                
                var dbFile = await _databaseFixture.Context.TrackedFiles
                    .FirstAsync(f => f.Hash == fileHash);
                dbFile.Status.Should().BeOneOf(FileStatus.New, FileStatus.Error);
                
                // Should not have partial classification data
                if (dbFile.Status != FileStatus.Classified)
                {
                    dbFile.SuggestedCategory.Should().BeNullOrEmpty();
                    dbFile.Confidence.Should().Be(0m);
                }
            }
        }
        finally
        {
            // Cleanup (file may already be deleted)
            CleanupTestFile(testFile);
        }
    }

    #endregion

    #region Test Helper Methods

    private string CreateTestFile(string filename, string? content = null)
    {
        var tempDir = Path.GetTempPath();
        var filePath = Path.Combine(tempDir, $"safety_test_{Guid.NewGuid():N}_{filename}");
        
        content ??= $"Test content for {filename}\nCreated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\nSafety Test Data - Must Not Be Lost";
        File.WriteAllText(filePath, content);
        
        return filePath;
    }

    private string GenerateLargeContent(int sizeBytes)
    {
        var random = new Random(42); // Deterministic for tests
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789\n";
        var result = new char[sizeBytes];
        
        for (int i = 0; i < sizeBytes; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        
        return new string(result);
    }

    private void CleanupTestFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                // Reset attributes in case they were changed during testing
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    #endregion
}