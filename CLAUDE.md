# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**MediaButler** is an intelligent TV series file organization system that uses machine learning to automatically categorize and move video files based on filenames. The system learns from user feedback to improve accuracy over time.

**Key Features:**
- Multi-platform: Web, Android App, REST API
- API-first design optimized for NAS ARM32 deployment (1GB RAM, <300MB footprint)
- Single user, no authentication required
- ML-powered classification using FastText (~20MB model)
- File identification via SHA256 hashing
- Handles related files (subtitles, metadata) automatically

## Architecture - "Simple Made Easy"

**Vertical Slice Architecture** over traditional layered architecture, following Rich Hickey's "Simple Made Easy" principles:
- **Compose, Don't Complect**: Independent components rather than braided layers
- **Values Over State**: Immutable data structures and explicit result patterns  
- **Declarative Over Imperative**: Clear, intention-revealing code
- **Single Responsibility**: Each component has one role/task/objective

### Project Structure
```
MediaButler/
├── MediaButler.Shared/              # Pure domain models, contracts
├── MediaButler.API.Contracts/       # API-specific HTTP contracts
├── MediaButler.Data/                # Pure data access (EF Core, SQLite)
├── MediaButler.Core/                # Business logic
├── MediaButler.Services/            # Application services
├── MediaButler.ML/                  # ML classification engine
└── MediaButler.API/                 # REST API with vertical slices
```

### API Design Patterns

#### Feature-Based Organization
```
MediaButler.API/Features/
├── FileManagement/
│   ├── ScanFiles/
│   │   ├── ScanFilesEndpoint.cs     # Minimal API endpoint
│   │   ├── ScanFilesCommand.cs      # Request model
│   │   ├── ScanFilesHandler.cs      # Business logic
│   │   └── ScanFilesValidator.cs    # Validation
│   └── TrackFile/
├── Classification/
└── Monitoring/
```

#### Key Patterns Used
- **Minimal APIs**: .NET 8 native approach, no controller overhead
- **Command-Handler Pattern**: Direct handlers without MediatR complexity
- **Result Pattern**: Explicit success/failure return types
- **Background Services**: Separate processing from HTTP requests
- **Options Pattern**: Strongly-typed configuration

#### Simple Dependencies Flow
```
API Endpoints → Handlers → Services → Data Access
             ↘ Shared Models ↙
```

**Technology Stack:**
- .NET 8 with C# 12
- SQLite with Entity Framework Core
- ASP.NET Core Minimal API
- Serilog for logging
- Swagger for API documentation
- FastText for ML classification (20MB model)
- File system monitoring via FileSystemWatcher

**Database Schema (8 core tables):**
- `TrackedFiles`: Main file tracking with hash, path, category, confidence
- `PendingConfirmations`: Files awaiting user confirmation
- `SeriesPatterns`: Learned patterns for each TV series
- `TrainingData`: ML model training samples
- `Jobs`: Background job tracking
- `FileOperations`: Operation log for rollback capability
- `ModelConfig`: ML model configuration and versioning
- `ReconciliationLog`: File system/DB sync logs

## Development Commands

### Build and Run
```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build MediaButler.API/MediaButler.API.csproj

# Run API (development mode with Swagger)
dotnet run --project MediaButler.API

# Run in production mode
dotnet run --project MediaButler.API --configuration Release
```

### Testing
```bash
# Run all tests
dotnet test

# Run specific test projects
dotnet test tests/MediaButler.Tests.Unit           # Unit tests only
dotnet test tests/MediaButler.Tests.Integration    # Integration tests only
dotnet test tests/MediaButler.Tests.Acceptance     # Acceptance tests only

# Run tests with coverage (when coverage tool is installed)
dotnet test --collect:"XPlat Code Coverage"

# Run tests in parallel
dotnet test --parallel

# Run tests with detailed output
dotnet test --verbosity detailed
```

