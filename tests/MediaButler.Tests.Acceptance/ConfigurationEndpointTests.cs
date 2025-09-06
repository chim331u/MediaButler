using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MediaButler.Core.Enums;
using MediaButler.Tests.Acceptance.Infrastructure;

namespace MediaButler.Tests.Acceptance;

/// <summary>
/// Acceptance tests for Configuration API endpoints.
/// Tests basic configuration CRUD operations to validate API functionality.
/// </summary>
public class ConfigurationEndpointTests : ApiTestBase
{
    public ConfigurationEndpointTests(MediaButlerWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateConfigurationSetting_WithValidData_ShouldCreateSetting()
    {
        // Arrange
        var createRequest = new
        {
            key = "Test.NewSetting",
            value = "test-value",
            description = "A test configuration setting",
            requiresRestart = false
        };

        // Act
        var response = await PostJsonAsync("/api/config/settings", createRequest);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.Created);
        
        var content = await response.Content.ReadAsStringAsync();
        var setting = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        
        setting.GetProperty("key").GetString().Should().Be("Test.NewSetting");
        setting.GetProperty("rawValue").GetString().Should().Be("\"test-value\"");
    }

    [Fact]
    public async Task GetConfigurationSetting_WithValidKey_ShouldReturnSetting()
    {
        // Arrange - Create a setting first
        var createRequest = new
        {
            key = "Test.GetSetting",
            value = "get-test-value",
            description = "A test setting for GET operation",
            requiresRestart = false
        };
        await PostJsonAsync("/api/config/settings", createRequest);

        // Act
        var response = await Client.GetAsync("/api/config/settings/Test.GetSetting");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var setting = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        
        setting.GetProperty("key").GetString().Should().Be("Test.GetSetting");
        setting.GetProperty("value").GetString().Should().Be("get-test-value");
    }

    [Fact]
    public async Task GetConfigurationSetting_WithInvalidKey_ShouldReturnNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/config/settings/NONEXISTENT.Key");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.NotFound);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("error");
    }

    [Fact]
    public async Task UpdateConfigurationSetting_WithValidData_ShouldUpdateSetting()
    {
        // Arrange - Create a setting first
        var createRequest = new
        {
            key = "Test.UpdateSetting",
            value = "original-value",
            description = "Original description",
            requiresRestart = false
        };
        await PostJsonAsync("/api/config/settings", createRequest);

        var updateRequest = new
        {
            value = "updated-value",
            description = "Updated description",
            requiresRestart = true
        };

        // Act
        var response = await PutJsonAsync("/api/config/settings/Test.UpdateSetting", updateRequest);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var setting = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        
        setting.GetProperty("rawValue").GetString().Should().Be("\"updated-value\"");
        setting.GetProperty("requiresRestart").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DeleteConfigurationSetting_WithValidKey_ShouldDeleteSetting()
    {
        // Arrange - Create a setting first
        var createRequest = new
        {
            key = "Test.DeleteSetting",
            value = "delete-test-value",
            description = "A test setting for DELETE operation",
            requiresRestart = false
        };
        await PostJsonAsync("/api/config/settings", createRequest);

        // Act
        var response = await Client.DeleteAsync("/api/config/settings/Test.DeleteSetting");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.NoContent);
        
        // Verify deletion by trying to get the setting
        var getResponse = await Client.GetAsync("/api/config/settings/Test.DeleteSetting");
        getResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateConfigurationSetting_WithInvalidKey_ShouldReturnBadRequest()
    {
        // Arrange - Use invalid key format
        var createRequest = new
        {
            key = "invalid-key-format",
            value = "test-value",
            description = "Invalid key format",
            requiresRestart = false
        };

        // Act
        var response = await PostJsonAsync("/api/config/settings", createRequest);

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExportConfiguration_ShouldReturnConfigurationData()
    {
        // Arrange - Create a setting first
        var createRequest = new
        {
            key = "Test.ExportSetting",
            value = "export-value",
            description = "A test setting for export",
            requiresRestart = false
        };
        await PostJsonAsync("/api/config/settings", createRequest);

        // Act
        var response = await Client.GetAsync("/api/config/export");

        // Assert
        response.Should().HaveStatusCode(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        var export = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
        
        export.GetProperty("configuration").Should().NotBeNull();
        export.GetProperty("exportedAt").Should().NotBeNull();
    }
}