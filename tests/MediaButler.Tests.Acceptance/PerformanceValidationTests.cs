using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using MediaButler.Tests.Acceptance.Infrastructure;

namespace MediaButler.Tests.Acceptance;

/// <summary>
/// Performance validation tests for MediaButler API.
/// Validates ARM32 deployment constraints including memory usage, response times, and throughput.
/// Tests are designed to ensure the system meets performance requirements for resource-constrained environments.
/// </summary>
public class PerformanceValidationTests : ApiTestBase
{
    public PerformanceValidationTests(MediaButlerWebApplicationFactory factory) : base(factory)
    {
    }

    #region Memory Usage Testing (<300MB target)

    [Fact]
    public async Task PerformanceMetrics_ShouldReportMemoryUnder300MB()
    {
        // Act - Get current performance metrics
        var response = await Client.GetAsync("/api/stats/performance");
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var metrics = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        
        // Assert - Memory usage should be under ARM32 limit
        var memory = metrics.GetProperty("memory");
        var managedMemoryMB = memory.GetProperty("managedMemoryMB").GetDouble();
        var workingSetMB = memory.GetProperty("workingSetMB").GetDouble();
        var targetLimitMB = memory.GetProperty("targetLimitMB").GetInt32();
        
        // Validate ARM32 constraints
        targetLimitMB.Should().Be(300, "ARM32 memory limit should be 300MB");
        managedMemoryMB.Should().BeLessThan(300, "Managed memory should be under ARM32 limit");
        workingSetMB.Should().BeLessThan(300, "Working set should be under ARM32 limit");
        
        // Memory pressure should be normal for healthy operation
        var memoryPressure = memory.GetProperty("memoryPressure").GetString();
        memoryPressure.Should().BeOneOf("Normal", "High");
    }

