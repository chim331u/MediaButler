# Watch Folder File Processing Flow

This document provides a comprehensive schema of the complete flow when a new file is added to the watch folder in MediaButler.

## Complete File Processing Flow Schema

### 1. File Discovery Phase

**Trigger**: New file added to watch folder

```
FileSystemWatcher Event → FileDiscoveryService.OnFileCreated()
├── Debounce Logic (3s delay)
├── File Validation (size, extension, exclude patterns)
└── Queue for Processing
```

**Database Operations**:
- **Table**: `TrackedFiles`
- **Status**: `New`
- **BaseEntity Fields**: `CreatedAt`, `CreatedBy`, `IsDeleted=false`

### 2. Background Processing Queue

**Service**: `FileProcessingService` (Background Hosted Service)
```
Channel<FileProcessingRequest> → ProcessFileAsync()
├── SHA256 Hash calculation
├── Database persistence
└── Queue for ML Classification
```

**Database Updates**:
- **Status**: `New` → `Processing`
- **Fields**: `Hash`, `OriginalPath`, `FileSizeBytes`
- **BaseEntity**: `LastUpdateDate` updated

### 3. ML Classification Pipeline

**Service**: `ClassificationService` via `MLClassificationService`

```
ProcessFileAsync() → ClassifyFileAsync()
├── Tokenization (filename parsing)
├── Feature extraction
├── ML Model prediction
└── Confidence evaluation
```

**Decision Flow**:
```
Confidence ≥ 0.85 → Auto-classify → Status: Classified
Confidence 0.50-0.84 → Manual review → Status: Classified (pending confirmation)
Confidence < 0.50 → Unknown series → Status: Classified (manual category)
```

**Database Updates**:
- **Status**: `Processing` → `Classified`
- **Fields**: `Category`, `Confidence`, `AlternativePredictions`

### 4. File Organization Phase

**Trigger**: User confirmation or auto-classification

```
FilesController.ConfirmCategory() → FileOrganizationService
├── Target path generation
├── Directory creation
├── File move operation
└── Related files handling (.srt, .nfo)
```

**Status Flow**:
```
Classified → ReadyToMove → Moving → Moved
```

**Database Updates**:
- **Status**: `Classified` → `ReadyToMove` → `Moving` → `Moved`
- **Fields**: `MovedToPath`, `OrganizedAt`

### 5. Real-time Notifications

**SignalR Hubs**:
- `NotificationHub`: General system notifications
- `FileProcessingHub`: File-specific processing updates

**Events Sent**:
```
- file.discovered
- file.processing
- file.classified
- file.moved
- scan.progress
- error.classification_failed
- error.move_failed
```

## Complete Database Schema

### TrackedFiles Entity
```sql
CREATE TABLE TrackedFiles (
    Id INTEGER PRIMARY KEY,
    Hash TEXT NOT NULL UNIQUE,
    OriginalPath TEXT NOT NULL,
    FileName TEXT NOT NULL,
    FileSizeBytes BIGINT,
    Status INTEGER NOT NULL, -- FileStatus enum
    Category TEXT,
    Confidence REAL,
    AlternativePredictions TEXT, -- JSON array
    MovedToPath TEXT,
    RetryCount INTEGER DEFAULT 0,
    LastError TEXT,
    OrganizedAt DATETIME,

    -- BaseEntity fields
    CreatedAt DATETIME NOT NULL,
    CreatedBy TEXT,
    LastUpdateDate DATETIME,
    UpdatedBy TEXT,
    IsDeleted BOOLEAN DEFAULT 0,
    DeletedAt DATETIME,
    DeletedBy TEXT
);
```

### FileStatus Enum Values
```csharp
public enum FileStatus
{
    New = 0,           // Just discovered
    Processing = 1,    // Being processed
    Classified = 2,    // ML classification complete
    ReadyToMove = 3,   // Confirmed, ready for organization
    Moving = 4,        // File move in progress
    Moved = 5,         // Successfully organized
    Error = 6,         // Processing failed
    Retry = 7,         // Queued for retry
    Ignored = 8        // User marked as ignored
}
```

