# MediaButler Development Guide - Task 1.7.3

**Generated**: September 6, 2025  
**Version**: 1.0.0  
**Target**: New Developer Onboarding and Team Standards

## Quick Start Guide for New Developers

### Prerequisites
- **.NET 8 SDK** - Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
- **Visual Studio Code** or **Visual Studio 2022** (recommended)
- **Git** for version control
- **SQLite** (included with .NET, no separate install needed)
- **Optional**: Docker for containerized development

### Repository Setup

#### 1. Clone Repository
```bash
git clone https://github.com/yourusername/mediabutler.git
cd mediabutler/MediaButler
```

#### 2. Restore Dependencies
```bash
# Restore all project dependencies
dotnet restore

# Verify build
dotnet build
```

#### 3. Initialize Database
```bash
# Apply EF Core migrations
dotnet ef database update --project src/MediaButler.Data --startup-project src/MediaButler.API

# Verify database creation
ls src/MediaButler.API/mediabutler.db
```

#### 4. Run Application
```bash
# Development mode with hot reload
dotnet run --project src/MediaButler.API

# API will be available at:
# - http://localhost:5000 (HTTP)
# - https://localhost:5001 (HTTPS)
# - Swagger UI: https://localhost:5001/swagger
```

#### 5. Verify Installation
```bash
# Run all tests
dotnet test

# Expected output:
# - 129 unit tests passing
# - 45 integration tests passing  
# - 69 acceptance tests passing
# - Total: 243 tests, 100% pass rate

# Check API health
curl http://localhost:5000/api/health
```

### Development Environment Configuration

#### Visual Studio Code Extensions
```json
{
  "recommendations": [
    "ms-dotnettools.csharp",
    "ms-dotnettools.csdevkit",
    "ms-vscode.vscode-json",
    "humao.rest-client",
    "ms-dotnettools.vscode-dotnet-runtime",
    "bradlc.vscode-tailwindcss"
  ]
}
```

#### Required VS Code Settings
```json
{
  "dotnet.defaultSolution": "MediaButler.sln",
  "omnisharp.enableEditorConfigSupport": true,
  "csharp.semanticHighlighting.enabled": true,
  "files.exclude": {
    "**/bin": true,
    "**/obj": true,
    "**/*.db": true
  }
}
```

## Testing Strategy Documentation

### Test Architecture Overview

MediaButler uses a **3-tier testing pyramid** that prioritizes behavioral validation over coverage percentages, following **"Simple Made Easy"** principles.

```
      ┌────────────────────────────────────┐
      │     Acceptance Tests (69)          │ ← End-to-end workflows
      │   - API contract validation        │   High confidence,
      │   - Performance validation         │   Slower execution
      │   - Workflow testing               │
      └────────────────────────────────────┘
             ┌─────────────────────────────────────┐
             │      Integration Tests (45)         │ ← Component interactions
             │   - Database operations             │   Medium speed,
             │   - Service integrations            │   Real dependencies
             │   - Repository patterns             │
             └─────────────────────────────────────┘
                    ┌──────────────────────────────────────┐
                    │         Unit Tests (129)             │ ← Individual components
                    │   - Domain logic validation          │   Fast execution,
                    │   - Business rule testing            │   Isolated testing
                    │   - Edge case coverage               │
                    └──────────────────────────────────────┘
```

### Test Project Structure
```
tests/
├── MediaButler.Tests.Unit/           # 129 fast unit tests
│   ├── Common/                       # Result pattern, base types
│   ├── Entities/                     # Domain entity behavior
│   ├── Services/                     # Business logic validation
│   ├── Builders/                     # Test data builders
│   └── ObjectMothers/                # Complex object creation
├── MediaButler.Tests.Integration/    # 45 integration tests
│   ├── Data/                         # Database operations
│   ├── Services/                     # Service layer integration
│   └── Infrastructure/               # Test infrastructure
└── MediaButler.Tests.Acceptance/     # 69 acceptance tests
    ├── Controllers/                  # API endpoint tests
    ├── Workflows/                    # End-to-end scenarios
    ├── Performance/                  # ARM32 validation
    └── Infrastructure/               # Test server setup
```

### Test Naming Conventions

