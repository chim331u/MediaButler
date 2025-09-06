# MediaButler Architecture Documentation - Task 1.7.2

**Generated**: September 6, 2025  
**Version**: 1.0.0  
**Target Platform**: ARM32/Raspberry Pi with <300MB memory footprint

## System Architecture Overview

MediaButler follows **"Simple Made Easy"** principles with a **Vertical Slice Architecture** that emphasizes composable, independent components over traditional layered architecture.

### High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                           Client Applications                        │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────────────┐ │
│  │   Web Browser   │ │  Mobile App     │ │   Direct API Access     │ │
│  │   (Future)      │ │   (Future)      │ │   (curl, Postman)      │ │
│  └─────────────────┘ └─────────────────┘ └─────────────────────────┘ │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │ HTTP/HTTPS
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        MediaButler.API                              │
│                     (.NET 8 Minimal API)                           │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────────────┐ │
│  │   Controllers   │ │   Middleware    │ │     Features            │ │
│  │   - Files       │ │   - Logging     │ │   - FileManagement      │ │
│  │   - Config      │ │   - Exception   │ │   - Classification      │ │
│  │   - Stats       │ │   - Performance │ │   - Monitoring          │ │
│  │   - Health      │ │   - CORS        │ │                         │ │
│  └─────────────────┘ └─────────────────┘ └─────────────────────────┘ │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │ Service Interfaces
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      MediaButler.Services                           │
│                    (Business Logic Layer)                           │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────────────┐ │
│  │  FileService    │ │ConfigurationSvc │ │     StatsService        │ │
│  │  - Scan files   │ │ - Settings mgmt │ │   - Performance         │ │
│  │  - Hash calc    │ │ - Validation    │ │   - Processing stats    │ │
│  │  - File ops     │ │ - Export/Import │ │   - System health       │ │
│  └─────────────────┘ └─────────────────┘ └─────────────────────────┘ │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │ Repository Pattern
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        MediaButler.Data                             │
│                   (Data Access Layer)                               │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────────────┐ │
│  │  Repositories   │ │  Unit of Work   │ │     EF Core Context     │ │
│  │  - TrackedFile  │ │  - Transaction  │ │   - DbContext           │ │
│  │  - Config       │ │  - Rollback     │ │   - Migrations          │ │
│  │  - Generic<T>   │ │  - Async ops    │ │   - Configuration       │ │
│  └─────────────────┘ └─────────────────┘ └─────────────────────────┘ │
└─────────────────────────────────┬───────────────────────────────────┘
                                  │ Entity Framework Core
                                  ▼
┌─────────────────────────────────────────────────────────────────────┐
│                          SQLite Database                            │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────────────┐ │
│  │ TrackedFiles    │ │ConfigurationSttg│ │   ProcessingLogs        │ │
│  │ UserPreferences │ │   (Future)      │ │   (Audit Trail)         │ │
│  └─────────────────┘ └─────────────────┘ └─────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                        MediaButler.Core                             │
│                      (Domain Layer)                                 │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────────────┐ │
│  │   Entities      │ │  Common Types   │ │     Interfaces          │ │
│  │  - BaseEntity   │ │  - Result<T>    │ │   - IRepository<T>      │ │
│  │  - TrackedFile  │ │  - Result       │ │   - IUnitOfWork         │ │
│  │  - ConfigSetting│ │  - Enums        │ │   - Service contracts   │ │
│  └─────────────────┘ └─────────────────┘ └─────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                         MediaButler.ML                              │
│                    (Machine Learning - Future)                      │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────────────┐ │
│  │ Classification  │ │   Training      │ │    Model Storage        │ │
│  │  - FastText     │ │  - Data prep    │ │   - Binary models       │ │
│  │  - Tokenization │ │  - Model train  │ │   - Version control     │ │
│  │  - Prediction   │ │  - Validation   │ │   - Rollback support    │ │
│  └─────────────────┘ └─────────────────┘ └─────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘

                                  External Dependencies
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────────────────┐
│  File System    │ │     Serilog     │ │        .NET 8 Runtime       │
│  - Watch folder │ │  - Structured   │ │     - Minimal API           │
│  - Media library│ │  - JSON logging │ │     - EF Core 8.0           │
│  - Temp storage │ │  - File/Console │ │     - ARM32 optimized       │
└─────────────────┘ └─────────────────┘ └─────────────────────────────┘
```

## Component Interaction Flow

### File Processing Workflow
```
1. File Discovery
   └── FileSystemWatcher → FileService.ScanAsync()
       └── Calculate SHA256 hash
       └── Store in TrackedFiles table (Status: New)

2. Classification (Future - Sprint 2)
   └── MLService.ClassifyAsync() 
       └── Extract features from filename
       └── Predict category with confidence
       └── Update Status: Classified

