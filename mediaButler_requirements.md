# 🎩 MediaButler - Requirements & Architecture Document

> Your Personal Media Organizer - Organizing your TV series library with intelligence and elegance

[![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)]()
[![Platform](https://img.shields.io/badge/platform-ARM32%20|%20x64-green.svg)]()
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)]()

## 📋 Project Overview

**MediaButler** is an intelligent file organization system for TV series that uses machine learning to automatically categorize and move video files based on their filenames. The system learns from user feedback to improve accuracy over time.

**Tagline**: "Your Personal Media Organizer"  
**Secondary**: "Organizing your series, silently and efficiently"

## 🎯 Core Requirements

### Platform & Deployment
- **Multi-platform**: Web, Android App, REST API
- **Deployment Target**: NAS ARM32 with 1GB RAM
- **Architecture**: API-first design
- **User Model**: Single user, no authentication required
- **Performance Constraints**: Optimized for low memory usage (<300MB footprint)

### File Processing
- **File Types**: TV series video files (each file is an episode)
- **Category Definition**: Category = TV series name (no season subdivision)
- **Identification**: SHA256 hash for unique file identification
- **Related Files**: Subtitles (.srt, .sub, .ass) and metadata (.nfo) moved together but not tracked in DB

### Machine Learning
- **Classification Based On**: Filename only (primary), file size (secondary)
- **Model Type**: Lightweight local model (FastText recommended, ~20MB)
- **Training Data**: 2000 pre-categorized examples available
- **Incremental Learning**: User confirmations feed back into model
- **Training Frequency**: Weekly or on-demand via API
- **Accuracy Priority**: Precision over performance (processing <50 files/minute acceptable)
- **Category Management**: Dynamic/infinite categories, auto-create new series with low confidence

## 🤖 ML Model Architecture

### Classification Pipeline
```
1. PRE-PROCESSING
   Input: "The.Walking.Dead.S11E24.FINAL.ITA.ENG.1080p.mkv"
   ↓
2. TOKENIZATION
   Tokens: ["the", "walking", "dead", "s11e24", "final", "ita", "eng", "1080p"]
   ↓
3. FEATURE EXTRACTION
   - Series tokens: ["the", "walking", "dead"]
   - Episode markers: ["s11e24"]
   - Quality/Lang tags: ["1080p", "ita", "eng"]
   ↓
4. EMBEDDING
   Vector representation (dim=50-100)
   ↓
5. SIMILARITY MATCHING
   Compare with known series embeddings
   ↓
6. DECISION
   Category + Confidence Score
```

### Confidence Thresholds
- `> 0.85`: Auto-classify (but always pending confirmation)
- `0.50-0.85`: Suggest with alternatives
- `< 0.50`: Likely new series

## 📊 Database Schema

### Core Tables
1. **TrackedFiles**: Main file tracking (hash, path, category, status, confidence)
2. **PendingConfirmations**: Files awaiting user confirmation
3. **SeriesPatterns**: Learned patterns for each series
4. **TrainingData**: Samples for ML model training
5. **Jobs**: Background job tracking
6. **FileOperations**: Operation log for rollback capability
7. **ModelConfig**: ML model configuration and versioning
8. **ReconciliationLog**: File system/DB sync logs

### File States
```
NEW → CLASSIFIED → CONFIRMED → MOVED
Additional states: ERROR, RETRY (max 3 attempts)
```

### Update Strategy
- Database updates AFTER successful file operations
- Transaction support for bulk operations
- Retry logic with exponential backoff (5s, 30s, 60s)
- DB as single source of truth
- Periodic reconciliation job for filesystem/DB alignment

## 📁 Folder Structure & Organization

