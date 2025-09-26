# Batch File Processing API Implementation

## Overview

This document describes the complete implementation of the batch file processing API for MediaButler, following the architecture pattern from the move-file example. The implementation provides efficient, scalable batch operations with real-time progress tracking and comprehensive error handling.

## Architecture Summary

The implementation follows a clean, layered architecture:

```
API Controller → Service Layer → Background Processor → File Operations
      ↓              ↓              ↓                    ↓
  Validation    Job Queuing    Real-time Updates    Database Updates
```

## Key Components Implemented

### 1. **Request/Response Models**
- `BatchOrganizeRequest` - Comprehensive request model with validation
- `FileActionDto` - Individual file action specification
- `BatchJobResponse` - Detailed response with progress tracking
- `FileProcessingResult` - Individual file processing results
- `FileOrganizeOperation` - Internal operation model

### 2. **API Controller** (`FileActionsController`)
```csharp
POST /api/v1/file-actions/organize-batch      // Submit batch job
GET  /api/v1/file-actions/batch-status/{id}   // Get job status
POST /api/v1/file-actions/batch-cancel/{id}   // Cancel job
GET  /api/v1/file-actions/batch-jobs          // List jobs
POST /api/v1/file-actions/validate-batch      // Validate request
```

### 3. **Service Layer** (`FileActionsService`)
- Request validation and file lookup
- Path generation and operation preparation
- Hangfire job queuing
- Status tracking and management

### 4. **Background Processing** (`BatchFileProcessor`)
- Asynchronous file processing
- Configurable concurrency (ARM32 optimized)
- Real-time SignalR notifications
- Comprehensive error handling
- Database updates and audit trails

### 5. **Real-time Communications** (`FileProcessingHub`)
- Dedicated SignalR hub for file operations
- Strongly-typed client interface
- Group-based notifications for specific jobs
- Connection management and health checking

### 6. **Validation** (`BatchOrganizeRequestValidator`)
- FluentValidation rules for comprehensive input validation
- File hash format validation (SHA256)
- Category name and path validation
- Metadata validation and size limits

### 7. **Repository Extensions**
- Optimized batch database operations
- File lookup by hash collections
- Validation helpers for batch operations
- Status summary and monitoring queries

## Key Features

### **ARM32 Optimization**
- Configurable worker count based on CPU cores
- Memory-conscious batch sizes (max 1000 files)
- Optimized database queries and updates
- Resource management with semaphores

### **Error Handling**
- `ContinueOnError` flag for resilient processing
- Individual file error tracking
- Comprehensive error notifications via SignalR
- Graceful degradation and recovery options

### **Real-time Monitoring**
- Live progress updates via SignalR
- Per-file processing status
- Job-specific notification groups
- Connection health monitoring

### **Validation & Safety**
- Pre-flight validation endpoint
- Dry-run capability for testing
- Path validation and conflict detection
- Comprehensive input validation

## Usage Examples

### Basic Batch Request
```json
{
  "files": [
    {
      "hash": "a1b2c3d4e5f6789012345678901234567890abcdef1234567890abcdef123456",
      "confirmedCategory": "BREAKING BAD"
    },
    {
      "hash": "b2c3d4e5f6789012345678901234567890abcdef1234567890abcdef1234567",
      "confirmedCategory": "THE OFFICE"
    }
  ],
  "batchName": "Evening Organization",
  "continueOnError": true,
  "dryRun": false
}
```

### Real-time Monitoring (JavaScript)
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/file-processing")
    .build();

// Join specific batch job
await connection.invoke("JoinBatchJob", jobId);

// Listen for progress updates
connection.on("BatchJobProgress", (jobId, total, processed, successful, failed) => {
    console.log(`Progress: ${processed}/${total} (${successful} success, ${failed} failed)`);
});

connection.on("FileProcessingCompleted", (result) => {
    console.log(`File ${result.fileName}: ${result.success ? 'Success' : 'Failed'}`);
});
```

## Configuration

### Hangfire Setup
```csharp
builder.Services.AddHangfire(config => config
    .UseSqliteStorage(connectionString)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings());

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Math.Max(1, Math.Min(Environment.ProcessorCount, 4));
    options.ServerTimeout = TimeSpan.FromMinutes(5);
});
```

### ARM32 Optimizations
- Maximum 4 concurrent workers
- Batch size limits (1000 files max, 100 recommended)
- Memory-conscious processing patterns
- Configurable concurrency per job

## Monitoring & Debugging

### Hangfire Dashboard
- Available at `/hangfire` in development
- Real-time job monitoring
- Queue status and worker health
- Failed job inspection and retry

### SignalR Hub Endpoints
- `/file-processing` - File operation notifications
- `/notifications` - General system notifications

### API Endpoints for Monitoring
```http
GET /api/v1/file-actions/batch-jobs           # List recent jobs
GET /api/v1/file-actions/batch-status/{id}    # Detailed job status
GET /api/examples/system-info                  # System capabilities
```

## Performance Characteristics

### **Throughput**
- Target: <50 files/minute (precision over speed)
- Configurable concurrency (1-4 workers on ARM32)
- Batch processing with optimized database operations

### **Memory Usage**
- Target footprint: <300MB total system
- Streaming file operations
- Efficient database query patterns
- Garbage collection optimization

### **Reliability**
- Atomic file operations with rollback capability
- Comprehensive error tracking and recovery
- Health monitoring and alerting
- Background job persistence across restarts

## Integration with Existing MediaButler Architecture

The batch API seamlessly integrates with existing MediaButler components:

- **File Organization Service** - Reuses existing file movement logic
- **Path Generation Service** - Uses established path calculation
- **Rollback Service** - Provides operation rollback capabilities
- **SignalR Notifications** - Extends existing real-time system
- **BaseEntity Pattern** - Maintains audit trails and soft deletes
- **Result Pattern** - Consistent error handling throughout

## Testing & Validation

### Example Endpoints
```http
GET /api/examples/batch-organize-requests     # Sample requests
GET /api/examples/api-usage                   # Usage workflows
GET /api/examples/system-info                 # System configuration
```

### Validation Endpoint
```http
POST /api/v1/file-actions/validate-batch     # Pre-validate requests
```

## Future Enhancements

- **Priority Processing** - Job prioritization based on user preferences
- **Scheduled Batches** - Time-based batch processing
- **Advanced Filtering** - More sophisticated file selection criteria
- **Performance Analytics** - Detailed processing metrics and optimization
- **Mobile Integration** - Mobile app batch operation support

## Conclusion

This implementation provides a robust, scalable, and maintainable batch file processing system that follows MediaButler's "Simple Made Easy" principles while delivering enterprise-grade capabilities optimized for ARM32 NAS deployment.

The architecture supports the proven patterns from the move-file example while extending MediaButler's existing capabilities, ensuring seamless integration and consistent user experience across all file management operations.