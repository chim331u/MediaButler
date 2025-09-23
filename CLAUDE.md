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

## 📁 Project Structure
```
MediaButler/
├── src/
│   ├── MediaButler.API/           # .NET 8 Minimal API with vertical slices
│   ├── MediaButler.Core/          # Domain models, interfaces, BaseEntity
│   ├── MediaButler.Data/          # EF Core, SQLite, Repository pattern
│   ├── MediaButler.ML/            # Classification engine, separate from domain
│   ├── MediaButler.Services/      # Business logic, application services
│   ├── MediaButler.Web/           # Web UI (Blazor Server/WebAssembly)
│   └── MediaButler.Mobile/        # Android app (future)
├── tests/
│   ├── MediaButler.Tests.Unit/           # 45+ fast unit tests
│   ├── MediaButler.Tests.Integration/    # 30+ integration tests
│   └── MediaButler.Tests.Acceptance/     # 25+ acceptance tests
├── docker/
│   └── Dockerfile.arm32          # ARM32/Raspberry Pi deployment
├── docs/
│   ├── dev_planning.md           # Complete development plan
│   ├── api-documentation.md      # Swagger/OpenAPI specs
│   └── deployment-guide.md       # ARM32 deployment guide
├── models/                       # ML models storage (~20MB FastText)
├── configs/
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── appsettings.Production.json
└── README.md
```

### API Design Patterns

#### Controller-Based Organization
```
MediaButler.API/Controllers/
├── FilesController.cs           # File operations and management
├── FileActionsController.cs     # Batch file processing operations
├── StatsController.cs           # Statistics and monitoring
├── HealthController.cs          # Health checks and diagnostics
├── ProcessingController.cs      # Processing workflow management
├── SystemController.cs          # System maintenance operations
├── MetricsController.cs         # Performance metrics and monitoring
├── NotificationTestController.cs # SignalR testing endpoints
└── ExamplesController.cs        # API usage examples
```

#### Key Patterns Used
- **ASP.NET Core Controllers**: Traditional controller-based API with clear separation
- **Repository Pattern**: Data access abstraction with UnitOfWork
- **Dependency Injection**: Service layer composition via built-in DI
- **Global Filters**: Model validation and exception handling
- **Background Services**: Custom lightweight task queue system for ARM32 optimization
- **Options Pattern**: Strongly-typed configuration
- **BaseEntity Pattern**: Consistent audit trail and soft delete across all entities

#### Simple Dependencies Flow
```
Controllers → Services → Repositories → Data Access
           ↘ Shared Models ↙
```

**Technology Stack:**
- .NET 8 with C# 12
- SQLite with Entity Framework Core
- ASP.NET Core Web API with Controllers
- Serilog for logging
- Swagger for API documentation
- FastText for ML classification (20MB model)
- File system monitoring via FileSystemWatcher

**Database Schema (Enhanced with BaseEntity):**
- `TrackedFiles`: Main file tracking with BaseEntity audit properties
- `ProcessingLogs`: Operation audit trail with BaseEntity
- `UserPreferences`: User-specific settings with BaseEntity
- `SeriesPatterns`: Learned patterns for ML classification
- `TrainingData`: ML model training samples
- `Jobs`: Background job tracking
- `FileOperations`: Operation log for rollback capability

**Note**: `ConfigurationSettings` table removed in favor of static configuration from `appsettings.json` only.

## Development Commands

### Build and Run
```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/MediaButler.API/MediaButler.API.csproj

# Run API (development mode with Swagger)
dotnet run --project src/MediaButler.API

# Run in production mode
dotnet run --project src/MediaButler.API --configuration Release
```

### Web Development (Blazor WebAssembly)
```bash
# Run Web UI (Blazor WebAssembly) - development mode
dotnet run --project src/MediaButler.Web

# Build Web UI for production with static files
dotnet publish src/MediaButler.Web -c Release -o ./dist/web

# Run both API and Web concurrently (development)
# Terminal 1: Start API server
dotnet run --project src/MediaButler.API
# Terminal 2: Start Web UI (configure API base URL in appsettings)
dotnet run --project src/MediaButler.Web

# Watch mode for Web UI development (auto-reload on changes)
dotnet watch --project src/MediaButler.Web
```

