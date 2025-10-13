# MediaButler API Endpoint Documentation

## Comprehensive API Endpoint Analysis

This document provides a complete reference of all API endpoints in the MediaButler.API project, organized by controller, with information about HTTP methods, routes, descriptions, parameters, responses, and Web UI callers.

---

## **FilesController** (`/api/files`)
File management endpoints for tracked files in MediaButler. Handles CRUD operations, scanning, classification, and file movement.

| Endpoint | Method | Description | Parameters | Response | Caller (Web Project) |
|----------|--------|-------------|------------|----------|---------------------|
| `/api/files` | GET | Gets tracked files with pagination and optional filtering by single status | **Query**: `skip` (int, default: 0), `take` (int, default: 20, max: 100), `status` (string, optional), `category` (string, optional) | `200 OK`: `TrackedFileResponse[]`<br>`400 Bad Request`: Invalid parameters | `FilesApiService.GetFilesAsync()` |
| `/api/files/by-statuses` | GET | Gets tracked files filtered by multiple status values with pagination, search, and ordering | **Query**: `skip` (int), `take` (int), `statuses` (string[], required), `category` (string, optional), `searchTerm` (string, optional), `orderBy` (string, optional), `descending` (bool, default: true) | `200 OK`: `PaginatedFilesResponse` (with `Items[]`, `Total`, `Skip`, `Take`)<br>`400 Bad Request`: Invalid parameters | `FilesApiService.GetFilesByStatusesAsync()` |
| `/api/files/{hash}` | GET | Gets a specific tracked file by its SHA256 hash | **Route**: `hash` (string, 64-char SHA256) | `200 OK`: `TrackedFileResponse`<br>`400 Bad Request`: Invalid hash<br>`404 Not Found`: File not found | `FilesApiService.GetFileAsync()` |
| `/api/files` | POST | Registers a new file for tracking | **Body**: `AddFileRequest` { `FilePath`: string (required, max 500 chars) } | `201 Created`: `TrackedFileResponse`<br>`400 Bad Request`: Invalid file info<br>`409 Conflict`: File already exists | Not called from Web UI |
| `/api/files/pending` | GET | Gets files awaiting user confirmation after classification | None | `200 OK`: `TrackedFileResponse[]`<br>`500 Internal Server Error`: Failed to retrieve | `FilesApiService.GetPendingFilesAsync()` |
| `/api/files/ready-for-classification` | GET | Gets files ready for ML classification processing | **Query**: `limit` (int, default: 50, max: 500) | `200 OK`: `TrackedFileResponse[]`<br>`400 Bad Request`: Invalid limit | `FilesApiService.GetFilesReadyForClassificationAsync()` |
| `/api/files/{hash}/confirm` | POST | Confirms a file's category assignment and marks it ready for processing | **Route**: `hash` (string, 64-char SHA256)<br>**Body**: `ConfirmCategoryRequest` { `Category`: string (required, 1-100 chars) } | `200 OK`: `TrackedFileResponse`<br>`400 Bad Request`: Invalid request<br>`404 Not Found`: File not found | `FilesApiService.ConfirmFileCategoryAsync()` |
| `/api/files/{hash}/moved` | POST | Marks a file as moved to its target location | **Route**: `hash` (string, 64-char SHA256)<br>**Body**: `MarkMovedRequest` { `TargetPath`: string (required, max 500 chars) } | `200 OK`: `TrackedFileResponse`<br>`400 Bad Request`: Invalid request<br>`404 Not Found`: File not found | `FilesApiService.MarkFileAsMovedAsync()` |
| `/api/files/{hash}` | DELETE | Soft deletes a tracked file (sets IsActive to false) | **Route**: `hash` (string, 64-char SHA256)<br>**Body**: `DeleteFileRequest` { `Reason`: string (optional, max 200 chars) } | `204 No Content`: Success<br>`400 Bad Request`: Invalid hash<br>`404 Not Found`: File not found | `FilesApiService.DeleteFileAsync()` |
| `/api/files/categories` | GET | Gets distinct categories from tracked files, sorted alphabetically | None | `200 OK`: `string[]`<br>`500 Internal Server Error`: Failed to retrieve | `FilesApiService.GetDistinctCategoriesAsync()` |
| `/api/files/scan` | POST | Manually triggers a scan of configured watch folders to discover new files | **Body**: `ScanFoldersRequest` { `TimeoutSeconds`: int (default: 300) } (optional) | `200 OK`: `ScanResult`<br>`400 Bad Request`: Invalid request<br>`500 Internal Server Error`: Scan failed | `FilesApiService.ScanFoldersAsync()` |
| `/api/files/scan/folder` | POST | Manually triggers a scan of a specific folder path | **Body**: `ScanSpecificFolderRequest` { `FolderPath`: string (required, max 500 chars), `TimeoutSeconds`: int (default: 300) } | `200 OK`: `ScanResult`<br>`400 Bad Request`: Invalid path<br>`404 Not Found`: Folder not found<br>`500 Internal Server Error`: Scan failed | `FilesApiService.ScanSpecificFolderAsync()` |

