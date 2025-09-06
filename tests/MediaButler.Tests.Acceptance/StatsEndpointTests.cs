using System.Net;
using System.Text.Json;
using FluentAssertions;
using MediaButler.Tests.Acceptance.Infrastructure;

namespace MediaButler.Tests.Acceptance;

/// <summary>
/// Acceptance tests for Stats API endpoints.
/// Tests basic monitoring and analytics functionality for MediaButler system statistics.
/// </summary>
public class StatsEndpointTests : ApiTestBase
{
    public StatsEndpointTests(MediaButlerWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetProcessingStats_ShouldReturnValidResponse()
    {
        // Act
        var response = await Client.GetAsync("/api/stats/processing");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // Verify it's valid JSON
        var stats = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        stats.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetMLPerformanceStats_ShouldReturnValidResponse()
    {
        // Act
        var response = await Client.GetAsync("/api/stats/ml-performance");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // Verify it's valid JSON
        var stats = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        stats.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetSystemHealthStats_ShouldReturnValidResponse()
    {
        // Act
        var response = await Client.GetAsync("/api/stats/system-health");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // Verify it's valid JSON
        var stats = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        stats.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetActivityStats_WithValidDateRange_ShouldReturnValidResponse()
    {
        // Arrange
        var startDate = DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd");
        var endDate = DateTime.Today.ToString("yyyy-MM-dd");

        // Act
        var response = await Client.GetAsync($"/api/stats/activity?startDate={startDate}&endDate={endDate}");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // Verify it's valid JSON
        var stats = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        stats.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetActivityStats_WithInvalidDateRange_ShouldReturnBadRequest()
    {
        // Arrange - Start date after end date
        var startDate = DateTime.Today.ToString("yyyy-MM-dd");
        var endDate = DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd");

        // Act
        var response = await Client.GetAsync($"/api/stats/activity?startDate={startDate}&endDate={endDate}");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Start date must be before end date");
    }

    [Fact]
    public async Task GetActivityStats_WithInvalidDateFormat_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync("/api/stats/activity?startDate=invalid-date");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid start date format");
    }

    [Fact]
    public async Task GetThroughputStats_WithValidHours_ShouldReturnValidResponse()
    {
        // Act
        var response = await Client.GetAsync("/api/stats/throughput?hours=24");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // Verify it's valid JSON
        var stats = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        stats.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetThroughputStats_WithInvalidHours_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync("/api/stats/throughput?hours=200");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Hours must be between 1 and 168");
    }

    [Fact]
    public async Task GetErrorAnalysis_WithValidDays_ShouldReturnValidResponse()
    {
        // Act
        var response = await Client.GetAsync("/api/stats/errors?days=7");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // Verify it's valid JSON
        var stats = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        stats.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetErrorAnalysis_WithInvalidDays_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync("/api/stats/errors?days=100");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Days must be between 1 and 90");
    }

    [Fact]
    public async Task GetHistoricalTrends_WithInvalidDays_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync("/api/stats/trends?days=400");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Days must be between 1 and 365");
    }

    [Fact]
    public async Task GetDashboardSummary_ShouldReturnValidResponse()
    {
        // Act
        var response = await Client.GetAsync("/api/stats/dashboard");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        
        // Verify it's valid JSON
        var stats = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        stats.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetPerformanceMetrics_ShouldReturnSystemPerformance()
    {
        // Act
        var response = await Client.GetAsync("/api/stats/performance");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        
        // Verify performance metrics structure (this endpoint has hardcoded response structure)
        stats.GetProperty("timestamp").Should().NotBeNull();
        stats.GetProperty("memory").Should().NotBeNull();
        stats.GetProperty("process").Should().NotBeNull();
        stats.GetProperty("system").Should().NotBeNull();
        
        // Verify ARM32 memory constraints
        var memory = stats.GetProperty("memory");
        memory.GetProperty("targetLimitMB").GetInt32().Should().Be(300);
        memory.GetProperty("managedMemoryMB").GetDouble().Should().BeGreaterThan(0);
        memory.GetProperty("workingSetMB").GetDouble().Should().BeGreaterThan(0);
        memory.GetProperty("memoryPressure").GetString().Should().BeOneOf("High", "Normal");
    }
}