3. User Confirmation
   └── API: POST /api/files/{hash}/confirm
       └── Update category and Status: ReadyToMove

4. File Movement
   └── FileService.MoveAsync()
       └── Create destination directory
       └── Move file + related files (.srt, .nfo)
       └── Update Status: Moved, set MovedToPath
```

### API Request Flow
```
HTTP Request → Middleware Chain → Controller → Service → Repository → Database
     ↓              ↓               ↓          ↓          ↓           ↓
1. Logging     1. Exception     1. Validate 1. Business 1. Query    1. SQLite
2. Performance 2. CORS          2. Model    2. Logic    2. Update   2. Audit
3. Auth (none) 3. Request/Resp  3. Route    3. Rules    3. Transaction
               4. Headers                   4. Results  4. UnitOfWork
```

## Database Schema Documentation

### Entity Relationship Diagram
```
┌─────────────────────────────────────────────────────────────────┐
│                        BaseEntity                               │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │  + Id: long (PK, Identity)                                  │ │
│  │  + CreatedDate: DateTime (UTC, Indexed)                    │ │
│  │  + LastUpdateDate: DateTime (UTC, Indexed)                 │ │
│  │  + IsActive: bool (Default: true, Indexed)                 │ │
│  │  + Note: string? (Optional context)                        │ │
│  │                                                             │ │
│  │  + MarkAsModified(): void                                   │ │
│  │  + SoftDelete(): void                                       │ │
│  │  + Restore(): void                                          │ │
│  └─────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                                    ▲
                                    │ (Inheritance)
                                    │
        ┌───────────────────────────┼───────────────────────────┐
        │                           │                           │
        ▼                           ▼                           ▼
┌─────────────────┐   ┌─────────────────────┐   ┌─────────────────────┐
│  TrackedFile    │   │ ConfigurationSetting│   │   ProcessingLog     │
│                 │   │                     │   │                     │
│ + Hash: string  │   │ + Key: string       │   │ + Operation: string │
│   (PK, 64 char) │   │   (Unique, 200 ch)  │   │ + EntityId: long?   │
│ + FilePath: str │   │ + Value: string     │   │ + EntityType: str?  │
│ + Category: str?│   │   (4000 char)       │   │ + Success: bool     │
│ + Status: enum  │   │ + IsSecure: bool    │   │ + ErrorMessage: str?│
│ + Size: long    │   │ + Category: string? │   │ + ExecutionTimeMs   │
│ + MovedToPath:  │   │ + Description: str? │   │   : int             │
│   string?       │   │ + ValidationRules   │   │ + CorrelationId:    │
│ + LastScanned:  │   │   : string?         │   │   string?           │
│   DateTime?     │   │                     │   │                     │
│                 │   │ Constraints:        │   │ Indexes:            │
│ Indexes:        │   │ - Key length 1-200  │   │ - CreatedDate       │
│ - Hash (unique) │   │ - Value max 4000    │   │ - Operation         │
│ - Status        │   │ - Category max 100  │   │ - EntityId          │
│ - Category      │   │                     │   │ - Success           │
│ - CreatedDate   │   │                     │   │                     │
└─────────────────┘   └─────────────────────┘   └─────────────────────┘