---

## **FileActionsController** (`/api/v1/file-actions`)
Batch file operation endpoints providing background job processing and progress tracking.

| Endpoint | Method | Description | Parameters | Response | Caller (Web Project) |
|----------|--------|-------------|------------|----------|---------------------|
| `/api/v1/file-actions/organize-batch` | POST | Organizes multiple files in a background batch operation | **Body**: `BatchOrganizeRequest` { `Files`: `FileActionDto[]` (required), `ContinueOnError`: bool, `ValidateTargetPaths`: bool, `CreateDirectories`: bool, `DryRun`: bool, `BatchName`: string, `MaxConcurrency`: int } | `200 OK`: `BatchJobResponse`<br>`400 Bad Request`: Invalid request<br>`500 Internal Server Error`: Processing error | `FilesApiService.OrganizeBatchAsync()` |
| `/api/v1/file-actions/batch-status/{jobId}` | GET | Gets the current status of a batch job with detailed progress | **Route**: `jobId` (string)<br>**Query**: `includeDetails` (bool, default: false) | `200 OK`: `BatchJobResponse`<br>`404 Not Found`: Job not found<br>`500 Internal Server Error`: Error retrieving status | `FilesApiService.GetBatchStatusAsync()` |
| `/api/v1/file-actions/batch-cancel/{jobId}` | POST | Cancels a running or queued batch job | **Route**: `jobId` (string) | `200 OK`: Cancellation confirmed<br>`404 Not Found`: Job not found<br>`409 Conflict`: Job cannot be cancelled<br>`500 Internal Server Error`: Cancellation error | Not called from Web UI |
| `/api/v1/file-actions/batch-jobs` | GET | Gets a list of recent batch jobs with summary information | **Query**: `status` (string, optional), `limit` (int, default: 50, max: 200), `offset` (int, default: 0) | `200 OK`: `BatchJobResponse[]`<br>`400 Bad Request`: Invalid pagination<br>`500 Internal Server Error`: Error retrieving jobs | Not called from Web UI |
| `/api/v1/file-actions/validate-batch` | POST | Validates a batch organize request without executing it | **Body**: `BatchOrganizeRequest` (same as organize-batch) | `200 OK`: Validation results<br>`400 Bad Request`: Validation failed<br>`500 Internal Server Error`: Validation error | Not called from Web UI |
| `/api/v1/file-actions/ignore/{hash}` | POST | Marks a file as ignored, preventing it from being processed (terminal state) | **Route**: `hash` (string, SHA256) | `200 OK`: File ignored successfully<br>`400 Bad Request`: Invalid hash or cannot ignore<br>`404 Not Found`: File not found<br>`500 Internal Server Error`: Error occurred | `FilesApiService.IgnoreFileAsync()` |

---

