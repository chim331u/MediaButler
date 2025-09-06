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
}