#### Web UI Status Filtering (Updated)

The Web UI now features simplified status filtering with intelligent auto-refresh capabilities:

**Status Groups**:
- **ALL**: Displays files across all statuses and categories
- **To Classify**: Shows files in New, Processing, or Classified states
- **Ready To Move**: Shows files ready for organization (ReadyToMove status)
- **Error**: Shows files in Retry or Error states requiring attention
- **Ignored**: Shows files marked as ignored

**Smart Auto-Refresh Features**:
- **New File Detection**: When SignalR detects a new file, automatically switches to "ALL" view
- **File Processing Updates**: Refreshes current view when file processing completes
- **Scan Results**: Auto-switches to "ALL" when folder scan discovers new files

**Technical Implementation**:
- Uses new `GET /api/files/by-statuses` endpoint for efficient multi-status filtering
- SignalR integration for real-time updates without manual refresh
- Respects API pagination limits (max 100 records per request)

### Testing
```bash
# Run all tests (target: 270+ tests)
dotnet test

# Run specific test projects
dotnet test tests/MediaButler.Tests.Unit           # Unit tests (110+ tests)
dotnet test tests/MediaButler.Tests.Integration    # Integration tests (90+ tests)
dotnet test tests/MediaButler.Tests.Acceptance     # Acceptance tests (70+ tests)

# Run tests with coverage (target: 82% coverage)
dotnet test --collect:"XPlat Code Coverage"

# Generate coverage report
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"TestResults/CoverageReport" -reporttypes:Html

# Run performance validation tests
dotnet test --filter "Category=Performance"
```

### Database Operations
```bash
# Add Entity Framework migration (with BaseEntity support)
dotnet ef migrations add <MigrationName> --project src/MediaButler.Data --startup-project src/MediaButler.API

# Update database (migrations are auto-applied on startup)
dotnet ef database update --project src/MediaButler.Data --startup-project src/MediaButler.API

# Drop database (caution: destroys all data)
dotnet ef database drop --project src/MediaButler.Data --startup-project src/MediaButler.API

# View migration history
dotnet ef migrations list --project src/MediaButler.Data --startup-project src/MediaButler.API
```

