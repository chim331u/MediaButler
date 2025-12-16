# MediaButler Go API

High-performance Go rewrite of MediaButler API, optimized for ARM32 NAS deployment.

## 🎯 Goals

- **66% memory reduction**: 150MB → 50MB
- **75% faster response times**: 80ms → 20ms
- **73% smaller binary**: 45MB → 12MB
- Proof of Concept with 15 core endpoints

## 🏗️ Architecture

- **Web Framework**: Chi Router (lightweight, idiomatic)
- **Database**: SQLC (type-safe SQL, zero reflection)
- **Config**: Viper (multi-source configuration)
- **Logging**: Zerolog (zero-allocation logging)
- **ML Integration**: HTTP to Python FastAPI service

## 📋 Prerequisites

- **Go 1.21+** (install via `brew install go`)
- **SQLC** (install via `brew install sqlc` or `go install github.com/sqlc-dev/sqlc/cmd/sqlc@latest`)
- **.NET 8 SDK** (for running existing MediaButler API in parallel)
- **SQLite** (usually pre-installed on macOS)

## 🚀 Quick Start

### 1. Install Dependencies

```bash
# Install Go (if not already installed)
brew install go

# Install SQLC for code generation
brew install sqlc

# Verify installations
go version
sqlc version
```

### 2. Generate Database Code

```bash
# From MediaButler-Go directory
sqlc generate
```

This generates type-safe Go code from SQL queries in `internal/db/queries/*.sql`.

### 3. Download Go Dependencies

```bash
go mod download
go mod tidy
```

### 4. Configure Application

Edit `configs/config.json` to match your environment:

```json
{
  "database": {
    "path": "/path/to/mediabutler.db"
  },
  "ml": {
    "service_url": "http://localhost:5002"
  }
}
```

### 5. Build and Run

```bash
# Build the API server
go build -o bin/mediabutler-api cmd/api/main.go

# Run the server
./bin/mediabutler-api -config configs/config.json

# Or use go run for development
go run cmd/api/main.go -config configs/config.json
```

The API will be available at `http://localhost:5001`

## 📁 Project Structure

```
MediaButler-Go/
├── cmd/
│   ├── api/           # HTTP server entrypoint
│   └── worker/        # Background job worker
├── internal/
│   ├── api/           # HTTP handlers and routing
│   ├── db/            # Database queries and generated code
│   ├── domain/        # Domain entities (TrackedFile, FileStatus)
│   ├── service/       # Business logic services
│   ├── repository/    # Data access layer
│   └── config/        # Configuration management
├── pkg/
│   ├── result/        # Result<T> pattern for error handling
│   └── pagination/    # Pagination utilities
├── configs/           # Configuration files
└── scripts/           # Build and deployment scripts
```

## 🛠️ Development Workflow

### Code Generation

After modifying SQL queries in `internal/db/queries/*.sql`:

```bash
sqlc generate
```

### Running Tests

```bash
# Run all tests
go test ./...

# Run tests with coverage
go test -cover ./...

# Run tests verbosely
go test -v ./...

# Run specific test
go test -run TestFileService_CreateFile ./internal/service
```

### Building for ARM32

```bash
# Cross-compile for ARM32
GOOS=linux GOARCH=arm GOARM=7 go build -ldflags="-s -w" -o bin/mediabutler-api-arm32 cmd/api/main.go

# Build size comparison
ls -lh bin/
```

### Docker Build

```bash
# Build ARM32 Docker image
docker build -f Dockerfile.arm32 -t mediabutler-go:arm32 .

# Run container
docker run -p 5001:5001 \
  -v /path/to/config.json:/app/config.json \
  -v /path/to/data:/data \
  mediabutler-go:arm32
```

## 📊 Performance Testing

### Benchmarking

```bash
# Benchmark specific functions
go test -bench=. -benchmem ./internal/service

# Generate CPU profile
go test -cpuprofile=cpu.prof -bench=. ./internal/service
go tool pprof cpu.prof

# Generate memory profile
go test -memprofile=mem.prof -bench=. ./internal/service
go tool pprof mem.prof
```

### Load Testing

```bash
# Using vegeta
echo "GET http://localhost:5001/api/health" | vegeta attack -duration=30s -rate=100 | vegeta report

# Using k6
k6 run scripts/load-test.js
```

## 🔄 Parallel Deployment with .NET API

Run both APIs side-by-side using nginx:

```nginx
# nginx.conf
upstream dotnet_api {
    server localhost:5000;
}

upstream go_api {
    server localhost:5001;
}

server {
    listen 80;

    # Route core endpoints to Go
    location ~ ^/api/(files|health|processing|stats) {
        proxy_pass http://go_api;
    }

    # Route remaining endpoints to .NET
    location /api/ {
        proxy_pass http://dotnet_api;
    }
}
```

## 🐛 Debugging

### Logging

```bash
# View application logs
tail -f /data/logs/mediabutler.log

# Enable debug logging
# Edit config.json: "logging": { "level": "debug" }
```

### Profiling

```bash
# Enable pprof endpoint in development
# Navigate to http://localhost:6060/debug/pprof/

# Download heap profile
curl http://localhost:6060/debug/pprof/heap > heap.prof
go tool pprof heap.prof
```

## 📝 Current Status

### ✅ Completed (Week 1)

- [x] Project structure and Go module initialization
- [x] EF Core schema export to SQLite SQL
- [x] Configuration management with Viper
- [x] Domain entities (TrackedFile, FileStatus)
- [x] Result<T> pattern for error handling
- [x] SQLC query definitions (40+ queries)
- [x] SQLC configuration setup

### 🔄 In Progress

- [ ] SQLC code generation (requires Go installation)
- [ ] Repository pattern implementation
- [ ] UnitOfWork pattern implementation

### 📅 Next Steps (Week 2)

- [ ] Service layer (FileService, MLClient, StatsService)
- [ ] Python ML service wrapper (FastAPI)
- [ ] Unit tests for services

### 📅 Week 3

- [ ] HTTP API handlers (health, files, processing)
- [ ] Chi router setup with middleware
- [ ] Integration tests

### 📅 Week 4

- [ ] Application bootstrap (main.go)
- [ ] Docker ARM32 build
- [ ] Performance benchmarks
- [ ] E2E testing

## 🔗 Related Documentation

- [Migration Plan](/Users/luca/.claude/plans/eager-purring-puzzle.md)
- [.NET API Documentation](../MediaButler/CLAUDE.md)
- [API Endpoints Reference](../MediaButler/docs/API-Endpoints.md)

## 📄 License

Same license as main MediaButler project.

## 🙋 Support

For issues and questions, refer to the main MediaButler repository.