## **HealthController** (`/api/health`)
Health check and system monitoring endpoints for MediaButler API.

| Endpoint | Method | Description | Parameters | Response | Caller (Web Project) |
|----------|--------|-------------|------------|----------|---------------------|
| `/api/health` | GET | Gets basic health status of the MediaButler API | None | `200 OK`: `{ Status, Timestamp, Version, Service }` | `HealthApiService.GetBasicHealthAsync()` |
| `/api/health/detailed` | GET | Gets detailed system health including database connectivity and performance metrics | None | `200 OK`: Detailed health with `Database`, `Memory`, `Processing`, `FileOperations`, `MachineLearning` sections<br>`500 Internal Server Error`: Health check failed | `HealthApiService.GetDetailedHealthAsync()` |
| `/api/health/ready` | GET | Gets readiness status indicating if service is ready to handle requests | None | `200 OK`: Service ready<br>`503 Service Unavailable`: Service not ready | Not called from Web UI |
| `/api/health/live` | GET | Gets liveness status indicating if service is alive and running | None | `200 OK`: `{ Status: "Alive", Timestamp, Uptime }` | Not called from Web UI |
| `/api/health/ml` | GET | Gets machine learning service health status | None | `200 OK`: ML health status<br>`503 Service Unavailable`: ML services unavailable | Not called from Web UI |

---

## **MetricsController** (`/api/metrics`)
System metrics and monitoring data endpoints for real-time performance tracking.

| Endpoint | Method | Description | Parameters | Response | Caller (Web Project) |
|----------|--------|-------------|------------|----------|---------------------|
| `/api/metrics/health` | GET | Gets comprehensive system health summary | None | `200 OK`: `SystemHealthSummary`<br>`500 Internal Server Error`: Error occurred | Not called from Web UI |
| `/api/metrics/queue` | GET | Gets current processing queue metrics and throughput data | **Query**: `timeWindowHours` (int, default: 1, max: 168) | `200 OK`: `QueueMetrics`<br>`400 Bad Request`: Invalid time window<br>`500 Internal Server Error`: Error occurred | Not called from Web UI |
| `/api/metrics/classification` | GET | Gets ML classification performance metrics and accuracy data | **Query**: `timeWindowHours` (int, default: 24, max: 168) | `200 OK`: `ClassificationMetrics`<br>`400 Bad Request`: Invalid time window<br>`500 Internal Server Error`: Error occurred | Not called from Web UI |
| `/api/metrics/errors` | GET | Gets error rate metrics and failure analysis data | **Query**: `timeWindowHours` (int, default: 24, max: 168) | `200 OK`: `ErrorMetrics`<br>`400 Bad Request`: Invalid time window<br>`500 Internal Server Error`: Error occurred | Not called from Web UI |
| `/api/metrics/performance` | GET | Gets performance metrics and resource utilization data | **Query**: `timeWindowHours` (int, default: 1, max: 168) | `200 OK`: `PerformanceMetrics`<br>`400 Bad Request`: Invalid time window<br>`500 Internal Server Error`: Error occurred | Not called from Web UI |
| `/api/metrics/status` | GET | Gets current system status in simplified format for health checks | None | `200 OK`: System healthy<br>`503 Service Unavailable`: System has warnings or critical issues | Not called from Web UI |
| `/api/metrics/ping` | GET | External monitoring systems health check probe (minimal response) | None | `200 OK`: `{ status: "ok", timestamp, version }`<br>`503 Service Unavailable`: Service unavailable | Not called from Web UI |

---

## **NotificationTestController** (`/api/notificationtest`)
Test endpoints for SignalR real-time notification functionality.

