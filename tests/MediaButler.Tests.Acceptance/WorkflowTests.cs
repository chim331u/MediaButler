using System.Net;
using System.Text.Json;
using FluentAssertions;
using MediaButler.Core.Enums;
using MediaButler.Tests.Acceptance.Infrastructure;

namespace MediaButler.Tests.Acceptance;

/// <summary>
/// Acceptance tests for end-to-end workflow scenarios in MediaButler.
/// Tests complete file processing workflows, error handling, and performance characteristics.
/// Validates the system's behavior under various load and error conditions.
/// </summary>
public class WorkflowTests : ApiTestBase
{
    public WorkflowTests(MediaButlerWebApplicationFactory factory) : base(factory)
    {
    }

    #region End-to-End File Processing Workflow

    [Fact]
    public async Task CompleteFileProcessingWorkflow_WithDatabaseSeededFile_ShouldProcessSuccessfully()
    {
        // Arrange - Seed a file directly in the database (simulating file discovery)
        var testFile = await SeedTrackedFileAsync("Complete.Workflow.Test.2023.1080p.mkv");

        // Act & Assert - Step 1: Verify file exists and is in New status
        var getResponse = await Client.GetAsync($"/api/files/{testFile.Hash}");
        getResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var getContent = await getResponse.Content.ReadAsStringAsync();
        var retrievedFile = JsonSerializer.Deserialize<JsonElement>(getContent, JsonOptions);
        retrievedFile.GetProperty("status").GetInt32().Should().Be(0); // 0 = New

        // Act & Assert - Step 2: Simulate classification by updating file status
        await Factory.SeedDatabaseAsync(context =>
        {
            var existingFile = context.TrackedFiles.FirstOrDefault(f => f.Hash == testFile.Hash);
            if (existingFile != null)
            {
                existingFile.Status = FileStatus.Classified;
                existingFile.SuggestedCategory = "TEST MOVIES";
                existingFile.Confidence = 0.85m;
            }
        });

        // Act & Assert - Step 3: Confirm category
        var confirmRequest = new { category = "CONFIRMED MOVIES" };
        var confirmResponse = await PostJsonAsync($"/api/files/{testFile.Hash}/confirm", confirmRequest);
        confirmResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var confirmContent = await confirmResponse.Content.ReadAsStringAsync();
        var confirmedFile = JsonSerializer.Deserialize<JsonElement>(confirmContent, JsonOptions);
        confirmedFile.GetProperty("status").GetInt32().Should().Be(3); // 3 = ReadyToMove
        confirmedFile.GetProperty("category").GetString().Should().Be("CONFIRMED MOVIES");

        // Act & Assert - Step 4: Mark as moved
        var moveRequest = new { targetPath = "/library/CONFIRMED_MOVIES/Complete.Workflow.Test.2023.1080p.mkv" };
        var moveResponse = await PostJsonAsync($"/api/files/{testFile.Hash}/moved", moveRequest);
        moveResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var moveContent = await moveResponse.Content.ReadAsStringAsync();
        var movedFile = JsonSerializer.Deserialize<JsonElement>(moveContent, JsonOptions);
        movedFile.GetProperty("status").GetInt32().Should().Be(5); // 5 = Moved
        movedFile.GetProperty("targetPath").GetString().Should().Be("/library/CONFIRMED_MOVIES/Complete.Workflow.Test.2023.1080p.mkv");

        // Act & Assert - Step 5: Verify final state
        var finalResponse = await Client.GetAsync($"/api/files/{testFile.Hash}");
        finalResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var finalContent = await finalResponse.Content.ReadAsStringAsync();
        var finalFile = JsonSerializer.Deserialize<JsonElement>(finalContent, JsonOptions);
        finalFile.GetProperty("status").GetInt32().Should().Be(5); // 5 = Moved
        finalFile.GetProperty("category").GetString().Should().Be("CONFIRMED MOVIES");
    }