#### Unit Test Pattern
```csharp
public class EntityNameTests
{
    [Fact]
    public void MethodName_WithCondition_ShouldExpectedBehavior()
    {
        // Given - Arrange test data
        var entity = new Entity();
        
        // When - Act on the entity
        var result = entity.MethodName(validInput);
        
        // Then - Assert expected behavior
        result.Should().Be(expectedValue);
    }
}
```

#### Integration Test Pattern
```csharp
public class ServiceIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task ServiceMethod_WithRealDatabase_ShouldPersistCorrectly()
    {
        // Given - Real database context
        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IService>();
        
        // When - Execute service operation
        var result = await service.ProcessAsync(testData);
        
        // Then - Verify database state
        result.IsSuccess.Should().BeTrue();
        // Additional database verification...
    }
}
```

#### Acceptance Test Pattern
```csharp
public class FeatureAcceptanceTests : ApiTestBase
{
    [Fact]
    public async Task CompleteWorkflow_HappyPath_ShouldSucceed()
    {
        // Given - API client and test data
        var client = Factory.CreateClient();
        var testFile = await SeedTestFileAsync();
        
        // When - Execute complete workflow
        var scanResult = await client.PostAsync("/api/files/scan", scanRequest);
        var confirmResult = await client.PostAsync($"/api/files/{hash}/confirm", confirmRequest);
        var moveResult = await client.PostAsync($"/api/files/{hash}/move");
        
        // Then - Verify end-to-end success
        moveResult.Should().HaveStatusCode(HttpStatusCode.OK);
        // Verify file system state, database updates, etc.
    }
}
```

### Testing Best Practices

#### Test Data Management
```csharp
// ✅ Good: Use builders for complex objects
var trackedFile = TrackedFileBuilder
    .Create()
    .WithHash("abc123")
    .WithStatus(FileStatus.New)
    .WithCategory("TEST SERIES")
    .Build();

// ✅ Good: Use object mothers for common scenarios
var newVideoFile = TrackedFileObjectMother.NewVideoFile();
var classifiedFile = TrackedFileObjectMother.ClassifiedFile();

// ❌ Avoid: Hard-coded test data in tests
var file = new TrackedFile 
{ 
    Hash = "hardcoded", 
    FilePath = "/hard/coded/path.mkv" 
};
```

#### Assertion Patterns
```csharp
// ✅ Good: Fluent assertions with clear intent
result.IsSuccess.Should().BeTrue("file processing should succeed");
result.Value.Should().NotBeNull();
result.Value.Status.Should().Be(FileStatus.Moved);

// ✅ Good: Multiple specific assertions
response.Should().HaveStatusCode(HttpStatusCode.OK);
var content = await response.Content.ReadAsStringAsync();
var data = JsonSerializer.Deserialize<ApiResponse>(content);
data.Success.Should().BeTrue();

// ❌ Avoid: Multiple unrelated assertions in one test
// ❌ Avoid: Generic Assert.True without context
```

### Running Tests

#### Daily Development Workflow
```bash
# Quick unit test feedback (< 5 seconds)
dotnet test tests/MediaButler.Tests.Unit

# Full test suite before commits (< 30 seconds)
dotnet test

# Integration tests only
dotnet test tests/MediaButler.Tests.Integration

# Performance validation
dotnet test --filter "Category=Performance"
```

#### CI/CD Pipeline Tests
```bash
# Full test suite with coverage
dotnet test --collect:"XPlat Code Coverage" --logger:trx

# Generate coverage report
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"TestResults/CoverageReport" -reporttypes:Html

# Test report validation
# - Unit tests: 100% pass required
# - Integration tests: 100% pass required  
# - Acceptance tests: 100% pass required
# - Coverage: Focus on business logic over percentage
```

## Code Style and Conventions Guide

### C# Coding Standards

#### Naming Conventions
```csharp
// ✅ Classes: PascalCase
public class FileService { }
public class TrackedFileRepository { }

// ✅ Methods: PascalCase
public async Task<Result<TrackedFile>> GetFileAsync(string hash) { }
public bool IsValidCategory(string category) { }

// ✅ Properties: PascalCase
public string FilePath { get; set; }
public FileStatus Status { get; private set; }

// ✅ Private fields: camelCase with underscore
private readonly ILogger _logger;
private readonly IRepository<TrackedFile> _repository;

// ✅ Parameters and locals: camelCase
public Result ProcessFile(string filePath, FileStatus targetStatus)
{
    var processedFile = new TrackedFile();
    return Result.Success(processedFile);
}

// ✅ Constants: PascalCase
public const int MaxFileNameLength = 255;
public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
```

