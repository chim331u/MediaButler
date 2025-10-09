# Hangfire Integration - Sprint Plan

## Sprint 1: Infrastructure Setup (Week 1)

### Step 1.1: Create MediaButler.Batch Project ✅
- [x] `dotnet new worker -n MediaButler.Batch -f net8.0`
- [x] Add NuGet packages: Hangfire.Core, Hangfire.AspNetCore, Hangfire.Storage.SQLite
- [x] Add project references: Services, Data, Core
- [x] Create directory structure (Jobs/, Services/, Filters/, Configuration/)
- **Commit:** `89b50cb` - MediaButler.Batch project created

### Step 1.2: Configure Separate Hangfire Database ✅
- [x] Add `HangfireConnection` to appsettings.json (both projects)
- [x] Configure SQLite with WAL mode: `Data Source=/data/mediabutler-hangfire.db;Cache=Shared;Journal Mode=WAL;`
- [x] Verify database paths for Dev vs Production
- **Production DB**: `/data/mediabutler-hangfire.db`
- **Development DB**: `../../temp/mediabutler-hangfire.dev.db`

### Step 1.3: Configure Hangfire in API (Client Mode) ✅
- [x] Remove custom queue: `AddCustomBackgroundTaskQueue(100)`
- [x] Add Hangfire client: `AddHangfire(config => config.UseSQLiteStorage(...))`
- [x] Set `WorkerCount = 0` (API only enqueues)
- [x] Add dashboard: `app.UseHangfireDashboard("/hangfire")`
- [x] Add Hangfire NuGet packages to API project
- [x] Add required using statements (Hangfire, Hangfire.Storage.SQLite, Hangfire.Dashboard)
- **Dashboard URL**: `http://localhost:5000/hangfire` (Development only)

### Step 1.4: Configure Hangfire in Batch (Server Mode) ✅
- [x] Setup Hangfire storage with ARM32 optimization
- [x] Configure server: `WorkerCount = 2`, queues: `["critical", "default", "low-priority"]`
- [x] Setup recurring job registration (RecurringJobRegistrationService.cs)
- [x] Add Serilog packages (Serilog.AspNetCore, Serilog.Settings.Configuration)
- [x] Fix SQLite connection string format (simple path instead of connection string)
- [x] Test Batch worker: database created successfully with WAL mode
- **Commit:** Batch worker configured and tested

---

## Sprint 2: SignalR Integration (Week 2)

### Step 2.1: Create SignalRNotificationClient (Batch) ✅
- [x] Implement `SignalRNotificationClient.cs` in `MediaButler.Batch/Services/`
- [x] Add notification batching (10 items or 500ms flush with Timer)
- [x] Implement methods: `NotifyBatchJobStartedAsync`, `NotifyBatchJobProgressAsync`, `NotifyBatchJobCompletedAsync`, `NotifyBatchJobFailedAsync`
- [x] Add `JobNotification` model class with JobId, JobType, EventType, Message, Data, Timestamp
- [x] Implement automatic flush on batch size reached or timer interval
- [x] Add IDisposable pattern for graceful shutdown with final flush
- **Features**: ConcurrentQueue, SemaphoreSlim for thread-safety, configurable batching

### Step 2.2: Create API Notification Endpoint ✅
- [x] Create `NotificationsController.cs` in `MediaButler.API/Controllers/`
- [x] Add `[HttpPost("batch")]` endpoint accepting BatchNotification[] array
- [x] Implement `RouteNotificationAsync` to forward to SignalR hubs
- [x] Add `BatchNotification` model for deserialization (matches JobNotification)
- [x] Route to FileProcessingHub for batch events (started, progress, completed, failed)
- [x] Route to NotificationHub for scan, training, and error notifications
- **Event Types**: batch.*, scan.*, training.* support

### Step 2.3: Configure HTTP Client in Batch ✅
- [x] Register HttpClient for SignalR API calls with AddHttpClient<SignalRNotificationClient>
- [x] Add configuration: `SignalRClient:ApiBaseUrl`, `NotificationEndpoint`, `BatchSize`, `FlushIntervalMs`, `TimeoutSeconds`
- [x] Add DI registration in `Program.cs` as Singleton
- [x] Configure BaseAddress, Timeout, User-Agent header
- [x] Both API and Batch builds successful
- **Configuration**: Already exists in appsettings.json (both projects)

---

## Sprint 3: Migrate Batch Processing (Week 3)

