# 🏗️ Move Files v2 Architecture - Complete Flow Analysis

## **Overview**
The v2 move-files endpoint (`/api/v2/actions/move-files`) implements a modern, layered architecture with comprehensive validation, structured error handling, and optimized batch operations.

## **🎯 Architecture Layers**

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   HTTP Request  │───▶│   Validation    │───▶│   Service       │───▶│   Repository    │
│   POST /move    │    │   Layer         │    │   Layer         │    │   Layer         │
└─────────────────┘    └─────────────────┘    └─────────────────┘    └─────────────────┘
                                ▼                        ▼                        ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  Background Job │◀───│   Job Queue     │◀───│   Response      │◀───│   File System   │
│  Processing     │    │   (Hangfire)    │    │   Generation    │    │   Operations    │
└─────────────────┘    └─────────────────┘    └─────────────────┘    └─────────────────┘
```

## **📋 Detailed Flow Breakdown**

### **Phase 1: Request Entry & Validation**

| **Step** | **Component** | **Action** | **Details** |
|----------|---------------|------------|-------------|
| **1.1** | `ActionsEndpointV2.cs:54` | **HTTP Request Received** | `POST /api/v2/actions/move-files` |
| **1.2** | `ValidationFilter<MoveFilesRequest>` | **Automatic Validation** | FluentValidation rules applied |
| **1.3** | **Request Binding** | **Model Binding** | `MoveFilesRequest` object created |

**Request Structure:**
```csharp
MoveFilesRequest {
    List<FileMoveDto> FilesToMove,
    bool ContinueOnError = false,
    bool ValidateCategories = true,
    bool CreateDirectories = true
}
```

### **Phase 2: Service Layer Processing**

| **Step** | **Component** | **Action** | **Details** |
|----------|---------------|------------|-------------|
| **2.1** | `ActionsService.cs:75` | **Service Entry** | `MoveFilesAsync(request, cancellationToken)` |
| **2.2** | **Logging** | **Operation Start** | Log file count and operation details |
| **2.3** | **File ID Extraction** | **Prepare Validation** | `request.FilesToMove.Select(f => f.Id).ToList()` |

### **Phase 3: Repository Validation**

| **Step** | **Component** | **Action** | **Details** |
|----------|---------------|------------|-------------|
| **3.1** | `ActionsRepository` | **Batch File Lookup** | `GetFilesByIdsAsync(fileIds, cancellationToken)` |
| **3.2** | **Database Query** | **Optimized Batch Load** | Single query for all file IDs |
| **3.3** | **Missing File Check** | **Validation Logic** | `fileIds.Except(existingFiles.Keys)` |
| **3.4** | **Error Handling** | **Conditional Abort** | Fail if missing files and `!ContinueOnError` |

### **Phase 4: Job Queue & Response**

| **Step** | **Component** | **Action** | **Details** |
|----------|---------------|------------|-------------|
| **4.1** | **Hangfire** | **Job Enqueueing** | `BackgroundJob.Enqueue<IHangFireJobService>()` |
| **4.2** | **Response Creation** | **Structured Response** | `ActionJobResponse` with metadata |
| **4.3** | **HTTP Response** | **Success Return** | `Results.Ok(result.Value)` |

**Response Structure:**
```csharp
ActionJobResponse {
    string JobId,
    string Status = "Queued",
    DateTime StartTime,
    int TotalItems,
    Dictionary<string, object> Metadata
}
```

## **⚙️ Background Job Execution**

### **Phase 5: Job Processing (`HangFireJobService.MoveFilesJob`)**

| **Stage** | **Method/Operation** | **Purpose** | **Error Handling** |
|-----------|---------------------|-------------|-------------------|
| **5.1** | **Configuration Loading** | Load `ORIGINDIR` & `DESTDIR` | Early abort with SignalR notification |
| **5.2** | **Batch File Loading** | `_context.FilesDetail.Where().ToDictionaryAsync()` | Optimize database access |
| **5.3** | **File Processing Loop** | Individual file processing | Continue on error logic |
| **5.4** | **Physical File Operations** | `File.Move()` + `Directory.CreateDirectory()` | Per-file error handling |
| **5.5** | **Database Updates** | In-memory entity updates | Batch commit later |
| **5.6** | **Training Data Collection** | ML training data preparation | Batch file write |
| **5.7** | **Real-time Notifications** | SignalR progress updates | Per-file completion status |
| **5.8** | **Batch Commit** | `UpdateRange()` + `SaveChangesAsync()` | Database optimization |
| **5.9** | **Training Data Write** | `File.AppendAllTextAsync()` | ML model preparation |
| **5.10** | **Final Notification** | Job completion summary | Success/failure metrics |

## **🔄 Key Operations Detail**

### **Database Operations**
```csharp
// 1. Batch validation (Repository Layer)
var existingFiles = await _actionsRepository.GetFilesByIdsAsync(fileIds, cancellationToken);

// 2. Batch loading (Background Job)
var dbFiles = await _context.FilesDetail
    .Where(f => fileIds.Contains(f.Id))
    .ToDictionaryAsync(f => f.Id, cancellationToken);

// 3. Batch update (Background Job)
_context.FilesDetail.UpdateRange(successfulFiles);
await _context.SaveChangesAsync(cancellationToken);
```

### **File System Operations**
```csharp
// 1. Path construction
var fileOrigin = Path.Combine(_originDir, _file.Name);
var folderDest = Path.Combine(_destDir, file.FileCategory);
var fileDest = Path.Combine(folderDest, _file.Name);

// 2. Directory creation
if (!Directory.Exists(folderDest))
    Directory.CreateDirectory(folderDest);

// 3. File movement
File.Move(fileOrigin, fileDest);
```

### **Real-time Notifications**
```csharp
// Individual file progress
await _notificationHub.Clients.All.SendAsync("moveFilesNotifications",
    file.Id, _file.Name, destinationPath, status, result);

// Job completion
await _notificationHub.Clients.All.SendAsync("jobNotifications",
    completionSummary, overallResult, totalFiles, movedFiles, failedFiles);
```

## **🚀 Performance Optimizations**

### **Batch Operations**
- **Repository Validation**: Single query for all file IDs
- **Database Loading**: `ToDictionaryAsync()` for O(1) lookup
- **Database Updates**: `UpdateRange()` instead of individual saves
- **Training Data**: Single file append operation

### **Error Resilience**
- **Continue on Error**: Optional failure tolerance
- **Missing File Handling**: Graceful degradation
- **Per-file Error Tracking**: Individual failure notifications
- **Transaction Safety**: Batch operations with rollback capability

### **Real-time Feedback**
- **Progress Tracking**: Per-file completion notifications
- **Performance Metrics**: Timing information per operation
- **Status Updates**: Real-time job progress via SignalR

## **📊 Architecture Benefits**

| **Aspect** | **Implementation** | **Benefit** |
|------------|-------------------|-------------|
| **Separation of Concerns** | Endpoint → Service → Repository → Job | Clear responsibility boundaries |
| **Validation** | FluentValidation + Repository checks | Comprehensive error prevention |
| **Performance** | Batch operations throughout | Reduced database load |
| **Monitoring** | Structured logging + SignalR | Real-time visibility |
| **Error Handling** | Result Pattern + structured responses | Consistent error management |
| **Scalability** | Background job processing | Non-blocking operations |

This v2 architecture provides a robust, scalable, and maintainable solution for file movement operations with comprehensive error handling and real-time progress tracking.