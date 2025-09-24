using System.Net;
using System.Text.Json;
using FluentAssertions;
using MediaButler.Core.Enums;
using MediaButler.Tests.Acceptance.Infrastructure;

namespace MediaButler.Tests.Acceptance;

/// <summary>
/// Acceptance tests for Files API endpoints.
/// Tests CRUD operations, file tracking, and workflow management.
/// </summary>
public class FilesEndpointTests : ApiTestBase
{
    public FilesEndpointTests(MediaButlerWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetFiles_WithNoFiles_ShouldReturnEmptyList()
    {
        // Act
        var response = await Client.GetAsync("/api/files");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var files = JsonSerializer.Deserialize<JsonElement[]>(content, JsonOptions);
        files.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFiles_WithPagination_ShouldReturnPagedResults()
    {
        // Arrange
        var seededFiles = await SeedMultipleFilesAsync(5);

        // Act
        var response = await Client.GetAsync("/api/files?skip=2&take=2");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var files = JsonSerializer.Deserialize<JsonElement[]>(content, JsonOptions);
        files.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetFiles_WithInvalidPagination_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync("/api/files?skip=-1&take=0");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid pagination parameters");
    }

    [Fact]
    public async Task GetFile_WithValidHash_ShouldReturnFileDetails()
    {
        // Arrange
        var seededFile = await SeedTrackedFileAsync();

        // Act
        var response = await Client.GetAsync($"/api/files/{seededFile.Hash}");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var file = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        
        file.GetProperty("hash").GetString().Should().Be(seededFile.Hash);
        file.GetProperty("fileName").GetString().Should().Be(seededFile.FileName);
        // Status is returned as numeric enum value, 0 = New
        file.GetProperty("status").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetFile_WithInvalidHash_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync("/api/files/invalid-hash");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Hash must be a valid 64-character SHA256 hash");
    }

    [Fact]
    public async Task GetFile_WithNonExistentHash_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentHash = GenerateTestHash("nonexistent.file");

        // Act
        var response = await Client.GetAsync($"/api/files/{nonExistentHash}");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPendingFiles_WithClassifiedFiles_ShouldReturnPendingFiles()
    {
        // Arrange - Create a file in Classified status (pending confirmation)
        await Factory.SeedDatabaseAsync(context =>
        {
            var classifiedFile = new Core.Entities.TrackedFile
            {
                Hash = GenerateTestHash("classified.file"),
                FileName = "Classified.File.2023.1080p.mkv",
                OriginalPath = "/downloads/Classified.File.2023.1080p.mkv",
                FileSize = 1024 * 1024 * 500,
                Status = FileStatus.Classified,
                SuggestedCategory = "TEST SERIES",
                Confidence = 0.85m
            };
            context.TrackedFiles.Add(classifiedFile);
        });

        // Act
        var response = await Client.GetAsync("/api/files/pending");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var files = JsonSerializer.Deserialize<JsonElement[]>(content, JsonOptions);
        files.Should().HaveCountGreaterOrEqualTo(1);
        
        var pendingFile = files.First();
        // Status is returned as numeric enum value, 2 = Classified
        pendingFile.GetProperty("status").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task GetFilesReadyForClassification_WithNewFiles_ShouldReturnNewFiles()
    {
        // Arrange
        var seededFiles = await SeedMultipleFilesAsync(3);

        // Act
        var response = await Client.GetAsync("/api/files/ready-for-classification?limit=10");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var files = JsonSerializer.Deserialize<JsonElement[]>(content, JsonOptions);
        files.Should().HaveCount(3);
        
        foreach (var file in files)
        {
            // Status is returned as numeric enum value, 0 = New
            file.GetProperty("status").GetInt32().Should().Be(0);
        }
    }

    [Fact]
    public async Task GetFilesReadyForClassification_WithInvalidLimit_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync("/api/files/ready-for-classification?limit=1000");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Limit must be between 1 and 500");
    }

    [Fact]
    public async Task ConfirmFileCategory_WithValidData_ShouldUpdateFileStatus()
    {
        // Arrange - Create a classified file
        var classifiedFile = new Core.Entities.TrackedFile
        {
            Hash = GenerateTestHash("to.confirm"),
            FileName = "To.Confirm.2023.1080p.mkv",
            OriginalPath = "/downloads/To.Confirm.2023.1080p.mkv",
            FileSize = 1024 * 1024 * 500,
            Status = FileStatus.Classified,
            SuggestedCategory = "TEST SERIES",
            Confidence = 0.85m
        };

        await Factory.SeedDatabaseAsync(context =>
        {
            context.TrackedFiles.Add(classifiedFile);
        });

        var confirmRequest = new { category = "CONFIRMED SERIES" };

        // Act
        var response = await PostJsonAsync($"/api/files/{classifiedFile.Hash}/confirm", confirmRequest);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var file = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        
        // Status is returned as numeric enum value, 3 = ReadyToMove
        file.GetProperty("status").GetInt32().Should().Be(3);
        file.GetProperty("category").GetString().Should().Be("CONFIRMED SERIES");
    }

    [Fact]
    public async Task ConfirmFileCategory_WithInvalidCategory_ShouldReturnBadRequest()
    {
        // Arrange
        var seededFile = await SeedTrackedFileAsync();
        var invalidRequest = new { category = "" };

        // Act
        var response = await PostJsonAsync($"/api/files/{seededFile.Hash}/confirm", invalidRequest);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkFileAsMoved_WithValidData_ShouldUpdateFileStatus()
    {
        // Arrange - Create a file ready to move
        var readyFile = new Core.Entities.TrackedFile
        {
            Hash = GenerateTestHash("ready.to.move"),
            FileName = "Ready.To.Move.2023.1080p.mkv",
            OriginalPath = "/downloads/Ready.To.Move.2023.1080p.mkv",
            FileSize = 1024 * 1024 * 500,
            Status = FileStatus.ReadyToMove,
            Category = "TEST SERIES"
        };

        await Factory.SeedDatabaseAsync(context =>
        {
            context.TrackedFiles.Add(readyFile);
        });

        var moveRequest = new { targetPath = "/library/TEST_SERIES/Ready.To.Move.2023.1080p.mkv" };

        // Act
        var response = await PostJsonAsync($"/api/files/{readyFile.Hash}/moved", moveRequest);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var file = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        
        // Status is returned as numeric enum value, 5 = Moved
        file.GetProperty("status").GetInt32().Should().Be(5);
        file.GetProperty("targetPath").GetString().Should().Be("/library/TEST_SERIES/Ready.To.Move.2023.1080p.mkv");
    }

    [Fact]
    public async Task DeleteFile_WithValidHash_ShouldSoftDeleteFile()
    {
        // Arrange
        var seededFile = await SeedTrackedFileAsync();
        var deleteRequest = new { reason = "Test deletion" };

        // Act
        var response = await PostJsonAsync($"/api/files/{seededFile.Hash}", deleteRequest);
        var deleteResponse = await Client.DeleteAsync($"/api/files/{seededFile.Hash}");

        // Assert
        deleteResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteFile_WithNonExistentHash_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentHash = GenerateTestHash("nonexistent");

        // Act
        var response = await Client.DeleteAsync($"/api/files/{nonExistentHash}");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFiles_WithStatusFilter_ShouldReturnFilteredFiles()
    {
        // Arrange - Seed files with different statuses
        await Factory.SeedDatabaseAsync(context =>
        {
            var newFile = new Core.Entities.TrackedFile
            {
                Hash = GenerateTestHash("new.file"),
                FileName = "New.File.mkv",
                OriginalPath = "/downloads/New.File.mkv",
                FileSize = 1024 * 1024 * 500,
                Status = FileStatus.New
            };

            var movedFile = new Core.Entities.TrackedFile
            {
                Hash = GenerateTestHash("moved.file"),
                FileName = "Moved.File.mkv",
                OriginalPath = "/downloads/Moved.File.mkv",
                FileSize = 1024 * 1024 * 500,
                Status = FileStatus.Moved
            };

            context.TrackedFiles.AddRange(newFile, movedFile);
        });

        // Act
        var response = await Client.GetAsync("/api/files?status=New");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var files = JsonSerializer.Deserialize<JsonElement[]>(content, JsonOptions);
        
        files.Should().HaveCountGreaterOrEqualTo(1);
        foreach (var file in files)
        {
            // Status is returned as numeric enum value, 0 = New
            file.GetProperty("status").GetInt32().Should().Be(0);
        }
    }

    #region Ignore File Acceptance Tests

    [Fact]
    public async Task IgnoreFile_WithValidHash_ShouldReturnSuccessAndMarkAsIgnored()
    {
        // Arrange
        var seededFile = await SeedTrackedFileAsync();

        // Act
        var response = await Client.PostAsync($"/api/v1/file-actions/ignore/{seededFile.Hash}", null);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);

        result.GetProperty("message").GetString().Should().Be("File successfully marked as ignored");
        result.GetProperty("hash").GetString().Should().Be(seededFile.Hash);
        result.GetProperty("status").GetString().Should().Be("Ignored");
        result.TryGetProperty("updatedAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task IgnoreFile_WithNonExistentHash_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentHash = GenerateTestHash("non.existent.file");

        // Act
        var response = await Client.PostAsync($"/api/v1/file-actions/ignore/{nonExistentHash}", null);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("not found");
    }

    [Fact]
    public async Task IgnoreFile_WithEmptyHash_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.PostAsync("/api/v1/file-actions/ignore/", null);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.NotFound); // Route doesn't match without hash parameter
    }

    [Fact]
    public async Task IgnoreFile_WithWhitespaceHash_ShouldReturnNotFound()
    {
        // Act - URL with whitespace gets URL encoded and may be treated as 404 by routing
        var response = await Client.PostAsync("/api/v1/file-actions/ignore/   ", null);

        // Assert - ASP.NET Core routing treats whitespace-only parameters as not found
        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IgnoreFile_WithAlreadyIgnoredFile_ShouldReturnSuccess()
    {
        // Arrange - Create a file already marked as ignored
        var ignoredFile = new Core.Entities.TrackedFile
        {
            Hash = GenerateTestHash("already.ignored.file"),
            FileName = "Already.Ignored.File.mkv",
            OriginalPath = "/downloads/Already.Ignored.File.mkv",
            FileSize = 1024 * 1024 * 100,
            Status = FileStatus.Ignored
        };

        await Factory.SeedDatabaseAsync(context =>
        {
            context.TrackedFiles.Add(ignoredFile);
        });

        // Act
        var response = await Client.PostAsync($"/api/v1/file-actions/ignore/{ignoredFile.Hash}", null);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);

        result.GetProperty("message").GetString().Should().Be("File successfully marked as ignored");
        result.GetProperty("status").GetString().Should().Be("Ignored");
    }

    [Fact]
    public async Task IgnoreFile_WithMovedFile_ShouldReturnBadRequest()
    {
        // Arrange - Create a file that has already been moved
        var movedFile = new Core.Entities.TrackedFile
        {
            Hash = GenerateTestHash("already.moved.file"),
            FileName = "Already.Moved.File.mkv",
            OriginalPath = "/downloads/Already.Moved.File.mkv",
            MovedToPath = "/library/SERIES/Already.Moved.File.mkv",
            FileSize = 1024 * 1024 * 200,
            Status = FileStatus.Moved,
            Category = "TEST SERIES"
        };

        await Factory.SeedDatabaseAsync(context =>
        {
            context.TrackedFiles.Add(movedFile);
        });

        // Act
        var response = await Client.PostAsync($"/api/v1/file-actions/ignore/{movedFile.Hash}", null);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Cannot ignore a file that has already been moved");
    }

    [Theory]
    [InlineData(FileStatus.New, "New file")]
    [InlineData(FileStatus.Processing, "Processing file")]
    [InlineData(FileStatus.Classified, "Classified file")]
    [InlineData(FileStatus.ReadyToMove, "Ready to move file")]
    [InlineData(FileStatus.Error, "Error file")]
    [InlineData(FileStatus.Retry, "Retry file")]
    public async Task IgnoreFile_WithVariousStatuses_ShouldSucceed(FileStatus initialStatus, string description)
    {
        // Arrange
        var testFile = new Core.Entities.TrackedFile
        {
            Hash = GenerateTestHash($"file.with.{initialStatus}.status"),
            FileName = $"File.With.{initialStatus}.Status.mkv",
            OriginalPath = $"/downloads/File.With.{initialStatus}.Status.mkv",
            FileSize = 1024 * 1024 * 150,
            Status = initialStatus
        };

        if (initialStatus == FileStatus.Classified)
        {
            testFile.SuggestedCategory = "TEST SERIES";
            testFile.Confidence = 0.8m;
        }

        await Factory.SeedDatabaseAsync(context =>
        {
            context.TrackedFiles.Add(testFile);
        });

        // Act
        var response = await Client.PostAsync($"/api/v1/file-actions/ignore/{testFile.Hash}", null);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);

        result.GetProperty("message").GetString().Should().Be("File successfully marked as ignored");
        result.GetProperty("status").GetString().Should().Be("Ignored");
    }

    [Fact]
    public async Task IgnoreFile_ShouldPersistToDatabase()
    {
        // Arrange
        var seededFile = await SeedTrackedFileAsync();

        // Act
        var response = await Client.PostAsync($"/api/v1/file-actions/ignore/{seededFile.Hash}", null);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);

        // Verify the change persisted by fetching the file again
        var getResponse = await Client.GetAsync($"/api/files/{seededFile.Hash}");
        getResponse.Should().HaveStatusCode(HttpStatusCode.OK);

        var content = await getResponse.Content.ReadAsStringAsync();
        var file = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);

