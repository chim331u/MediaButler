# 🎩 MediaButler - NEXT STEP Implementation Plan

[![Version](https://img.shields.io/badge/version-1.0.6-blue.svg)]()
[![Platform](https://img.shields.io/badge/platform-ARM32%20|%20ARM64%20|%20x64-green.svg)]()
[![.NET](https://img.shields.io/badge/.NET-8.0%20|%2010-purple.svg)]()
[![Docker](https://img.shields.io/badge/docker-ready-blue.svg)]()

---

## 📊 STATUS OVERVIEW (Last Updated: 2025-01-18)

### ✅ COMPLETED TASKS

| Task | Status | Completed Date | Commit | Notes |
|------|--------|----------------|--------|-------|
| Add ML after file scan complete | ✅ DONE | 2025-01-18 | `7721340` | Switched from pattern matching to ML.NET trained model |
| Schedule file scan Hangfire job | ✅ DONE | 2025-01-18 | `73003a2` | Created FileDiscoveryJob (currently every 5 min) |
| Replace FileSystemWatcher with Hangfire | ✅ DONE | 2025-01-18 | `73003a2` | Removed ~350 lines of FileSystemWatcher code |

---

## 🎯 SPRINT 1: Critical Production Features (2-3 days)

### Priority: HIGH | Target: Week 1

#### 1.1 Adjust File Scan Schedule to 12 Hours ⏰
**Current**: Every 5 minutes (`*/5 * * * *`)
**Target**: Every 12 hours (`0 */12 * * *`)

**Files to Modify**:
- `src/MediaButler.API/appsettings.json:159` - Keep at 5 min for development
- `src/MediaButler.API/appsettings.Production.json` - Change to 12 hours

**Implementation**:
```json
// appsettings.Development.json (keep for quick testing)
"FileDiscovery": {
  "Enabled": true,
  "CronExpression": "*/5 * * * *"
}

// appsettings.Production.json (production use)
"FileDiscovery": {
  "Enabled": true,
  "CronExpression": "0 */12 * * *"  // Every 12 hours at minute 0
}
```

**Estimated Time**: 15 minutes
**Testing**: Verify cron expression in Hangfire Dashboard

---

#### 1.2 Create Housekeeping Job - Log Cleanup 🧹
**Purpose**: Delete log files older than 30 days
**Schedule**: Daily at 2:00 AM (`0 2 * * *`)

**Implementation Steps**:

1. **Create Job Class** (`src/MediaButler.API/Jobs/Recurring/LogCleanupJob.cs`):
```csharp
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaButler.API.Jobs.Recurring;

/// <summary>
/// Hangfire recurring job for cleaning up old log files.
/// Removes log files older than configured retention period (default: 30 days).
/// </summary>
[Queue("low-priority")]
[AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 60, 300 })]
public class LogCleanupJob
{
    private readonly ILogger<LogCleanupJob> _logger;
    private readonly IConfiguration _configuration;
    private const int DefaultRetentionDays = 30;

    public LogCleanupJob(
        ILogger<LogCleanupJob> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    [JobDisplayName("Log Cleanup - Delete Old Logs")]
    public async Task CleanupOldLogsAsync()
    {
        _logger.LogInformation("Log cleanup job started");

        try
        {
            var logPath = _configuration.GetValue<string>("Serilog:WriteTo:1:Args:path", "/data/logs/");
            var logDirectory = Path.GetDirectoryName(logPath) ?? "/data/logs";
            var retentionDays = _configuration.GetValue<int>("Serilog:ARM32Optimization:LogRetentionDays", DefaultRetentionDays);
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

            if (!Directory.Exists(logDirectory))
            {
                _logger.LogWarning("Log directory not found: {LogDirectory}", logDirectory);
                return;
            }

            // Find all .log files older than retention period
            var logFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.AllDirectories);
            var oldLogs = logFiles
                .Where(f => File.GetCreationTimeUtc(f) < cutoffDate)
                .ToList();

            _logger.LogInformation("Found {Count} log files older than {Days} days", oldLogs.Count, retentionDays);

            var deletedCount = 0;
            var deletedSize = 0L;

            foreach (var logFile in oldLogs)
            {
                try
                {
                    var fileInfo = new FileInfo(logFile);
                    var fileSize = fileInfo.Length;

                    File.Delete(logFile);
                    deletedCount++;
                    deletedSize += fileSize;

                    _logger.LogDebug("Deleted old log file: {LogFile} ({Size} bytes)", logFile, fileSize);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete log file: {LogFile}", logFile);
                }
            }

            _logger.LogInformation(
                "Log cleanup completed. Deleted {DeletedCount}/{TotalCount} files, freed {FreedMB:F2} MB",
                deletedCount, oldLogs.Count, deletedSize / (1024.0 * 1024.0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Log cleanup job failed");
            throw;
        }
    }
}
```

2. **Register Job** (`src/MediaButler.API/Program.cs`):
```csharp
// Add after line 135 (FileDiscoveryJob registration)
builder.Services.AddScoped<MediaButler.API.Jobs.Recurring.LogCleanupJob>();
```

3. **Enable in RecurringJobRegistrationService** (`src/MediaButler.API/Services/RecurringJobRegistrationService.cs:83-96`):
```csharp
// Uncomment and update existing code (lines 83-96)
var logCleanupConfig = recurringJobsConfig.GetSection("LogCleanup");
if (logCleanupConfig.GetValue<bool>("Enabled", false))
{
    var cronExpression = logCleanupConfig["CronExpression"] ?? "0 2 * * *";
    _logger.LogInformation("Registering LogCleanup job with cron: {Cron}", cronExpression);

    _recurringJobManager.AddOrUpdate<MediaButler.API.Jobs.Recurring.LogCleanupJob>(
        "log-cleanup",
        job => job.CleanupOldLogsAsync(),
        cronExpression,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    _logger.LogInformation("LogCleanup recurring job registered successfully");
}
```

4. **Update Configuration** (`src/MediaButler.API/appsettings.json:169-172`):
```json
"LogCleanup": {
  "Enabled": true,  // Change from false
  "CronExpression": "0 2 * * *"  // Daily at 2:00 AM
}
```

**Estimated Time**: 2 hours
**Testing**:
- Check Hangfire Dashboard for job registration
- Manually trigger job to verify log deletion
- Monitor logs for completion messages

---

#### 1.3 Create ML Training Recurring Job 🤖
**Purpose**: Retrain ML model weekly to improve accuracy
**Schedule**: Weekly on Sunday at 3:00 AM (`0 3 * * 0`)

**Implementation Steps**:

1. **Create Job Class** (`src/MediaButler.API/Jobs/Recurring/ModelTrainingJob.cs`):
```csharp
using Hangfire;
using MediaButler.Services.ML;
using Microsoft.Extensions.Logging;

namespace MediaButler.API.Jobs.Recurring;

/// <summary>
/// Hangfire recurring job for ML model training.
/// Retrains classification model weekly using database training data.
/// </summary>
[Queue("low-priority")]
[AutomaticRetry(Attempts = 1, DelaysInSeconds = new[] { 300 })]  // Retry once after 5 min
[DisableConcurrentExecution(timeoutInSeconds: 1800)]  // 30 min max, no concurrent runs
public class ModelTrainingJob
{
    private readonly IDatabaseTrainingService _trainingService;
    private readonly ILogger<ModelTrainingJob> _logger;

    public ModelTrainingJob(
        IDatabaseTrainingService trainingService,
        ILogger<ModelTrainingJob> logger)
    {
        _trainingService = trainingService ?? throw new ArgumentNullException(nameof(trainingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [JobDisplayName("ML Model Training - Weekly Retraining")]
    public async Task TrainModelAsync()
    {
        _logger.LogInformation("Weekly ML model training job started");

        try
        {
            var result = await _trainingService.TrainModelFromDatabaseAsync(CancellationToken.None);

            if (result.IsSuccess)
            {
                var trainingResult = result.Value;
                _logger.LogInformation(
                    "Weekly ML training completed successfully. " +
                    "Model Version: {Version}, " +
                    "Accuracy: {Accuracy:P2}, " +
                    "Training Samples: {TrainingSamples}, " +
                    "Categories: {Categories}, " +
                    "Duration: {Duration}s",
                    trainingResult.ModelVersion,
                    trainingResult.Metrics.Accuracy,
                    trainingResult.TrainingSamples.Count,
                    trainingResult.Categories.Count,
                    trainingResult.TrainingDuration.TotalSeconds);

                // Log per-category metrics
                foreach (var categoryMetric in trainingResult.Metrics.PerCategoryMetrics)
                {
                    _logger.LogDebug(
                        "Category '{Category}': Precision={Precision:P2}, Recall={Recall:P2}, F1={F1:P2}",
                        categoryMetric.Category,
                        categoryMetric.Precision,
                        categoryMetric.Recall,
                        categoryMetric.F1Score);
                }
            }
            else
            {
                _logger.LogError("Weekly ML training failed: {Error}", result.Error);
                throw new InvalidOperationException($"Model training failed: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weekly ML model training job failed with exception");
            throw;
        }
    }
}
```

2. **Register Job** (`src/MediaButler.API/Program.cs`):
```csharp
// Add after LogCleanupJob registration
builder.Services.AddScoped<MediaButler.API.Jobs.Recurring.ModelTrainingJob>();
```

3. **Enable in RecurringJobRegistrationService** (`src/MediaButler.API/Services/RecurringJobRegistrationService.cs:53-66`):
```csharp
// Uncomment and update existing code (lines 53-66)
var modelTrainingConfig = recurringJobsConfig.GetSection("ModelTraining");
if (modelTrainingConfig.GetValue<bool>("Enabled", false))
{
    var cronExpression = modelTrainingConfig["CronExpression"] ?? "0 3 * * 0";
    _logger.LogInformation("Registering ModelTraining job with cron: {Cron}", cronExpression);

    _recurringJobManager.AddOrUpdate<MediaButler.API.Jobs.Recurring.ModelTrainingJob>(
        "model-training",
        job => job.TrainModelAsync(),
        cronExpression,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });

    _logger.LogInformation("ModelTraining recurring job registered successfully");
}
```

4. **Update Configuration** (`src/MediaButler.API/appsettings.json:161-164`):
```json
"ModelTraining": {
  "Enabled": true,  // Change from false
  "CronExpression": "0 3 * * 0"  // Weekly Sunday at 3:00 AM
}
```

**Estimated Time**: 3 hours
**Testing**:
- Manually trigger job in Hangfire Dashboard
- Verify model file updated in `models/` directory
- Check model version incremented
- Verify hot reload picks up new model

---

#### 1.4 Test All Recurring Jobs ✅
**Purpose**: Ensure all Hangfire jobs work correctly

**Test Checklist**:
- [ ] FileDiscoveryJob executes on schedule
- [ ] LogCleanupJob deletes old logs correctly
- [ ] ModelTrainingJob trains and saves model
- [ ] Jobs appear in Hangfire Dashboard
- [ ] Retry logic works on failures
- [ ] Logs are properly written

**Estimated Time**: 2 hours

---

## 🔧 SPRINT 2: Code Quality & Refactoring (3-4 days)

### Priority: MEDIUM | Target: Week 2

#### 2.1 Run Unused Code Analysis 🔍

**Tools**:
```bash
# Install dotnet-unused
dotnet tool install -g dotnet-unused

# Run analysis
dotnet unused --solution MediaButler.sln

# Alternative: Use Roslyn analyzers
dotnet add package Microsoft.CodeAnalysis.NetAnalyzers
```

**Known Candidates for Review**:
- `IPredictionService` (replaced by `IClassificationService`) - Keep for backward compatibility
- `PredictionService.PredictAsync()` - May still be used in tests
- Unused repository methods
- Deprecated DTOs/models

**Output**: Generate report of unused code for manual review

**Estimated Time**: 4 hours

---

#### 2.2 Remove Unused Methods and Code 🧹

**Process**:
1. Review unused code analysis report
2. Verify code is truly unused (check tests, dependencies)
3. Remove safe candidates
4. Update tests if needed
5. Run full test suite to verify no breakage

**Safety Rules**:
- Don't remove public APIs (may be used by external consumers)
- Keep code marked as "Future use"
- Don't remove test utilities

**Estimated Time**: 6 hours

---

#### 2.3 Check Architectural Issues and Fix Them 🏗️

**Review Areas**:

1. **Service Boundaries**:
   - Verify ML services isolated in `MediaButler.ML`
   - Check for circular dependencies
   - Ensure clean layer separation

2. **Dependency Injection**:
   - Verify correct lifetimes (Scoped vs Singleton)
   - Check for service locator anti-pattern
   - Validate background job service resolution

3. **"Simple Made Easy" Compliance**:
   - Check for complecting (braiding of concerns)
   - Verify single responsibility
   - Ensure values over state
   - Validate declarative patterns

4. **Error Handling**:
   - Ensure Result pattern used consistently
   - Verify all exceptions logged
   - Check error classification coverage

**Tools**:
```bash
# Install ArchUnitNET for architecture testing
dotnet add package ArchUnitNET --version 0.10.6

# Create architecture tests
# tests/MediaButler.Tests.Architecture/ArchitectureTests.cs
```

**Estimated Time**: 8 hours

---

#### 2.4 Fix Tests After ML Architecture Change 🧪

**Test Categories to Review**:

1. **Unit Tests** (250+ tests):
   - Update mocks from `IPredictionService` to `IClassificationService`
   - Verify ML.NET prediction expectations
   - Fix TokenizerService test (1 pre-existing failure)

2. **Integration Tests** (300+ tests):
   - Update file processing workflow tests
   - Verify model loading in test environment
   - Check classification result assertions

3. **Acceptance Tests** (240+ tests):
   - Validate end-to-end file processing
   - Verify ML classification in full workflow
   - Check confidence threshold behaviors

**Commands**:
```bash
# Run all tests
dotnet test

# Run by category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Acceptance"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

**Target**: 100% test pass rate (790+ tests)

**Estimated Time**: 8 hours

---

## 📚 SPRINT 3: Documentation Updates (1-2 days)

### Priority: MEDIUM | Target: Week 2-3

#### 3.1 Update CLAUDE.md 📖

**Sections to Update**:

1. **ML Classification Pipeline**:
   - Document switch from pattern matching to ML.NET
   - Add FastTextClassificationService details
   - Document hot reload feature
   - Add model training workflow

2. **Hangfire Recurring Jobs**:
   - Update recurring jobs table with all 3 jobs
   - Add cron expressions
   - Document job priorities and queues

3. **Background Processing Architecture**:
   - Update architecture diagram
   - Document single-process combined mode
   - Add job registration flow

4. **Configuration Section**:
   - Add Hangfire:RecurringJobs configuration details
   - Document log cleanup settings
   - Add model training configuration

**Estimated Time**: 2 hours

---

#### 3.2 Update API Documentation 📄

**File**: `docs/api-documentation.md`

**Sections to Add/Update**:

1. **ML Training Endpoints**:
   - `GET /api/training/trainModel` - Trigger manual training
   - Document training response schema
   - Add model version tracking

2. **Model Info Endpoints**:
   - Document model metadata endpoint
   - Add cache statistics
   - Document category discovery

3. **Classification Workflow**:
   - Update workflow diagram
   - Document confidence thresholds
   - Add alternative predictions

**Estimated Time**: 2 hours

---

#### 3.3 Update README.md 📝

**Updates Needed**:

1. **Features Section**:
   - Change "ML classification (placeholder)" to "ML.NET SDCA Maximum Entropy"
   - Add hot reload support
   - Add LRU prediction caching
   - Add weekly auto-retraining

2. **Architecture Section**:
   - Update to reflect Hangfire recurring jobs
   - Add recurring jobs list:
     - File Discovery (every 12 hours)
     - Log Cleanup (daily)
     - Model Training (weekly)

3. **Version Bump**:
   - Update from 1.0.6 to 1.1.0 (feature additions)

**Estimated Time**: 1 hour

---

#### 3.4 Update Deployment Guide 📦

**File**: `docs/deployment-guide.md`

**Sections to Add/Update**:

1. **Model Files**:
   - Document model file requirements
   - Add model versioning
   - Document hot reload behavior

2. **Hangfire Configuration**:
   - Add recurring jobs configuration
   - Document Hangfire Dashboard access
   - Add job monitoring instructions

3. **Production Checklist**:
   - Verify model file exists
   - Check recurring job schedules
   - Configure log retention
   - Set up model training schedule

**Estimated Time**: 2 hours

---

## 📊 ADDITIONAL RECOMMENDATIONS

### Performance Monitoring

**Add Metrics For**:
- ML classification latency (per file)
- Model cache hit rate
- Hangfire job execution times
- File processing throughput
- Background queue depth

**Tools**:
- Application Insights (Azure)
- Prometheus + Grafana
- Custom metrics endpoint

---

### Health Checks Enhancement

**Verify/Add**:
- ✅ ML model health check (already exists: `MLModelHealthCheck`)
- [ ] Database connection health check
- [ ] Hangfire server health check
- [ ] File system access health check
- [ ] Model file existence check

**Endpoint**: `GET /api/health`

---

### Logging Improvements

**Enhancements**:
- [ ] Add structured logging for all ML predictions
- [ ] Add timing metrics to all Hangfire jobs
- [ ] Ensure job start/complete/error logged
- [ ] Add correlation IDs for file processing workflows
- [ ] Add ML model version to classification logs

---

## 🎯 SPRINT SUMMARY

| Sprint | Duration | Focus | Tasks | Priority |
|--------|----------|-------|-------|----------|
| Sprint 1 | 2-3 days | Production Features | File scan schedule, LogCleanup, ModelTraining, Testing | **HIGH** |
| Sprint 2 | 3-4 days | Code Quality | Unused code removal, Architecture fixes, Test fixes | **MEDIUM** |
| Sprint 3 | 1-2 days | Documentation | CLAUDE.md, README.md, API docs, Deployment guide | **MEDIUM** |

**Total Estimated Time**: 6-9 days (1.5-2 weeks)

---

## 📝 NOTES

- All Hangfire jobs should use `low-priority` queue to avoid blocking file processing
- Model training job should have concurrent execution disabled
- Log cleanup should be configurable via appsettings (retention days)
- Keep development file scan at 5 minutes for quick testing
- Document all cron expressions clearly
- Add proper error handling and retry logic to all jobs
- Ensure all jobs are properly registered in Hangfire Dashboard

---

**Last Updated**: 2025-01-18
**Version**: 1.1.0-dev
**Status**: Ready for Sprint 1 Implementation
