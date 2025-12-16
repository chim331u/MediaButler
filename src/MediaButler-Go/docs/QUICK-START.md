# MediaButler Go API - Quick Start

## 🚀 Start the API (Choose One)

```bash
# Option 1: Run directly
cd src/MediaButler-Go && go run cmd/api/main.go

# Option 2: VS Code keyboard shortcut
Cmd+Shift+B  # (Mac) or Ctrl+Shift+B (Windows/Linux)

# Option 3: VS Code Debug
F5  # Start debugging with breakpoints
```

## ✅ Verify API is Running

```bash
# Quick health check
curl http://localhost:5001/health

# Expected response:
# {"status":"healthy","version":"1.0.0","timestamp":"..."}
```

## 🧪 Test Endpoints

### Using VS Code REST Client (Easiest)
1. Open `api-tests.http`
2. Click "Send Request" above any endpoint
3. View response in split pane

### Using curl

```bash
# Health check
curl http://localhost:5001/health | jq

# Detailed health (with memory stats)
curl http://localhost:5001/health/detailed | jq

# Get all files
curl http://localhost:5001/api/files | jq

# Processing stats
curl http://localhost:5001/api/stats/processing | jq

# Queue status
curl http://localhost:5001/api/processing/queue/status | jq
```

## 🧪 Run Tests

```bash
cd src/MediaButler-Go

# All tests
go test ./... -v

# With coverage
go test ./... -cover

# Specific package
go test ./internal/service -v
```

## 📝 Key Files

- `cmd/api/main.go` - Entry point with DI setup
- `internal/api/router.go` - All 15 endpoints
- `configs/config.json` - Configuration
- `api-tests.http` - Test all endpoints
- `docs/Go-API-Testing-Guide.md` - Complete guide

## 🔧 Common Issues

### Port in use
```bash
lsof -i :5001  # Find process
kill -9 PID    # Kill it
```

### Database not found
```bash
mkdir -p data  # Create local data dir
# Or edit configs/config.json to use ./data/mediabutler.db
```

### Import errors
```bash
go mod tidy    # Fix dependencies
```

## 📚 All 15 Tier 1 Endpoints

```
GET  /health                                # Basic health
GET  /health/ready                          # Readiness
GET  /health/detailed                       # Detailed metrics

GET  /api/files                             # Get files (paginated)
GET  /api/files/by-statuses                 # Multi-status filter
GET  /api/files/pending                     # Pending confirmation
GET  /api/files/categories                  # All categories
GET  /api/files/{hash}                      # Get single file
POST /api/files                             # Register file
POST /api/files/{hash}/confirm              # Confirm category
POST /api/files/{hash}/moved                # Mark as moved
DELETE /api/files/{hash}                    # Delete file

GET  /api/stats/processing                  # Processing stats
GET  /api/processing/queue/status           # Queue status
POST /api/v1/file-actions/ignore/{hash}     # Ignore file
```

## 🎯 Complete Workflow Test

```bash
# 1. Register file
curl -X POST http://localhost:5001/api/files \
  -H "Content-Type: application/json" \
  -d '{"filePath":"/watch/test.mkv","hash":"abc123","fileSize":1000}' | jq

# 2. Get pending
curl http://localhost:5001/api/files/pending | jq

# 3. Confirm category
curl -X POST http://localhost:5001/api/files/abc123/confirm \
  -H "Content-Type: application/json" \
  -d '{"category":"TEST"}' | jq

# 4. Mark moved
curl -X POST http://localhost:5001/api/files/abc123/moved \
  -H "Content-Type: application/json" \
  -d '{"movedToPath":"/library/TEST/test.mkv"}' | jq

# 5. Check stats
curl http://localhost:5001/api/stats/processing | jq
```

## 🚦 Server Status Indicators

```
✅ Server running:
   {"level":"info","address":"0.0.0.0:5001","message":"HTTP server starting"}

❌ Port in use:
   listen tcp :5001: bind: address already in use

❌ Database error:
   Failed to initialize database: unable to open database file

❌ Config not found:
   Failed to load configuration: failed to read config file
```

## 📖 Full Documentation

See `docs/Go-API-Testing-Guide.md` for complete testing guide.