| Endpoint | Method | Description | Parameters | Response | Caller (Web Project) |
|----------|--------|-------------|------------|----------|---------------------|
| `/api/notificationtest/job-progress` | POST | Test endpoint to send a job progress notification via SignalR | **Query**: `message` (string, default: "Test notification"), `progress` (int, default: 50) | `200 OK`: Notification sent<br>`500 Internal Server Error`: Error occurred | Not called from Web UI |
| `/api/notificationtest/file-move` | POST | Test endpoint to send a file move notification via SignalR | **Query**: `fileId` (int, default: 1), `fileName` (string, default: "test.mkv"), `status` (string, default: "Moving") | `200 OK`: Notification sent<br>`500 Internal Server Error`: Error occurred | Not called from Web UI |
| `/api/notificationtest/system-status` | POST | Test endpoint to send a system status notification via SignalR | **Query**: `component` (string, default: "Scanner"), `status` (string, default: "Active"), `message` (string, default: "System is running normally") | `200 OK`: Notification sent<br>`500 Internal Server Error`: Error occurred | Not called from Web UI |
| `/api/notificationtest/error` | POST | Test endpoint to send an error notification via SignalR | **Query**: `errorType` (string, default: "test_error"), `message` (string, default: "This is a test error notification") | `200 OK`: Notification sent<br>`500 Internal Server Error`: Error occurred | Not called from Web UI |

---

## **StatsController** (`/api/stats`)
Monitoring and analytics endpoints for MediaButler system statistics.

| Endpoint | Method | Description | Parameters | Response | Caller (Web Project) |
|----------|--------|-------------|------------|----------|---------------------|
| `/api/stats/processing` | GET | Gets comprehensive processing statistics for the MediaButler system | None | `200 OK`: `ProcessingStats`<br>`500 Internal Server Error`: Failed to retrieve | Not called from Web UI |
| `/api/stats/ml-performance` | GET | Gets ML classification performance metrics and accuracy statistics | None | `200 OK`: `MLPerformanceStats`<br>`500 Internal Server Error`: Failed to retrieve | Not called from Web UI |
| `/api/stats/system-health` | GET | Gets system health metrics including error rates and processing performance | None | `200 OK`: `SystemHealthStats`<br>`500 Internal Server Error`: Failed to retrieve | Not called from Web UI |
| `/api/stats/activity` | GET | Gets file processing activity for a specific date range | **Query**: `startDate` (string, format: yyyy-MM-dd, default: 7 days ago), `endDate` (string, format: yyyy-MM-dd, default: today+1) | `200 OK`: `ActivityStats`<br>`400 Bad Request`: Invalid date range<br>`500 Internal Server Error`: Failed to retrieve | Not called from Web UI |
| `/api/stats/categories` | GET | Gets category distribution showing how files are organized | None | `200 OK`: `CategoryStats`<br>`500 Internal Server Error`: Failed to retrieve | Not called from Web UI |
| `/api/stats/throughput` | GET | Gets processing throughput metrics showing files processed over time | **Query**: `hours` (int, default: 24, max: 168) | `200 OK`: `ThroughputStats`<br>`400 Bad Request`: Invalid hours parameter<br>`500 Internal Server Error`: Failed to retrieve | Not called from Web UI |
| `/api/stats/errors` | GET | Gets error analysis showing common errors and failure patterns | **Query**: `days` (int, default: 7, max: 90) | `200 OK`: `ErrorStats`<br>`400 Bad Request`: Invalid days parameter<br>`500 Internal Server Error`: Failed to retrieve | Not called from Web UI |
| `/api/stats/file-sizes` | GET | Gets file size distribution statistics to understand storage patterns | None | `200 OK`: `FileSizeStats`<br>`500 Internal Server Error`: Failed to retrieve | Not called from Web UI |
| `/api/stats/trends` | GET | Gets historical trends for processing metrics over time | **Query**: `days` (int, default: 30, max: 365) | `200 OK`: `TrendStats`<br>`400 Bad Request`: Invalid days parameter<br>`500 Internal Server Error`: Failed to retrieve | Not called from Web UI |
| `/api/stats/dashboard` | GET | Gets a comprehensive dashboard summary with key metrics | None | `200 OK`: `DashboardStats`<br>`500 Internal Server Error`: Failed to retrieve | Not called from Web UI |
| `/api/stats/performance` | GET | Gets system performance metrics including memory usage and processing speeds | None | `200 OK`: Performance data with `Memory`, `Process`, `System` sections | Not called from Web UI |