    [Fact]
    public async Task FileWorkflow_WithCategoryCorrection_ShouldAllowRecategorization()
    {
        // Arrange - Create a file and set it to Classified status
        var testFile = await SeedTrackedFileAsync("Recategorization.Test.S01E01.mkv");

        await Factory.SeedDatabaseAsync(context =>
        {
            var existingFile = context.TrackedFiles.FirstOrDefault(f => f.Hash == testFile.Hash);
            if (existingFile != null)
            {
                existingFile.Status = FileStatus.Classified;
                existingFile.SuggestedCategory = "WRONG CATEGORY";
                existingFile.Confidence = 0.65m;
            }
        });

        // Act & Assert - User corrects the category
        var correctRequest = new { category = "CORRECT SERIES" };
        var correctResponse = await PostJsonAsync($"/api/files/{testFile.Hash}/confirm", correctRequest);
        correctResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var correctContent = await correctResponse.Content.ReadAsStringAsync();
        var correctedFile = JsonSerializer.Deserialize<JsonElement>(correctContent, JsonOptions);
        correctedFile.GetProperty("category").GetString().Should().Be("CORRECT SERIES");
        correctedFile.GetProperty("status").GetInt32().Should().Be(3); // 3 = ReadyToMove
    }

    [Fact]
    public async Task FileWorkflow_GetFilesReadyForClassification_ShouldReturnNewFiles()
    {
        // Arrange - Seed multiple files with New status
        var testFiles = await SeedMultipleFilesAsync(3);

        // Act - Get files ready for classification
        var readyResponse = await Client.GetAsync("/api/files/ready-for-classification?limit=10");
        readyResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var readyContent = await readyResponse.Content.ReadAsStringAsync();
        var readyFiles = JsonSerializer.Deserialize<JsonElement[]>(readyContent, JsonOptions);
        
        // Assert - Should contain our test files
        readyFiles.Should().HaveCountGreaterOrEqualTo(3);
        var readyHashes = readyFiles.Select(f => f.GetProperty("hash").GetString()).ToList();
        
        foreach (var testFile in testFiles)
        {
            readyHashes.Should().Contain(testFile.Hash);
        }
    }

    [Fact]
    public async Task FileWorkflow_GetPendingFiles_ShouldReturnClassifiedFiles()
    {
        // Arrange - Create a file in Classified status
        var testFile = await SeedTrackedFileAsync("Pending.Test.2023.mkv");

        await Factory.SeedDatabaseAsync(context =>
        {
            var existingFile = context.TrackedFiles.FirstOrDefault(f => f.Hash == testFile.Hash);
            if (existingFile != null)
            {
                existingFile.Status = FileStatus.Classified;
                existingFile.SuggestedCategory = "PENDING MOVIES";
                existingFile.Confidence = 0.75m;
            }
        });

        // Act - Get pending files
        var pendingResponse = await Client.GetAsync("/api/files/pending");
        pendingResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var pendingContent = await pendingResponse.Content.ReadAsStringAsync();
        var pendingFiles = JsonSerializer.Deserialize<JsonElement[]>(pendingContent, JsonOptions);
        
        // Assert - Should contain our classified file
        pendingFiles.Should().HaveCountGreaterOrEqualTo(1);
        pendingFiles.Should().Contain(f => f.GetProperty("hash").GetString() == testFile.Hash);
    }

    #endregion

    #region Error Handling Scenarios

