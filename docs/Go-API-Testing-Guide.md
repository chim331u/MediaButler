# Go API Testing Guide

Complete guide for running and testing the MediaButler Go API.

## 📋 Prerequisites

1. **Go 1.23+** installed: `go version`
2. **VS Code** with Go extension installed
3. **Optional**: REST Client extension for VS Code

## 🚀 Running the API

### Method 1: Terminal (Quickest)

```bash
# Navigate to Go project
cd src/MediaButler-Go

# Run the API
go run cmd/api/main.go
```

Expected output:
```json
{"level":"info","version":"1.0.0","port":"5001","time":"2025-12-16T...","message":"Starting MediaButler Go API"}
{"level":"info","path":"/data/mediabutler.db","time":"2025-12-16T...","message":"Database initialized"}
{"level":"info","time":"2025-12-16T...","message":"Services initialized"}
{"level":"info","time":"2025-12-16T...","message":"Router configured with all endpoints"}
{"level":"info","address":"0.0.0.0:5001","time":"2025-12-16T...","message":"HTTP server starting"}
```

### Method 2: VS Code Tasks (Recommended)

1. Press `Cmd+Shift+P` (Mac) or `Ctrl+Shift+P` (Windows/Linux)
2. Type "Tasks: Run Task"
3. Select "Go: Run API"

Or use keyboard shortcut: `Cmd+Shift+B` (Mac) / `Ctrl+Shift+B` (Windows/Linux)

### Method 3: VS Code Debugger

1. Open `cmd/api/main.go`
2. Press `F5` or click "Run > Start Debugging"
3. Select "Go API - Debug" configuration
4. Set breakpoints by clicking left of line numbers

### Method 4: Build and Run Binary

```bash
cd src/MediaButler-Go

# Build optimized binary
go build -ldflags="-s -w" -o bin/mediabutler-api cmd/api/main.go

# Run binary
./bin/mediabutler-api
```

## 🧪 Testing the API

### Option A: Using VS Code REST Client (Easiest)

1. Install "REST Client" extension by Huachao Mao
2. Open `src/MediaButler-Go/api-tests.http`
3. Click "Send Request" above any `###` endpoint
4. View response in split pane

### Option B: Using curl (Terminal)

#### Health Endpoints

```bash
# Basic health check
curl http://localhost:5001/health

# Readiness check
curl http://localhost:5001/health/ready

# Detailed health (with metrics)
curl http://localhost:5001/health/detailed | jq
```

#### File Management

```bash
# Get all files (default: New status)
curl http://localhost:5001/api/files?skip=0&take=20 | jq

# Get files by status
curl "http://localhost:5001/api/files?status=Classified&skip=0&take=20" | jq

# Get files by multiple statuses
curl "http://localhost:5001/api/files/by-statuses?statuses=New&statuses=Classified" | jq

# Get pending files
curl http://localhost:5001/api/files/pending | jq

# Get all categories
curl http://localhost:5001/api/files/categories | jq

# Register new file
curl -X POST http://localhost:5001/api/files \
  -H "Content-Type: application/json" \
  -d '{
    "filePath": "/watch/Breaking.Bad.S01E01.mkv",
    "hash": "abc123def456789012345678901234567890123456789012345678901234",
    "fileSize": 1024000000
  }' | jq

# Confirm file category (replace HASH)
curl -X POST http://localhost:5001/api/files/HASH/confirm \
  -H "Content-Type: application/json" \
  -d '{"category": "BREAKING BAD"}' | jq

# Mark file as moved (replace HASH)
curl -X POST http://localhost:5001/api/files/HASH/moved \
  -H "Content-Type: application/json" \
  -d '{"movedToPath": "/library/BREAKING BAD/Breaking.Bad.S01E01.mkv"}' | jq

# Delete file (replace HASH)
curl -X DELETE http://localhost:5001/api/files/HASH | jq
```

#### Processing & Stats

```bash
# Get processing statistics
curl http://localhost:5001/api/stats/processing | jq

# Get queue status
curl http://localhost:5001/api/processing/queue/status | jq

# Ignore file (replace HASH)
curl -X POST http://localhost:5001/api/v1/file-actions/ignore/HASH \
  -H "Content-Type: application/json" \
  -d '{"reason": "Duplicate file"}' | jq
```

### Option C: Using Postman

1. Import endpoints from `api-tests.http`
2. Set base URL: `http://localhost:5001`
3. Test individual endpoints

## 🧪 Running Unit Tests

### Run All Tests

```bash
cd src/MediaButler-Go

# Run all tests with verbose output
go test ./... -v

# Run tests with coverage
go test ./... -cover

# Generate coverage report
go test ./... -coverprofile=coverage.out
go tool cover -html=coverage.out
```

### Run Specific Tests

```bash
# Test specific package
go test ./internal/service -v

# Test specific file
go test ./internal/service/file_service_test.go -v

# Test specific function
go test ./internal/service -run TestFileService_RegisterFile -v
```

### VS Code Test Runner

1. Open test file (e.g., `file_service_test.go`)
2. Click "run test" or "debug test" above test functions
3. View results in Test Explorer panel

## 🔧 Common Issues and Solutions

### Issue 1: Database Not Found

**Error**: `Failed to initialize database: open database: unable to open database file`

**Solution**:
```bash
# Create data directory
mkdir -p /data

# Or update config to use local path
# Edit configs/config.json:
{
  "database": {
    "path": "./data/mediabutler.db"
  }
}

# Create local data directory
mkdir -p data
```

