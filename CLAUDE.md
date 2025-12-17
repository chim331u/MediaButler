# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Quick Start (First Time Setup)

```bash
# 1. Verify prerequisites
dotnet --list-sdks  # Should show .NET 8, 9, and 10

# 2. Clone and navigate to repository
cd /path/to/MediaButler

# 3. Build the solution
dotnet build

# 4. Run tests to verify setup
dotnet test

# 5. Start the API server (includes Hangfire background worker)
dotnet run --project src/MediaButler.API

# 6. (Optional) Start Web UI in another terminal
dotnet run --project src/MediaButler.Web
```

**Access Points:**
- API Swagger: http://localhost:5000/swagger
- Web UI: http://localhost:5001 (if running)
- Hangfire Dashboard: http://localhost:5000/hangfire (development only)
- Health Check: http://localhost:5000/api/health
- Go API: http://localhost:5002 (when running Go API)
- SSE Events: http://localhost:5002/events (Go API real-time events)

## Project Overview

**MediaButler** is an intelligent TV series file organization system that uses machine learning to automatically categorize and move video files based on filenames. The system learns from user feedback to improve accuracy over time.

**Key Features:**
- Multi-platform: Web, Android App, REST API
- API-first design optimized for NAS ARM32 deployment (1GB RAM, <300MB footprint)
- Single user, no authentication required
- ML-powered classification using FastText (~20MB model)
- File identification via SHA256 hashing
- Handles related files (subtitles, metadata) automatically

## Common Issues and Solutions

### Issue: "Database is locked" error
**Solution**: SQLite is configured with WAL mode. Ensure only one process is accessing the database. Check for orphaned connections.

### Issue: Hangfire jobs not running
**Solution**:
1. Check Hangfire dashboard at `/hangfire`
2. Verify `appsettings.json` has correct `RecurringJobs` configuration
3. Ensure background service is registered in `Program.cs`

### Issue: ML classification returning low confidence
**Solution**:
1. Check training data quality in database
2. Verify model file exists in `models/` directory
3. Retrain model via `/api/training/trainModel` endpoint
4. Review tokenization patterns for your content type

### Issue: File discovery not detecting new files
**Solution**:
1. Verify watch folder path in `appsettings.json`
2. Check file permissions on watch folder
3. Ensure file meets minimum size requirement (default: 1MB)
4. Check `ExcludePatterns` aren't filtering your files

### Issue: Memory usage exceeding 300MB on ARM32
**Solution**:
1. Check `MaxBatchSize` in ML configuration (reduce if needed)
2. Verify `WorkerCount` in Hangfire settings (should be 2 for ARM32)
3. Monitor GC behavior in logs
4. Consider reducing `RetainedFileCountLimit` for logs

### Debugging Tips
```bash
# Check application logs
cat /data/logs/mediabutler-*.log | tail -n 100

# Check error logs only
cat /data/logs/mediabutler-errors-*.log

# Monitor Hangfire job status (development)
# Navigate to http://localhost:5000/hangfire

# Check ML model status
curl http://localhost:5000/api/health/ml

# View current system statistics
curl http://localhost:5000/api/stats
```

## Architecture - "Simple Made Easy"

**Vertical Slice Architecture** over traditional layered architecture, following Rich Hickey's "Simple Made Easy" principles:
- **Compose, Don't Complect**: Independent components rather than braided layers
- **Values Over State**: Immutable data structures and explicit result patterns
- **Declarative Over Imperative**: Clear, intention-revealing code
- **Single Responsibility**: Each component has one role/task/objective

### Architecture: Single-Process Combined Mode
```
MediaButler.API (Combined HTTP Server + Background Worker)
├── Controllers → HTTP API endpoints
├── Hangfire Client → Enqueue jobs
├── Hangfire Server → Execute jobs in background
├── Jobs/Batch/ → Batch processing job implementations
├── SignalR Hubs → Real-time notifications to web clients
└── Services → Shared business logic

Databases
├── Hangfire Database (SQLite) → Job persistence at /data/mediabutler-hangfire.db
└── MediaButler Database (SQLite) → Application data at /data/mediabutler.db
```