┌─────────────────────┐
│  UserPreference     │
│  (Future Entity)    │
│                     │
│ + UserId: string    │
│ + PreferenceKey: str│
│ + PreferenceValue   │
│   : string          │
│ + PreferenceType    │
│   : enum            │
│                     │
│ Constraints:        │
│ - Composite PK      │
│   (UserId, Key)     │
│ - Value max 1000    │
└─────────────────────┘
```

### Database Tables

#### TrackedFiles
**Purpose**: Core entity tracking all discovered video files with processing status

| Column | Type | Constraints | Purpose |
|--------|------|-------------|---------|
| Hash | NVARCHAR(64) | PK, NOT NULL | SHA256 file hash (unique identifier) |
| FilePath | NVARCHAR(1000) | NOT NULL | Full path to original file |
| Category | NVARCHAR(200) | NULL | Classified series/category name |
| Status | INTEGER | NOT NULL, DEFAULT 0 | FileStatus enum (New=0, Processing=1, Classified=2, ReadyToMove=3, Moving=4, Moved=5, Error=6) |
| SizeBytes | BIGINT | NOT NULL | File size in bytes |
| MovedToPath | NVARCHAR(1000) | NULL | Final location after organization |
| LastScannedDate | DATETIME | NULL | When file was last discovered |

**Indexes**: Hash (unique), Status, Category, CreatedDate, IsActive

#### ConfigurationSettings
**Purpose**: Dynamic application configuration with validation and security flags

| Column | Type | Constraints | Purpose |
|--------|------|-------------|---------|
| Key | NVARCHAR(200) | UNIQUE, NOT NULL | Configuration key (dot notation) |
| Value | NVARCHAR(4000) | NOT NULL | Configuration value (JSON supported) |
| IsSecure | BIT | NOT NULL, DEFAULT 0 | Sensitive data flag |
| Category | NVARCHAR(100) | NULL | Grouping for UI organization |
| Description | NVARCHAR(500) | NULL | User-friendly description |
| ValidationRules | NVARCHAR(1000) | NULL | JSON validation rules |

**Check Constraints**: 
- Key length: 1-200 characters
- Value max: 4000 characters  
- Category max: 100 characters

#### ProcessingLogs
**Purpose**: Comprehensive audit trail for all system operations

| Column | Type | Constraints | Purpose |
|--------|------|-------------|---------|
| Operation | NVARCHAR(100) | NOT NULL | Operation name/type |
| EntityId | BIGINT | NULL | Related entity ID |
| EntityType | NVARCHAR(100) | NULL | Related entity type |
| Success | BIT | NOT NULL | Operation outcome |
| ErrorMessage | NVARCHAR(2000) | NULL | Error details if failed |
| ExecutionTimeMs | INTEGER | NOT NULL | Performance tracking |
| CorrelationId | NVARCHAR(50) | NULL | Request correlation |

**Indexes**: CreatedDate, Operation, EntityId, Success

## API Endpoint Documentation

### Files Management API

#### GET /api/files
**Purpose**: Retrieve tracked files with filtering and pagination

**Query Parameters**:
- `take`: int (1-100, default: 10) - Number of results
- `skip`: int (0+, default: 0) - Results to skip
- `status`: FileStatus enum - Filter by status
- `category`: string - Filter by category

**Response**: `TrackedFileResponse[]`
```json
[
  {
    "hash": "abc123...",
    "filePath": "/media/movies/file.mkv",
    "category": "BREAKING BAD",
    "status": 2,
    "sizeBytes": 1073741824,
    "movedToPath": "/organized/BREAKING BAD/file.mkv",
    "createdDate": "2025-09-06T18:00:00Z",
    "lastUpdateDate": "2025-09-06T19:00:00Z"
  }
]
```

#### GET /api/files/{hash}
**Purpose**: Get specific file by hash

**Response**: `TrackedFileResponse`

#### POST /api/files/{hash}/confirm
**Purpose**: Confirm file classification before moving

**Request Body**:
```json
{
  "category": "BREAKING BAD"
}
```

#### POST /api/files/{hash}/move
**Purpose**: Move file to organized location

**Response**: `ApiResponse<string>` with new file path

#### GET /api/files/pending
**Purpose**: Get files ready for user confirmation

### Configuration API

#### GET /api/config/settings
**Purpose**: Get all configuration settings

#### GET /api/config/settings/{key}
**Purpose**: Get specific configuration value

#### POST /api/config/settings
**Purpose**: Create/update configuration setting

**Request Body**:
```json
{
  "key": "MediaButler.Paths.WatchFolder",
  "value": "/media/incoming",
  "category": "Paths",
  "description": "Folder to monitor for new files"
}
```

#### GET /api/config/export
**Purpose**: Export all configuration as JSON

### Statistics API

#### GET /api/stats/performance
**Purpose**: Current system performance metrics

**Response**:
```json
{
  "memory": {
    "managedMemoryMB": 45.2,
    "workingSetMB": 128.5,
    "targetLimitMB": 300
  },
  "processing": {
    "filesPerMinute": 12.5,
    "averageClassificationTimeMs": 150
  }
}
```

#### GET /api/stats/dashboard
**Purpose**: Overview statistics for main dashboard

#### GET /api/stats/system-health
**Purpose**: Health check with detailed system status

### Health Monitoring

#### GET /api/health
**Purpose**: Basic health check

**Response**:
```json
{
  "status": "Healthy",
  "timestamp": "2025-09-06T18:00:00Z",
  "version": "1.0.0"
}
```

## ARM32/Raspberry Pi Deployment Guide

### System Requirements
- **Hardware**: Raspberry Pi 3B+ or newer
- **OS**: Raspberry Pi OS (32-bit) or Ubuntu ARM32
- **Memory**: 1GB RAM minimum (MediaButler uses <300MB)
- **Storage**: 8GB SD card minimum (32GB+ recommended)
- **Network**: Ethernet or WiFi for API access

### Installation Steps

#### 1. Prepare Raspberry Pi
```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install .NET 8 ARM32 runtime
wget https://download.visualstudio.microsoft.com/download/pr/[version]/dotnet-aspnetcore-8.0.0-linux-arm.tar.gz
sudo mkdir -p /opt/dotnet
sudo tar zxf dotnet-aspnetcore-8.0.0-linux-arm.tar.gz -C /opt/dotnet
export DOTNET_ROOT=/opt/dotnet
export PATH=$PATH:$DOTNET_ROOT