### Step 3.1: Create BatchFileProcessingJob ✅
- [x] Create `BatchFileProcessingJob.cs` in `MediaButler.Batch/Jobs/Batch/`
- [x] Add attributes: `[Queue("default")]`, `[AutomaticRetry(Attempts = 3, DelaysInSeconds = [30, 60, 120])]`
- [x] Inject: `IFileOrganizationService`, `SignalRNotificationClient`, `ILogger`
- [x] Implement `ProcessBatchAsync` with progress notifications (every 5 files)
- [x] Add ARM32 delay: `await Task.Delay(50)` between files
- [x] Register all required services in Program.cs (DbContext, Repositories, Services)
- [x] Add EF Core SQLite package to MediaButler.Batch
- [x] Fix namespace issues (IFileOrganizationService in Core.Services)
- [x] Build successful
- **Features**: Error handling, continueOnError, success/fail tracking, SignalR integration

### Step 3.2: Refactor FileActionsController ✅
- [x] Replace `IBackgroundTaskQueue` with `IBackgroundJobClient`
- [x] Change `OrganizeBatch`: `_jobClient.Enqueue<IBatchFileProcessor>(...)`
- [x] Update response: return custom job ID
- [x] Create IBatchFileProcessor interface in Core to avoid circular dependency
- [x] Implement interface in BatchFileProcessingJob
- [x] Add Hangfire.Core package to Services project
- [x] Stub out GetBatchStatusAsync and CancelBatchJobAsync for Step 3.3
- **Commit:** Step 3.2 refactoring complete - API now enqueues Hangfire jobs

### Step 3.3: Update Job Status Endpoints ✅
- [x] Implement GetBatchStatusAsync using Hangfire `JobStorage.Current.GetConnection()`
- [x] Implement CancelBatchJobAsync using `BackgroundJob.Delete(jobId)`
- [x] Implement GetBatchJobsAsync using Hangfire monitoring API
- [x] Add helper methods: MapHangfireState, MapStatusToHangfireState, CreateJobResponse
- [x] Handle different DTO types from monitoring API (EnqueuedJobDto, ProcessingJobDto, etc.)
- [x] Use Hangfire job ID as primary tracking identifier
- [x] Remove optional parameters from IBatchFileProcessor (Hangfire limitation)
- **Commit:** Step 3.3 complete - Job monitoring via Hangfire API

### Step 3.4: Test Batch Operations
- [ ] Test batch organize via Web UI
- [ ] Verify SignalR progress updates received
- [ ] Validate job persistence across restart
- [ ] Check Hangfire dashboard at `/hangfire`

---

## Sprint 4: Additional Job Types (Week 4)

### Step 4.1: File Discovery Job
- [ ] Create `FileDiscoveryJob.cs` in `MediaButler.Batch/Jobs/FileProcessing/`
- [ ] Add recurring job: `*/10 * * * *` (every 10 minutes)
- [ ] Send notifications: scan started, files discovered, scan completed
- [ ] Migrate logic from `FileDiscoveryHostedService`

### Step 4.2: Model Training Job
- [ ] Create `ModelTrainingJob.cs` in `MediaButler.Batch/Jobs/MachineLearning/`
- [ ] Add recurring job: `0 3 * * 0` (Sunday 3 AM)
- [ ] Send notifications: training started, epoch progress, completed/failed
- [ ] Add queue: `[Queue("low-priority")]`

### Step 4.3: Database Maintenance Jobs
- [ ] Create `DatabaseMaintenanceJob.cs` in `MediaButler.Batch/Jobs/Maintenance/`
- [ ] Implement VACUUM: `PRAGMA vacuum;`, ANALYZE: `PRAGMA optimize;`
- [ ] Add recurring job: `0 2 * * 0` (Sunday 2 AM)
- [ ] Target both databases: MediaButler + Hangfire

### Step 4.4: Health Check Job
- [ ] Create `HealthCheckJob.cs` in `MediaButler.Batch/Jobs/Monitoring/`
- [ ] Check: disk space, memory usage, database connectivity
- [ ] Add recurring job: `*/5 * * * *` (every 5 minutes)
- [ ] Send notifications: system status updates

---

## Sprint 5: Cleanup & Testing (Week 5)

### Step 5.1: Remove Custom Queue Infrastructure
- [ ] Delete `BackgroundTaskQueue.cs`, `QueuedHostedService.cs`, `IBackgroundTaskQueue.cs`
- [ ] Delete `BackgroundTaskQueueExtensions.cs`, `CustomBatchFileProcessor.cs`
- [ ] Remove `AddCustomBackgroundTaskQueue()` from API Program.cs
- [ ] Remove 6 custom queue files from `MediaButler.Services/Background/`