**Architecture Evolution:**
- **Previous**: Dual-process design (MediaButler.API + MediaButler.Batch)
- **Current**: Single-process combined mode for simplified deployment and reduced overhead
- **Rationale**: Eliminates inter-process communication complexity, reduces memory footprint, simplifies ARM32 deployment

## 📁 Project Structure
```
MediaButler/
├── src/
│   ├── MediaButler.API/           # .NET 8 REST API + Hangfire worker (combined)
│   ├── MediaButler.Core/          # Domain models, interfaces, BaseEntity
│   ├── MediaButler.Data/          # EF Core, SQLite, Repository pattern
│   ├── MediaButler.ML/            # Classification engine, separate from domain
│   ├── MediaButler.Services/      # Business logic, application services
│   ├── MediaButler.Web/           # Web UI (Blazor WebAssembly .NET 10)
│   ├── MediaButler.Mobile/        # Android app (MAUI .NET 9)
│   └── MediaButler-Go/            # Go API migration (PoC in progress)
├── tests/
│   ├── MediaButler.Tests.Unit/           # 250+ fast unit tests
│   ├── MediaButler.Tests.Integration/    # 300+ integration tests
│   └── MediaButler.Tests.Acceptance/     # 240+ acceptance tests
├── docker/
│   └── Dockerfile.arm32          # ARM32/Raspberry Pi deployment
├── docs/
│   ├── dev_planning.md           # Complete development plan
│   ├── api-documentation.md      # Swagger/OpenAPI specs
│   └── deployment-guide.md       # ARM32 deployment guide
├── models/                       # ML models storage (~20MB FastText)
└── README.md
```

## Technology Stack

- **.NET 8** with C# 12 (API, Services, Core, Data, ML components)
- **.NET 10** preview (Web UI - Blazor WebAssembly)
- **.NET 9** (Mobile - MAUI Android)
- **SQLite** with Entity Framework Core
- **ASP.NET Core** Web API with Controllers
- **Hangfire** 1.8.14 with SQLite storage (combined client + server mode)
- **Radzen.Blazor** for modern Web UI components
- **SignalR** for real-time Web UI updates
- **Serilog** for logging
- **Swagger** for API documentation
- **ML.NET FastText** for ML classification (20MB model)
- **FileSystemWatcher** for file system monitoring

## Development Commands - Quick Reference

### Prerequisites Check
```bash
# Check .NET SDKs (need 8, 9, and 10)
dotnet --list-sdks

# Check Go version (for Go migration)
go version

# Check SQLC installation (for Go migration)
sqlc version
```

### Build and Run
```bash
# Build entire solution
dotnet build

# Run API with background worker (development mode)
dotnet run --project src/MediaButler.API

# Run in production mode
dotnet run --project src/MediaButler.API --configuration Release

# Run Web UI (in separate terminal)
dotnet run --project src/MediaButler.Web

# Watch mode for Web UI development (auto-reload)
dotnet watch --project src/MediaButler.Web
```

### Testing - Quick Reference
```bash
# Run all tests (790+ comprehensive tests)
dotnet test

# Run specific test project
dotnet test tests/MediaButler.Tests.Unit
dotnet test tests/MediaButler.Tests.Integration
dotnet test tests/MediaButler.Tests.Acceptance

# Run a specific test by name
dotnet test --filter "FullyQualifiedName~TokenizerServiceTests.ExtractSeriesName_WithValidFilename_ReturnsExpectedSeries"

# Run tests in a specific class
dotnet test --filter "FullyQualifiedName~TokenizerServiceTests"

# Run tests by category
dotnet test --filter "Category=Performance"

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Database Operations
```bash
# Add Entity Framework migration
dotnet ef migrations add <MigrationName> --project src/MediaButler.Data --startup-project src/MediaButler.API

