using System;
using System.Text.Json;
using FluentAssertions;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Tests.Unit.Infrastructure;
using Xunit;

namespace MediaButler.Tests.Unit.Entities;

/// <summary>
/// Unit tests for ConfigurationSetting domain entity.
/// Tests configuration validation, type safety, and audit trail functionality.
/// Follows "Simple Made Easy" principles - testing configuration behavior without infrastructure complexity.
/// </summary>
public class ConfigurationSettingTests : TestBase
{
    [Fact]
    public void ConfigurationSetting_WithRequiredProperties_ShouldCreateSuccessfully()
    {
        // Arrange
        var key = "MediaButler.ML.ConfidenceThreshold";
        var value = "0.75";
        var section = "ML";
        
        // Act
        var setting = new ConfigurationSetting
        {
            Key = key,
            Value = value,
            Section = section
        };
        
        // Assert
        setting.Key.Should().Be(key);
        setting.Value.Should().Be(value);
        setting.Section.Should().Be(section);
        setting.DataType.Should().Be(ConfigurationDataType.String); // Default
        setting.RequiresRestart.Should().BeFalse(); // Default
        setting.Description.Should().BeNull(); // Optional
    }

    [Fact]
    public void UpdateValue_WithValidStringValue_ShouldUpdateSuccessfully()
    {
        // Arrange
        var setting = CreateTestSetting(ConfigurationDataType.String);
        var newValue = "new string value";
        var reason = "User preference change";
        
        // Act
        setting.UpdateValue(newValue, reason);
        
        // Assert
        setting.Value.Should().Be(newValue);
        setting.Note.Should().Contain($"Updated: {reason}");
    }

    [Fact]
    public void UpdateValue_WithValidIntegerValue_ShouldUpdateSuccessfully()
    {
        // Arrange
        var setting = CreateTestSetting(ConfigurationDataType.Integer);
        var newValue = "42";
        
        // Act
        setting.UpdateValue(newValue);
        
        // Assert
        setting.Value.Should().Be(newValue);
    }

    [Fact]
    public void UpdateValue_WithInvalidIntegerValue_ShouldThrowArgumentException()
    {
        // Arrange
        var setting = CreateTestSetting(ConfigurationDataType.Integer);
        var invalidValue = "not a number";
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            setting.UpdateValue(invalidValue));
        
