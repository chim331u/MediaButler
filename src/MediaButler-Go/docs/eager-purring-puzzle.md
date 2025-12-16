# MediaButler .NET to Go API Migration Plan

## Executive Summary

Migrate MediaButler API from .NET 8 to Go using a **Proof of Concept** approach, focusing on core endpoints with Go-native solutions to achieve significant performance improvements for ARM32 NAS deployment.

**Goals:**
- **Performance**: 66% memory reduction (150MB → 50MB), 75% faster response times
- **Scope**: ~25 core endpoints (40% of 64 total endpoints)
- **Strategy**: Parallel deployment with nginx routing, shared SQLite database
- **Timeline**: 4 weeks for PoC

---

## Phase 1: Core Endpoints (PoC Scope)

### Tier 1: Critical Path - 15 Endpoints

**File Management (9 endpoints) - Week 2-3**
```
GET  /api/files                          # Paginated list with filters
GET  /api/files/by-statuses              # Multi-status filter (Web UI primary)
GET  /api/files/{hash}                   # File detail lookup
POST /api/files                          # File registration
GET  /api/files/pending                  # User confirmation queue
POST /api/files/{hash}/confirm           # Category confirmation
POST /api/files/{hash}/moved             # Organization complete
GET  /api/files/categories               # Dropdown population
POST /api/files/scan                     # Manual folder scan
```

**Health & Monitoring (3 endpoints) - Week 3**
```
GET  /api/health                         # Liveness probe
GET  /api/health/ready                   # Readiness probe
GET  /api/health/detailed                # Diagnostics
```

**Processing (3 endpoints) - Week 3**
```
GET  /api/stats/processing               # Dashboard metrics
GET  /api/processing/queue/status        # Queue monitoring
POST /api/v1/file-actions/ignore/{hash}  # File ignore action
```