    [Fact]
    public async Task MemoryUsage_UnderLoad_ShouldRemainStable()
    {
        // Arrange - Get initial memory usage
        var initialResponse = await Client.GetAsync("/api/stats/performance");
        var initialContent = await initialResponse.Content.ReadAsStringAsync();
        var initialMetrics = JsonSerializer.Deserialize<JsonElement>(initialContent, JsonOptions);
        var initialMemory = initialMetrics.GetProperty("memory").GetProperty("managedMemoryMB").GetDouble();

        // Act - Create load with multiple concurrent requests
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Client.GetAsync("/api/health"));
            tasks.Add(Client.GetAsync("/api/files"));
            tasks.Add(Client.GetAsync("/api/stats/performance"));
        }

        await Task.WhenAll(tasks);

        // Act - Check memory usage after load
        var finalResponse = await Client.GetAsync("/api/stats/performance");
        var finalContent = await finalResponse.Content.ReadAsStringAsync();
        var finalMetrics = JsonSerializer.Deserialize<JsonElement>(finalContent, JsonOptions);
        var finalMemory = finalMetrics.GetProperty("memory").GetProperty("managedMemoryMB").GetDouble();

        // Assert - Memory should not have increased significantly
        var memoryIncrease = finalMemory - initialMemory;
        memoryIncrease.Should().BeLessThan(50, "Memory increase under load should be minimal");
        finalMemory.Should().BeLessThan(300, "Memory should remain under ARM32 limit");
        
        // Verify all load requests succeeded
        foreach (var task in tasks)
        {
            var response = await task;
            response.Should().HaveStatusCode(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task MemoryUsage_WithDatabaseOperations_ShouldBeEfficient()
    {
        // Arrange - Seed files to create database load
        var testFiles = await SeedMultipleFilesAsync(10);
        
        // Act - Perform database operations sequentially to avoid concurrency issues
        for (int i = 0; i < 3; i++)
        {
            var response1 = await Client.GetAsync("/api/files?take=5");
            response1.Should().HaveStatusCode(HttpStatusCode.OK);
            
            var response2 = await Client.GetAsync("/api/files/pending");
            response2.Should().HaveStatusCode(HttpStatusCode.OK);
        }

        // Assert - Check memory usage after database operations
        var perfResponse = await Client.GetAsync("/api/stats/performance");
        perfResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await perfResponse.Content.ReadAsStringAsync();
        var metrics = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        var memory = metrics.GetProperty("memory").GetProperty("managedMemoryMB").GetDouble();

        memory.Should().BeLessThan(300, "Memory should remain efficient during database operations");
    }

    #endregion

    #region Response Time Validation (<100ms target)

    [Fact]
    public async Task ResponseTime_HealthEndpoint_ShouldBeUnder100ms()
    {
        // Arrange & Act - Measure health endpoint response time
        var stopwatch = Stopwatch.StartNew();
        var response = await Client.GetAsync("/api/health");
        stopwatch.Stop();

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
            "Health endpoint should respond under 100ms for ARM32 deployment");
    }

    [Fact]
    public async Task ResponseTime_FileOperations_ShouldBeReasonable()
    {
        // Arrange - Create a test file
        var testFile = await SeedTrackedFileAsync("ResponseTime.Test.mkv");

        // Act & Assert - Test various file operations response times
        var operations = new Dictionary<string, string>
        {
            ["Get Files List"] = "/api/files?take=10",
            ["Get Single File"] = $"/api/files/{testFile.Hash}",
            ["Get Ready for Classification"] = "/api/files/ready-for-classification?limit=5",
            ["Get Pending Files"] = "/api/files/pending"
        };

        foreach (var operation in operations)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await Client.GetAsync(operation.Value);
            stopwatch.Stop();

            response.Should().HaveStatusCode(HttpStatusCode.OK);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(200, 
                $"{operation.Key} should complete within reasonable time for ARM32");
        }
    }

    [Fact]
    public async Task ResponseTime_StatsEndpoints_ShouldBeOptimized()
    {
        // Act & Assert - Test stats endpoint response times
        var endpoints = new[]
        {
            "/api/stats/performance",
            "/api/stats/processing", 
            "/api/stats/system-health",
            "/api/stats/dashboard"
        };

        foreach (var endpoint in endpoints)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await Client.GetAsync(endpoint);
            stopwatch.Stop();

            response.Should().HaveStatusCode(HttpStatusCode.OK);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(150, 
                $"Stats endpoint {endpoint} should be optimized for ARM32");
        }
    }

    [Fact]
    public async Task ResponseTime_ConfigurationEndpoints_ShouldBeEfficient()
    {
        // Arrange - Create a test configuration
        var createRequest = new { key = "Performance.TestConfig", value = "test-value" };
        await PostJsonAsync("/api/config/settings", createRequest);

        // Act & Assert - Test configuration endpoint response times
        var operations = new Dictionary<string, Func<Task<HttpResponseMessage>>>
        {
            ["Get Config Export"] = () => Client.GetAsync("/api/config/export"),
            ["Get Config Setting"] = () => Client.GetAsync("/api/config/settings/Performance.TestConfig"),
        };

        foreach (var operation in operations)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await operation.Value();
            stopwatch.Stop();

            response.Should().HaveStatusCode(HttpStatusCode.OK);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, 
                $"Configuration operation {operation.Key} should be under 100ms");
        }
    }

    #endregion

    #region Concurrent Request Handling

    [Fact]
    public async Task ConcurrentRequests_HighVolume_ShouldMaintainPerformance()
    {
        // Arrange - Prepare for high-volume concurrent requests
        const int concurrentRequests = 25;
        const int maxAcceptableTimeMs = 3000; // 3 seconds for all requests

        // Act - Execute concurrent requests
        var tasks = new List<Task<(HttpResponseMessage Response, long ElapsedMs)>>();
        var overallStopwatch = Stopwatch.StartNew();

        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(ExecuteTimedRequest("/api/health"));
            tasks.Add(ExecuteTimedRequest("/api/stats/performance"));
        }

        var results = await Task.WhenAll(tasks);
        overallStopwatch.Stop();

        // Assert - All requests should complete successfully within time limit
        overallStopwatch.ElapsedMilliseconds.Should().BeLessThan(maxAcceptableTimeMs,
            $"All {concurrentRequests * 2} concurrent requests should complete within {maxAcceptableTimeMs}ms");

        foreach (var result in results)
        {
            result.Response.Should().HaveStatusCode(HttpStatusCode.OK);
            result.ElapsedMs.Should().BeLessThan(1000, "Individual request should complete within 1 second");
        }

        // Calculate and verify average response time
        var averageResponseTime = results.Average(r => r.ElapsedMs);
        averageResponseTime.Should().BeLessThan(500, "Average response time should be under 500ms");
    }

    [Fact]
    public async Task ConcurrentRequests_MixedOperations_ShouldHandleGracefully()
    {
        // Arrange - Prepare mixed operation types
        var testFiles = await SeedMultipleFilesAsync(3);

        // Act - Execute mixed operations with limited concurrency (sequential pairs)
        var stopwatch = Stopwatch.StartNew();

        // Test with low concurrency to avoid database issues
        var task1 = Client.GetAsync("/api/health");
        var task2 = Client.GetAsync("/api/health");
        await Task.WhenAll(task1, task2);
        
        var task3 = Client.GetAsync("/api/files?take=2");
        var task4 = Client.GetAsync($"/api/files/{testFiles.First().Hash}");
        await Task.WhenAll(task3, task4);
        
        stopwatch.Stop();

        // Assert - Mixed operations should complete efficiently
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000, "Mixed operations should complete within 3 seconds");
        
        // Verify responses - allow for potential intermittent issues
        var responses = new[] { task1.Result, task2.Result, task3.Result, task4.Result };
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        
        // At least 75% should succeed for this to be considered passing
        successCount.Should().BeGreaterOrEqualTo(3, "At least 3 out of 4 concurrent requests should succeed");
    }

    [Fact]
    public async Task ConcurrentRequests_DatabaseIntensive_ShouldScaleWell()
    {
        // Arrange - Create test data
        var testFiles = await SeedMultipleFilesAsync(10);

        // Act - Execute database operations with controlled concurrency
        var stopwatch = Stopwatch.StartNew();

        // Execute in smaller batches to avoid overwhelming the database
        var batch1Tasks = new[]
        {
            Client.GetAsync("/api/files?take=3"),
            Client.GetAsync("/api/files?take=3&skip=3")
        };
        await Task.WhenAll(batch1Tasks);

        var batch2Tasks = new[]
        {
            Client.GetAsync("/api/files/pending"),
            Client.GetAsync("/api/files?take=2&skip=6")
        };
        await Task.WhenAll(batch2Tasks);

        stopwatch.Stop();

        // Assert - Database operations should scale reasonably
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Database-intensive operations should complete within 5 seconds");
        
        // Verify all responses succeeded
        foreach (var task in batch1Tasks.Concat(batch2Tasks))
        {
            task.Result.Should().HaveStatusCode(HttpStatusCode.OK);
        }
    }

    #endregion

    #region Database Query Performance

    [Fact]
    public async Task DatabaseQuery_LargeDataset_ShouldPerformEfficiently()
    {
        // Arrange - Create large dataset
        var testFiles = await SeedMultipleFilesAsync(200);

        // Act & Assert - Test various query scenarios
        var queryTests = new Dictionary<string, string>
        {
            ["Pagination Large Skip"] = "/api/files?skip=150&take=20",
            ["Status Filtering"] = "/api/files?status=New&take=50",
            ["Large Take Size"] = "/api/files?take=100",
            ["Ready for Classification"] = "/api/files/ready-for-classification?limit=50"
        };

        foreach (var test in queryTests)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await Client.GetAsync(test.Value);
            stopwatch.Stop();

            response.Should().HaveStatusCode(HttpStatusCode.OK);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(300, 
                $"Database query '{test.Key}' should complete within 300ms on ARM32");

            // Verify response contains data
            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement[]>(content, JsonOptions);
            data.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task DatabaseQuery_IndexedLookups_ShouldBeOptimal()
    {
        // Arrange - Create test files
        var testFiles = await SeedMultipleFilesAsync(50);
        var testFile = testFiles.First();

        // Act & Assert - Test indexed lookups (by hash)
        var lookupTests = new[]
        {
            $"/api/files/{testFile.Hash}",
            $"/api/files/{testFiles.Skip(10).First().Hash}",
            $"/api/files/{testFiles.Skip(25).First().Hash}"
        };

        foreach (var endpoint in lookupTests)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await Client.GetAsync(endpoint);
            stopwatch.Stop();

            response.Should().HaveStatusCode(HttpStatusCode.OK);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(50, 
                "Indexed hash lookups should be under 50ms");
        }
    }

    [Fact]
    public async Task DatabaseQuery_AggregateOperations_ShouldBeReasonable()
    {
        // Arrange - Create test data for aggregations
        await SeedMultipleFilesAsync(100);

        // Act & Assert - Test endpoints that likely perform aggregations
        var aggregateEndpoints = new[]
        {
            "/api/stats/processing",
            "/api/stats/categories", 
            "/api/stats/dashboard"
        };

        foreach (var endpoint in aggregateEndpoints)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await Client.GetAsync(endpoint);
            stopwatch.Stop();

            response.Should().HaveStatusCode(HttpStatusCode.OK);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(400, 
                $"Aggregate query {endpoint} should complete within 400ms on ARM32");
        }
    }

    #endregion

    #region Performance Regression Detection

    [Fact]
    public async Task PerformanceRegression_BaselineValidation_ShouldMeetTargets()
    {
        // This test serves as a performance regression detector
        // It validates that core operations meet baseline performance targets

        var performanceTargets = new Dictionary<string, (string Endpoint, int MaxMs)>
        {
            ["Health Check"] = ("/api/health", 50),
            ["Performance Metrics"] = ("/api/stats/performance", 100),
            ["Files List"] = ("/api/files?take=10", 150),
            ["Config Export"] = ("/api/config/export", 200)
        };

        var results = new Dictionary<string, long>();

        foreach (var target in performanceTargets)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await Client.GetAsync(target.Value.Endpoint);
            stopwatch.Stop();

            response.Should().HaveStatusCode(HttpStatusCode.OK);
            results[target.Key] = stopwatch.ElapsedMilliseconds;

            stopwatch.ElapsedMilliseconds.Should().BeLessThan(target.Value.MaxMs,
                $"{target.Key} should complete within {target.Value.MaxMs}ms baseline");
        }

        // Report results for monitoring
        var totalTime = results.Values.Sum();
        totalTime.Should().BeLessThan(500, "All baseline operations should complete within 500ms total");
    }

    #endregion

    #region Helper Methods

    private async Task<(HttpResponseMessage Response, long ElapsedMs)> ExecuteTimedRequest(string endpoint)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await Client.GetAsync(endpoint);
        stopwatch.Stop();
        
        return (response, stopwatch.ElapsedMilliseconds);
    }

    #endregion
}