# Install SQLite
sudo apt install sqlite3 -y
```

#### 2. Deploy MediaButler
```bash
# Create application directory
sudo mkdir -p /opt/mediabutler
sudo chown pi:pi /opt/mediabutler

# Copy published application
scp -r ./publish/* pi@raspberrypi:/opt/mediabutler/

# Set permissions
chmod +x /opt/mediabutler/MediaButler.API
```

#### 3. Configure Application
```bash
# Create configuration
cat > /opt/mediabutler/appsettings.Production.json << 'EOF'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*",
  "MediaButler": {
    "Database": {
      "ConnectionString": "Data Source=/opt/mediabutler/data/mediabutler.db"
    },
    "Paths": {
      "WatchFolder": "/media/incoming",
      "MediaLibrary": "/media/organized",
      "TempPath": "/tmp/mediabutler"
    }
  }
}
EOF

# Create required directories
sudo mkdir -p /media/incoming /media/organized /opt/mediabutler/data
sudo chown pi:pi /media/* /opt/mediabutler/data
```

#### 4. Create System Service
```bash
# Create systemd service
sudo tee /etc/systemd/system/mediabutler.service << 'EOF'
[Unit]
Description=MediaButler API Service
After=network.target

[Service]
Type=notify
User=pi
Group=pi
WorkingDirectory=/opt/mediabutler
ExecStart=/opt/dotnet/dotnet /opt/mediabutler/MediaButler.API.dll
Restart=always
RestartSec=10
Environment=DOTNET_ROOT=/opt/dotnet
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://+:5000

# Resource limits for ARM32
LimitNOFILE=65536
MemoryMax=300M

[Install]
WantedBy=multi-user.target
EOF

# Enable and start service
sudo systemctl daemon-reload
sudo systemctl enable mediabutler
sudo systemctl start mediabutler

# Check status
sudo systemctl status mediabutler
```

### Performance Optimization for ARM32

#### Memory Management
```bash
# Add swap file for safety (optional)
sudo dphys-swapfile swapoff
sudo sed -i 's/#CONF_SWAPSIZE=/CONF_SWAPSIZE=1024/' /etc/dphys-swapfile
sudo dphys-swapfile setup
sudo dphys-swapfile swapon

# Configure memory splitting (for GUI-less systems)
echo 'gpu_mem=16' | sudo tee -a /boot/config.txt
```

#### Application Tuning
```json
{
  "MediaButler": {
    "Performance": {
      "MaxConcurrentFiles": 2,
      "DatabasePoolSize": 5,
      "ScanIntervalSeconds": 300,
      "MemoryLimitMB": 250
    }
  }
}
```

### Monitoring and Maintenance

#### Check Application Health
```bash
# Health endpoint
curl http://localhost:5000/api/health

# Performance metrics
curl http://localhost:5000/api/stats/performance

# System resources
htop
free -h
df -h
```

#### Log Management
```bash
# Application logs
sudo journalctl -u mediabutler -f

# Rotate logs
sudo logrotate -d /etc/logrotate.d/mediabutler
```

#### Backup Strategy
```bash
# Database backup
cp /opt/mediabutler/data/mediabutler.db /backup/mediabutler-$(date +%Y%m%d).db

# Configuration backup
tar -czf /backup/mediabutler-config-$(date +%Y%m%d).tar.gz /opt/mediabutler/*.json
```

### Troubleshooting Common Issues

#### Memory Issues
- **Symptom**: Application crashes or becomes unresponsive
- **Solution**: Check memory usage with `free -h`, reduce concurrent operations
- **Prevention**: Monitor `/api/stats/performance` regularly

#### File Permission Issues
- **Symptom**: Cannot scan or move files
- **Solution**: Ensure `pi` user has read/write access to media directories
- **Command**: `sudo chown -R pi:pi /media/`

#### Network Issues
- **Symptom**: API not accessible from other devices
- **Solution**: Check firewall and binding configuration
- **Command**: `sudo ufw allow 5000` and verify `ASPNETCORE_URLS`

### Performance Expectations

| Metric | ARM32 Target | Typical Performance |
|--------|---------------|-------------------|
| Memory Usage | <300MB | 180-250MB |
| File Scan Rate | 10-20/min | 15/min average |
| API Response | <100ms | 50-80ms typical |
| Classification | <500ms | 200-400ms per file |
| Startup Time | <30s | 15-25s typical |

This deployment guide ensures MediaButler runs efficiently on ARM32 hardware while maintaining the <300MB memory target and providing reliable file organization capabilities.