## Service Architecture Flow

```
FileSystemWatcher
    ↓
FileDiscoveryService (Background Service)
    ↓ (Channel)
FileProcessingService (Background Service)
    ↓
ClassificationService → MLClassificationService
    ↓
FileRepository (Database Update)
    ↓ (SignalR)
FileProcessingHub (Real-time notifications)
    ↓
Web UI (Blazor + SignalR updates)
```

## Error Handling & Retry Logic

**Retry Strategy**:
```
Error → Status: Retry (MaxRetries: 3)
├── Classification errors: Re-queue with delay
├── File system errors: Check permissions, retry
└── Database errors: Log and manual intervention
```

**Recovery Flow**:
```
Status: Error → Manual intervention or retry
└── Admin can reset to any previous status
```

## ARM32 Optimizations

**Resource Constraints**:
- Max concurrent file processing: 2
- Memory threshold: 300MB
- Queue capacity limits
- Batch processing for ML operations

**Performance Monitoring**:
- Processing time tracking
- Memory usage alerts
- Background service health checks

## Key Implementation Files

- `src/MediaButler.Services/Background/FileDiscoveryService.cs` - File system monitoring
- `src/MediaButler.Services/Background/FileProcessingService.cs` - Background processing
- `src/MediaButler.Core/Entities/TrackedFile.cs` - Main entity with BaseEntity
- `src/MediaButler.API/Hubs/NotificationHub.cs` - General notifications
- `src/MediaButler.API/Hubs/FileProcessingHub.cs` - File-specific updates
- `src/MediaButler.Services/Classification/ClassificationService.cs` - ML pipeline
- `src/MediaButler.Services/Organization/FileOrganizationService.cs` - File operations

This schema shows the complete end-to-end flow from file detection through final organization, with all database operations, status transitions, and service interactions clearly mapped out according to "Simple Made Easy" principles.

## Detailed ML Queuing Mechanism

### File Discovery to ML Queue Flow

#### 1. File Detection
**FileSystemWatcher Event** → `FileDiscoveryService.OnCreated()`
```csharp
// src/MediaButler.Services/Background/FileDiscoveryService.cs
private async void OnCreated(object sender, FileSystemEventArgs e)
{
    await ProcessFileEventAsync(e.FullPath, "Created");
}
```

#### 2. Debounce and Validation
```csharp
private async Task ProcessFileEventAsync(string filePath, string eventType)
{
    // Debounce logic (3 second delay)
    await Task.Delay(TimeSpan.FromSeconds(_debounceDelaySeconds));

    // File validation (size, extension, exclude patterns)
    if (!IsValidFileForProcessing(filePath)) return;

    // Queue for processing
    await QueueFileForProcessingAsync(filePath);
}
```

#### 3. Queue for Processing (.NET Channel)
**Key Implementation**: MediaButler uses **System.Threading.Channels** for queuing
```csharp
private async Task QueueFileForProcessingAsync(string filePath)
{
    var request = new FileProcessingRequest
    {
        FilePath = filePath,
        Priority = ProcessingPriority.Normal,
        RequestedAt = DateTime.UtcNow
    };

    // Write to Channel for FileProcessingService consumption
    await _processingQueue.Writer.WriteAsync(request);
}
```

#### 4. Background Processing Service Consumption
**FileProcessingService** (Background Hosted Service) consumes from channel:
```csharp
// src/MediaButler.Services/Background/FileProcessingService.cs
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await foreach (var request in _processingQueue.Reader.ReadAllAsync(stoppingToken))
    {
        try
        {
            await ProcessFileAsync(request, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file {FilePath}", request.FilePath);
        }
    }
}
```

#### 5. File Processing to ML Classification
```csharp
private async Task ProcessFileAsync(FileProcessingRequest request, CancellationToken cancellationToken)
{
    // 1. Calculate SHA256 hash
    var hash = await _fileService.CalculateHashAsync(request.FilePath);

    // 2. Create/Update TrackedFile entity
    var trackedFile = await CreateOrUpdateTrackedFileAsync(request.FilePath, hash);

    // 3. Update status to Processing
    trackedFile.Status = FileStatus.Processing;
    await _fileRepository.UpdateAsync(trackedFile);

    // 4. **QUEUE FOR ML CLASSIFICATION**
    await QueueForClassificationAsync(trackedFile);
}
```