        exception.Message.Should().Contain($"Value '{invalidValue}' is not a valid integer");
    }

    [Fact]
    public void UpdateValue_WithValidBooleanValue_ShouldUpdateSuccessfully()
    {
        // Arrange
        var setting = CreateTestSetting(ConfigurationDataType.Boolean);
        var newValue = "true";
        
        // Act
        setting.UpdateValue(newValue);
        
        // Assert
        setting.Value.Should().Be(newValue);
    }

    [Theory]
    [InlineData("not a boolean")]
    [InlineData("1")]
    [InlineData("yes")]
    public void UpdateValue_WithInvalidBooleanValue_ShouldThrowArgumentException(string invalidValue)
    {
        // Arrange
        var setting = CreateTestSetting(ConfigurationDataType.Boolean);
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            setting.UpdateValue(invalidValue));
        
        exception.Message.Should().Contain($"Value '{invalidValue}' is not a valid boolean");
    }

    [Fact]
    public void UpdateValue_WithValidPathValue_ShouldUpdateSuccessfully()
    {
        // Arrange
        var setting = CreateTestSetting(ConfigurationDataType.Path);
        var validPath = "/valid/file/path";
        
        // Act
        setting.UpdateValue(validPath);
        
        // Assert
        setting.Value.Should().Be(validPath);
    }

    [Theory]
    [InlineData("path<with>invalid")]
    [InlineData("path|with|pipes")]
    [InlineData("path\"with\"quotes")]
    public void UpdateValue_WithInvalidPathValue_ShouldThrowArgumentException(string invalidPath)
    {
        // Arrange
        var setting = CreateTestSetting(ConfigurationDataType.Path);
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            setting.UpdateValue(invalidPath));
        
        exception.Message.Should().Contain("contains invalid path characters");
    }

    [Fact]
    public void UpdateValue_WithValidJsonValue_ShouldUpdateSuccessfully()
    {
        // Arrange
        var setting = CreateTestSetting(ConfigurationDataType.Json);
        var validJson = "{\"key\": \"value\", \"number\": 42}";
        
        // Act
        setting.UpdateValue(validJson);
        
        // Assert
        setting.Value.Should().Be(validJson);
    }

    [Fact]
    public void UpdateValue_WithInvalidJsonValue_ShouldThrowArgumentException()
    {
        // Arrange
        var setting = CreateTestSetting(ConfigurationDataType.Json);
        var invalidJson = "{\"incomplete\": \"json\"";
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            setting.UpdateValue(invalidJson));
        
        exception.Message.Should().Contain("is not valid JSON");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateValue_WithNullOrWhitespaceValue_ShouldThrowArgumentNullException(string? invalidValue)
    {
        // Arrange
        var setting = CreateTestSetting(ConfigurationDataType.String);
        
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            setting.UpdateValue(invalidValue!));
        
        exception.ParamName.Should().Be("newValue");
    }

    [Fact]
    public void UpdateValue_WithReason_ShouldAppendToExistingNote()
    {
        // Arrange
        var setting = CreateTestSetting(ConfigurationDataType.String);
        setting.Note = "Initial configuration";
        
        var newValue = "updated value";
        var reason = "User requested change";
        
        // Act
        setting.UpdateValue(newValue, reason);
        
        // Assert
        setting.Note.Should().Contain("Initial configuration");
        setting.Note.Should().Contain($"Updated: {reason}");
        setting.Note.Should().Contain("\n"); // Should append with newline
    }

    [Fact]
    public void UpdateValue_WithReasonOnEmptyNote_ShouldSetNote()
    {
        // Arrange
        var setting = CreateTestSetting(ConfigurationDataType.String);
        setting.Note.Should().BeNull(); // Initial state
        
        var newValue = "updated value";
        var reason = "System configuration";
        
        // Act
        setting.UpdateValue(newValue, reason);
        
        // Assert
        setting.Note.Should().Be($"Updated: {reason}");
    }

    [Fact]
    public void UpdateValue_WithoutReason_ShouldNotModifyNote()
    {
        // Arrange
        var setting = CreateTestSetting(ConfigurationDataType.String);
        var originalNote = "Original note";
        setting.Note = originalNote;
        
        // Act
        setting.UpdateValue("new value");
        
        // Assert
        setting.Note.Should().Be(originalNote); // Should remain unchanged
    }

    [Fact]
    public void ConfigurationSetting_WithRestartRequired_ShouldIndicateRestartNeeded()
    {
        // Arrange & Act
        var setting = new ConfigurationSetting
        {
            Key = "MediaButler.Database.ConnectionString",
            Value = "Data Source=test.db",
            Section = "Database",
            RequiresRestart = true,
            Description = "Database connection string"
        };
        
        // Assert
        setting.RequiresRestart.Should().BeTrue();
        setting.Description.Should().NotBeNull();
    }

    [Fact]
    public void ConfigurationSetting_ValidationScenarios_ShouldHandleAllDataTypes()
    {
        // Test all data types for comprehensive coverage
        var stringSetting = CreateTestSetting(ConfigurationDataType.String);
        var integerSetting = CreateTestSetting(ConfigurationDataType.Integer);
        var booleanSetting = CreateTestSetting(ConfigurationDataType.Boolean);
        var pathSetting = CreateTestSetting(ConfigurationDataType.Path);
        var jsonSetting = CreateTestSetting(ConfigurationDataType.Json);
        
        // All should accept their respective valid values
        stringSetting.Invoking(s => s.UpdateValue("any string")).Should().NotThrow();
        integerSetting.Invoking(s => s.UpdateValue("123")).Should().NotThrow();
        booleanSetting.Invoking(s => s.UpdateValue("false")).Should().NotThrow();
        pathSetting.Invoking(s => s.UpdateValue("/valid/path")).Should().NotThrow();
        jsonSetting.Invoking(s => s.UpdateValue("{\"valid\": true}")).Should().NotThrow();
        
        // All should reject invalid values for their types
        integerSetting.Invoking(s => s.UpdateValue("not int")).Should().Throw<ArgumentException>();
        booleanSetting.Invoking(s => s.UpdateValue("not bool")).Should().Throw<ArgumentException>();
        pathSetting.Invoking(s => s.UpdateValue("invalid<path>")).Should().Throw<ArgumentException>();
        jsonSetting.Invoking(s => s.UpdateValue("invalid json")).Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Creates a test ConfigurationSetting with the specified data type.
    /// </summary>
    private static ConfigurationSetting CreateTestSetting(ConfigurationDataType dataType)
    {
        return new ConfigurationSetting
        {
            Key = $"MediaButler.Test.{dataType}Setting",
            Value = GetDefaultValueForType(dataType),
            Section = "Test",
            DataType = dataType,
            Description = $"Test {dataType} setting"
        };
    }

    /// <summary>
    /// Gets a valid default value for the specified data type.
    /// </summary>
    private static string GetDefaultValueForType(ConfigurationDataType dataType)
    {
        return dataType switch
        {
            ConfigurationDataType.String => "default string",
            ConfigurationDataType.Integer => "0",
            ConfigurationDataType.Boolean => "false",
            ConfigurationDataType.Path => "/default/path",
            ConfigurationDataType.Json => "{\"default\": true}",
            _ => "default"
        };
    }
}