#### File Organization
```csharp
// File header order:
using System;                          // System namespaces first
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;    // Third-party namespaces

using MediaButler.Core.Common;         // Project namespaces last
using MediaButler.Core.Entities;

namespace MediaButler.Services;        // File-scoped namespace (C# 10+)

/// <summary>
/// Service for managing tracked file operations and lifecycle.
/// Implements business logic for file discovery, classification, and organization.
/// </summary>
public class FileService : IFileService
{
    // Private fields first
    private readonly ILogger<FileService> _logger;
    private readonly IRepository<TrackedFile> _repository;
    
    // Constructor
    public FileService(ILogger<FileService> logger, IRepository<TrackedFile> repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }
    
    // Public methods
    // Private methods last
}
```

#### Method Structure
```csharp
/// <summary>
/// Processes a file through the complete workflow: scan, classify, and move.
/// </summary>
/// <param name="filePath">Full path to the file to process</param>
/// <param name="cancellationToken">Cancellation token for async operation</param>
/// <returns>Result containing the processed TrackedFile or error information</returns>
public async Task<Result<TrackedFile>> ProcessFileAsync(
    string filePath, 
    CancellationToken cancellationToken = default)
{
    // Input validation first
    if (string.IsNullOrWhiteSpace(filePath))
        return Result<TrackedFile>.Failure("File path cannot be empty");
    
    if (!File.Exists(filePath))
        return Result<TrackedFile>.Failure($"File not found: {filePath}");
    
    try
    {
        // Business logic with clear steps
        _logger.LogInformation("Processing file: {FilePath}", filePath);
        
        var hash = await CalculateHashAsync(filePath, cancellationToken);
        var trackedFile = await _repository.GetByHashAsync(hash, cancellationToken);
        
        if (trackedFile is null)
        {
            trackedFile = CreateTrackedFile(filePath, hash);
            await _repository.AddAsync(trackedFile, cancellationToken);
        }
        
        return Result<TrackedFile>.Success(trackedFile);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to process file: {FilePath}", filePath);
        return Result<TrackedFile>.Failure($"Processing failed: {ex.Message}");
    }
}
```

### Entity Design Patterns

#### BaseEntity Usage
```csharp
// ✅ All domain entities inherit from BaseEntity
public class TrackedFile : BaseEntity
{
    // Primary business properties
    public string Hash { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public FileStatus Status { get; set; }
    
    // Business methods
    public void MarkAsProcessing()
    {
        Status = FileStatus.Processing;
        MarkAsModified(); // BaseEntity method
    }
    
    public void ClassifyAs(string category)
    {
        Category = category;
        Status = FileStatus.Classified;
        MarkAsModified();
    }
}
```

#### Result Pattern Implementation
```csharp
// ✅ Always use Result<T> for operations that can fail
public async Task<Result<TrackedFile>> GetFileByHashAsync(string hash)
{
    if (string.IsNullOrWhiteSpace(hash))
        return Result<TrackedFile>.Failure("Hash cannot be empty");
    
    var file = await _repository.GetByHashAsync(hash);
    return file is not null 
        ? Result<TrackedFile>.Success(file)
        : Result<TrackedFile>.Failure($"File not found with hash: {hash}");
}

// ✅ Chain operations using Result extensions
public async Task<Result> ProcessWorkflowAsync(string filePath)
{
    return await ScanFileAsync(filePath)
        .ThenAsync(file => ClassifyFileAsync(file))
        .ThenAsync(file => MoveFileAsync(file))
        .TapError(error => _logger.LogError("Workflow failed: {Error}", error));
}
```

### API Controller Patterns