### Database Operations
```bash
# Add Entity Framework migration
dotnet ef migrations add <MigrationName> --project MediaButler.Data --startup-project MediaButler.API

# Update database
dotnet ef database update --project MediaButler.Data --startup-project MediaButler.API

# Drop database
dotnet ef database drop --project MediaButler.Data --startup-project MediaButler.API
```

### Package Management
```bash
# Add package to specific project
dotnet add MediaButler.API package <PackageName>

# Remove package
dotnet remove MediaButler.API package <PackageName>

# Restore packages
dotnet restore
```

## File Organization Logic

The system organizes files into a flat folder structure:

```
/destination/
├── BREAKING BAD/
│   ├── Breaking.Bad.S01E01.mkv
│   ├── Breaking.Bad.S01E01.srt
│   └── Breaking.Bad.S02E01.mkv
├── THE OFFICE/
│   └── The.Office.S01E01.mkv
└── ONE PIECE/
    └── [Trash] One.Piece.1089.1080p.mkv
```

**Organization Rules:**
- Flat structure (no season subfolders)
- UPPERCASE category names (TV series names)
- Original filenames preserved
- Related files (.srt, .sub, .ass, .nfo) moved together
- Character sanitization for folder names (<>:"/\|?*)

## ML Classification Pipeline

The system uses a 6-stage classification process:

1. **Pre-processing**: Clean filename input
2. **Tokenization**: Extract meaningful tokens
3. **Feature Extraction**: Identify series tokens, episode markers, quality tags
4. **Embedding**: Convert to vector representation (dim=50-100)
5. **Similarity Matching**: Compare with known series embeddings
6. **Decision**: Output category + confidence score

**Confidence Thresholds:**
- `> 0.85`: Auto-classify (pending confirmation)
- `0.50-0.85`: Suggest with alternatives
- `< 0.50`: Likely new series

## File States and Workflow

```
NEW → CLASSIFIED → CONFIRMED → MOVED
Additional states: ERROR, RETRY (max 3 attempts)
```

**Processing Pipeline:**
1. File discovery via FileSystemWatcher
2. SHA256 hash calculation and DB storage
3. ML classification with confidence scoring
4. User confirmation required for all files
5. Physical file movement with transaction support
6. Feedback loop for model improvement

## API Endpoints Structure

Key endpoint categories:
- **File Operations**: `/api/files/*` - CRUD operations for tracked files
- **Bulk Operations**: `/api/pending`, `/api/confirm/bulk` - Mass operations
- **ML Operations**: `/api/classify`, `/api/ml/train` - Classification and training
- **Configuration**: `/api/config/*` - Path and settings management
- **Maintenance**: `/api/maintenance/*` - System maintenance tasks
- **Real-time**: `/api/events` - Server-Sent Events for live updates
- **Monitoring**: `/api/health`, `/api/stats` - System health and metrics

## Configuration

The system uses `appsettings.json` for configuration with the following key sections:
- `MediaButler.Paths`: Watch folder, media library, pending review paths
- `MediaButler.ML`: Model configuration, thresholds, training intervals
- `MediaButler.Butler`: Scan intervals, retry logic, auto-organize settings

## Development Philosophy - "Simple Made Easy"

This project strictly adheres to Rich Hickey's "Simple Made Easy" principles:

### Core Principles
- **Simple vs Easy**: Choose **simple** (un-braided, one-fold) over **easy** (familiar, near at hand)
- **Avoid Complecting**: Never braid together disparate concepts - each component has one role/task/objective
- **Compose, Don't Complect**: Place independent, simple components together rather than intertwining them
- **Objective Simplicity**: Focus on structural simplicity that can be visually/objectively verified

### Implementation Guidelines
- **Values over State**: Prefer immutable data structures; avoid complecting value and time
- **Declarative over Imperative**: Describe *what* rather than *how* (SQL, rule systems preferred)
- **Abstraction Policy**: Use "Who, What, When, Where, Why" to separate concerns and draw away from physical implementation
- **Polymorphism à la Carte**: Define data structures, function sets, and their connections independently for flexibility
- **Avoid Incidental Complexity**: Complexity from tool/construct choices is "your fault" - choose constructs that produce simple artifacts

### Code Quality Over Development Speed
- **Reasoning Matters**: Code must be reasonably understood by humans, not just pass tests
- **Guard Rails Are Not Solutions**: Tests, type checkers, and refactoring tools help but don't address underlying complexity
- **Sensibilities Development**: Cultivate awareness to recognize complecting and interconnections that could be independent
- **Artifact Focus**: Judge constructs by the simplicity of their long-term running artifacts, not immediate programmer convenience

### Avoid These Complexity Sources
- **State** (complects value and time)
- **Objects** (complect state, identity, and behavior) 
- **Inheritance** (complects types and implementations)
- **Loops/Fold** (complect what, how, and order)
- **Conditionals/Case** (complect decisions and actions)
- **Syntax** (complect meaning and structure)

### Prefer These Simple Alternatives
- **Functions** (inputs → outputs, no hidden state)
- **Immutable Data** (values that don't change)
- **Declarative Constructs** (SQL, rules, configuration)
- **Queues** (decouple producers and consumers)
- **Maps** (uniform data access patterns)
- **Namespaces** (simple organization without hierarchy)

## Memory and Performance Constraints

Designed for ARM32 NAS with 1GB RAM:
- Target memory footprint: <300MB
- Processing rate: <50 files/minute (precision over speed)
- Lightweight FastText model: ~20MB
- SQLite for minimal resource overhead
- Optimized batch processing with configurable concurrency limits

## Real-time Features

The system provides real-time updates via Server-Sent Events for:
- File scan progress (`scan.started`, `scan.found`, `scan.completed`)
- Move operations (`move.started`, `move.progress`, `move.completed`)
- ML training status (`training.started`, `training.completed`)
- Error notifications (`error.move_failed`, `error.classification_failed`)

## ML Model Implementation Decisions

### Python vs .NET for ML
**Decision: .NET with ONNX Runtime** for simpler deployment and single-process architecture
- Reduces operational complexity (no microservice orchestration)
- Eliminates inter-process communication overhead
- Simplifies ARM32 deployment (single binary)
- **Future Consideration**: Python microservice remains viable for advanced ML features

### Tokenization Rules for Series Name Extraction
**Standardized tokenization pipeline:**
```csharp
// Input: "The.Walking.Dead.S11E24.FINAL.ITA.ENG.1080p.mkv"
// 1. Normalize separators: dots/underscores → spaces
// 2. Extract series tokens before episode markers (S##E##, Season, Episode)
// 3. Remove quality indicators (1080p, 720p, HDTV, BluRay)
// 4. Remove language codes (ITA, ENG, SUB, DUB)
// 5. Remove release tags (FINAL, REPACK, PROPER)
// Output series tokens: ["the", "walking", "dead"]
```

### Embedding Storage in SQLite
**Vector storage strategy:**
- Store embeddings as BLOB fields in `SeriesPatterns` table
- Use JSON for metadata (dimensions, model version, confidence)
- Similarity search via in-memory vector comparison (acceptable for <1000 series)
- Index on series name for fast pattern lookup
```sql
CREATE TABLE SeriesPatterns (
    Id INTEGER PRIMARY KEY,
    SeriesName TEXT NOT NULL,
    EmbeddingVector BLOB NOT NULL,
    ModelVersion TEXT NOT NULL,
    CreatedAt DATETIME NOT NULL
);
```

### Model Versioning Strategy
**Simple versioning approach:**
- Store model files as `fasttext_v{version}.bin` in models directory
- Track active model version in `ModelConfig` table
- Rollback capability: switch active version pointer, retrain if needed
- Maximum 3 model versions retained (disk space optimization)
- Version format: semantic versioning (1.0.0, 1.1.0, 2.0.0)

## Future Client Applications (Suggestions)

### **Blazor WebAssembly (.NET 8)**
**Suggested Project**: `MediaButler.Web`
- **Why WASM**: Runs entirely client-side, reduces server load
- **Dependencies**: `MediaButler.API.Contracts` for HTTP communication
- **Benefits**: Single codebase, C# throughout, offline capability
- **Architecture**: Lightweight UI shell consuming REST API

### **MAUI Android (.NET 9)**
**Suggested Project**: `MediaButler.Mobile`  
- **Why .NET 9**: Latest performance improvements for mobile
- **Why Android Only**: Aligns with NAS/home server use case
- **Dependencies**: `MediaButler.API.Contracts` for API communication
- **Features**: File notifications, quick confirmations, system monitoring

### **Client Architecture Pattern**
```
Web/Mobile → API.Contracts → HTTP Client → MediaButler.API
```

Both clients remain lightweight UI shells that communicate with the central API, maintaining the "simple" principle by avoiding duplicated business logic while enabling rich user experiences across platforms.

## Test Strategy - "Simple Made Easy" Approach

MediaButler follows a comprehensive 3-tier testing strategy that prioritizes reasoning about code behavior over mere coverage metrics.

### Test Projects Structure
```
tests/
├── MediaButler.Tests.Unit/           # Fast, isolated unit tests
├── MediaButler.Tests.Integration/    # Component integration tests  
└── MediaButler.Tests.Acceptance/     # End-to-end business scenarios
```

### Testing Philosophy

#### **Tests as Reasoning Tools, Not Guard Rails**
Following Rich Hickey's principle that tests don't solve complexity but help verify simple systems:
- **Focus on Behavior**: Test what the code does, not how it does it
- **Simple Test Structure**: Given-When-Then pattern for clarity
- **Values Over State**: Test with immutable inputs and verify immutable outputs
- **Declarative Assertions**: Clear, intention-revealing test names and assertions

#### **Test Pyramid Approach**
```
    /\     Acceptance Tests (Few, Slow, High Confidence)
   /  \    - End-to-end file processing workflows
  /____\   - API contract validation
 /      \  - ML classification accuracy
/__________\ 
Integration Tests (Some, Medium Speed)
- Database operations
- File system interactions  
- ML model integration

Unit Tests (Many, Fast, Low-Level)
- Pure function testing
- Business logic validation
- Edge case coverage
```

### Test Categories and Responsibilities

#### **1. Unit Tests (MediaButler.Tests.Unit)**
**Purpose**: Test individual components in isolation
**Speed**: <100ms per test
**Scope**: Single class or function

**Key Areas**:
- **Tokenization Logic**: Filename parsing and series name extraction
- **Classification Algorithms**: ML model decision logic
- **File Operations**: Hash calculation, path validation
- **Business Rules**: Confidence thresholds, retry logic
- **Domain Models**: Value object behavior

**Example Structure**:
```csharp
public class TokenizerServiceTests
{
    [Theory]
    [InlineData("The.Walking.Dead.S01E01.mkv", "The Walking Dead")]
    [InlineData("Breaking.Bad.S05E16.FINAL.1080p.mkv", "Breaking Bad")]
    public void ExtractSeriesName_WithValidFilename_ReturnsExpectedSeries(
        string filename, string expected)
    {
        // Given - Arrange
        var tokenizer = new TokenizerService();
        
        // When - Act
        var result = tokenizer.ExtractSeriesName(filename);
        
        // Then - Assert
        result.Should().Be(expected);
    }
}
```

#### **2. Integration Tests (MediaButler.Tests.Integration)**
**Purpose**: Test component interactions and external dependencies
**Speed**: 100ms-2s per test
**Scope**: Multiple components working together

**Key Areas**:
- **Database Integration**: EF Core operations, migrations
- **File System Operations**: File watching, moving, hashing
- **ML Pipeline**: Model loading, training, prediction
- **API Layer**: Minimal API endpoints with real dependencies
- **Background Services**: FileWatcher, ClassificationService

**Example Structure**:
```csharp
public class FileClassificationIntegrationTests : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task ClassifyFile_WithTrainedModel_UpdatesDatabase()
    {
        // Given - Real database and ML model
        using var scope = _serviceProvider.CreateScope();
        var classifier = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        var repository = scope.ServiceProvider.GetRequiredService<IFileRepository>();
        
        var testFile = new TrackedFile { /* ... */ };
        
        // When
        var result = await classifier.ClassifyAsync(testFile);
        
        // Then
        result.Category.Should().NotBeNull();
        result.Confidence.Should().BeGreaterThan(0.5);
        
        var savedFile = await repository.GetByHashAsync(testFile.Hash);
        savedFile.Category.Should().Be(result.Category);
    }
}
```

#### **3. Acceptance Tests (MediaButler.Tests.Acceptance)**
**Purpose**: Validate complete business scenarios and API contracts
**Speed**: 1s-10s per test
**Scope**: Full system workflows

**Key Areas**:
- **End-to-End File Processing**: Scan → Classify → Confirm → Move
- **API Contract Validation**: HTTP endpoints with real payloads
- **ML Model Accuracy**: Classification performance benchmarks
- **Error Handling**: Retry logic, failure recovery
- **Performance Requirements**: ARM32 memory constraints

**Example Structure**:
```csharp
public class FileProcessingAcceptanceTests : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task CompleteFileProcessingWorkflow_NewFile_ProcessedSuccessfully()
    {
        // Given - API client and test file
        var client = _factory.CreateClient();
        var testFile = CreateTestVideoFile("The.Office.S01E01.mkv");
        
        // When - Complete workflow
        await client.PostAsync("/api/scan/folder", new { path = testFile.Directory });
        
        var pendingFiles = await client.GetFromJsonAsync<TrackedFile[]>("/api/pending");
        var fileToConfirm = pendingFiles.Should().ContainSingle().Subject;
        
        await client.PostAsync($"/api/files/{fileToConfirm.Hash}/confirm", 
            new { category = "THE OFFICE" });
        
        await client.PostAsync($"/api/files/{fileToConfirm.Hash}/move");
        
        // Then - Verify final state
        var processedFile = await client.GetFromJsonAsync<TrackedFile>(
            $"/api/files/{fileToConfirm.Hash}");
        
        processedFile.Status.Should().Be(FileStatus.Moved);
        processedFile.MovedToPath.Should().Contain("THE OFFICE");
        File.Exists(processedFile.MovedToPath).Should().BeTrue();
    }
}
```

### Testing Tools and Frameworks

#### **Core Testing Stack**
- **xUnit**: Test framework (simple, focused)
- **FluentAssertions**: Readable assertion library
- **Microsoft.AspNetCore.Mvc.Testing**: API testing infrastructure
- **Testcontainers**: Database testing with real SQLite instances
- **Bogus**: Test data generation
- **Moq** (minimal usage): Only for external dependencies

#### **Test Doubles Strategy**
**Prefer Real Objects Over Mocks** (following "Simple Made Easy"):
- Use in-memory SQLite for database tests
- Use temporary directories for file system tests
- Use lightweight ML models for classification tests
- Mock only external services (network calls, hardware)

### Test Quality Guidelines

#### **Simple Test Principles**
1. **One Assertion Per Test**: Each test verifies one behavior
2. **Descriptive Names**: Test names describe the scenario and expected outcome
3. **Independent Tests**: No shared state between tests
4. **Fast Feedback**: Unit tests complete in <10s, integration in <60s
5. **Deterministic**: Tests produce consistent results

#### **Test Maintainability**
- **Shared Test Utilities**: Common setup in base classes
- **Test Data Builders**: Fluent builders for complex objects  
- **Custom Assertions**: Domain-specific assertion methods
- **Test Categories**: Organize tests by feature/component

### Performance and Constraints

#### **ARM32 Testing Considerations**
- **Memory-Conscious Tests**: Monitor memory usage during test runs
- **Timeout Configurations**: Appropriate timeouts for slower ARM32 execution
- **Parallel Execution**: Careful parallel test execution to avoid resource contention
- **Test Data Size**: Limit test file sizes to avoid I/O bottlenecks

#### **Test Execution Strategy**
```bash
# Fast feedback loop (unit tests only)
dotnet test tests/MediaButler.Tests.Unit

# Full confidence (all tests)  
dotnet test

# CI/CD pipeline (with coverage and reporting)
dotnet test --collect:"XPlat Code Coverage" --logger:trx
```

### Continuous Quality Assurance

#### **Quality Gates**
- **Minimum 80% Code Coverage**: Focus on critical paths
- **All Tests Must Pass**: No skipped or ignored tests in CI
- **Performance Benchmarks**: Classification speed and memory usage tests  
- **API Contract Validation**: Ensure backward compatibility

This testing strategy ensures MediaButler maintains high quality while following "Simple Made Easy" principles - tests serve as reasoning tools about system behavior rather than complex safety nets that mask underlying complexity.

## Development Workflow Pattern

### **Safe Development with Git Checkpoints**

Follow this pattern for all significant code changes to ensure safe, reversible development:

#### **1. Start Task Commit**
Before beginning any substantial update or feature implementation:
```bash
git add .
git commit -m "start task: [brief description of what you're about to implement]"
```

**Examples:**
```bash
git commit -m "start task: implement file tokenization service"
git commit -m "start task: add ML classification pipeline"  
git commit -m "start task: create file movement background service"
git commit -m "start task: refactor API endpoints to vertical slices"
```

#### **2. Implementation Phase**
- Make incremental changes following "Simple Made Easy" principles
- Test frequently during development
- Keep changes focused on the stated task

#### **3. Completion Review**
After completing all updates, always ask the user:
```
Task "[description]" implementation complete.

Would you like to:
1. **Save the updates** - Commit the changes permanently
2. **Revert the changes** - Return to the "start task" checkpoint

Please choose your preferred action.
```

#### **4. User Decision**
- **Save**: Create a descriptive commit with the completed work
- **Revert**: Use `git reset --hard HEAD~[number_of_commits]` to return to the start checkpoint

### **Benefits of This Pattern**

#### **Risk Mitigation**
- **Safe Experimentation**: Try different approaches without fear
- **Easy Rollback**: Return to known-good state instantly
- **Clear Boundaries**: Each task has defined start and end points

#### **Development Confidence**
- **Checkpoint Security**: Always have a safe point to return to
- **User Control**: Final decision on keeping or discarding changes
- **Incremental Progress**: Small, manageable chunks of work

#### **Code Quality**
- **Focused Changes**: Each task addresses one specific area
- **Review Opportunity**: User can evaluate each completed task
- **Clean History**: Clear commit messages describing work progression

### **Example Workflow**
```bash
# 1. Start new task
git commit -m "start task: implement file hash calculation service"

# 2. Make changes (implementation phase)
# ... develop FileHashService
# ... add unit tests
# ... update dependencies

# 3. Completion review
# Ask user: Save updates or revert?

# 4a. If save:
git add .
git commit -m "implement file hash calculation service

- Add FileHashService with SHA256 hashing
- Include unit tests for edge cases  
- Update dependency injection
- Follow Simple Made Easy principles"

# 4b. If revert:
git reset --hard HEAD~1  # Back to "start task" commit
```

This pattern ensures all development work is safe, reversible, and under user control, maintaining confidence while following the "Simple Made Easy" philosophy of clear, reasoned decision-making.