### Step 5.2: Migrate ARM32MemoryFilter
- [ ] Move `ARM32MemoryMonitor.cs` to `MediaButler.Batch/Filters/`
- [ ] Create `ARM32MemoryFilter.cs` inheriting `JobFilterAttribute, IServerFilter`
- [ ] Implement `OnPerforming`: check memory, requeue if pressure detected
- [ ] Register filter globally: `GlobalJobFilters.Filters.Add(new ARM32MemoryFilter())`

### Step 5.3: Integration Testing
- [ ] Test batch file processing (50 files)
- [ ] Test job persistence: restart Batch worker during job
- [ ] Test SignalR notifications: verify Web UI receives updates
- [ ] Test concurrent jobs: 2 workers processing simultaneously
- [ ] Test retry logic: simulate failure, verify 3 retries

### Step 5.4: Performance Benchmarking
- [ ] Measure memory: API + Batch combined (target: < 300MB)
- [ ] Measure job latency: enqueue to start (target: < 5s)
- [ ] Measure notification latency: job to Web UI (target: < 100ms)
- [ ] Compare vs custom queue: throughput, memory, CPU

---

## Sprint 6: Deployment & Documentation (Week 6)

### Step 6.1: Docker Configuration
- [ ] Create `Dockerfile.batch` for ARM32 deployment
- [ ] Update `docker-compose.yml`: add `mediabutler-batch` service
- [ ] Set memory limits: API 150MB, Batch 150MB
- [ ] Test: `docker-compose up` on ARM32 device

### Step 6.2: Systemd Services
- [ ] Create `/etc/systemd/system/mediabutler-batch.service`
- [ ] Configure: `MemoryMax=150M`, `Restart=always`
- [ ] Add dependency: `After=mediabutler-api.service`
- [ ] Test: `systemctl start mediabutler-batch`

### Step 6.3: Update CLAUDE.md
- [ ] Replace "Background Processing Architecture" section
- [ ] Document Hangfire configuration options
- [ ] Update job types table with SignalR notifications
- [ ] Add dashboard access instructions: `/hangfire`
- [ ] Document dual-database architecture

### Step 6.4: Acceptance Criteria Validation
- [ ] ✅ All batch operations work with Hangfire
- [ ] ✅ Memory footprint < 300MB on ARM32
- [ ] ✅ Job state survives restart
- [ ] ✅ Dashboard accessible at `/hangfire`
- [ ] ✅ SignalR real-time notifications working
- [ ] ✅ 790+ tests pass (update test mocks for Hangfire)

---

## Rollback Plan

### If Hangfire Integration Fails:
1. **Revert code**: `git revert <commit-hash>`
2. **Restore custom queue**: Uncomment `AddCustomBackgroundTaskQueue(100)`
3. **Remove Hangfire packages**: `dotnet remove package Hangfire.*`
4. **Delete Hangfire DB**: `rm /data/mediabutler-hangfire.db`
5. **Restart services**: Both API and old queue system

### Rollback Decision Points:
- Memory > 350MB consistently
- Job processing fails > 10% of time
- SignalR notifications fail > 20% of time
- Tests fail < 80% pass rate

---

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Memory (API + Batch) | < 300MB | `docker stats` or `htop` |
| Job Processing Time | < 2s per file | Hangfire dashboard |
| Notification Latency | < 100ms | Browser DevTools Network tab |
| Job Success Rate | > 95% | Hangfire statistics |
| Code Reduction | -6 files (custom queue) | Git diff |
| Test Pass Rate | 100% (790+ tests) | `dotnet test` |

---

## Estimated Timeline

- **Total Duration**: 6 weeks (30 working days)
- **Sprint Length**: 1 week per sprint
- **Effort Distribution**:
  - Infrastructure: 20%
  - SignalR Integration: 20%
  - Job Migration: 30%
  - Testing & Cleanup: 20%
  - Deployment & Docs: 10%

---

## Dependencies

- ✅ Existing SignalR hubs (`NotificationHub`, `FileProcessingHub`)
- ✅ Existing services (`IFileOrganizationService`, `INotificationService`)
- ✅ SQLite database (`MediaButlerDbContext`)
- ⚠️ Hangfire.Storage.SQLite ARM32 compatibility (verify before Sprint 1)
- ⚠️ Web UI SignalR client updates (minor changes for new notification types)
