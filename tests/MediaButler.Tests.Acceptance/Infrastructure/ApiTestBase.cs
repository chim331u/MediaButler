using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Data;
using Microsoft.Extensions.DependencyInjection;

namespace MediaButler.Tests.Acceptance.Infrastructure;

/// <summary>
/// Base class for API acceptance tests.
/// Provides common HTTP client functionality and test utilities.
/// Follows "Simple Made Easy" principles with clean test setup and teardown.
/// </summary>
public abstract class ApiTestBase : IClassFixture<MediaButlerWebApplicationFactory>, IAsyncLifetime
{
    protected readonly MediaButlerWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions;

    protected ApiTestBase(MediaButlerWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        
        // Configure JSON serialization options to match API settings
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Initializes the test database before each test.
    /// Override in derived classes to provide specific test data.
    /// </summary>
    public virtual async Task InitializeAsync()
    {
        // Ensure database is created and clean
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MediaButlerDbContext>();
        await context.Database.EnsureCreatedAsync();
        await Factory.CleanDatabaseAsync();
    }

    /// <summary>
    /// Cleans up after each test to ensure isolation.
    /// </summary>
    public virtual async Task DisposeAsync()
    {
        await Factory.CleanDatabaseAsync();
    }

    /// <summary>
    /// Seeds the test database with a tracked file for testing.
    /// </summary>
    protected async Task<TrackedFile> SeedTrackedFileAsync(
        string fileName = "Test.Movie.2023.1080p.mkv",
        string originalPath = "/downloads/Test.Movie.2023.1080p.mkv")
    {
        var trackedFile = new TrackedFile
        {
            Hash = GenerateTestHash(fileName),
            FileName = fileName,
            OriginalPath = originalPath,
            FileSize = 1024 * 1024 * 500, // 500MB
            Status = Core.Enums.FileStatus.New
        };

        await Factory.SeedDatabaseAsync(context =>
        {
            context.TrackedFiles.Add(trackedFile);
        });

        return trackedFile;
    }

    /// <summary>
    /// Seeds multiple tracked files with different statuses for testing.
    /// </summary>
    protected async Task<List<TrackedFile>> SeedMultipleFilesAsync(int count = 3)
    {
        var files = new List<TrackedFile>();

        for (int i = 0; i < count; i++)
        {
            var fileName = $"Test.File.{i}.2023.1080p.mkv";
            var file = new TrackedFile
            {
                Hash = GenerateTestHash(fileName),
                FileName = fileName,
                OriginalPath = $"/downloads/{fileName}",
                FileSize = 1024 * 1024 * (500 + i * 100), // Varying file sizes
                Status = Core.Enums.FileStatus.New
            };
            files.Add(file);
        }

        await Factory.SeedDatabaseAsync(context =>
        {
            context.TrackedFiles.AddRange(files);
        });

        return files;
    }

    /// <summary>
    /// Makes a GET request and returns the response as the specified type.
    /// </summary>
    protected async Task<T?> GetAsync<T>(string endpoint)
    {
        var response = await Client.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    /// <summary>
    /// Makes a POST request with JSON content and returns the response.
    /// </summary>
    protected async Task<HttpResponseMessage> PostJsonAsync<T>(string endpoint, T content)
    {
        return await Client.PostAsJsonAsync(endpoint, content, JsonOptions);
    }

    /// <summary>
    /// Makes a PUT request with JSON content and returns the response.
    /// </summary>
    protected async Task<HttpResponseMessage> PutJsonAsync<T>(string endpoint, T content)
    {
        return await Client.PutAsJsonAsync(endpoint, content, JsonOptions);
    }

    /// <summary>
    /// Makes a DELETE request and returns the response.
    /// </summary>
    protected async Task<HttpResponseMessage> DeleteAsync(string endpoint)
    {
        return await Client.DeleteAsync(endpoint);
    }

    /// <summary>
    /// Asserts that the response has the expected status code and returns the deserialized content.
    /// </summary>
    protected async Task<T> AssertSuccessResponseAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(
            $"Expected success status code but got {response.StatusCode}. Content: {await response.Content.ReadAsStringAsync()}");
        
        var content = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        content.Should().NotBeNull();
        return content!;
    }

    /// <summary>
    /// Asserts that the response indicates an error with the expected status code.
    /// </summary>
    protected async Task AssertErrorResponseAsync(HttpResponseMessage response, System.Net.HttpStatusCode expectedStatusCode)
    {
        response.StatusCode.Should().Be(expectedStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Generates a consistent test hash for a given input.
    /// </summary>
    protected static string GenerateTestHash(string input)
    {
        // Simple deterministic hash generation for testing
        var hash = input.GetHashCode().ToString("x8").PadRight(64, '0');
        return hash.Substring(0, 64);
    }

    /// <summary>
    /// Waits for a condition to be met within the specified timeout.
    /// Useful for testing asynchronous operations.
    /// </summary>
    protected async Task WaitForConditionAsync(Func<Task<bool>> condition, TimeSpan timeout, string description = "condition")
    {
        var endTime = DateTime.UtcNow.Add(timeout);
        
        while (DateTime.UtcNow < endTime)
        {
            if (await condition())
                return;
                
            await Task.Delay(100); // Check every 100ms
        }
        
        throw new TimeoutException($"Timeout waiting for {description} after {timeout}");
    }
}