#### Minimal API Style
```csharp
// ✅ Feature-based endpoint organization
public static class FilesEndpoints
{
    public static void MapFilesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/files")
            .WithTags("Files")
            .WithOpenApi();
        
        group.MapGet("", GetFiles)
            .WithSummary("Get tracked files with optional filtering");
            
        group.MapGet("{hash}", GetFileByHash)
            .WithSummary("Get specific file by hash");
            
        group.MapPost("{hash}/confirm", ConfirmFile)
            .WithSummary("Confirm file classification");
    }
    
    private static async Task<IResult> GetFiles(
        [AsParameters] PaginationRequest request,
        IFileService fileService)
    {
        var result = await fileService.GetFilesAsync(request.Take, request.Skip);
        return result.IsSuccess 
            ? Results.Ok(result.Value)
            : Results.BadRequest(result.Error);
    }
}
```

#### Model Validation
```csharp
// ✅ Use data annotations for input validation
public record ConfirmCategoryRequest
{
    [Required(ErrorMessage = "Category is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Category must be 1-200 characters")]
    [RegularExpression(@"^[A-Z0-9\s\-\.]+$", ErrorMessage = "Category contains invalid characters")]
    public string Category { get; init; } = string.Empty;
}

// ✅ Custom validation attributes for domain rules
[AttributeUsage(AttributeTargets.Property)]
public class ValidFileHashAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string hash) return false;
        return hash.Length == 64 && hash.All(char.IsLetterOrDigit);
    }
}
```

### Database and Repository Patterns

#### Repository Implementation
```csharp
// ✅ Generic repository with domain-specific extensions
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly MediaButlerDbContext Context;
    protected readonly DbSet<T> DbSet;
    
    protected Repository(MediaButlerDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }
    
    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(e => e.IsActive)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }
    
    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(e => e.IsActive)
            .OrderByDescending(e => e.CreatedDate)
            .ToListAsync(cancellationToken);
    }
}

// ✅ Domain-specific repository extensions
public class TrackedFileRepository : Repository<TrackedFile>, ITrackedFileRepository
{
    public TrackedFileRepository(MediaButlerDbContext context) : base(context) { }
    
    public async Task<TrackedFile?> GetByHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(f => f.IsActive)
            .FirstOrDefaultAsync(f => f.Hash == hash, cancellationToken);
    }
    
    public async Task<IReadOnlyList<TrackedFile>> GetByStatusAsync(
        FileStatus status, 
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(f => f.IsActive && f.Status == status)
            .OrderBy(f => f.CreatedDate)
            .ToListAsync(cancellationToken);
    }
}
```

#### Entity Configuration
```csharp
// ✅ Explicit configuration for all entities
public class TrackedFileConfiguration : BaseEntityConfiguration<TrackedFile>
{
    public override void Configure(EntityTypeBuilder<TrackedFile> builder)
    {
        base.Configure(builder); // BaseEntity configuration
        
        builder.ToTable("TrackedFiles");
        
        builder.Property(e => e.Hash)
            .IsRequired()
            .HasMaxLength(64);
            
        builder.Property(e => e.FilePath)
            .IsRequired()
            .HasMaxLength(1000);
            
        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<int>();
            
        // Indexes for performance
        builder.HasIndex(e => e.Hash).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.Category);
    }
}
```

## Troubleshooting Common Issues

### Build and Compilation Issues

#### Missing Dependencies
**Symptom**: `dotnet build` fails with package restore errors

**Solution**:
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore with verbose output
dotnet restore --verbosity diagnostic

# Force package download
dotnet restore --force --no-cache
```

#### EF Core Migration Issues
**Symptom**: Database update fails or migration conflicts

**Solution**:
```bash
# Reset database to clean state
rm src/MediaButler.API/mediabutler.db

# Regenerate migrations
dotnet ef migrations remove --project src/MediaButler.Data --startup-project src/MediaButler.API
dotnet ef migrations add InitialCreate --project src/MediaButler.Data --startup-project src/MediaButler.API

# Apply migrations
dotnet ef database update --project src/MediaButler.Data --startup-project src/MediaButler.API
```

### Test Execution Issues

#### Integration Test Database Conflicts
**Symptom**: Integration tests fail with database lock errors

**Solution**:
```csharp
// Ensure proper test isolation
public class IntegrationTestBase : IDisposable
{
    protected readonly MediaButlerWebApplicationFactory Factory;
    
