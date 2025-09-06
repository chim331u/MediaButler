using System.Net;
using FluentAssertions;
using MediaButler.Tests.Acceptance.Infrastructure;

namespace MediaButler.Tests.Acceptance;

/// <summary>
/// Acceptance tests for health endpoint.
/// Tests basic API availability and health check functionality.
/// </summary>
public class HealthEndpointTests : ApiTestBase
{
    public HealthEndpointTests(MediaButlerWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetHealth_ShouldReturnHealthy()
    {
        // Act
        var response = await Client.GetAsync("/api/health");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
        content.Should().Contain("MediaButler.API");
    }

    [Fact]
    public async Task GetHealthCheck_ShouldReturnDatabaseStatus()
    {
        // Act
        var response = await Client.GetAsync("/api/health");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("version");
    }

    [Fact]
    public async Task GetDetailedHealth_ShouldReturnSystemMetrics()
    {
        // Act
        var response = await Client.GetAsync("/api/health/detailed");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("memory");
        content.Should().Contain("database");
        content.Should().Contain("processing");
    }

    [Fact]
    public async Task GetReadiness_ShouldReturnReadyStatus()
    {
        // Act
        var response = await Client.GetAsync("/api/health/ready");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Ready");
        content.Should().Contain("databaseConnected");
    }

    [Fact]
    public async Task GetLiveness_ShouldReturnAliveStatus()
    {
        // Act
        var response = await Client.GetAsync("/api/health/live");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Alive");
        content.Should().Contain("uptime");
    }
}