# Update database (migrations auto-apply on startup)
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

# List outdated packages
dotnet list package --outdated
```

## Current Development Status

**Branch**: `golangApi` - Active Go API migration in progress

**Completed Work:**
- ✅ **Sprint 1**: Foundation & Domain (Complete - 243+ tests passing)
- ✅ **Sprint 2**: ML Classification Engine (Complete - Italian content optimization)
- ✅ **Sprint 3**: File Operations & Automation (Substantially complete - 92.9% test pass rate)
- ✅ **Sprint 4**: Web UI (Partial - Core components implemented)

**Current Test Status:**
- **Total**: 520 tests implemented (790+ target)
- **Pass Rate**: 92.9% (483 passing, 37 failing)
- **Unit Tests**: 359/388 passing (92.5%)
- **Integration Tests**: 55/63 passing (87.3%)
- **Acceptance Tests**: 69/69 passing (100%)

**Known Issues:**
- 37 failing tests in ML performance validation and tokenization
- Minor test failures primarily in cross-validation logic
- Some database stack overflow issues during heavy operations

**Active Work Areas:**
1. Go API migration (PoC in progress - Week 2: Service layer)
2. Test stabilization to reach 95% pass rate
3. Web UI polish and completion
4. Mobile app development (planned)

**Go Migration Status**: Week 1 Complete, Week 2 In Progress
- Foundation and database layer complete
- Service layer implementation ongoing
- See Go Migration section below for details

## API Design Patterns

### Controller-Based Organization
```
MediaButler.API/Controllers/
├── FilesController.cs               # File operations and management
├── FileActionsController.cs         # Batch file processing operations
├── TrainingController.cs            # ML model training operations
├── StatsController.cs               # Statistics and monitoring
├── HealthController.cs              # Health checks and diagnostics
├── ProcessingController.cs          # Processing workflow management
├── SystemController.cs              # System maintenance operations
├── MetricsController.cs             # Performance metrics and monitoring
├── NotificationsController.cs       # Batch worker to SignalR bridge
└── NotificationTestController.cs    # SignalR testing endpoints
```

### Key Patterns Used
- **ASP.NET Core Controllers**: Traditional controller-based API with clear separation
- **Repository Pattern**: Data access abstraction with UnitOfWork
- **Dependency Injection**: Service layer composition via built-in DI
- **Global Filters**: Model validation and exception handling
- **Hangfire Background Jobs**: Persistent job processing in same process (ARM32 optimized)
- **Options Pattern**: Strongly-typed configuration
- **BaseEntity Pattern**: Consistent audit trail and soft delete across all entities

## Database Architecture

**MediaButler Database** (`/data/mediabutler.db`):
- `TrackedFiles`: Main file tracking with BaseEntity audit properties
- `ProcessingLogs`: Operation audit trail with BaseEntity
- `UserPreferences`: User-specific settings with BaseEntity
- `SeriesPatterns`: Learned patterns for ML classification
- `TrainingData`: ML model training samples
- `FileOperations`: Operation log for rollback capability

**Hangfire Database** (`/data/mediabutler-hangfire.db`):
- Managed by Hangfire (auto-created)
- Job state, queues, statistics
- Retention: 7 days succeeded, 30 days failed
- Optimized with WAL mode for ARM32

**Note**: `ConfigurationSettings` table removed in favor of static configuration from `appsettings.json` only.

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

The system uses **ML.NET with FastText** for intelligent file classification.

### 6-Stage Classification Process

1. **Pre-processing**: Clean filename input (normalize separators, lowercase)
2. **Tokenization**: Extract meaningful tokens using `TokenizerService`
3. **Feature Extraction**: Identify series tokens, episode markers, quality tags
4. **ML.NET Prediction**: FastText model prediction with confidence scoring
5. **Alternative Predictions**: Generate top-N alternative suggestions
6. **Decision**: Output category + confidence score

**Confidence Thresholds:**
- `> 0.85`: Auto-classify (high confidence, pending confirmation)
- `0.50-0.85`: Suggest with alternatives (medium confidence)
- `< 0.50`: Likely new series (low confidence)

**Tokenization Example:**
```csharp
// Input: "The.Walking.Dead.S11E24.FINAL.ITA.ENG.1080p.mkv"
// 1. Normalize separators: dots/underscores → spaces
// 2. Extract series tokens before episode markers (S##E##)
// 3. Remove quality indicators (1080p, 720p, HDTV, BluRay)
// 4. Remove language codes (ITA, ENG, SUB, DUB)
// 5. Remove release tags (FINAL, REPACK, PROPER)
// Output: ["the", "walking", "dead"]
```

**Model Training Workflow:**
1. Users confirm file categorizations via Web UI or API
2. Training data accumulated in `TrainingData` table
3. Manual or scheduled training via `/api/training/trainModel` endpoint
4. Model versioning with automatic backup (max 3 versions retained)
5. Hot reload: New model loaded without service restart
6. Model performance metrics logged and tracked

## File States and Workflow

```
NEW → CLASSIFIED → CONFIRMED → MOVED
Additional states: ERROR, RETRY (max 3 attempts), IGNORED (terminal state)
```

**File State Transitions:**
- **IGNORED**: Terminal state that can be reached from any non-MOVED status
- Files marked as IGNORED are excluded from processing workflows
- IGNORED files cannot be moved (business rule enforcement)

**Processing Pipeline:**
1. File discovery via Hangfire recurring job (scheduled folder scans)
2. SHA256 hash calculation and DB storage (with BaseEntity audit)
3. ML.NET classification with confidence scoring (FastText model)
4. User confirmation required for all files
5. Physical file movement with transaction support
6. Feedback loop for model improvement (training data accumulation)

## API Endpoints Structure

Key endpoint categories:
- **File Operations**: `/api/files/*` - CRUD operations for tracked files
- **Batch Operations**: `/api/v1/file-actions/*` - Batch file processing and organization
- **Processing**: `/api/processing/*` - File processing workflows and ML operations
- **Training**: `/api/training/*` - ML model training and management
- **System Operations**: `/api/system/*` - System maintenance and configuration
- **Monitoring**: `/api/health`, `/api/stats`, `/api/metrics/*` - System health and metrics
- **Real-time**: SignalR hubs at `/notifications` and `/file-processing` - Live updates

For complete API documentation, see `docs/api-documentation.md`

## Background Processing Architecture

The system uses **Hangfire** in combined client+server mode for background job processing, optimized for ARM32 deployment.

### Hangfire Recurring Jobs

| Job Name | Purpose | Schedule | Queue |
|----------|---------|----------|-------|
| **FileDiscoveryJob** | Scan watch folders for new files | Dev: Every 5 min<br>Prod: Every 12 hours | `default` |
| **LogCleanupJob** | Delete log files older than 30 days | Daily at 2:00 AM | `low-priority` |
| **ModelTrainingJob** | Retrain ML model with accumulated data | Weekly (Sunday 3:00 AM) | `low-priority` |

### ARM32 Optimizations
- Worker count limited to 2 to prevent resource exhaustion
- Configurable queue priorities (critical, default, low-priority)
- Job retention: 7 days succeeded, 30 days failed
- WAL mode for SQLite to improve concurrent access
- Progress reporting via SignalR for real-time updates

## Configuration

Following "Simple Made Easy" principles, the system uses **pure static configuration** from `appsettings.json` only.

**Configuration Files:**
- `src/MediaButler.API/appsettings.json` - Base configuration
- `src/MediaButler.API/appsettings.Development.json` - Development overrides
- `src/MediaButler.API/appsettings.Production.json` - Production overrides

**Key Configuration Sections:**
- `MediaButler.Paths` - File system paths (watch folder, library, pending)
- `MediaButler.FileDiscovery` - File monitoring settings
- `MediaButler.ML` - ML classification and training settings
- `Hangfire.RecurringJobs` - Scheduled job configuration

For detailed configuration reference, see the Configuration section at the end of this document.

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
- **Abstraction Policy**: Use "Who, What, When, Where, Why" to separate concerns
- **Avoid Incidental Complexity**: Complexity from tool/construct choices is "your fault"

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
- Hangfire worker count limited to 2 concurrent jobs

## Real-time Features

The system provides real-time updates via two mechanisms:

**SignalR (Legacy .NET API):**
- `/notifications` - General system notifications
- `/file-processing` - File processing and batch operation updates

**Server-Sent Events / SSE (Go API - Preferred):**
- `/events` - Real-time event stream using SSE (EventSource API)
- `/events/stats` - Connection statistics

**Event Types (Both SignalR and SSE):**
- File scan progress (`scan.started`, `scan.found`, `scan.completed`)
- Move operations (`move.started`, `move.progress`, `move.completed`)
- ML training status (`training.started`, `training.completed`)
- Batch processing updates (`batch.started`, `batch.progress`, `batch.completed`, `batch.failed`)
- Error notifications (`error.move_failed`, `error.classification_failed`)
- Connection events (`connected` - SSE only)

## Test Strategy

MediaButler follows a comprehensive 3-tier testing strategy.

### Test Projects Structure
```
tests/
├── MediaButler.Tests.Unit/           # Fast, isolated unit tests (250+ tests)
├── MediaButler.Tests.Integration/    # Component integration tests (300+ tests)
└── MediaButler.Tests.Acceptance/     # End-to-end business scenarios (240+ tests)
```

**Total Test Coverage**: 520+ tests implemented (target: 790+)

### Testing Philosophy

Following Rich Hickey's principle that tests don't solve complexity but help verify simple systems:
- **Focus on Behavior**: Test what the code does, not how it does it
- **Simple Test Structure**: Given-When-Then pattern for clarity
- **Values Over State**: Test with immutable inputs and verify immutable outputs
- **Declarative Assertions**: Clear, intention-revealing test names

### Test Pyramid
```
    /\     Acceptance Tests (240+ tests, slow, high confidence)
   /  \    - End-to-end file processing workflows
  /____\   - API contract validation
 /      \
/__________\
Integration Tests (300+ tests, medium speed)
- Database operations with BaseEntity
- File system interactions
- ML model integration

Unit Tests (250+ tests, fast, low-level)
- Pure function testing
- Business logic validation
- Edge case coverage
```

### Quality Gates
- **Minimum 82% Code Coverage**: Focus on critical paths
- **All Tests Must Pass**: No skipped or ignored tests in CI
- **Performance Benchmarks**: Classification speed and memory usage tests
- **API Contract Validation**: Ensure backward compatibility

## Web UI Status (Blazor WebAssembly - .NET 10)

### Status Filtering Features

**Individual Status Filters**:
- **ALL**: Displays files across all statuses and categories
- **New**: Shows files just discovered but not yet processed
- **Classified**: Shows files with completed ML classification
- **Moved**: Shows files successfully organized to their final location
- **Error**: Shows files with processing errors requiring attention
- **Ignored**: Shows files marked as ignored by user

**Smart Auto-Refresh Features**:
- **New File Detection**: When SignalR detects a new file, automatically switches to "ALL" view
- **File Processing Updates**: Refreshes current view when file processing completes
- **Scan Results**: Auto-switches to "ALL" when folder scan discovers new files

**Server-Side Pagination**:
- **20 records per page**: Efficient loading with server-side pagination
- **Case-insensitive search**: Real-time search across filename and category (500ms debounce)
- **Column sorting**: Click column headers to sort by FileName, Category, Status, CreatedDate, LastUpdateDate
- **Default sorting**: LastUpdateDate descending (most recently updated first)

## Go API Migration Project (Active)

**Status**: Proof of Concept (PoC) - 95% Complete, Integration Testing Phase

MediaButler is undergoing a parallel Go API implementation to achieve significant performance improvements for ARM32 NAS deployment.

### Migration Goals

| Metric | .NET Baseline | Go Target | Improvement |
|--------|---------------|-----------|-------------|
| Memory (idle) | 150MB | 50MB | **66% reduction** |
| Memory (peak) | 280MB | 120MB | **57% reduction** |
| Response (p50) | 80ms | 20ms | **75% faster** |
| Response (p95) | 400ms | 150ms | **62% faster** |
| Binary size | 45MB | 12MB | **73% smaller** |
| Cold start | 3.5s | 0.8s | **77% faster** |

### Go Technology Stack

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Web Framework** | Chi Router | Lightweight (10KB), idiomatic Go |
| **Database** | SQLC | Compile-time SQL generation, ARM32-friendly |
| **Background Jobs** | Asynq | Redis-backed queue matching Hangfire |
| **Real-time** | Server-Sent Events (SSE) | Native HTTP streaming, browser EventSource API |
| **ML Integration** | HTTP → .NET Internal | Keep existing ML.NET service |
| **Config** | Viper | Multi-source config (JSON/ENV) |
| **Logging** | Zerolog | Zero-allocation, 10x faster |

### Go Development Commands

```bash
# Prerequisites (from MediaButler-Go directory)
brew install go sqlc

# Generate type-safe database code from SQL
make generate  # or: sqlc generate

# Download dependencies
make deps      # or: go mod download

# Build API server
make build                    # Output: bin/mediabutler-api
make build-arm32              # Cross-compile for ARM32

# Run Go API (development)
go run cmd/api/main.go -config configs/config.json
# API available at http://localhost:5002
# SSE available at http://localhost:5002/events

# Test SSE connection
curl -N http://localhost:5002/events
# Or using a browser: open http://localhost:5002/events

# Run both APIs concurrently for testing
# Terminal 1: .NET API
dotnet run --project src/MediaButler.API
# Terminal 2: Go API
cd src/MediaButler-Go && go run cmd/api/main.go -config configs/config.json
# Terminal 3: Web UI (connects to Go API via SSE)
dotnet run --project src/MediaButler.Web
```

### Implementation Status

**✅ Week 1 Complete (Foundation):**
- Go module and project structure
- Database schema export from EF Core
- Configuration management with Viper
- Domain entities (TrackedFile, FileStatus, BaseEntity)
- Result<T> pattern for error handling
- SQLC query definitions (40+ queries)
- Repository pattern implementation
- UnitOfWork pattern for transactions

**✅ Week 2 Complete (Service Layer):**
- FileService implementation with full business logic
- ML HTTP client (calls .NET internal endpoint `/internal/ml/classify`)
- StatsService implementation
- 1,100+ lines of comprehensive unit tests

**✅ Week 3 Complete (HTTP API Layer):**
- All 15 Tier 1 endpoints implemented (files, health, processing)
- Complete middleware stack (CORS, logging, recovery, request ID)
- Chi router with full dependency injection
- Graceful shutdown and signal handling

**✅ Week 3.5 Complete (Real-time Communication):**
- Server-Sent Events (SSE) broker implementation
- 11 event types matching SignalR events
- Web UI migrated from SignalR to SSE client
- EventSource-compatible streaming
- Internal ML endpoint in .NET API

**🔄 Week 4 In Progress (Deployment & Validation):**
- Integration tests with live database
- Docker ARM32 build configuration
- Performance benchmarks on ARM32 hardware
- Go/No-Go decision

For detailed Go migration documentation, see:
- `src/MediaButler-Go/README.md`
- `docs/eager-purring-puzzle.md`

---

## Detailed Configuration Reference

### MediaButler.Paths Configuration

| Setting | Description | Example |
|---------|-------------|---------|
| `MediaLibrary` | Target directory for organized media files | `/library` |
| `WatchFolder` | Primary directory monitored for new files | `/watch` |
| `PendingReview` | Directory for files awaiting user confirmation | `/tmp/mediabutler/pending` |

### MediaButler.FileDiscovery Configuration

| Setting | Description | Example |
|---------|-------------|---------|
| `WatchFolders` | Array of directories to monitor | `["/watch"]` |
| `EnableFileSystemWatcher` | Enable real-time file monitoring | `true` |
| `ScanIntervalMinutes` | Interval between periodic scans | `10` |
| `FileExtensions` | Supported file extensions | `[".mkv", ".mp4", ".avi"]` |
| `ExcludePatterns` | Regex patterns for files to ignore | `[".*tmp", ".*part"]` |
| `MinFileSizeMB` | Minimum file size threshold | `1` |
| `DebounceDelaySeconds` | Delay before processing file changes | `8` |
| `MaxConcurrentScans` | Maximum concurrent scanning operations | `1` |

### MediaButler.ML Configuration

**Core ML Settings:**

| Setting | Description | Example |
|---------|-------------|---------|
| `ModelPath` | Directory containing ML model files | `"models"` |
| `ActiveModelVersion` | Current model version identifier | `"1.0.0"` |
| `AutoClassifyThreshold` | Confidence threshold for auto-classification | `0.85` |
| `SuggestionThreshold` | Minimum confidence for suggestions | `0.50` |
| `MaxClassificationTimeMs` | Maximum time for classification | `500` |
| `MaxAlternativePredictions` | Number of alternative suggestions | `3` |
| `EnableBatchProcessing` | Enable batch processing | `true` |
| `MaxBatchSize` | Maximum files per batch | `50` |

**Tokenization Settings:**

| Setting | Description | Example |
|---------|-------------|---------|
| `NormalizeSeparators` | Convert dots/underscores to spaces | `true` |
| `RemoveQualityIndicators` | Strip quality tags (1080p, 720p) | `true` |
| `RemoveLanguageCodes` | Remove language codes (ITA, ENG) | `true` |
| `RemoveReleaseTags` | Strip release tags (FINAL, REPACK) | `true` |
| `ConvertToLowercase` | Normalize to lowercase | `true` |
| `MinTokenLength` | Minimum character length for tokens | `2` |

**Training Settings:**

| Setting | Description | Example |
|---------|-------------|---------|
| `TrainingRatio` | Fraction of data for training | `0.7` |
| `ValidationRatio` | Fraction for validation | `0.2` |
| `NumberOfIterations` | Training iterations | `100` |
| `LearningRate` | Learning rate for training | `0.1` |
| `UseEarlyStopping` | Stop if validation plateaus | `true` |
| `MinimumAccuracy` | Minimum acceptable accuracy | `0.75` |

### Hangfire Configuration

| Setting | Description | Example |
|---------|-------------|---------|
| `Server.ServerName` | Unique server instance name | `"mediabutler-api-worker"` |
| `Server.WorkerCount` | Number of concurrent workers | `2` |
| `Server.Queues` | Priority queues for processing | `["critical", "default", "low-priority"]` |
| `Server.HeartbeatInterval` | Worker heartbeat interval | `"00:00:30"` |

### ARM32 Optimization Settings

| Setting | Description | Example |
|---------|-------------|---------|
| `MemoryThresholdMB` | Maximum memory usage threshold | `200` |
| `AutoGCTriggerMB` | Memory level to trigger GC | `150` |
| `PerformanceThresholdMs` | Maximum acceptable operation time | `1000` |
| `MaxLogFileSizeMB` | Maximum log file size | `50` |