### Implementation Order
1. ✅ Health endpoints (validates infrastructure)
2. ✅ Read-only file endpoints (GET /api/files/*)
3. ✅ File mutation endpoints (POST confirm/moved)
4. ✅ Processing endpoints (queue, stats)
5. 🔄 Batch operations (post-PoC)

---

## Phase 2: Go Technology Stack

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Web Framework** | Chi Router | Lightweight (10KB), idiomatic Go, excellent middleware |
| **Database** | SQLC | Compile-time SQL generation, zero reflection, ARM32-friendly |
| **Background Jobs** | Asynq (or goroutines) | Redis-backed queue matching Hangfire architecture |
| **Real-time** | Server-Sent Events | Simpler than WebSockets, one-way fits notifications |
| **ML Integration** | HTTP to Python | Keep existing FastText model, wrap in FastAPI |
| **Config** | Viper | Multi-source config (JSON/ENV), live reload |
| **Logging** | Zerolog | Zero-allocation, 10x faster than stdlib |

### Key Dependencies
```go
github.com/go-chi/chi/v5              // HTTP routing
github.com/kyleconroy/sqlc            // Type-safe SQL
github.com/hibiken/asynq              // Background jobs (optional Redis)
github.com/spf13/viper                // Configuration
github.com/rs/zerolog                 // Structured logging
github.com/mattn/go-sqlite3           // SQLite driver
```

---

## Phase 3: Project Structure

```
mediabutler-go/
├── cmd/
│   ├── api/main.go                    # HTTP server entrypoint
│   └── worker/main.go                 # Background job worker
├── internal/
│   ├── api/
│   │   ├── handlers/                  # HTTP handlers (controllers)
│   │   │   ├── files.go               # FilesController → 600 LOC
│   │   │   ├── health.go              # HealthController → 200 LOC
│   │   │   ├── processing.go          # ProcessingController → 200 LOC
│   │   │   └── stats.go               # StatsController → 300 LOC
│   │   ├── middleware/                # CORS, logging, recovery
│   │   └── router.go                  # Chi router setup
│   ├── db/
│   │   ├── queries/                   # SQLC SQL definitions
│   │   │   ├── schema.sql             # Exported from EF Core
│   │   │   └── files.sql              # TrackedFile queries
│   │   └── *.go                       # Generated SQLC code
│   ├── domain/
│   │   ├── file.go                    # TrackedFile business logic
│   │   └── status.go                  # FileStatus enum
│   ├── service/
│   │   ├── file_service.go            # IFileService implementation
│   │   ├── ml_client.go               # ML HTTP client
│   │   └── stats_service.go           # Statistics service
│   ├── repository/
│   │   ├── file_repo.go               # Repository pattern
│   │   └── transaction.go             # UnitOfWork pattern
│   └── config/
│       └── config.go                  # Viper configuration
├── pkg/
│   ├── result/result.go               # Result<T> pattern
│   └── pagination/pagination.go       # Pagination utilities
├── configs/
│   └── config.json                    # Configuration file
├── Dockerfile.arm32                   # ARM32 Docker build
└── Makefile                           # Build automation
```

**Estimated LOC: ~5,000 (vs .NET: ~15,000)**

---

## Phase 4: Data Layer Design

### Repository Pattern in Go

**Interface:**
```go
type FileRepository interface {
    // Core CRUD
    GetByHash(ctx context.Context, hash string) (*domain.TrackedFile, error)
    GetByStatus(ctx context.Context, status domain.FileStatus) ([]domain.TrackedFile, error)
    Create(ctx context.Context, file *domain.TrackedFile) error
    Update(ctx context.Context, file *domain.TrackedFile) error
    SoftDelete(ctx context.Context, hash string, reason *string) error

    // Workflow queries
    GetFilesReadyForClassification(ctx context.Context, limit int) ([]domain.TrackedFile, error)
    GetFilesAwaitingConfirmation(ctx context.Context) ([]domain.TrackedFile, error)

    // Pagination
    GetFilesPaged(ctx context.Context, params PagedQuery) (*PagedResult, error)
    GetFilesByStatuses(ctx context.Context, params MultiStatusQuery) (*PagedResult, error)

    // Analytics
    GetProcessingStats(ctx context.Context) (map[domain.FileStatus]int, error)
    GetDistinctCategories(ctx context.Context) ([]string, error)
}
```

### SQLC Query Examples
```sql
-- internal/db/queries/files.sql

-- name: GetFileByHash :one
SELECT * FROM tracked_files
WHERE hash = ? AND is_active = 1
LIMIT 1;

-- name: GetFilesByStatuses :many
SELECT * FROM tracked_files
WHERE status IN (sqlc.slice('statuses'))
  AND is_active = 1
  AND (sqlc.narg('search_term') IS NULL
       OR file_name LIKE '%' || sqlc.narg('search_term') || '%')
ORDER BY last_update_date DESC
LIMIT ? OFFSET ?;

-- name: GetProcessingStats :many
SELECT status, COUNT(*) as count
FROM tracked_files
WHERE is_active = 1
GROUP BY status;
```

### Transaction Management (UnitOfWork)
```go
type Transaction struct {
    tx      *sql.Tx
    queries *db.Queries
}

func (t *Transaction) Files() FileRepository {
    return &fileRepository{queries: t.queries}
}

// Usage
txn, _ := uow.Begin(ctx)
defer txn.Rollback()

file, _ := txn.Files().GetByHash(ctx, hash)
file.ConfirmCategory(category)
txn.Files().Update(ctx, file)

txn.Commit()
```

### Audit Trail (BaseEntity Pattern)
```go
type BaseEntity struct {
    ID             int64      `db:"id"`
    CreatedDate    time.Time  `db:"created_date"`
    LastUpdateDate time.Time  `db:"last_update_date"`
    IsActive       bool       `db:"is_active"`
    Note           *string    `db:"note"`
}

func (e *BaseEntity) MarkAsModified() {
    e.LastUpdateDate = time.Now().UTC()
}

func (e *BaseEntity) SoftDelete(reason *string) {
    e.IsActive = false
    e.Note = reason
}
```

---

## Phase 5: Performance Optimizations

### ARM32 Targets

| Metric | .NET Baseline | Go Target | Improvement |
|--------|---------------|-----------|-------------|
| Memory (idle) | 150MB | 50MB | **66% reduction** |
| Memory (peak) | 280MB | 120MB | **57% reduction** |
| Response (p50) | 80ms | 20ms | **75% faster** |
| Response (p95) | 400ms | 150ms | **62% faster** |
| Binary size | 45MB | 12MB | **73% smaller** |
| Cold start | 3.5s | 0.8s | **77% faster** |

### Optimization Techniques

**1. Connection Pooling**
```go
db.SetMaxOpenConns(1)      // SQLite: single writer
db.SetMaxIdleConns(2)      // Keep connections warm
```

**2. Index Preservation**
```sql
-- Port all 9 EF Core indexes
CREATE INDEX IX_TrackedFiles_Status_IsActive
    ON tracked_files(status, is_active);
CREATE INDEX IX_TrackedFiles_MultiStatus_Query
    ON tracked_files(status, is_active, last_update_date);
-- ... 7 more indexes
```

**3. Memory Management**
```go
debug.SetGCPercent(50)                          // Aggressive GC
debug.SetMemoryLimit(150 * 1024 * 1024)         // 150MB hard limit
```

**4. Streaming Responses**
```go
// Stream large result sets instead of buffering
for file := range svc.StreamFiles(ctx, params) {
    json.NewEncoder(w).Encode(file)
}
```

**5. ARM32 Compilation**
```bash
GOOS=linux GOARCH=arm GOARM=7 go build -ldflags="-s -w" -o mediabutler-api
```

---

## Phase 6: Migration Path

### Parallel Deployment with nginx

```
┌─────────────────┐
│  API Gateway    │
│  (nginx)        │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
┌───▼───┐ ┌──▼───┐
│.NET   │ │Go    │
│API    │ │API   │
│:5000  │ │:5001 │
└───┬───┘ └──┬───┘
    │         │
┌───▼─────────▼───┐
│  SQLite DB      │
│  (shared)       │
└─────────────────┘
```

**nginx Routing Configuration:**
```nginx
# Route Tier 1 endpoints to Go
location ~ ^/api/(files|health|processing|stats) {
    proxy_pass http://localhost:5001;
}

# Route remaining endpoints to .NET
location /api/ {
    proxy_pass http://localhost:5000;
}
```

### API Compatibility

**JSON Response Format (PascalCase preserved):**
```go
type TrackedFileResponse struct {
    Hash              string    `json:"hash"`
    FileName          string    `json:"fileName"`          // Match .NET casing
    OriginalPath      string    `json:"originalPath"`
    Status            string    `json:"status"`
    SuggestedCategory *string   `json:"suggestedCategory"` // Nullable
    CreatedDate       time.Time `json:"createdDate"`
}
```

**Status Code Mapping:**
- 200 OK → Success with body
- 400 Bad Request → Validation errors
- 404 Not Found → Resource not found
- 500 Internal Server Error → Unexpected error

### Testing Strategy

**Test Pyramid:**
- **70% Unit Tests**: Service layer, domain logic
- **25% Integration Tests**: Repository, HTTP handlers
- **5% E2E Tests**: Postman collection against both APIs

**Compatibility Validation:**
```bash
# Run E2E tests against both APIs
newman run api-tests.json --env dotnet.json
newman run api-tests.json --env go.json
diff dotnet-results.json go-results.json  # Validate identical responses
```

---

## Phase 7: Implementation Roadmap

### Week 1: Foundation (Infrastructure)

**Day 1-2: Setup**
- [ ] Initialize Go module: `go mod init mediabutler-go`
- [ ] Create directory structure following layout above
- [ ] Setup SQLC: `internal/db/queries/schema.sql`
- [ ] Export EF Core schema to SQLite-compatible SQL

**Day 3-4: Configuration & Domain**
- [ ] `internal/config/config.go` - Viper configuration
- [ ] `internal/domain/file.go` - TrackedFile domain entity
- [ ] `pkg/result/result.go` - Result<T> pattern
- [ ] `internal/domain/status.go` - FileStatus enum

**Day 5-7: Data Layer**
- [ ] `internal/db/queries/files.sql` - SQLC query definitions
- [ ] Generate SQLC code: `sqlc generate`
- [ ] `internal/repository/file_repo.go` - Repository implementation
- [ ] `internal/repository/transaction.go` - UnitOfWork pattern

### Week 2: Services (Business Logic)

**Day 8-10: Core Services**
- [ ] `internal/service/file_service.go` - File management (600 LOC)
- [ ] `internal/service/ml_client.go` - ML HTTP client (200 LOC)
- [ ] `internal/service/stats_service.go` - Statistics (300 LOC)
- [ ] Unit tests for services

**Day 11-14: Python ML Service**
- [ ] Wrap existing ML.NET model in FastAPI
- [ ] HTTP endpoints: POST /classify, GET /health
- [ ] Docker container for ML service
- [ ] Integration tests with Go client

### Week 3: HTTP API (Handlers & Routing)

**Day 15-16: Infrastructure**
- [ ] `internal/api/router.go` - Chi router setup
- [ ] `internal/api/middleware/` - CORS, logging, recovery, request ID
- [ ] `internal/api/handlers/health.go` - Health endpoints

**Day 17-19: File Endpoints**
- [ ] `internal/api/handlers/files.go` - File CRUD (600 LOC)
  - GET /api/files (pagination)
  - GET /api/files/by-statuses
  - GET /api/files/{hash}
  - POST /api/files/{hash}/confirm
  - POST /api/files/{hash}/moved
- [ ] Integration tests with test database

**Day 20-21: Processing & Stats**
- [ ] `internal/api/handlers/processing.go` - Processing endpoints
- [ ] `internal/api/handlers/stats.go` - Statistics endpoints
- [ ] E2E tests with Postman

### Week 4: Deployment & Validation

**Day 22-23: Application Bootstrap**
- [ ] `cmd/api/main.go` - HTTP server entrypoint
- [ ] Dependency injection setup
- [ ] Graceful shutdown handling
- [ ] `Dockerfile.arm32` - Multi-stage build
- [ ] `docker-compose.yml` - Development stack

**Day 24-25: Background Jobs (Optional for PoC)**
- [ ] `internal/jobs/file_discovery.go` - Folder scanning
- [ ] Asynq task handler or goroutine-based queue
- [ ] `cmd/worker/main.go` - Worker entrypoint

**Day 26-28: Testing & Performance**
- [ ] Run full test suite
- [ ] Performance benchmarks vs .NET baseline
- [ ] Memory profiling (pprof)
- [ ] Load testing with vegeta/k6
- [ ] Document performance improvements

---

## Phase 8: Critical Files (Priority Order)

### Must Create (Foundation)
1. `internal/config/config.go` - Configuration management (Viper)
2. `internal/db/queries/schema.sql` - Database schema
3. `internal/domain/file.go` - TrackedFile entity
4. `pkg/result/result.go` - Result<T> pattern

### Data Layer
5. `internal/db/queries/files.sql` - SQLC queries
6. `internal/repository/file_repo.go` - Repository
7. `internal/repository/transaction.go` - UnitOfWork

### Services
8. `internal/service/file_service.go` - Core business logic
9. `internal/service/ml_client.go` - ML integration
10. `internal/service/stats_service.go` - Statistics

### HTTP API
11. `internal/api/router.go` - Routing setup
12. `internal/api/handlers/health.go` - Health checks
13. `internal/api/handlers/files.go` - File endpoints
14. `internal/api/handlers/processing.go` - Processing endpoints

### Deployment
15. `cmd/api/main.go` - Application entrypoint
16. `Dockerfile.arm32` - Container build
17. `docker-compose.yml` - Dev environment
18. `Makefile` - Build automation

---

## Success Criteria

### Performance Metrics (Validated on ARM32)
- ✅ Memory idle: <50MB (vs .NET 150MB)
- ✅ Memory peak: <120MB (vs .NET 280MB)
- ✅ Response p50: <20ms (vs .NET 80ms)
- ✅ Response p95: <150ms (vs .NET 400ms)
- ✅ Binary size: <12MB (vs .NET 45MB)

### Functional Requirements
- ✅ All 15 Tier 1 endpoints functional
- ✅ API responses match .NET format exactly
- ✅ E2E test suite passes against Go API
- ✅ Shared database works with both APIs

### Quality Gates
- ✅ 70% unit test coverage
- ✅ Zero critical bugs
- ✅ Zero data corruption
- ✅ Graceful degradation on errors

---

## Risk Mitigation

### Technical Risks

| Risk | Mitigation |
|------|------------|
| ML integration complexity | Use simple HTTP API, keep Python ML service |
| SQLite locking issues | WAL mode, single writer pattern |
| SignalR client incompatibility | Provide SSE + WebSocket fallback |

### Operational Risks

| Risk | Mitigation |
|------|------------|
| Data migration failure | Shared database, no migration needed for PoC |
| Client breakage | Extensive E2E testing, JSON schema validation |
| Performance regression | Benchmarks, rollback via nginx routing |

### Rollback Plan

**Triggers:**
- Memory >250MB sustained
- Error rate >5%
- Response time >1000ms p95

**Procedure:**
1. Update nginx to route 100% to .NET
2. Stop Go API
3. Investigate offline
4. Document root cause

---

## Next Steps

1. **Review & Approve**: Validate technology choices
2. **Environment Setup**: Provision ARM32 dev board
3. **Week 1 Sprint**: Infrastructure + Data Layer
4. **Week 2 Sprint**: Services + ML Integration
5. **Week 3 Sprint**: HTTP API
6. **Week 4 Sprint**: Deployment + Validation
7. **Go/No-Go Decision**: Evaluate PoC results

---

## Appendix: Technology Decision Matrix

| Component | Options Considered | Selected | Why |
|-----------|-------------------|----------|-----|
| Web Framework | Gin, Echo, Chi, stdlib | **Chi** | Lightweight, idiomatic, context-aware |
| Database | GORM, sqlc, database/sql | **SQLC** | Type-safe, no reflection, predictable perf |
| Jobs | Goroutines, Temporal, Asynq | **Asynq** | Matches Hangfire, persistence, monitoring |
| Real-time | WebSockets, SSE, Polling | **SSE** | Simpler, one-way fits use case |
| ML | ONNX, gRPC, HTTP | **HTTP** | Keep existing model, easier debugging |
| Config | Viper, envconfig, koanf | **Viper** | Multi-source, live reload, nested structs |
| Logging | logrus, zap, zerolog | **Zerolog** | Zero-alloc, fastest, smallest footprint |

---

**Estimated Total Effort: 4 weeks (1 developer)**
**Estimated LOC: ~5,000 Go (vs ~15,000 .NET)**
**Expected Performance Gain: 66% memory reduction, 75% faster responses**