---

## **SystemController** (`/api/system`)
System status and resource monitoring endpoints.

| Endpoint | Method | Description | Parameters | Response | Caller (Web Project) |
|----------|--------|-------------|------------|----------|---------------------|
| `/api/system/storage` | GET | Gets the current storage status including usage and capacity | None | `200 OK`: `StorageStatus` { `UsedGB`, `TotalGB`, `FilesTotalSizeGB` }<br>`500 Internal Server Error`: Error occurred | Not called from Web UI |
| `/api/system/memory` | GET | Gets the current memory usage | None | `200 OK`: `MemoryInfo` { `UsageMB` }<br>`500 Internal Server Error`: Error occurred | Not called from Web UI |

---

## **TrainingController** (`/api/training`)
ML model training operations with background processing and real-time updates.

| Endpoint | Method | Description | Parameters | Response | Caller (Web Project) |
|----------|--------|-------------|------------|----------|---------------------|
| `/api/training/start` | POST | Starts ML model training as a background job with real-time progress updates | **Body**: `TrainingRequest` { `Epochs`: int (optional), `LearningRate`: float (optional), `BatchSize`: int (optional), `ForceRetrain`: bool (default: false) } | `200 OK`: `TrainingResponse`<br>`400 Bad Request`: Invalid request<br>`409 Conflict`: Training already in progress<br>`500 Internal Server Error`: Error occurred | `TrainingApiService.StartTrainingAsync()` |
| `/api/training/status/{sessionId}` | GET | Gets the current status of a training session | **Route**: `sessionId` (string) | `200 OK`: `TrainingStatusResponse`<br>`404 Not Found`: Session not found | `TrainingApiService.GetTrainingStatusAsync()` |
| `/api/training/sessions` | GET | Gets all active and recent training sessions (last 10) | None | `200 OK`: `TrainingStatusResponse[]` | `TrainingApiService.GetTrainingSessionsAsync()` |

---

## **ProcessingController** (`/api/processing`)
Processing queue operations and ML evaluation endpoints.

| Endpoint | Method | Description | Parameters | Response | Caller (Web Project) |
|----------|--------|-------------|------------|----------|---------------------|
| `/api/processing/queue/status` | GET | Gets the current processing queue status | None | `200 OK`: `ProcessingQueueStatus`<br>`500 Internal Server Error`: Error occurred | Not called from Web UI |
| `/api/processing/ml-evaluation/queue` | POST | Queues files for ML evaluation/re-evaluation based on specified statuses (New, Classified) | **Body**: `MlEvaluationRequest` { `FilterByCategory`: string (optional), `ForceReEvaluation`: bool (default: true) } | `200 OK`: `MlEvaluationResponse`<br>`400 Bad Request`: Invalid request<br>`500 Internal Server Error`: Error occurred | `FilesApiService.QueueMlEvaluationAsync()` |

---

## **NotificationsController** (`/api/notifications`)
Receives notifications from Batch worker and forwards to SignalR hubs for real-time client updates.

| Endpoint | Method | Description | Parameters | Response | Caller (Web Project) |
|----------|--------|-------------|------------|----------|---------------------|
| `/api/notifications/batch` | POST | Receives batch notifications from Hangfire worker and forwards to SignalR clients | **Body**: `BatchNotification[]` { `JobId`, `JobType`, `EventType`, `Message`, `Data`, `Timestamp` } | `200 OK`: `{ processed: count }`<br>`400 Bad Request`: No notifications provided | Not called from Web UI (internal) |

---

## **ExamplesController** (`/api/examples`)
Example requests and documentation for testing and understanding the MediaButler API.