### Organization Rules
- **Structure**: Flat (series folders only, no season subfolders)
- **Folder Naming**: UPPERCASE category name as confirmed by user
- **Character Sanitization**: Remove invalid filesystem characters (<>:"/\|?*)
- **File Naming**: Keep original filename unchanged
- **Destination Path**: User-configurable via API
- **Permissions**: Standard 755 for created directories
- **Space Check**: Verify available space before move operations

### Example Structure
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

## 📁 Project Structure

```
MediaButler/
├── src/
│   ├── MediaButler.API/           # .NET 8 Minimal API
│   ├── MediaButler.Core/          # Domain models, interfaces
│   ├── MediaButler.Data/          # EF Core, SQLite
│   ├── MediaButler.ML/            # Classification engine
│   ├── MediaButler.Services/      # Business logic
│   ├── MediaButler.Web/           # Web UI (Blazor/SvelteKit)
│   └── MediaButler.Mobile/        # Android app
├── tests/
│   ├── MediaButler.Tests.Unit/
│   └── MediaButler.Tests.Integration/
├── docker/
│   └── Dockerfile.arm32
├── docs/
│   ├── api-documentation.md
│   └── deployment-guide.md
├── models/                        # ML models storage
├── configs/
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── appsettings.Production.json
└── README.md
```

## 🔄 Workflow

### Processing Pipeline
1. **File Discovery**: FileWatcher detects new files → Calculate hash → Add to DB
2. **Classification**: ML model assigns category with confidence score
3. **User Confirmation**: All files require user confirmation before moving
4. **File Movement**: Physical move → Update DB → Handle conflicts (rename with (2))
5. **Feedback Loop**: Confirmed categories improve model

### Conflict Resolution
- Duplicate files: Copy with "(2)" suffix and notify user
- Automatic versioning enabled
- Operation rollback capability via logs

## 🔔 Real-time Notifications

### Events to Notify (via SSE/WebSocket)

#### SCAN_JOB
- `scan.started`: { timestamp, folder }
- `scan.found`: { count, timestamp }
- `scan.completed`: { total, new, existing, duration }

#### MOVE_JOB
- `move.started`: { jobId, totalFiles }
- `move.progress`: { jobId, fileName, current, total }
- `move.completed`: { jobId, success, failed, duration }

#### TRAINING
- `training.started`: { timestamp }
- `training.completed`: { accuracy, samplesUsed, duration }

#### ERROR
- `error.move_failed`: { fileName, reason, retryCount }
- `error.classification_failed`: { fileName, reason }

## 💾 Configuration

### MediaButler Settings (appsettings.json)
```json
{
  "MediaButler": {
    "Version": "1.0.0",
    "Instance": {
      "Name": "MediaButler-NAS",
      "Description": "Personal Media Organization Service"
    },
    "Paths": {
      "WatchFolder": "/mnt/nas/downloads/completed",
      "MediaLibrary": "/mnt/nas/TV Series",
      "PendingReview": "/mnt/nas/MediaButler/Pending",
      "ModelsPath": "./models"
    },
    "ML": {
      "ModelType": "FastText",
      "ConfidenceThreshold": 0.75,
      "MaxConcurrentClassifications": 2,
      "RetrainInterval": "Weekly"
    },
    "Butler": {
      "ScanInterval": 60,
      "MaxRetries": 3,
      "RetryDelaySeconds": [5, 30, 60],
      "AutoOrganize": false
    }
  }
}
```

### Features
- Backup/restore of configurations
- Dynamic path configuration via API
- Settings immediately operational after change
- Configuration stored in appsettings.json

## 🛠️ Technical Stack

### Recommended Architecture
- **Backend**: .NET 8 Minimal API with SQLite
- **ML Service**: FastText in Python microservice or ONNX Runtime in .NET
- **Real-time**: SignalR or Server-Sent Events
- **File Monitoring**: FileSystemWatcher
- **Database**: SQLite with Entity Framework Core
- **Logging**: Serilog with rolling file logs

### API Endpoints
```
# File Operations
POST /api/scan/folder
GET  /api/files
GET  /api/files/{hash}
POST /api/files/scan
PUT  /api/files/{hash}/confirm
POST /api/files/{hash}/move

# Bulk Operations
GET  /api/pending
POST /api/confirm/bulk

# ML Operations
POST /api/classify
POST /api/ml/train
GET  /api/ml/status

# Configuration
GET  /api/config/paths
PUT  /api/config/paths
GET  /api/backup
POST /api/restore

# Maintenance
POST /api/maintenance/reconcile
GET  /api/jobs/current
GET  /api/jobs/{id}/progress

# Real-time
GET  /api/events (SSE stream)

# Monitoring
GET  /api/health
GET  /api/stats
GET  /api/files/status-summary
GET  /api/files/errors
```

## 🏷️ Branding Elements

```yaml
Name: MediaButler
Tagline: "Your Personal Media Organizer"
Secondary: "Organizing your series, silently and efficiently"

Color Palette:
  Primary: #2C3E50    # Dark Blue-Grey (Professional)
  Secondary: #E74C3C  # Subtle Red (Accent)
  Success: #27AE60    # Green (Confirmed)
  Warning: #F39C12    # Orange (Pending)
  Background: #ECF0F1 # Light Grey
  
Logo Concept:
  - Bow tie icon combined with folder
  - Minimalist butler silhouette
  - Clean, professional aesthetic
```

## 🚀 Development Plan

### Phase Priority
1. **Phase 1**: Core API with database (2-3 days)
2. **Phase 2**: ML classification system (3-4 days)
3. **Phase 3**: File watcher and movement logic (2-3 days)
4. **Phase 4**: Real-time notifications (2 days)
5. **Phase 5**: Web UI (3-4 days)
6. **Phase 6**: Android app (4-5 days)

### Claude Code Integration
- Tasks divided into small, manageable chunks
- Each phase broken into 2-5 day sprints
- Clear task definitions for AI-assisted development

### First Task for Implementation
```markdown
Task: Initialize MediaButler API Project
Goal: Create the basic API structure with health checks and database setup
Time: 2-3 hours

Steps:
1. Create .NET 8 Minimal API project structure
2. Setup SQLite with EF Core
3. Create domain models (TrackedFile, etc.)
4. Add Serilog logging
5. Create health check endpoint
6. Add Swagger documentation
7. Create docker file for ARM32

Deliverables:
- Working API with /health endpoint
- SQLite database initialized
- Swagger UI available
- Basic logging configured
```

## ❓ OPEN QUESTIONS

### ML Model Implementation
1. **Python vs .NET for ML**: Should the ML component be a separate Python microservice (more flexible, better libraries) or integrated in .NET with ONNX Runtime (simpler deployment)?
2. **Tokenizer Details**: Need to define exact tokenization rules for series name extraction from various filename patterns
3. **Embedding Storage**: How to efficiently store and query embedding vectors in SQLite?
4. **Model Versioning**: Strategy for managing multiple model versions and rollback?

### Performance & Optimization
1. **Batch Processing Size**: Optimal batch size for bulk confirmations on ARM32?
2. **Memory Management**: Specific strategies for staying under 300MB with concurrent operations?
3. **Index Strategy**: Which database indexes are critical for performance?
4. **Caching Strategy**: What should be cached in memory vs always read from DB?

### User Interface
1. **Web Framework**: Blazor WASM vs SvelteKit for minimal resource usage?
2. **Mobile Approach**: MAUI vs Flutter for Android app?
3. **Real-time Updates**: SignalR vs SSE vs WebSockets - best for ARM32?
4. **UI Features**: Dashboard layout, statistics visualization, bulk selection interface?

### Edge Cases
1. **Multi-episode Files**: How to handle files containing multiple episodes (S01E01-E02)?
2. **Naming Variations**: Strategy for handling series name variations without user-defined aliases?
3. **Special Episodes**: How to categorize specials, extras, bonus content?
4. **Incomplete Downloads**: How to detect and handle partially downloaded files?

### Operational
1. **Monitoring**: What metrics to expose for system health monitoring?
2. **Archive Strategy**: When/how to archive old moved files from DB?
3. **Import Existing Library**: Need tool to import already organized files?
4. **Export Features**: Export classification rules/patterns for backup or sharing?

### Integration
1. **Plex/Jellyfin Integration**: Should the system integrate with media servers?
2. **Notification Systems**: Email, push notifications, or just web UI?
3. **External APIs**: Integration with TVDB/TMDB for series name validation?
4. **Torrent Client Integration**: Auto-watch completed downloads folder?

## 📝 Project Initialization Commands

```bash
# Create solution
dotnet new sln -n MediaButler

# Create projects
dotnet new webapi -n MediaButler.API -f net8.0
dotnet new classlib -n MediaButler.Core -f net8.0
dotnet new classlib -n MediaButler.Data -f net8.0
dotnet new classlib -n MediaButler.Services -f net8.0
dotnet new classlib -n MediaButler.ML -f net8.0

# Add projects to solution
dotnet sln add src/MediaButler.API/MediaButler.API.csproj
dotnet sln add src/MediaButler.Core/MediaButler.Core.csproj
dotnet sln add src/MediaButler.Data/MediaButler.Data.csproj
dotnet sln add src/MediaButler.Services/MediaButler.Services.csproj
dotnet sln add src/MediaButler.ML/MediaButler.ML.csproj

# Add packages to API project
cd src/MediaButler.API
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Serilog.AspNetCore
dotnet add package Swashbuckle.AspNetCore

# Add packages to Data project
cd ../MediaButler.Data
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design

# Initialize Git
cd ../..
git init
echo "# MediaButler" >> README.md
git add .
git commit -m "Initial commit: MediaButler project structure"
```

---

*Document Version: 1.0*  
*Last Updated: 2024*  
*Status: Requirements Complete - Ready for Implementation*  
*Project Name: MediaButler*  
*Author: Luca*
