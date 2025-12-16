.PHONY: help generate build run test clean install-tools docker-build

# Default target
help:
	@echo "MediaButler Go API - Build Automation"
	@echo ""
	@echo "Available targets:"
	@echo "  install-tools    Install required development tools (sqlc)"
	@echo "  generate         Generate code from SQLC queries"
	@echo "  deps             Download and tidy Go dependencies"
	@echo "  build            Build the API server binary"
	@echo "  build-arm32      Cross-compile for ARM32"
	@echo "  run              Run the API server (development mode)"
	@echo "  test             Run all tests"
	@echo "  test-coverage    Run tests with coverage report"
	@echo "  lint             Run linters (golangci-lint)"
	@echo "  clean            Remove build artifacts"
	@echo "  docker-build     Build ARM32 Docker image"
	@echo "  all              Generate, build, and test"

# Install development tools
install-tools:
	@echo "Installing SQLC..."
	@go install github.com/sqlc-dev/sqlc/cmd/sqlc@latest
	@echo "Installing golangci-lint..."
	@go install github.com/golangci/golangci-lint/cmd/golangci-lint@latest
	@echo "Tools installed successfully!"

# Generate code from SQLC
generate:
	@echo "Generating code from SQL queries..."
	@sqlc generate
	@echo "Code generation complete!"

# Download and tidy dependencies
deps:
	@echo "Downloading dependencies..."
	@go mod download
	@go mod tidy
	@echo "Dependencies ready!"

# Build the API server
build: generate
	@echo "Building API server..."
	@mkdir -p bin
	@go build -ldflags="-s -w" -o bin/mediabutler-api cmd/api/main.go
	@echo "Build complete! Binary: bin/mediabutler-api"

# Cross-compile for ARM32
build-arm32: generate
	@echo "Cross-compiling for ARM32..."
	@mkdir -p bin
	@GOOS=linux GOARCH=arm GOARM=7 CGO_ENABLED=1 go build -ldflags="-s -w" -o bin/mediabutler-api-arm32 cmd/api/main.go
	@echo "ARM32 build complete! Binary: bin/mediabutler-api-arm32"
	@ls -lh bin/mediabutler-api-arm32

# Run the API server (development mode)
run: generate
	@echo "Starting API server..."
	@go run cmd/api/main.go -config configs/config.json

# Run all tests
test:
	@echo "Running tests..."
	@go test -v ./...

# Run tests with coverage
test-coverage:
	@echo "Running tests with coverage..."
	@go test -cover -coverprofile=coverage.out ./...
	@go tool cover -html=coverage.out -o coverage.html
	@echo "Coverage report generated: coverage.html"

# Run specific test
test-unit:
	@echo "Running unit tests..."
	@go test -v ./internal/service/... ./internal/domain/... ./pkg/...

test-integration:
	@echo "Running integration tests..."
	@go test -v ./internal/repository/... ./internal/api/...

# Lint code
lint:
	@echo "Running linters..."
	@golangci-lint run ./...

# Format code
fmt:
	@echo "Formatting code..."
	@go fmt ./...
	@goimports -w .

# Clean build artifacts
clean:
	@echo "Cleaning build artifacts..."
	@rm -rf bin/
	@rm -f coverage.out coverage.html
	@rm -f *.prof
	@echo "Clean complete!"

# Build Docker image for ARM32
docker-build:
	@echo "Building ARM32 Docker image..."
	@docker build -f Dockerfile.arm32 -t mediabutler-go:arm32 .
	@echo "Docker image built: mediabutler-go:arm32"

# Run Docker container
docker-run:
	@echo "Running Docker container..."
	@docker run -p 5001:5001 \
		-v $(PWD)/configs/config.json:/app/config.json \
		-v /data:/data \
		mediabutler-go:arm32

# Development workflow: generate, build, test
all: generate build test
	@echo "All tasks complete!"

# Watch for changes and rebuild (requires entr or fswatch)
watch:
	@echo "Watching for changes..."
	@find . -name '*.go' -o -name '*.sql' | entr -r make run

# Benchmark tests
benchmark:
	@echo "Running benchmarks..."
	@go test -bench=. -benchmem ./...

# Profile CPU usage
profile-cpu:
	@echo "Profiling CPU usage..."
	@go test -cpuprofile=cpu.prof -bench=. ./internal/service
	@go tool pprof cpu.prof

# Profile memory usage
profile-mem:
	@echo "Profiling memory usage..."
	@go test -memprofile=mem.prof -bench=. ./internal/service
	@go tool pprof mem.prof

# Download database from .NET project (for development)
sync-db:
	@echo "Syncing database from .NET project..."
	@cp ../MediaButler/data/mediabutler.db ./data/mediabutler.db
	@echo "Database synced!"