| Endpoint | Method | Description | Parameters | Response | Caller (Web Project) |
|----------|--------|-------------|------------|----------|---------------------|
| `/api/examples/batch-organize-requests` | GET | Gets example batch organize requests for testing and documentation | None | `200 OK`: Example request objects (SimpleExample, DryRunExample, CustomPathExample, LargeBatchExample) | Not called from Web UI |
| `/api/examples/api-usage` | GET | Gets example API usage scenarios and workflow descriptions | None | `200 OK`: API usage workflows (BasicWorkflow, ValidationWorkflow, MonitoringWorkflow, ErrorHandling, PerformanceOptimization) | Not called from Web UI |
| `/api/examples/system-info` | GET | Provides information about current system configuration and capabilities | None | `200 OK`: System configuration (BatchProcessing, SignalRConnections, Validation, Monitoring, SystemConstraints) | Not called from Web UI |

---

## Summary Statistics

- **Total Controllers**: 11
- **Total Endpoints**: 60+
- **Web UI Integration**: 19 endpoints actively called from `MediaButler.Web`
- **Primary Web Service**: `FilesApiService.cs` (15 API calls)
- **Supporting Web Services**:
  - `HealthApiService.cs` (2 API calls)
  - `TrainingApiService.cs` (3 API calls)

## Key Patterns

1. **RESTful Design**: Standard HTTP verbs (GET, POST, PUT, DELETE) with appropriate status codes
2. **Pagination**: Consistent pagination with `skip` and `take` parameters
3. **Result Pattern**: All services return `Result<T>` for explicit error handling
4. **Background Processing**: Batch operations use background job queue with SignalR progress updates
5. **Validation**: FluentValidation and ModelState validation on all endpoints
6. **ARM32 Optimization**: Designed for low-memory footprint (<300MB) and efficient processing

## API Usage from Web UI

The MediaButler Web UI (Blazor WebAssembly .NET 10) consumes the API through three main service classes:

### FilesApiService.cs
Primary service for file management operations:
- `GetFilesAsync()` - Retrieves paginated file lists
- `GetFilesByStatusesAsync()` - Multi-status file filtering with search
- `GetFileAsync()` - Individual file retrieval
- `GetPendingFilesAsync()` - Files awaiting confirmation
- `GetFilesReadyForClassificationAsync()` - Classification queue
- `ConfirmFileCategoryAsync()` - Category confirmation
- `MarkFileAsMovedAsync()` - Move completion
- `DeleteFileAsync()` - Soft delete
- `GetDistinctCategoriesAsync()` - Category list
- `ScanFoldersAsync()` - Folder scanning
- `ScanSpecificFolderAsync()` - Single folder scan
- `OrganizeBatchAsync()` - Batch operations
- `GetBatchStatusAsync()` - Batch progress tracking
- `IgnoreFileAsync()` - Mark files as ignored
- `QueueMlEvaluationAsync()` - ML re-evaluation

### HealthApiService.cs
System monitoring:
- `GetBasicHealthAsync()` - Basic health check
- `GetDetailedHealthAsync()` - Comprehensive health status

### TrainingApiService.cs
ML model training:
- `StartTrainingAsync()` - Initiate training
- `GetTrainingStatusAsync()` - Training progress
- `GetTrainingSessionsAsync()` - Training history

## SignalR Hubs

Real-time communication via two SignalR hubs:

### FileProcessingHub (`/hubs/file-processing`)
- File processing updates
- Classification results
- Movement progress
- Error notifications

### NotificationHub (`/hubs/notifications`)
- Batch job progress
- System status updates
- General notifications

## Authentication & Authorization

**Current State**: Single-user system with no authentication required (NAS deployment)
**Future Consideration**: Can be extended with ASP.NET Core Identity for multi-user scenarios

## File Paths Reference

- **API Controllers**: `src/MediaButler.API/Controllers/`
- **Web Services**: `src/MediaButler.Web/Services/`
- **API Documentation**: `docs/API-Endpoints-Reference.md`
