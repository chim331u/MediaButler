# 🎩 MediaButler - NEXT STEP list -


[![Version](https://img.shields.io/badge/version-1.0.1-blue.svg)]()
[![Platform](https://img.shields.io/badge/platform-ARM32%20|%20x64-green.svg)]()
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)]()

Based on the dev_planning.md document, here's the detailed breakdown for Sprint 1, Task 1.1.1: Project Structure Setup with specific commands:
Task 1.1.1: Project Structure Setup (1 hour)
Step 1: Initialize Solution and Core Projects (20 minutes)

# Create solution directory and initialize
```
mkdir MediaButler
cd MediaButler
dotnet new sln --name MediaButler
```

# Create src directory structure
```
mkdir src
mkdir tests
mkdir docker
mkdir docs
mkdir models
mkdir configs
```
# Create core projects following dependency hierarchy
```
cd src
```

# 1. Core project (no dependencies) - Domain models and interfaces
```
dotnet new classlib --name MediaButler.Core
dotnet sln ../MediaButler.sln add MediaButler.Core
```
# 2. Data project (depends only on Core) - EF Core, SQLite
```
dotnet new classlib --name MediaButler.Data  
dotnet sln ../MediaButler.sln add MediaButler.Data
```

# 3. ML project (depends only on Core) - Classification engine
```
dotnet new classlib --name MediaButler.ML
dotnet sln ../MediaButler.sln add MediaButler.ML
```

# 4. Services project (depends on Core, Data) - Business logic
```
dotnet new classlib --name MediaButler.Services
dotnet sln ../MediaButler.sln add MediaButler.Services
```

# 5. API project (depends on Services, ML) - Web API
```
dotnet new web --name MediaButler.API
dotnet sln ../MediaButler.sln add MediaButler.API
```
# 6. Web project (future UI) - placeholder for now
```
dotnet new classlib --name MediaButler.Web
dotnet sln ../MediaButler.sln add MediaButler.Web
```
# Core has NO dependencies (pure domain)

# Data depends on Core only
```
cd MediaButler.Data
dotnet add reference ../MediaButler.Core/MediaButler.Core.csproj
```
# ML depends on Core only (separate concern)
```
cd ../MediaButler.ML
dotnet add reference ../MediaButler.Core/MediaButler.Core.csproj
```
# Services depends on Core and Data (application layer)
```
cd ../MediaButler.Services
dotnet add reference ../MediaButler.Core/MediaButler.Core.csproj
dotnet add reference ../MediaButler.Data/MediaButler.Data.csproj
```

# API depends on Services and ML (composition root)
```
cd ../MediaButler.API
dotnet add reference ../MediaButler.Services/MediaButler.Services.csproj
dotnet add reference ../MediaButler.ML/MediaButler.ML.csproj
```

# Web (future) depends on Services only
```
cd ../MediaButler.Web
dotnet add reference ../MediaButler.Services/MediaButler.Services.csproj
cd ..
```

# Step 3: Create Test Projects (10 minutes)
## Create test directory structure
```
cd ../tests
```
## Unit tests project
```
dotnet new xunit --name MediaButler.Tests.Unit
dotnet sln ../MediaButler.sln add MediaButler.Tests.Unit
```

# Integration tests project  
```
dotnet new xunit --name MediaButler.Tests.Integration
dotnet sln ../MediaButler.sln add MediaButler.Tests.Integration
```

# Acceptance tests project
```
dotnet new xunit --name MediaButler.Tests.Acceptance
dotnet sln ../MediaButler.sln add MediaButler.Tests.Acceptance
cd ..
```
Step 4: Setup NuGet Packages Per Project (10 minutes)
```
cd src
```

# MediaButler.Core - Pure domain, minimal dependencies
```
cd MediaButler.Core
```
# No external packages needed for pure domain

# MediaButler.Data - EF Core and SQLite
```
cd ../MediaButler.Data
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.0
```

# MediaButler.ML - ML.NET for classification
```
cd ../MediaButler.ML
dotnet add package Microsoft.ML --version 3.0.1
dotnet add package Microsoft.ML.FastTree --version 3.0.1
```

# MediaButler.Services - Application services
```
cd ../MediaButler.Services
dotnet add package Microsoft.Extensions.DependencyInjection.Abstractions --version 8.0.0
dotnet add package Microsoft.Extensions.Logging.Abstractions --version 8.0.0
dotnet add package Microsoft.Extensions.Options --version 8.0.0
```

# MediaButler.API - Web API with Swagger
```
cd ../MediaButler.API
dotnet add package Microsoft.EntityFrameworkCore.Tools --version 8.0.0
dotnet add package Swashbuckle.AspNetCore --version 6.5.0
dotnet add package Serilog.AspNetCore --version 8.0.0
dotnet add package Microsoft.AspNetCore.OpenApi --version 8.0.0

cd ../..
```
Step 5: Setup Test Project Dependencies (5 minutes)
```
cd tests
```

# Unit tests - testing Core and Services
```
cd MediaButler.Tests.Unit
dotnet add reference ../../src/MediaButler.Core/MediaButler.Core.csproj
dotnet add reference ../../src/MediaButler.Services/MediaButler.Services.csproj
dotnet add package FluentAssertions --version 6.12.0
dotnet add package Moq --version 4.20.69
```

# Integration tests - testing Data layer
```
cd ../MediaButler.Tests.Integration  
dotnet add reference ../../src/MediaButler.Data/MediaButler.Data.csproj
dotnet add reference ../../src/MediaButler.Services/MediaButler.Services.csproj
dotnet add package FluentAssertions --version 6.12.0
dotnet add package Microsoft.EntityFrameworkCore.InMemory --version 8.0.0
dotnet add package Testcontainers --version 3.6.0
```

# Acceptance tests - testing API endpoints
```
cd ../MediaButler.Tests.Acceptance
dotnet add reference ../../src/MediaButler.API/MediaButler.API.csproj
dotnet add package FluentAssertions --version 6.12.0
dotnet add package Microsoft.AspNetCore.Mvc.Testing --version 8.0.0

cd ../..
```
Step 6: Verify No Circular Dependencies
bash# Build solution to verify dependency structure
```
dotnet build
```

# Expected output: Successful build with no circular reference errors
# If any circular dependencies exist, the build will fail with clear error messages
Step 7: Create Basic Directory Structure and Configuration Files
bash# Create configuration files
```
cd configs
echo '{}' > appsettings.json
echo '{}' > appsettings.Development.json  
echo '{}' > appsettings.Production.json
```

# Create documentation structure
```
cd ../docs
touch api-documentation.md
touch deployment-guide.md
touch dev_planning.md
```

# Create Docker structure
```
cd ../docker
touch Dockerfile.arm32
```

# Create models directory for ML storage
```
cd ../models
touch .gitkeep

cd ..
```
Verification Commands:
bash# Verify solution structure
```
dotnet sln list
```

# Verify all projects build without circular dependencies
```
dotnet build --verbosity normal
```

# Check dependency tree (requires dotnet-depends tool)
```
dotnet tool install --global dotnet-depends
dotnet depends
```

# Verify test projects can discover tests
```
dotnet test --list-tests
```
Expected Project Structure After Completion:
```
MediaButler/
├── MediaButler.sln
├── src/
│   ├── MediaButler.Core/          # ✅ No dependencies
│   ├── MediaButler.Data/          # ✅ → Core only  
│   ├── MediaButler.ML/            # ✅ → Core only
│   ├── MediaButler.Services/      # ✅ → Core, Data
│   ├── MediaButler.API/           # ✅ → Services, ML
│   └── MediaButler.Web/           # ✅ → Services only
├── tests/
│   ├── MediaButler.Tests.Unit/
│   ├── MediaButler.Tests.Integration/
│   └── MediaButler.Tests.Acceptance/
├── configs/
├── docs/
├── docker/
└── models/
```
This setup follows the "Simple Made Easy" principle by:

Clear Separation: Each project has single responsibility
One-Way Dependencies: No circular references or complecting
Composable Architecture: Components can be used independently
Explicit Boundaries: Clear interfaces between layers

The total time allocation of 1 hour allows for any troubleshooting of dependency issues or package version conflicts that may arise during setup.


# Sprint 1 - Tasks 1.1.2 & 1.1.3 Implementation Guide

## Task 1.1.2: BaseEntity Implementation (1 hour)

### Step 1: Create BaseEntity Abstract Class (25 minutes)

```bash
# Navigate to Core project
cd src/MediaButler.Core

# Create Common directory for shared domain concepts
mkdir Common
cd Common
touch BaseEntity.cs
```

**File Location**: `src/MediaButler.Core/Common/BaseEntity.cs`

### Step 2: Implement Audit Properties and Methods (20 minutes)

The BaseEntity should include:
- `DateTime CreatedDate` (set on creation)
- `DateTime LastUpdateDate` (updated on modifications)
- `string? Note` (optional context)
- `bool IsActive` (soft delete flag)
- `MarkAsModified()` method
- `SoftDelete()` method
- `Restore()` method

### Step 3: Add XML Documentation (10 minutes)

Ensure comprehensive XML documentation for:
- Class purpose and usage
- Each property with clear descriptions
- Each method with parameters and behavior
- Usage examples where helpful

### Step 4: Validation - No Infrastructure Dependencies (5 minutes)

```bash
# Verify no infrastructure references
cd src/MediaButler.Core
dotnet list reference
# Should show NO references - Core must remain pure domain
```

## Task 1.1.3: Core Domain Entities (1.5 hours)

### Step 1: Create Enums Directory and FileStatus (15 minutes)

```bash
# From MediaButler.Core root
mkdir Enums
cd Enums
touch FileStatus.cs
```

**FileStatus enum** should include:
- `New` - Just discovered
- `Processing` - Being analyzed
- `Classified` - ML classification complete
- `ReadyToMove` - User confirmed, ready for file operation
- `Moving` - Currently being moved
- `Moved` - Successfully organized
- `Error` - Failed processing
- `Retry` - Queued for retry
- `Ignored` - User chose to skip

### Step 2: Create Entities Directory Structure (10 minutes)

```bash
# From MediaButler.Core root
mkdir Entities
cd Entities
```

### Step 3: Implement TrackedFile Entity (35 minutes)

```bash
touch TrackedFile.cs
```

**TrackedFile** requirements:
- Inherit from BaseEntity
- Primary key: `Hash` (string) - SHA256 of file content
- File information: `FileName`, `OriginalPath`, `FileSize`
- Processing state: `Status` (FileStatus enum)
- ML results: `SuggestedCategory`, `Confidence`
- User decisions: `Category`, `TargetPath`
- Timestamps: `ClassifiedAt`, `MovedAt`
- Error tracking: `LastError`, `LastErrorAt`, `RetryCount`
- Business methods: `MarkAsClassified()`, `ConfirmCategory()`, `MarkAsMoved()`, `RecordError()`

### Step 4: Implement ProcessingLog Entity (20 minutes)

```bash
touch ProcessingLog.cs
```

**ProcessingLog** requirements:
- Inherit from BaseEntity
- Primary key: `Id` (Guid)
- Relationships: `FileHash` (foreign key to TrackedFile)
- Log data: `Level`, `Category`, `Message`, `Details`, `Exception`
- Performance: `DurationMs`
- Factory methods: `Info()`, `Warning()`, `Error()`

### Step 5: Implement ConfigurationSetting Entity (15 minutes)

```bash
touch ConfigurationSetting.cs
```

**ConfigurationSetting** requirements:
- Inherit from BaseEntity
- Primary key: `Key` (string)
- Data: `Value` (JSON string), `Section`, `Description`
- Metadata: `DataType`, `RequiresRestart`
- Business method: `UpdateValue()`

### Step 6: Implement UserPreference Entity (10 minutes)

```bash
touch UserPreference.cs
```

**UserPreference** requirements:
- Inherit from BaseEntity
- Primary key: `Id` (Guid)
- User context: `UserId` (string, default "default")
- Data: `Key`, `Value` (JSON), `Category`
- Business method: `UpdateValue()`

### Step 7: Create Supporting Enums (5 minutes)

```bash
cd ../Enums
touch ConfigurationDataType.cs
touch LogLevel.cs
```

**ConfigurationDataType**: String, Integer, Boolean, Path, Json  
**LogLevel**: Trace, Debug, Information, Warning, Error, Critical

### Step 8: Verification and Build Test (10 minutes)

```bash
# From solution root
cd ../../..
dotnet build src/MediaButler.Core

# Should build successfully with no warnings
# Verify file structure:
find src/MediaButler.Core -name "*.cs" -type f
```

## Complete Command Sequence

```bash
# Navigate to Core project
cd src/MediaButler.Core

# Create directory structure
mkdir Common Entities Enums

# BaseEntity implementation
cd Common
touch BaseEntity.cs

# Enums
cd ../Enums  
touch FileStatus.cs
touch ConfigurationDataType.cs
touch LogLevel.cs

# Entities
cd ../Entities
touch TrackedFile.cs
touch ProcessingLog.cs
touch ConfigurationSetting.cs
touch UserPreference.cs

# Verify structure and build
cd ../../..
tree src/MediaButler.Core
dotnet build src/MediaButler.Core
```

## Expected File Structure

```
src/MediaButler.Core/
├── Common/
│   └── BaseEntity.cs
├── Entities/
│   ├── TrackedFile.cs
│   ├── ProcessingLog.cs
│   ├── ConfigurationSetting.cs
│   └── UserPreference.cs
├── Enums/
│   ├── FileStatus.cs
│   ├── ConfigurationDataType.cs
│   └── LogLevel.cs
└── MediaButler.Core.csproj
```

## Quality Validation Checklist

After implementation, verify:

1. **No Infrastructure Dependencies**: `dotnet list reference` returns empty
2. **Compilation Success**: `dotnet build` completes without errors
3. **XML Documentation**: All public members documented
4. **Simple Made Easy Compliance**:
    - Each entity has single responsibility
    - No complecting of concerns
    - Clear state transitions in TrackedFile
    - Separate audit trail in ProcessingLog
5. **BaseEntity Usage**: All entities inherit from BaseEntity correctly

## Potential Issues and Solutions

**Issue**: Circular dependencies between entities  
**Solution**: Keep entities as pure data structures with minimal cross-references

**Issue**: Complex business logic in entities  
**Solution**: Keep entities focused on state management, move complex logic to services layer

**Issue**: Infrastructure concerns creeping in  
**Solution**: Regularly verify no external dependencies with `dotnet list reference`

This implementation ensures the domain layer remains pure and follows "Simple Made Easy" principles while providing a solid foundation for the rest of the application architecture.