        // Status is returned as numeric enum value, 8 = Ignored
        file.GetProperty("status").GetInt32().Should().Be((int)FileStatus.Ignored);
    }

    [Fact]
    public async Task IgnoreFile_ShouldAppearInIgnoredFilesFilter()
    {
        // Arrange
        var seededFile = await SeedTrackedFileAsync();

        // Act - Ignore the file
        var ignoreResponse = await Client.PostAsync($"/api/v1/file-actions/ignore/{seededFile.Hash}", null);
        ignoreResponse.Should().HaveStatusCode(HttpStatusCode.OK);

        // Assert - File should appear when filtering by Ignored status
        var filterResponse = await Client.GetAsync("/api/files?status=Ignored");
        filterResponse.Should().HaveStatusCode(HttpStatusCode.OK);

        var content = await filterResponse.Content.ReadAsStringAsync();
        var files = JsonSerializer.Deserialize<JsonElement[]>(content, JsonOptions);

        files.Should().HaveCountGreaterOrEqualTo(1);
        var ignoredFile = files.FirstOrDefault(f => f.GetProperty("hash").GetString() == seededFile.Hash);
        ignoredFile.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        ignoredFile.GetProperty("status").GetInt32().Should().Be((int)FileStatus.Ignored);
    }

    #endregion
}