### Issue 2: Port Already in Use

**Error**: `Server failed to start: listen tcp :5001: bind: address already in use`

**Solution**:
```bash
# Find process using port 5001
lsof -i :5001

# Kill process (replace PID)
kill -9 PID

# Or change port in configs/config.json
{
  "server": {
    "port": 5002
  }
}
```

### Issue 3: Config File Not Found

**Error**: `Failed to load configuration: failed to read config file`

**Solution**:
```bash
# Run from correct directory
cd src/MediaButler-Go
go run cmd/api/main.go

# Or use absolute path in main.go
```

### Issue 4: Import Errors

**Error**: `package github.com/lucapaganotti/mediabutler-go/internal/... is not in GOROOT`

**Solution**:
```bash
# Tidy dependencies
cd src/MediaButler-Go
go mod tidy

# Download dependencies
go mod download
```

## 📊 Monitoring Server Logs

### Watch Logs in Real-Time

```bash
# If logging to file
tail -f /data/logs/mediabutler.log | jq

# If logging to stdout (default)
# Logs appear in terminal where server is running
```

### Filter Logs by Level

```bash
# Show only errors
go run cmd/api/main.go 2>&1 | grep '"level":"error"'

# Show only warnings and errors
go run cmd/api/main.go 2>&1 | grep -E '"level":"(warn|error)"'
```

## 🎯 Complete Test Scenario

### Scenario: Process a New File End-to-End

```bash
# 1. Start API
cd src/MediaButler-Go
go run cmd/api/main.go

# 2. Check health
curl http://localhost:5001/health

# 3. Register new file
RESPONSE=$(curl -s -X POST http://localhost:5001/api/files \
  -H "Content-Type: application/json" \
  -d '{
    "filePath": "/watch/Breaking.Bad.S01E01.mkv",
    "hash": "a1b2c3d4e5f6789012345678901234567890123456789012345678901234",
    "fileSize": 1024000000
  }')

echo $RESPONSE | jq

# Extract hash from response
HASH=$(echo $RESPONSE | jq -r '.hash')

# 4. Verify file is pending
curl http://localhost:5001/api/files/pending | jq

# 5. Confirm category
curl -X POST http://localhost:5001/api/files/$HASH/confirm \
  -H "Content-Type: application/json" \
  -d '{"category": "BREAKING BAD"}' | jq

# 6. Mark as moved
curl -X POST http://localhost:5001/api/files/$HASH/moved \
  -H "Content-Type: application/json" \
  -d '{"movedToPath": "/library/BREAKING BAD/Breaking.Bad.S01E01.mkv"}' | jq

# 7. Verify final status
curl http://localhost:5001/api/files/$HASH | jq

# 8. Check processing stats
curl http://localhost:5001/api/stats/processing | jq
```

## 🎨 VS Code Extensions (Recommended)

1. **Go** (golang.go) - Go language support
2. **REST Client** (humao.rest-client) - Test APIs from .http files
3. **Better Comments** (aaron-bond.better-comments) - Color-coded comments
4. **Error Lens** (usernamehw.errorlens) - Inline error highlighting
5. **GitLens** (eamodio.gitlens) - Git insights

## 📝 Performance Testing

### Load Testing with Apache Bench

```bash
# Install ab (Apache Bench)
# Mac: brew install httpd
# Ubuntu: sudo apt-get install apache2-utils

# Test health endpoint (100 requests, 10 concurrent)
ab -n 100 -c 10 http://localhost:5001/health

# Test files endpoint
ab -n 100 -c 10 "http://localhost:5001/api/files?skip=0&take=20"
```

### Memory Profiling

```bash
# Run with profiling enabled
go run cmd/api/main.go &
PID=$!

# Capture memory profile after 30 seconds
sleep 30
curl http://localhost:5001/debug/pprof/heap > heap.prof

# Analyze profile
go tool pprof heap.prof
```

## 🔍 Debugging Tips

### Enable Verbose Logging

Edit `configs/config.json`:
```json
{
  "logging": {
    "level": "debug",
    "format": "console",
    "enable_caller": true
  }
}
```

### Add Breakpoints in VS Code

1. Click left margin next to line number (red dot appears)
2. Press `F5` to start debugging
3. Server pauses at breakpoint
4. Use Debug Console to inspect variables

### Print Request/Response

Add logging in handlers:
```go
log.Debug().
    Str("method", r.Method).
    Str("path", r.URL.Path).
    Interface("body", req).
    Msg("Request received")
```

## 📚 Next Steps

1. ✅ Run API and verify all health endpoints work
2. ✅ Test file registration endpoints
3. ✅ Test file workflow (register → confirm → move)
4. ✅ Run unit tests and verify 100% pass
5. ⏳ Create .NET internal ML endpoint at `/internal/ml/classify`
6. ⏳ Integration testing with real database
7. ⏳ Performance benchmarks on ARM32 hardware

## 🆘 Getting Help

If you encounter issues:
1. Check logs for error messages
2. Verify database exists and is accessible
3. Ensure config file is in correct location
4. Run `go mod tidy` to fix dependency issues
5. Check port 5001 is not in use

## 📖 Additional Resources

- Go Documentation: https://go.dev/doc/
- Chi Router: https://github.com/go-chi/chi
- Zerolog: https://github.com/rs/zerolog
- SQLC: https://sqlc.dev/
