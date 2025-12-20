# MediaButler

[![Version](https://img.shields.io/badge/version-1.1.0-blue.svg)]()
[![Platform](https://img.shields.io/badge/platform-ARM32%20|%20ARM64%20|%20x64-green.svg)]()
[![.NET](https://img.shields.io/badge/.NET-8.0%20|%209.0%20|%2010.0-purple.svg)]()
[![Docker](https://img.shields.io/badge/docker-ready-blue.svg)]()

**MediaButler** is an intelligent TV series file organization system that uses machine learning to automatically categorize and move video files based on filenames. The system learns from user feedback to improve accuracy over time.

## Features

- **Multi-Platform Support**: Web UI (Blazor .NET 10), Android App (MAUI .NET 9), REST API (.NET 8)
- **API-First Design**: Optimized for NAS ARM32 deployment (1GB RAM, <300MB memory footprint)
- **ML.NET Classification**: FastText-based text classification with ~20MB model size
- **Hot Reload Support**: Model updates without service restart
- **LRU Prediction Caching**: Improved performance for frequently classified files
- **Weekly Auto-Retraining**: Automatic model improvement via scheduled Hangfire jobs
- **File Identification**: SHA256 hashing for reliable file tracking
- **Smart File Handling**: Automatically moves related files (subtitles, metadata, NFO)
- **Single User Model**: No authentication required (designed for personal NAS use)
- **Real-Time Updates**: SignalR for live file processing notifications

## Quick Start

### Prerequisites

- .NET 8 SDK or later
- SQLite (included)
- 1GB RAM minimum (ARM32 NAS compatible)

### Run Locally

```bash
# Clone the repository
git clone https://github.com/yourusername/mediabutler.git
cd mediabutler/MediaButler

# Build the solution
dotnet build

# Run the API (includes background worker)
dotnet run --project src/MediaButler.API

# Access the API
# Swagger UI: http://localhost:5000/swagger
# Hangfire Dashboard: http://localhost:5000/hangfire

# Run the Web UI (in separate terminal)
dotnet run --project src/MediaButler.Web
# Access Web UI: http://localhost:5001
```

### Docker Deployment (ARM32)

```bash
# Build ARM32 Docker image
docker build -f docker/Dockerfile.arm32 -t mediabutler:arm32 .

# Run container
docker run -d \
  -p 5000:5000 \
  -v /path/to/watch:/watch \
  -v /path/to/library:/library \
  -v /path/to/data:/data \
  --name mediabutler \
  mediabutler:arm32
```

## Architecture

MediaButler follows **Vertical Slice Architecture** based on Rich Hickey's "Simple Made Easy" principles:

- **Compose, Don't Complect**: Independent components rather than braided layers
- **Values Over State**: Immutable data structures and explicit result patterns
- **Declarative Over Imperative**: Clear, intention-revealing code
- **Single Responsibility**: Each component has one role/task/objective

### System Components

```
MediaButler.API (.NET 8)
├── REST API endpoints (Controllers)
├── Hangfire Background Worker (combined mode)
│   ├── Recurring Jobs
│   │   ├── FileDiscoveryJob (Dev: every 5 min | Prod: every 12 hours)
│   │   ├── LogCleanupJob (Daily at 2:00 AM)
│   │   └── ModelTrainingJob (Weekly Sunday at 3:00 AM)
│   └── Batch Jobs (file processing)
├── SignalR Hubs (real-time notifications)
└── ML.NET FastText Classification

MediaButler.Web (.NET 10)
└── Blazor WebAssembly UI

MediaButler.Mobile (.NET 9)
└── MAUI Android App

Databases (SQLite)
├── MediaButler Database (/data/mediabutler.db)
└── Hangfire Database (/data/mediabutler-hangfire.db)
```

### Recurring Jobs (Hangfire)

| Job | Purpose | Schedule | Queue |
|-----|---------|----------|-------|
| **FileDiscoveryJob** | Scan watch folders for new files | Dev: Every 5 min<br>Prod: Every 12 hours | `default` |
| **LogCleanupJob** | Delete logs older than 30 days | Daily at 2:00 AM | `low-priority` |
| **ModelTrainingJob** | Retrain ML model with accumulated data | Weekly (Sunday 3:00 AM) | `low-priority` |

## Machine Learning Pipeline

MediaButler uses **ML.NET with FastText** for intelligent file classification:

1. **Tokenization**: Extract series name from filename (remove quality tags, episode markers, etc.)
2. **ML.NET Prediction**: FastText model predicts category with confidence scoring
3. **Alternative Suggestions**: Generate top-N alternative predictions
4. **User Confirmation**: All files require user confirmation before moving
5. **Training Data Accumulation**: Confirmed categorizations feed back into training data
6. **Weekly Retraining**: Automatic model improvement every Sunday at 3:00 AM

**Confidence Thresholds:**
- `> 0.85`: High confidence (auto-classify pending confirmation)
- `0.50-0.85`: Medium confidence (suggest with alternatives)
- `< 0.50`: Low confidence (likely new series)

## File Organization

MediaButler organizes files into a flat folder structure:

```
/library/
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
- Character sanitization for folder names

## Configuration

Configuration is managed via `appsettings.json` files in `src/MediaButler.API/`:

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides

**Key Configuration Sections:**
- `MediaButler.Paths` - File system paths (watch folder, library, pending)
- `MediaButler.FileDiscovery` - File monitoring settings
- `MediaButler.ML` - ML classification and training settings
- `Hangfire.RecurringJobs` - Scheduled job configuration

## Development

### Build and Test

```bash
# Build entire solution
dotnet build

# Run all tests (790+ comprehensive tests)
dotnet test

# Run specific test project
dotnet test tests/MediaButler.Tests.Unit
dotnet test tests/MediaButler.Tests.Integration
dotnet test tests/MediaButler.Tests.Acceptance

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Database Migrations

```bash
# Add new migration
dotnet ef migrations add <MigrationName> --project src/MediaButler.Data --startup-project src/MediaButler.API

# Update database (migrations auto-apply on startup)
dotnet ef database update --project src/MediaButler.Data --startup-project src/MediaButler.API
```

## Performance & Constraints

Designed for ARM32 NAS with limited resources:

- **Target Memory**: <300MB footprint
- **Processing Rate**: <50 files/minute (precision over speed)
- **Model Size**: ~20MB FastText model
- **Database**: SQLite for minimal overhead
- **Worker Count**: 2 concurrent Hangfire workers (ARM32 optimized)

## Documentation

- [CLAUDE.md](CLAUDE.md) - Complete development guide
- [API Documentation](docs/api-documentation.md) - REST API reference
- [Deployment Guide](docs/deployment-guide.md) - Production deployment
- [Development Plan](docs/dev_planning.md) - Project roadmap

## Technology Stack

- **.NET 8** - API and Services
- **.NET 10** - Blazor WebAssembly Web UI
- **.NET 9** - MAUI Android Mobile App
- **ML.NET** - Machine learning framework
- **FastText** - Text classification algorithm
- **SQLite** - Database (EF Core)
- **Hangfire** - Background job processing
- **SignalR** - Real-time notifications
- **Serilog** - Structured logging
- **Radzen.Blazor** - Web UI components

## License

[Your License Here]

## Contributing

[Your Contributing Guidelines Here]