#### 6. ML Classification Queue Entry Point
**This is where the ML queue happens**:
```csharp
private async Task QueueForClassificationAsync(TrackedFile file)
{
    var classificationRequest = new ClassificationRequest
    {
        FileId = file.Id,
        FileName = file.FileName,
        Hash = file.Hash,
        Priority = DetermineClassificationPriority(file)
    };

    // Write to ML Classification Channel
    await _classificationQueue.Writer.WriteAsync(classificationRequest);

    _logger.LogInformation("Queued file {FileName} for ML classification", file.FileName);
}
```

#### 7. ML Processing Service Consumption
**ClassificationService** or **MLProcessingService** consumes from ML queue:
```csharp
// Background service consuming ML classification requests
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await foreach (var request in _classificationQueue.Reader.ReadAllAsync(stoppingToken))
    {
        try
        {
            await PerformClassificationAsync(request, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ML classification failed for file {FileId}", request.FileId);
            await HandleClassificationErrorAsync(request, ex);
        }
    }
}
```

#### 8. Actual ML Classification
```csharp
private async Task PerformClassificationAsync(ClassificationRequest request, CancellationToken cancellationToken)
{
    // 1. Load file from database
    var trackedFile = await _fileRepository.GetByIdAsync(request.FileId);

    // 2. Perform ML classification
    var result = await _mlClassificationService.ClassifyAsync(trackedFile.FileName);

    // 3. Update database with results
    trackedFile.Category = result.Category;
    trackedFile.Confidence = result.Confidence;
    trackedFile.Status = FileStatus.Classified;
    trackedFile.AlternativePredictions = JsonSerializer.Serialize(result.Alternatives);

    await _fileRepository.UpdateAsync(trackedFile);

    // 4. Send SignalR notification
    await _notificationHub.SendFileClassifiedAsync(trackedFile.Id, result);
}
```

### Queue Architecture Summary

**Two-Stage Channel-Based Queuing**:

1. **File Processing Queue**: `Channel<FileProcessingRequest>`
   - Producer: `FileDiscoveryService`
   - Consumer: `FileProcessingService`
   - Purpose: File discovery → database registration → hash calculation

2. **ML Classification Queue**: `Channel<ClassificationRequest>`
   - Producer: `FileProcessingService.QueueForClassificationAsync()`
   - Consumer: `ClassificationService` or dedicated ML background service
   - Purpose: Database-registered files → ML classification

### Channel Configuration

**Bounded Channel Setup** (in DI configuration):
```csharp
// Program.cs or ServiceCollectionExtensions
services.AddSingleton(provider =>
{
    var options = new BoundedChannelOptions(capacity: 100)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = false,
        SingleWriter = false
    };
    return Channel.CreateBounded<FileProcessingRequest>(options);
});

services.AddSingleton(provider =>
{
    var options = new BoundedChannelOptions(capacity: 50)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,  // Only one ML service processes
        SingleWriter = false  // Multiple file processors can queue
    };
    return Channel.CreateBounded<ClassificationRequest>(options);
});
```

### Complete Queuing Flow

```
New File → FileSystemWatcher → FileDiscoveryService
    ↓ (Channel<FileProcessingRequest>)
FileProcessingService → Database Registration → Hash Calculation
    ↓ (Channel<ClassificationRequest>)
ClassificationService → ML Processing → Database Update → SignalR
```

This two-stage channel architecture ensures:
- **Efficient Processing**: Non-blocking producers with bounded consumers
- **ARM32 Optimization**: Controlled memory usage through bounded channels
- **Error Isolation**: Failures in one stage don't affect others
- **Scalability**: Each stage can be independently tuned for performance
- **Observability**: Clear separation allows for stage-specific monitoring