    [Fact]
    public async Task FileWorkflow_WithNonExistentFile_ShouldReturnNotFound()
    {
        // Act - Try to get a file that doesn't exist
        var nonExistentHash = GenerateTestHash("nonexistent.file");
        var response = await Client.GetAsync($"/api/files/{nonExistentHash}");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FileWorkflow_WithInvalidHash_ShouldReturnBadRequest()
    {
        // Act - Try to get a file with invalid hash format
        var response = await Client.GetAsync("/api/files/invalid-hash-format");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Hash must be a valid 64-character SHA256 hash");
    }

    [Fact]
    public async Task FileWorkflow_ConfirmWithInvalidCategory_ShouldReturnBadRequest()
    {
        // Arrange - Create a classified file
        var testFile = await SeedTrackedFileAsync("InvalidCategory.Test.mkv");
        
        await Factory.SeedDatabaseAsync(context =>
        {
            var existingFile = context.TrackedFiles.FirstOrDefault(f => f.Hash == testFile.Hash);
            if (existingFile != null)
            {
                existingFile.Status = FileStatus.Classified;
                existingFile.SuggestedCategory = "TEST";
                existingFile.Confidence = 0.8m;
            }
        });

        // Act - Try to confirm with empty category
        var invalidRequest = new { category = "" };
        var response = await PostJsonAsync($"/api/files/{testFile.Hash}/confirm", invalidRequest);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FileWorkflow_DeleteFile_ShouldSoftDelete()
    {
        // Arrange - Create a file
        var testFile = await SeedTrackedFileAsync("Delete.Test.mkv");

        // Act - Delete the file
        var deleteResponse = await Client.DeleteAsync($"/api/files/{testFile.Hash}");
        deleteResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

        // Assert - File should no longer be accessible
        var getResponse = await Client.GetAsync($"/api/files/{testFile.Hash}");
        getResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    #endregion

    #region Pagination and Filtering Validation

    [Fact]
    public async Task FilesPagination_WithLargeDataset_ShouldHandleCorrectly()
    {
        // Arrange - Create multiple files
        var testFiles = await SeedMultipleFilesAsync(10);

        // Act & Assert - Test pagination
        var page1Response = await Client.GetAsync("/api/files?skip=0&take=5");
        page1Response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var page1Content = await page1Response.Content.ReadAsStringAsync();
        var page1Files = JsonSerializer.Deserialize<JsonElement[]>(page1Content, JsonOptions);
        page1Files.Should().HaveCount(5);

        var page2Response = await Client.GetAsync("/api/files?skip=5&take=5");
        page2Response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var page2Content = await page2Response.Content.ReadAsStringAsync();
        var page2Files = JsonSerializer.Deserialize<JsonElement[]>(page2Content, JsonOptions);
        page2Files.Should().HaveCountGreaterOrEqualTo(5);

        // Verify no overlap between pages
        var page1Hashes = page1Files.Select(f => f.GetProperty("hash").GetString()).ToList();
        var page2Hashes = page2Files.Select(f => f.GetProperty("hash").GetString()).ToList();
        page1Hashes.Should().NotIntersectWith(page2Hashes);
    }

    [Fact]
    public async Task FilesFiltering_ByStatus_ShouldReturnCorrectResults()
    {
        // Arrange - Create files with different statuses
        var newFile = await SeedTrackedFileAsync("NewStatusFilter.mkv");
        var classifiedFile = await SeedTrackedFileAsync("ClassifiedStatusFilter.mkv");

        // Update one file to Classified status
        await Factory.SeedDatabaseAsync(context =>
        {
            var file = context.TrackedFiles.FirstOrDefault(f => f.Hash == classifiedFile.Hash);
            if (file != null)
            {
                file.Status = FileStatus.Classified;
                file.SuggestedCategory = "TEST CATEGORY";
                file.Confidence = 0.8m;
            }
        });

        // Act & Assert - Filter by New status
        var newFilesResponse = await Client.GetAsync("/api/files?status=New");
        newFilesResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var newFilesContent = await newFilesResponse.Content.ReadAsStringAsync();
        var newFiles = JsonSerializer.Deserialize<JsonElement[]>(newFilesContent, JsonOptions);
        newFiles.Should().Contain(f => f.GetProperty("hash").GetString() == newFile.Hash);

        // Act & Assert - Filter by Classified status  
        var classifiedFilesResponse = await Client.GetAsync("/api/files?status=Classified");
        classifiedFilesResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var classifiedFilesContent = await classifiedFilesResponse.Content.ReadAsStringAsync();
        var classifiedFiles = JsonSerializer.Deserialize<JsonElement[]>(classifiedFilesContent, JsonOptions);
        classifiedFiles.Should().Contain(f => f.GetProperty("hash").GetString() == classifiedFile.Hash);
    }

    [Fact]
    public async Task FilesPagination_WithInvalidParameters_ShouldReturnBadRequest()
    {
        // Act & Assert - Test invalid pagination parameters
        var invalidSkipResponse = await Client.GetAsync("/api/files?skip=-1&take=10");
        invalidSkipResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        
        var invalidTakeResponse = await Client.GetAsync("/api/files?skip=0&take=0");
        invalidTakeResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        
        var tooLargeTakeResponse = await Client.GetAsync("/api/files?skip=0&take=101");
        tooLargeTakeResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Performance Under Load Testing

    [Fact]
    public async Task ConcurrentApiRequests_ShouldHandleMultipleRequests()
    {
        // Arrange - Create multiple concurrent requests to lightweight endpoints
        var tasks = new List<Task<HttpResponseMessage>>();
        
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Client.GetAsync("/api/health"));
            tasks.Add(Client.GetAsync("/api/stats/performance"));
        }

        // Act - Execute all requests concurrently
        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should succeed
        foreach (var response in responses)
        {
            response.Should().HaveStatusCode(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task HighVolumeRequests_ShouldMaintainPerformance()
    {
        // Arrange & Act - Make multiple rapid requests to performance endpoint
        var tasks = new List<Task<HttpResponseMessage>>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Client.GetAsync("/api/stats/performance"));
        }

        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - All requests should succeed within reasonable time
        foreach (var response in responses)
        {
            response.Should().HaveStatusCode(HttpStatusCode.OK);
        }

        // Performance assertion - should complete within reasonable time
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // 5 seconds for 10 requests
        
        // Verify response times are reasonable
        var averageTimePerRequest = stopwatch.ElapsedMilliseconds / 10.0;
        averageTimePerRequest.Should().BeLessThan(500); // Average < 500ms per request
    }

    [Fact]
    public async Task SystemHealth_UnderLoad_ShouldRemainStable()
    {
        // Arrange - Create some load with mixed operations
        var tasks = new List<Task<HttpResponseMessage>>();
        
        // Mix of different endpoint calls
        tasks.Add(Client.GetAsync("/api/health"));
        tasks.Add(Client.GetAsync("/api/files"));
        tasks.Add(Client.GetAsync("/api/stats/performance"));
        tasks.Add(Client.GetAsync("/api/config/export"));
        
        // Act - Execute concurrent requests
        var responses = await Task.WhenAll(tasks);

        // Assert - System should handle mixed load gracefully
        foreach (var response in responses)
        {
            response.Should().HaveStatusCode(HttpStatusCode.OK);
        }

        // Verify system health endpoint still responds correctly after load
        var healthResponse = await Client.GetAsync("/api/health");
        healthResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var healthContent = await healthResponse.Content.ReadAsStringAsync();
        var health = JsonSerializer.Deserialize<JsonElement>(healthContent, JsonOptions);
        health.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task MultipleFileOperations_ShouldMaintainDataIntegrity()
    {
        // Arrange - Create multiple files and perform different operations
        var files = await SeedMultipleFilesAsync(5);

        // Act - Perform mixed operations concurrently
        var tasks = new List<Task<HttpResponseMessage>>();
        
        tasks.Add(Client.GetAsync($"/api/files/{files[0].Hash}"));
        tasks.Add(Client.GetAsync($"/api/files/{files[1].Hash}"));
        tasks.Add(Client.GetAsync("/api/files/ready-for-classification?limit=5"));
        tasks.Add(Client.GetAsync("/api/files?status=New&take=10"));
        
        var responses = await Task.WhenAll(tasks);

        // Assert - All operations should complete successfully
        foreach (var response in responses)
        {
            response.Should().HaveStatusCode(HttpStatusCode.OK);
        }

        // Verify data integrity - all files should still be accessible
        foreach (var file in files)
        {
            var getResponse = await Client.GetAsync($"/api/files/{file.Hash}");
            getResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        }
    }

    #endregion
}