    protected IntegrationTestBase()
    {
        Factory = new MediaButlerWebApplicationFactory();
        
        // Use unique database per test class
        var dbName = $"test_{Guid.NewGuid():N}.db";
        Factory.ConfigureDatabase(dbName);
    }
    
    public virtual void Dispose()
    {
        Factory?.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

#### Acceptance Test Timeouts
**Symptom**: API tests fail with timeout exceptions

**Solution**:
```csharp
// Configure appropriate timeouts
public class ApiTestBase
{
    protected HttpClient Client { get; private set; } = null!;
    
    protected virtual void ConfigureClient()
    {
        Client = Factory.CreateClient();
        Client.Timeout = TimeSpan.FromSeconds(30); // Longer timeout for acceptance tests
    }
}
```

### Runtime Issues

#### Memory Usage on ARM32
**Symptom**: Application crashes or becomes unresponsive on Raspberry Pi

**Investigation**:
```bash
# Monitor memory usage
curl http://localhost:5000/api/stats/performance

# Check system resources
free -h
ps aux | grep MediaButler

# Review logs
sudo journalctl -u mediabutler -f
```

**Solutions**:
```json
// Adjust configuration for lower memory usage
{
  "MediaButler": {
    "Performance": {
      "MaxConcurrentFiles": 1,
      "DatabasePoolSize": 3,
      "ScanIntervalSeconds": 600
    }
  }
}
```

#### File Permission Issues
**Symptom**: Cannot scan or move files

**Investigation**:
```bash
# Check file permissions
ls -la /media/incoming/
ls -la /media/organized/

# Check process owner
ps aux | grep MediaButler
```

**Solutions**:
```bash
# Fix permissions
sudo chown -R mediabutler:mediabutler /media/
sudo chmod -R 755 /media/

# Update systemd service user
sudo systemctl edit mediabutler
# Add:
# [Service]
# User=mediabutler
# Group=mediabutler
```

### Development Environment Issues

#### Hot Reload Not Working
**Symptom**: Changes not reflected during development

**Solution**:
```bash
# Ensure proper watch configuration
dotnet watch --project src/MediaButler.API

# Clear temporary files
rm -rf src/*/bin src/*/obj
dotnet build
```

#### Swagger UI Not Loading
**Symptom**: `/swagger` endpoint returns 404

**Verification**:
```csharp
// Ensure Swagger is configured in Program.cs
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MediaButler API V1");
        c.RoutePrefix = "swagger";
    });
}
```

### Performance Debugging

#### Slow API Response Times
**Investigation**:
```bash
# Use built-in performance monitoring
curl -w "%{time_total}" http://localhost:5000/api/files

# Check database performance
sqlite3 mediabutler.db "EXPLAIN QUERY PLAN SELECT * FROM TrackedFiles WHERE Status = 2;"
```

**Solutions**:
- Add database indexes for frequently queried columns
- Implement response caching for stats endpoints
- Use pagination for large result sets
- Profile SQL queries with EF Core logging

#### Database Query Optimization
```csharp
// ✅ Efficient querying patterns
public async Task<IReadOnlyList<TrackedFile>> GetPagedFilesAsync(int skip, int take)
{
    return await DbSet
        .Where(f => f.IsActive) // Use indexed column
        .OrderByDescending(f => f.CreatedDate) // Indexed ordering
        .Skip(skip)
        .Take(take)
        .AsNoTracking() // Read-only optimization
        .ToListAsync();
}

// ❌ Avoid: Loading unnecessary data
// var allFiles = await DbSet.ToListAsync(); // Don't load everything
// return allFiles.Where(f => f.Status == status).ToList(); // Don't filter in memory
```

### Deployment Issues

#### Service Won't Start on ARM32
**Investigation**:
```bash
# Check service status
sudo systemctl status mediabutler

# View detailed logs
sudo journalctl -u mediabutler --no-pager -l

# Check .NET runtime
/opt/dotnet/dotnet --info
```

**Common Solutions**:
- Verify .NET 8 ARM32 runtime is installed
- Check file permissions on application directory
- Ensure database file is writable
- Verify required directories exist (/media/incoming, /media/organized)

This development guide provides comprehensive guidance for new developers joining the MediaButler project, emphasizing the **"Simple Made Easy"** principles that guide the codebase architecture and development practices.