### Package Management
```bash
# Add package to specific project
dotnet add src/MediaButler.API package <PackageName>

# Remove package
dotnet remove src/MediaButler.API package <PackageName>

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
2. SHA256 hash calculation and DB storage (with BaseEntity audit)
3. ML classification with confidence scoring
4. User confirmation required for all files
5. Physical file movement with transaction support
6. Feedback loop for model improvement

## API Endpoints Structure

Key endpoint categories:
- **File Operations**: `/api/files/*` - CRUD operations for tracked files
  - `GET /api/files` - Get files with single status filter
  - `GET /api/files/by-statuses` - **NEW**: Get files with multiple status filters
- **Batch Operations**: `/api/v1/file-actions/*` - Batch file processing and organization
- **Processing**: `/api/processing/*` - File processing workflows and ML operations
- **System Operations**: `/api/system/*` - System maintenance and configuration
- **Monitoring**: `/api/health`, `/api/stats`, `/api/metrics/*` - System health and metrics
- **Real-time**: SignalR hubs at `/notifications` and `/file-processing` - Live updates

### New Multi-Status File Endpoint

**Endpoint**: `GET /api/files/by-statuses`

**Purpose**: Retrieve tracked files filtered by multiple status values, enabling efficient querying across multiple processing states.

**Parameters**:
- `skip` (int, default: 0) - Number of files to skip for pagination
- `take` (int, default: 20, max: 100) - Number of files to return
- `statuses` (string array, required) - Array of status values to filter by
- `category` (string, optional) - Category filter

**Example Usage**:
```http
GET /api/files/by-statuses?statuses=ReadyToMove&statuses=Moving&statuses=Moved&skip=0&take=50
GET /api/files/by-statuses?statuses=New&statuses=Processing&statuses=Classified&category=TV%20SERIES
```

**Response**: Array of TrackedFileResponse objects matching any of the specified statuses.

**Note**: Configuration endpoints (`/api/config/*`) removed in favor of static `appsettings.json` configuration.

## Background Processing Architecture

The system uses a custom lightweight background task queue as an ARM32-optimized alternative to Hangfire:

### Key Components
- **BackgroundTaskQueue**: Thread-safe queue with configurable capacity (default: 100)
- **QueuedHostedService**: Background service that processes queued tasks
- **CustomBatchFileProcessor**: Handles batch file operations with progress tracking
- **FileActionsService**: Orchestrates batch operations and provides status monitoring

### Queue Usage Pattern
```csharp
// Queue a batch processing job
var jobId = taskQueue.QueueBatchFileProcessing(operations, request);

// Monitor job status via API
GET /api/v1/file-actions/batch-status/{jobId}
GET /api/v1/file-actions/batch-jobs  // List all jobs
```

### ARM32 Optimizations
- Configurable concurrency limits to prevent resource exhaustion
- Memory-efficient task serialization
- Graceful degradation under memory pressure
- Progress reporting via SignalR for real-time updates

## Configuration - Simplified Static Configuration

Following "Simple Made Easy" principles, the system now uses **pure static configuration** from `appsettings.json` only. This eliminates the complexity of hybrid database/static configuration management.

The system uses `appsettings.json` for configuration located in `src/MediaButler.API/` with comprehensive sections for production deployment:

### Core Configuration Sections
The MediaButler configuration is organized into logical sections for different system components:

#### MediaButler.Paths Configuration
File system path configuration for core system directories.

| Setting | Description | Usage | Example |
|---------|-------------|-------|---------|
| `MediaLibrary` | Target directory for organized media files | Used by PathGenerationService to generate target paths for file organization | `/tmp/mediabutler/library` |
| `WatchFolder` | Primary directory monitored for new files | Used by FileDiscoveryService as the main watch folder for file detection | `../../temp/watch` |
| `PendingReview` | Directory for files awaiting user confirmation | Used for staging files before final organization (future use) | `/tmp/mediabutler/pending` |

#### MediaButler.FileDiscovery Configuration
File monitoring and discovery system settings optimized for ARM32 deployment.

| Setting | Description | Usage | Example |
|---------|-------------|-------|---------|
| `WatchFolders` | Array of directories to monitor for new files | FileDiscoveryService monitors these paths using FileSystemWatcher | `["../../temp/watch"]` |
| `EnableFileSystemWatcher` | Enable real-time file system monitoring | Controls whether FileSystemWatcher is used for immediate file detection | `true` |
| `ScanIntervalMinutes` | Interval between periodic folder scans | Backup scanning mechanism when FileSystemWatcher misses files | `5` |
| `FileExtensions` | Supported file extensions for processing | File filter in FileDiscoveryService to include only relevant files | `[".mkv", ".mp4", ".avi"]` |
| `ExcludePatterns` | Regex patterns for files to ignore | Used to skip temporary, partial, or system files during scanning | `[".*tmp", ".*part"]` |
| `MinFileSizeMB` | Minimum file size threshold in megabytes | Filters out small files that are likely not valid media content | `1` |
| `DebounceDelaySeconds` | Delay before processing file changes | Prevents processing files that are still being written or copied | `3` |
| `MaxConcurrentScans` | Maximum concurrent scanning operations | ARM32 optimization to prevent resource exhaustion | `2` |

#### MediaButler.ML Configuration
Machine learning pipeline configuration for classification and training.

##### Core ML Settings
| Setting | Description | Usage | Example |
|---------|-------------|-------|---------|
| `ModelPath` | Directory containing ML model files | Used by ModelTrainingService to load and save FastText models | `"models"` |
| `ActiveModelVersion` | Current model version identifier | Tracks which model version is active for classification | `"1.0.0"` |
| `AutoClassifyThreshold` | Confidence threshold for automatic classification | Files above this threshold are auto-classified without user confirmation | `0.85` |
| `SuggestionThreshold` | Minimum confidence for showing suggestions | Files above this threshold show classification suggestions to user | `0.50` |
| `MaxClassificationTimeMs` | Maximum time allowed for classification | Timeout protection to prevent classification from blocking ARM32 system | `500` |
| `MaxAlternativePredictions` | Number of alternative suggestions to show | Limits suggestion list size for better user experience | `3` |
| `EnableBatchProcessing` | Enable batch processing for multiple files | Performance optimization for processing multiple files together | `true` |
| `MaxBatchSize` | Maximum files per batch operation | ARM32 memory constraint to prevent system overload | `50` |
| `EnableAutoRetraining` | Enable automatic model retraining | Triggers retraining when sufficient new training data is available | `true` |
| `RetrainingThreshold` | Number of new samples before retraining | Minimum samples needed to trigger automatic model retraining | `100` |

##### Tokenization Settings
| Setting | Description | Usage | Example |
|---------|-------------|-------|---------|
| `NormalizeSeparators` | Convert dots/underscores to spaces | TokenizerService preprocessing for consistent token extraction | `true` |
| `RemoveQualityIndicators` | Strip quality tags (1080p, 720p, etc.) | Removes non-series identifying tokens during tokenization | `true` |
| `RemoveLanguageCodes` | Remove language codes (ITA, ENG, etc.) | Focuses tokenization on series name rather than language variants | `true` |
| `RemoveReleaseTags` | Strip release tags (FINAL, REPACK, etc.) | Removes release-specific tokens that don't identify series | `true` |
| `ConvertToLowercase` | Normalize all tokens to lowercase | Ensures consistent token matching regardless of filename case | `true` |
| `MinTokenLength` | Minimum character length for tokens | Filters out very short tokens that are unlikely to be meaningful | `2` |
| `CustomRemovalPatterns` | Additional regex patterns to remove | Allows custom filtering of specific patterns in filenames | `[]` |

##### Training Settings
| Setting | Description | Usage | Example |
|---------|-------------|-------|---------|
| `TrainingRatio` | Fraction of data used for training | ModelTrainingService splits data for training vs validation | `0.7` |
| `ValidationRatio` | Fraction of data used for validation | Used to evaluate model performance during training | `0.2` |
| `NumberOfIterations` | Training iterations for FastText model | Controls training duration and model complexity | `100` |
| `LearningRate` | Learning rate for model training | FastText training parameter affecting convergence speed | `0.1` |
| `UseEarlyStopping` | Stop training if validation accuracy plateaus | Prevents overfitting and reduces training time | `true` |
| `MinimumAccuracy` | Minimum acceptable model accuracy | Quality gate for accepting newly trained models | `0.75` |

##### Feature Engineering Settings
| Setting | Description | Usage | Example |
|---------|-------------|-------|---------|
| `UseEpisodeFeatures` | Extract episode number features | FeatureEngineeringService identifies S##E## patterns for context | `true` |
| `UseQualityFeatures` | Include video quality indicators | Helps distinguish between different releases of same content | `true` |
| `UseFileExtensionFeature` | Include file extension in features | Different extensions may indicate different content types | `true` |
| `EnableDetailedLogging` | Log detailed feature extraction info | Debug option for troubleshooting feature engineering | `false` |
| `EnablePredictionCaching` | Cache prediction results | Performance optimization to avoid re-classifying same files | `true` |

##### CSV Import Settings
| Setting | Description | Usage | Example |
|---------|-------------|-------|---------|
| `DefaultCsvPath` | Default path for training data CSV | Used by CSV import functionality for batch training data | `"data/training_data.csv"` |
| `Separator` | CSV column separator character | Defines CSV parsing format for training data import | `";"` |
| `NormalizeCategoryNames` | Normalize category names to uppercase | Ensures consistent category naming in training data | `true` |
| `SkipDuplicates` | Skip duplicate entries during import | Prevents duplicate training samples from affecting model | `true` |
| `ValidateFileExtensions` | Validate file extensions in training data | Ensures training data matches supported file types | `true` |
| `MaxSamples` | Maximum samples to import (0 = unlimited) | Limits training data size for memory management | `0` |
| `AutoImportOnStartup` | Automatically import CSV on system start | Enables automatic training data loading | `false` |
| `BackupPath` | Path for backing up training data | Creates backup copies before importing new data | `"data/backups/training_data_backup.csv"` |

### ARM32 Optimization Settings
Performance and memory optimization settings for ARM32 NAS deployment.

| Setting | Description | Usage | Example |
|---------|-------------|-------|---------|
| `MemoryThresholdMB` | Maximum memory usage threshold | System monitoring triggers cleanup when exceeded | `300` |
| `AutoGCTriggerMB` | Memory level to trigger garbage collection | Proactive memory management for ARM32 constraints | `250` |
| `PerformanceThresholdMs` | Maximum acceptable operation time | Performance monitoring and alerting threshold | `1000` |
| `MaxLogFileSizeMB` | Maximum log file size before rotation | Prevents log files from consuming excessive disk space | `50` |

**Configuration Files:**
- `src/MediaButler.API/appsettings.json` - Base configuration with ARM32 optimization
- `src/MediaButler.API/appsettings.Development.json` - Development overrides
- `src/MediaButler.API/appsettings.Production.json` - Production overrides

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
- **Syntax** (complects meaning and structure)

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
    CreatedAt DATETIME NOT NULL,
    LastUpdateDate DATETIME NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT 1
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
**Suggested Project**: `src/MediaButler.Web`
- **Why WASM**: Runs entirely client-side, reduces server load
- **Dependencies**: `MediaButler.API.Contracts` for HTTP communication
- **Benefits**: Single codebase, C# throughout, offline capability
- **Architecture**: Lightweight UI shell consuming REST API

### **MAUI Android (.NET 9)**
**Suggested Project**: `src/MediaButler.Mobile`
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
├── MediaButler.Tests.Unit/           # Fast, isolated unit tests (110+ tests)
├── MediaButler.Tests.Integration/    # Component integration tests (90+ tests)
└── MediaButler.Tests.Acceptance/     # End-to-end business scenarios (70+ tests)
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
    /\     Acceptance Tests (70+ tests, slow, high confidence)
   /  \    - End-to-end file processing workflows
  /____\   - API contract validation
 /      \  - ML classification accuracy
/__________\
Integration Tests (90+ tests, medium speed)
- Database operations with BaseEntity
- File system interactions
- ML model integration
- Web UI component testing

Unit Tests (110+ tests, fast, low-level)
- Pure function testing
- Business logic validation
- Edge case coverage
- Web component unit tests
```

### Test Categories and Responsibilities

#### **1. Unit Tests (MediaButler.Tests.Unit) - 110+ Tests**
**Purpose**: Test individual components in isolation
**Speed**: <100ms per test
**Scope**: Single class or function

**Key Areas**:
- **BaseEntity Behavior**: Audit trail, soft delete functionality (8 tests)
- **Tokenization Logic**: Filename parsing and series name extraction (12 tests)
- **Classification Algorithms**: ML model decision logic (10 tests)
- **File Operations**: Hash calculation, path validation (8 tests)
- **Business Rules**: Confidence thresholds, retry logic (7 tests)

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

#### **2. Integration Tests (MediaButler.Tests.Integration) - 90+ Tests**
**Purpose**: Test component interactions and external dependencies
**Speed**: 100ms-2s per test
**Scope**: Multiple components working together

**Key Areas**:
- **Database Integration**: EF Core operations with BaseEntity (12 tests)
- **File System Operations**: File watching, moving, hashing (8 tests)
- **ML Pipeline**: Model loading, training, prediction (6 tests)
- **API Layer**: API endpoints with real dependencies (6 tests)
- **Web UI Components**: Blazor component integration with API (8 tests)

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

#### **3. Acceptance Tests (MediaButler.Tests.Acceptance) - 70+ Tests**
**Purpose**: Validate complete business scenarios and API contracts
**Speed**: 1s-10s per test
**Scope**: Full system workflows

**Key Areas**:
- **End-to-End File Processing**: Scan → Classify → Confirm → Move (8 tests)
- **API Contract Validation**: HTTP endpoints with real payloads (6 tests)
- **ML Model Accuracy**: Classification performance benchmarks (4 tests)
- **Error Handling**: Retry logic, failure recovery (4 tests)
- **Performance Requirements**: ARM32 memory constraints (3 tests)

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
- **Test Data Builders**: Fluent builders for complex objects with BaseEntity support
- **Custom Assertions**: Domain-specific assertion methods
- **Test Categories**: Organize tests by feature/component

### Performance and Constraints

#### **ARM32 Testing Considerations**
- **Memory-Conscious Tests**: Monitor memory usage during test runs (<300MB target)
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
- **Minimum 82% Code Coverage**: Focus on critical paths
- **270+ Total Tests**: Comprehensive coverage across all layers
- **All Tests Must Pass**: No skipped or ignored tests in CI
- **Performance Benchmarks**: Classification speed and memory usage tests
- **API Contract Validation**: Ensure backward compatibility
- **Web UI Testing**: Component rendering and user interaction validation

This testing strategy ensures MediaButler maintains high quality while following "Simple Made Easy" principles - tests serve as reasoning tools about system behavior rather than complex safety nets that mask underlying complexity.

## Current Issues and Investigation Results

### Batch File Organization Issue (September 2025)

**Problem**: Files remain in watch folder after calling `api/v1/file-actions/organize-batch` endpoint. User expected files to be physically moved from watch folder to target locations.

**Investigation Results**:

1. **Web UI Implementation**: ✅ **WORKING CORRECTLY**
   - Blazor WebAssembly Files.razor correctly implements batch move functionality
   - Move button properly enabled only when files are in move queue
   - SignalR notifications properly handled with console logging
   - API service calls implemented correctly with proper DTOs
   - Fixed JSON deserialization issue with `ProcessingDurationMs` field (changed from `double` to `double?`)

2. **FileActionsController**: ✅ **EXISTS AND IMPLEMENTED**
   - Controller properly implemented at `/Users/luca/GitHub/mediabutler/MediaButler/src/MediaButler.API/Controllers/FileActionsController.cs`
   - All required endpoints implemented: `organize-batch`, `batch-status`, `validate-batch`, `batch-cancel`, `batch-jobs`
   - Route configuration correct: `[Route("api/v1/file-actions")]`
   - Dependency injection properly configured in Program.cs line 61: `AddScoped<IFileActionsService, FileActionsService>()`

3. **Background Processing Architecture**: ✅ **IMPLEMENTED BUT NOT RUNNING**
   - FileActionsService.cs (460 lines) - Batch orchestration service with proper error handling
   - BackgroundTaskQueue.cs (276 lines) - Lightweight task queue for ARM32 optimization
   - QueuedHostedService.cs (197 lines) - Background service processor
   - CustomBatchFileProcessor.cs (394 lines) - Actual file processing logic
   - BackgroundTaskQueueExtensions.cs - Service registration extensions

4. **Root Cause**: 🚨 **QueuedHostedService NOT STARTING**
   - **Symptom**: API endpoints return 404 Not Found for all `/api/v1/file-actions/*` routes
   - **Analysis**: Request logs show "Request reached the end of the middleware pipeline without being handled by application code"
   - **Evidence**: Startup logs show all other background services starting EXCEPT QueuedHostedService:
     - ✅ FileDiscoveryService, ProcessingCoordinator, FileProcessingService all start
     - ❌ QueuedHostedService startup log missing
   - **Impact**: IBackgroundTaskQueue dependency cannot be resolved → FileActionsController not registered → 404 errors

5. **Background Service Registration**: 🔍 **NEEDS INVESTIGATION**
   - Extension method `AddCustomBackgroundTaskQueue()` called in Program.cs line 64
   - Should register QueuedHostedService via `services.AddHostedService<QueuedHostedService>()`
   - Registration appears correct but service not starting - possible dependency issue

**Current Status**: The web UI is fully functional and the complete batch processing architecture is implemented. The issue is isolated to the QueuedHostedService not starting properly, which prevents the entire file-actions API from being available.

**Next Steps**:
1. Debug QueuedHostedService startup failure
2. Verify all dependencies can be resolved
3. Ensure CustomBatchFileProcessor can be instantiated
4. Test complete file move pipeline once background service is running

**Expected Resolution**: Once QueuedHostedService starts correctly, the organize-batch endpoint will be available and files will be physically moved from watch folders to target locations as expected.