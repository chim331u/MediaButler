#!/bin/bash
set -euo pipefail

# MediaButler QNAP NAS Deployment Script
# Optimized for 1GB RAM ARM32/ARM64 NAS systems
# Version: 1.0.0
# Author: MediaButler Team

# =============================================================================
# CONFIGURATION - Customize these parameters before deployment
# =============================================================================

# Default Configuration (can be overridden via environment variables)
GITHUB_REPO_URL="${GITHUB_REPO_URL:-https://github.com/luca/mediabutler}"
GITHUB_BRANCH="${GITHUB_BRANCH:-main}"
API_PORT="${API_PORT:-5000}"
WEB_PORT="${WEB_PORT:-3000}"
PROXY_PORT="${PROXY_PORT:-80}"
INSTALL_PATH="${INSTALL_PATH:-/share/Container/mediabutler}"
MEMORY_LIMIT_API="${MEMORY_LIMIT_API:-150m}"
MEMORY_LIMIT_WEB="${MEMORY_LIMIT_WEB:-100m}"
MEMORY_LIMIT_PROXY="${MEMORY_LIMIT_PROXY:-20m}"

# Advanced Configuration
DOCKER_REGISTRY="${DOCKER_REGISTRY:-}"
SSL_ENABLED="${SSL_ENABLED:-false}"
SSL_CERT_PATH="${SSL_CERT_PATH:-}"
SSL_KEY_PATH="${SSL_KEY_PATH:-}"
BACKUP_ENABLED="${BACKUP_ENABLED:-true}"
MONITORING_ENABLED="${MONITORING_ENABLED:-true}"

# =============================================================================
# LOGGING AND OUTPUT
# =============================================================================

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Logging functions
log() { echo -e "${BLUE}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1"; }
success() { echo -e "${GREEN}✅ [SUCCESS]${NC} $1"; }
warning() { echo -e "${YELLOW}⚠️  [WARNING]${NC} $1"; }
error() { echo -e "${RED}❌ [ERROR]${NC} $1"; exit 1; }
info() { echo -e "${CYAN}ℹ️  [INFO]${NC} $1"; }
step() { echo -e "${PURPLE}🔄 [STEP]${NC} $1"; }

# Create log file
LOG_FILE="${INSTALL_PATH}/deployment.log"
exec > >(tee -a "$LOG_FILE") 2>&1

# =============================================================================
# UTILITY FUNCTIONS
# =============================================================================

show_banner() {
    echo -e "${CYAN}"
    cat << 'EOF'
╔══════════════════════════════════════════════════════════════════════════════╗
║                     📺 MediaButler QNAP Deployment                          ║
║                        Automated Docker Deployment                          ║
║                          Optimized for 1GB RAM                              ║
╚══════════════════════════════════════════════════════════════════════════════╝
EOF
    echo -e "${NC}"
    echo
}

validate_parameters() {
    step "Validating deployment parameters..."

    # Required parameters
    if [[ -z "$GITHUB_REPO_URL" ]]; then
        error "GITHUB_REPO_URL parameter is required"
    fi

    # Validate ports
    if ! [[ "$API_PORT" =~ ^[0-9]+$ ]] || [[ "$API_PORT" -lt 1 ]] || [[ "$API_PORT" -gt 65535 ]]; then
        error "Invalid API_PORT: $API_PORT (must be 1-65535)"
    fi

    if ! [[ "$WEB_PORT" =~ ^[0-9]+$ ]] || [[ "$WEB_PORT" -lt 1 ]] || [[ "$WEB_PORT" -gt 65535 ]]; then
        error "Invalid WEB_PORT: $WEB_PORT (must be 1-65535)"
    fi

    if ! [[ "$PROXY_PORT" =~ ^[0-9]+$ ]] || [[ "$PROXY_PORT" -lt 1 ]] || [[ "$PROXY_PORT" -gt 65535 ]]; then
        error "Invalid PROXY_PORT: $PROXY_PORT (must be 1-65535)"
    fi

    # Validate memory limits
    if ! [[ "$MEMORY_LIMIT_API" =~ ^[0-9]+[mMgG]$ ]]; then
        error "Invalid MEMORY_LIMIT_API format: $MEMORY_LIMIT_API (use format like 150m or 1g)"
    fi

    success "Parameters validated successfully"
}

check_requirements() {
    step "Checking QNAP system requirements..."

    # Check if running on QNAP (optional)
    if [[ -f /etc/init.d/container-station.sh ]]; then
        success "QNAP Container Station detected"
    else
        warning "QNAP Container Station not detected - proceeding anyway"
    fi

    # Check available memory
    AVAILABLE_RAM=$(awk '/MemAvailable/ {print int($2/1024)}' /proc/meminfo 2>/dev/null || echo "unknown")
    if [[ "$AVAILABLE_RAM" != "unknown" ]] && [[ $AVAILABLE_RAM -lt 400 ]]; then
        error "Insufficient RAM: ${AVAILABLE_RAM}MB available, need at least 400MB"
    fi
    success "RAM check passed: ${AVAILABLE_RAM}MB available"

    # Check Docker
    if ! command -v docker >/dev/null 2>&1; then
        error "Docker not found. Please install Container Station first."
    fi
    DOCKER_VERSION=$(docker --version | cut -d' ' -f3 | cut -d',' -f1)
    success "Docker found: $DOCKER_VERSION"

    # Check Docker Compose
    if ! command -v docker-compose >/dev/null 2>&1; then
        error "Docker Compose not found. Please install Container Station first."
    fi
    COMPOSE_VERSION=$(docker-compose --version | cut -d' ' -f3 | cut -d',' -f1)
    success "Docker Compose found: $COMPOSE_VERSION"

    # Check architecture
    ARCH=$(uname -m)
    case "$ARCH" in
        armv7l) DOCKER_PLATFORM="linux/arm/v7"; success "Architecture: ARM32 (armv7l)" ;;
        aarch64) DOCKER_PLATFORM="linux/arm64"; success "Architecture: ARM64 (aarch64)" ;;
        x86_64) DOCKER_PLATFORM="linux/amd64"; success "Architecture: x86_64" ;;
        *) warning "Unsupported architecture: $ARCH - proceeding anyway" ;;
    esac

    # Check disk space
    AVAILABLE_DISK=$(df -h "$INSTALL_PATH" 2>/dev/null | awk 'NR==2 {print $4}' || echo "unknown")
    info "Available disk space: $AVAILABLE_DISK"

    # Check network connectivity
    if ping -c 1 github.com >/dev/null 2>&1; then
        success "Network connectivity verified"
    else
        error "No internet connection - cannot download source code"
    fi
}

cleanup_deployment() {
    step "Cleaning up previous deployment..."

    if [[ -d "$INSTALL_PATH" ]]; then
        cd "$INSTALL_PATH" || true

        # Stop existing containers
        if [[ -f "docker-compose.yml" ]]; then
            docker-compose down --remove-orphans 2>/dev/null || true
            success "Stopped existing containers"
        fi

        # Remove old images (keep last version as backup)
        docker image prune -f 2>/dev/null || true

        # Clean build cache
        docker builder prune -f 2>/dev/null || true

        success "Cleanup completed"
    else
        info "No previous deployment found"
    fi
}

download_source() {
    step "Downloading MediaButler source code..."

    # Create installation directory
    mkdir -p "$INSTALL_PATH"
    cd "$INSTALL_PATH"

    # Download source code
    DOWNLOAD_URL="${GITHUB_REPO_URL}/archive/refs/heads/${GITHUB_BRANCH}.zip"
    log "Downloading from: $DOWNLOAD_URL"

    if ! wget --progress=bar:force -O mediabutler.zip "$DOWNLOAD_URL" 2>&1; then
        error "Failed to download source code from GitHub"
    fi

    # Extract source code
    if ! unzip -q mediabutler.zip; then
        error "Failed to extract source code"
    fi

    # Move contents to current directory
    EXTRACTED_DIR=$(find . -maxdepth 1 -type d -name "*mediabutler*" -o -name "*MediaButler*" | head -1)
    if [[ -n "$EXTRACTED_DIR" ]]; then
        mv "$EXTRACTED_DIR"/* ./ 2>/dev/null || true
        mv "$EXTRACTED_DIR"/.* ./ 2>/dev/null || true
        rmdir "$EXTRACTED_DIR" 2>/dev/null || true
    fi

    # Cleanup
    rm -f mediabutler.zip

    success "Source code downloaded to $INSTALL_PATH"
}

create_docker_files() {
    step "Creating optimized Docker configuration..."

    mkdir -p docker

    # Create API Dockerfile
    cat > docker/Dockerfile.api << 'EOF'
# MediaButler API - Optimized for ARM32/ARM64 QNAP NAS
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /source

# Install dependencies for ARM cross-compilation
RUN apk add --no-cache clang build-base zlib-dev

# Copy project files for dependency restoration
COPY src/MediaButler.API/*.csproj src/MediaButler.API/
COPY src/MediaButler.Core/*.csproj src/MediaButler.Core/
COPY src/MediaButler.Data/*.csproj src/MediaButler.Data/
COPY src/MediaButler.ML/*.csproj src/MediaButler.ML/
COPY src/MediaButler.Services/*.csproj src/MediaButler.Services/

# Restore dependencies
RUN dotnet restore src/MediaButler.API/MediaButler.API.csproj

# Copy source code
COPY . .

# Build and publish with optimizations
RUN dotnet publish src/MediaButler.API/MediaButler.API.csproj \
    -c Release \
    -o /app \
    --self-contained true \
    --runtime linux-musl-x64 \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=link \
    -p:EnableCompressionInSingleFile=true

# Runtime stage - Ultra-minimal Alpine
FROM alpine:3.18
RUN apk add --no-cache \
    ca-certificates \
    tzdata \
    && addgroup -g 1000 mediabutler \
    && adduser -D -s /bin/sh -u 1000 -G mediabutler mediabutler

WORKDIR /app
COPY --from=build /app .
COPY --chown=mediabutler:mediabutler models/ ./models/ 2>/dev/null || true

# Create required directories
RUN mkdir -p data/{library,watch,temp} logs && \
    chown -R mediabutler:mediabutler data/ logs/

USER mediabutler
EXPOSE 5000

# Environment optimization for 1GB RAM constraint
ENV DOTNET_EnableDiagnostics=0 \
    DOTNET_gcServer=0 \
    DOTNET_gcConcurrent=false \
    DOTNET_GCHeapHardLimit=140000000 \
    DOTNET_GCHighMemPercent=75 \
    DOTNET_GCConserveMemory=9 \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:5000/health || exit 1

ENTRYPOINT ["./MediaButler.API"]
EOF

    # Create Web Dockerfile
    cat > docker/Dockerfile.web << 'EOF'
# MediaButler Web - Optimized for ARM32/ARM64 QNAP NAS
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /source

# Install dependencies
RUN apk add --no-cache nodejs npm clang build-base

# Copy project files
COPY src/MediaButler.Web/*.csproj src/MediaButler.Web/
COPY src/MediaButler.Core/*.csproj src/MediaButler.Core/

# Restore dependencies
RUN dotnet restore src/MediaButler.Web/MediaButler.Web.csproj

# Copy source code
COPY . .

# Build and publish
RUN dotnet publish src/MediaButler.Web/MediaButler.Web.csproj \
    -c Release \
    -o /app \
    --self-contained true \
    --runtime linux-musl-x64 \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=link \
    -p:EnableCompressionInSingleFile=true

# Runtime stage
FROM alpine:3.18
RUN apk add --no-cache \
    ca-certificates \
    tzdata \
    && addgroup -g 1000 mediabutler \
    && adduser -D -s /bin/sh -u 1000 -G mediabutler mediabutler

WORKDIR /app
COPY --from=build /app .

# Create required directories
RUN mkdir -p logs && chown -R mediabutler:mediabutler logs/

USER mediabutler
EXPOSE 3000

# Environment optimization
ENV DOTNET_EnableDiagnostics=0 \
    DOTNET_gcServer=0 \
    DOTNET_GCHeapHardLimit=90000000 \
    DOTNET_GCHighMemPercent=75 \
    DOTNET_GCConserveMemory=9 \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:3000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:3000 || exit 1

ENTRYPOINT ["./MediaButler.Web"]
EOF

    success "Docker files created"
}

create_compose_config() {
    step "Creating Docker Compose configuration..."

    # Create docker-compose.yml
    cat > docker-compose.yml << EOF
version: '3.8'

services:
  mediabutler-api:
    build:
      context: .
      dockerfile: docker/Dockerfile.api
      platforms:
        - ${DOCKER_PLATFORM:-linux/amd64}
    container_name: mediabutler-api
    restart: unless-stopped
    ports:
      - "${API_PORT}:5000"
    volumes:
      - ./data:/app/data
      - ./models:/app/models:ro
      - ./configs:/app/configs:ro
      - ./logs/api:/app/logs
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5000
      - MediaButler__Paths__MediaLibrary=/app/data/library
      - MediaButler__Paths__WatchFolder=/app/data/watch
      - MediaButler__Paths__PendingReview=/app/data/temp
      - Serilog__WriteTo__1__Args__path=/app/logs/api-.log
    deploy:
      resources:
        limits:
          memory: ${MEMORY_LIMIT_API}
          cpus: '0.5'
        reservations:
          memory: 100m
          cpus: '0.2'
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s
    networks:
      - mediabutler
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

  mediabutler-web:
    build:
      context: .
      dockerfile: docker/Dockerfile.web
      platforms:
        - ${DOCKER_PLATFORM:-linux/amd64}
    container_name: mediabutler-web
    restart: unless-stopped
    ports:
      - "${WEB_PORT}:3000"
    volumes:
      - ./logs/web:/app/logs
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:3000
      - API_BASE_URL=http://mediabutler-api:5000
    deploy:
      resources:
        limits:
          memory: ${MEMORY_LIMIT_WEB}
          cpus: '0.3'
        reservations:
          memory: 70m
          cpus: '0.1'
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:3000"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s
    depends_on:
      mediabutler-api:
        condition: service_healthy
    networks:
      - mediabutler
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

  nginx-proxy:
    image: nginx:alpine
    container_name: mediabutler-proxy
    restart: unless-stopped
    ports:
      - "${PROXY_PORT}:80"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./logs/nginx:/var/log/nginx
    deploy:
      resources:
        limits:
          memory: ${MEMORY_LIMIT_PROXY}
          cpus: '0.1'
        reservations:
          memory: 10m
          cpus: '0.05'
    depends_on:
      - mediabutler-api
      - mediabutler-web
    networks:
      - mediabutler
    logging:
      driver: "json-file"
      options:
        max-size: "5m"
        max-file: "2"

volumes:
  mediabutler-data:
    driver: local

networks:
  mediabutler:
    driver: bridge
    driver_opts:
      com.docker.network.driver.mtu: 1500
EOF

    # Create Nginx configuration
    cat > nginx.conf << 'EOF'
# MediaButler Nginx Configuration - Optimized for QNAP NAS
worker_processes auto;
worker_rlimit_nofile 1024;

events {
    worker_connections 512;
    use epoll;
    multi_accept on;
}

http {
    include /etc/nginx/mime.types;
    default_type application/octet-stream;

    # Logging
    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for"';

    access_log /var/log/nginx/access.log main;
    error_log /var/log/nginx/error.log warn;

    # Performance optimization
    sendfile on;
    tcp_nopush on;
    tcp_nodelay on;
    keepalive_timeout 65;
    types_hash_max_size 2048;
    client_max_body_size 100M;

    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_comp_level 6;
    gzip_types
        text/plain
        text/css
        text/xml
        text/javascript
        application/json
        application/javascript
        application/xml+rss
        application/atom+xml
        image/svg+xml;

    # Upstream servers
    upstream api {
        server mediabutler-api:5000 max_fails=3 fail_timeout=30s;
        keepalive 8;
    }

    upstream web {
        server mediabutler-web:3000 max_fails=3 fail_timeout=30s;
        keepalive 8;
    }

    server {
        listen 80;
        server_name _;

        # Security headers
        add_header X-Frame-Options "SAMEORIGIN" always;
        add_header X-Content-Type-Options "nosniff" always;
        add_header X-XSS-Protection "1; mode=block" always;

        # API routes
        location /api/ {
            proxy_pass http://api;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection 'upgrade';
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_cache_bypass $http_upgrade;
            proxy_read_timeout 300s;
            proxy_connect_timeout 75s;
        }

        # Health check endpoint
        location /health {
            proxy_pass http://api;
            proxy_http_version 1.1;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        # SignalR hub
        location /notifications {
            proxy_pass http://api;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection "upgrade";
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        # Web UI (default)
        location / {
            proxy_pass http://web;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection 'upgrade';
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_cache_bypass $http_upgrade;
        }

        # Static file caching
        location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
            proxy_pass http://web;
            expires 1y;
            add_header Cache-Control "public, immutable";
        }
    }
}
EOF

    success "Docker Compose configuration created"
}

setup_directories() {
    step "Setting up directory structure..."

    # Create required directories
    mkdir -p {data/{library,watch,temp},models,configs,logs/{api,web,nginx},backups}

    # Set proper permissions for QNAP
    if command -v chown >/dev/null 2>&1; then
        chown -R admin:administrators data/ models/ configs/ logs/ backups/ 2>/dev/null || true
    fi
    chmod -R 755 data/ models/ configs/ logs/ backups/

    # Create .env file
    cat > .env << EOF
# MediaButler Environment Configuration
COMPOSE_PROJECT_NAME=mediabutler
API_PORT=${API_PORT}
WEB_PORT=${WEB_PORT}
PROXY_PORT=${PROXY_PORT}
MEMORY_LIMIT_API=${MEMORY_LIMIT_API}
MEMORY_LIMIT_WEB=${MEMORY_LIMIT_WEB}
MEMORY_LIMIT_PROXY=${MEMORY_LIMIT_PROXY}
DOCKER_PLATFORM=${DOCKER_PLATFORM:-linux/amd64}
EOF

    success "Directory structure created"
}

build_and_deploy() {
    step "Building and deploying MediaButler containers..."

    # Set build arguments based on architecture
    export DOCKER_BUILDKIT=1
    export COMPOSE_DOCKER_CLI_BUILD=1

    # Build images with detailed progress
    log "Building API container..."
    if ! docker-compose build --progress=plain mediabutler-api; then
        error "Failed to build API container"
    fi
    success "API container built successfully"

    log "Building Web container..."
    if ! docker-compose build --progress=plain mediabutler-web; then
        error "Failed to build Web container"
    fi
    success "Web container built successfully"

    # Start services
    log "Starting MediaButler services..."
    if ! docker-compose up -d; then
        error "Failed to start services"
    fi

    # Wait for services to become healthy
    log "Waiting for services to become healthy..."
    local max_attempts=60
    local attempt=1

    while [[ $attempt -le $max_attempts ]]; do
        local healthy_count=$(docker-compose ps --filter health=healthy | wc -l)
        local total_services=3

        if [[ $healthy_count -ge $total_services ]]; then
            success "All services are healthy and running"
            return 0
        fi

        if [[ $((attempt % 6)) -eq 0 ]]; then
            log "Health check progress: $healthy_count/$total_services services healthy (attempt $attempt/$max_attempts)"
            docker-compose ps
        fi

        sleep 10
        ((attempt++))
    done

    warning "Services started but health check timeout reached"
    docker-compose ps
    docker-compose logs --tail=20
}

create_monitoring() {
    step "Setting up monitoring and maintenance..."

    # Create monitoring script
    cat > monitor-mediabutler.sh << 'EOF'
#!/bin/bash
# MediaButler Health Monitor for QNAP NAS
# This script monitors container health and resource usage

set -euo pipefail

INSTALL_PATH="$(dirname "$(readlink -f "$0")")"
LOG_FILE="$INSTALL_PATH/logs/monitor.log"
MAX_LOG_SIZE_MB=10

cd "$INSTALL_PATH"

# Logging function
log_monitor() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $1" >> "$LOG_FILE"
}

# Rotate logs if they get too large
rotate_logs() {
    if [[ -f "$LOG_FILE" ]] && [[ $(stat -f%z "$LOG_FILE" 2>/dev/null || stat -c%s "$LOG_FILE" 2>/dev/null || echo 0) -gt $((MAX_LOG_SIZE_MB * 1024 * 1024)) ]]; then
        mv "$LOG_FILE" "${LOG_FILE}.old"
        touch "$LOG_FILE"
        log_monitor "Log rotated due to size limit"
    fi
}

# Check container health
check_containers() {
    local failed_services=()

    # Check if all services are running
    local running_count=$(docker-compose ps --filter status=running | grep -c "Up" || echo 0)
    if [[ $running_count -lt 3 ]]; then
        log_monitor "WARNING: Not all services running ($running_count/3)"
        failed_services+=("containers_not_running")
    fi

    # Check health status
    local unhealthy=$(docker-compose ps --filter health=unhealthy | grep -v "^Name" | wc -l)
    if [[ $unhealthy -gt 0 ]]; then
        log_monitor "WARNING: $unhealthy unhealthy containers detected"
        failed_services+=("unhealthy_containers")
    fi

    return ${#failed_services[@]}
}

# Check resource usage
check_resources() {
    # Memory usage check
    local memory_usage=$(docker stats --no-stream --format "{{.MemUsage}}" | sed 's/MiB.*//' | awk '{sum+=$1} END {print int(sum)}')
    if [[ $memory_usage -gt 300 ]]; then
        log_monitor "WARNING: High memory usage: ${memory_usage}MB"
        return 1
    fi

    # Disk usage check
    local disk_usage=$(df -h "$INSTALL_PATH" | awk 'NR==2 {print $5}' | sed 's/%//')
    if [[ $disk_usage -gt 85 ]]; then
        log_monitor "WARNING: High disk usage: ${disk_usage}%"
        return 1
    fi

    log_monitor "INFO: Resource usage normal (Memory: ${memory_usage}MB, Disk: ${disk_usage}%)"
    return 0
}

# Check service endpoints
check_endpoints() {
    local failed=0

    # Check API health endpoint
    if ! curl -sf "http://localhost:$(grep API_PORT .env | cut -d= -f2)/health" >/dev/null 2>&1; then
        log_monitor "ERROR: API health check failed"
        ((failed++))
    fi

    # Check Web endpoint
    if ! curl -sf "http://localhost:$(grep WEB_PORT .env | cut -d= -f2)" >/dev/null 2>&1; then
        log_monitor "ERROR: Web health check failed"
        ((failed++))
    fi

    # Check Proxy endpoint
    if ! curl -sf "http://localhost:$(grep PROXY_PORT .env | cut -d= -f2)/health" >/dev/null 2>&1; then
        log_monitor "ERROR: Proxy health check failed"
        ((failed++))
    fi

    if [[ $failed -eq 0 ]]; then
        log_monitor "INFO: All endpoints responding"
    fi

    return $failed
}

# Restart unhealthy services
restart_unhealthy() {
    log_monitor "INFO: Attempting to restart unhealthy services"

    # Get unhealthy containers
    local unhealthy_containers=$(docker-compose ps --filter health=unhealthy --format "{{.Name}}")

    if [[ -n "$unhealthy_containers" ]]; then
        for container in $unhealthy_containers; do
            log_monitor "INFO: Restarting unhealthy container: $container"
            docker-compose restart "$(echo "$container" | sed 's/^mediabutler-//')" || true
        done
    else
        # If no specific unhealthy containers, restart all
        log_monitor "INFO: Restarting all services"
        docker-compose restart
    fi
}

# Main monitoring function
main() {
    rotate_logs

    local issues=0

    # Run checks
    check_containers || ((issues++))
    check_resources || ((issues++))
    check_endpoints || ((issues++))

    # If there are issues, attempt restart
    if [[ $issues -gt 0 ]]; then
        log_monitor "ALERT: $issues issues detected, attempting restart"
        restart_unhealthy

        # Wait and recheck
        sleep 30
        if check_endpoints; then
            log_monitor "SUCCESS: Services recovered after restart"
        else
            log_monitor "ERROR: Services still unhealthy after restart - manual intervention required"
        fi
    else
        log_monitor "INFO: All services healthy"
    fi
}

# Run monitoring
main
EOF

    chmod +x monitor-mediabutler.sh

    # Create backup script
    cat > backup-mediabutler.sh << 'EOF'
#!/bin/bash
# MediaButler Backup Script for QNAP NAS

set -euo pipefail

INSTALL_PATH="$(dirname "$(readlink -f "$0")")"
BACKUP_DIR="$INSTALL_PATH/backups"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_FILE="mediabutler_backup_$TIMESTAMP.tar.gz"

cd "$INSTALL_PATH"

echo "Creating backup: $BACKUP_FILE"

# Create backup
tar -czf "$BACKUP_DIR/$BACKUP_FILE" \
    --exclude='./backups' \
    --exclude='./logs' \
    --exclude='./.git' \
    ./

# Keep only last 5 backups
cd "$BACKUP_DIR"
ls -t mediabutler_backup_*.tar.gz | tail -n +6 | xargs rm -f 2>/dev/null || true

echo "Backup completed: $BACKUP_DIR/$BACKUP_FILE"
echo "Available backups:"
ls -lah mediabutler_backup_*.tar.gz 2>/dev/null || echo "No backups found"
EOF

    chmod +x backup-mediabutler.sh

    # Add monitoring to crontab if possible
    if command -v crontab >/dev/null 2>&1; then
        local cron_job="*/5 * * * * cd $INSTALL_PATH && ./monitor-mediabutler.sh >/dev/null 2>&1"
        local backup_job="0 2 * * 0 cd $INSTALL_PATH && ./backup-mediabutler.sh >/dev/null 2>&1"

        (crontab -l 2>/dev/null | grep -v "monitor-mediabutler\|backup-mediabutler"; echo "$cron_job"; echo "$backup_job") | crontab - 2>/dev/null || warning "Could not add monitoring to crontab"
        success "Monitoring configured (runs every 5 minutes)"
        success "Weekly backups configured (runs Sundays at 2 AM)"
    else
        warning "Crontab not available - manual monitoring setup required"
    fi
}

show_deployment_info() {
    local host_ip=$(hostname -I | awk '{print $1}' || echo "localhost")

    echo
    success "🎉 MediaButler deployment completed successfully!"
    echo
    echo "📋 Deployment Information:"
    echo "════════════════════════════════════════════════════════════════"
    echo "🌐 Web Interface:     http://${host_ip}:${PROXY_PORT}"
    echo "🔌 API Endpoint:      http://${host_ip}:${PROXY_PORT}/api"
    echo "📁 Installation Path: ${INSTALL_PATH}"
    echo "💾 Memory Limits:     API:${MEMORY_LIMIT_API}, Web:${MEMORY_LIMIT_WEB}, Proxy:${MEMORY_LIMIT_PROXY}"
    echo "🏗️  Architecture:      ${DOCKER_PLATFORM:-linux/amd64}"
    echo
    echo "🛠️  Management Commands:"
    echo "════════════════════════════════════════════════════════════════"
    echo "📊 View status:       cd ${INSTALL_PATH} && docker-compose ps"
    echo "📜 View logs:         cd ${INSTALL_PATH} && docker-compose logs -f"
    echo "🔄 Restart services:  cd ${INSTALL_PATH} && docker-compose restart"
    echo "⏹️  Stop services:     cd ${INSTALL_PATH} && docker-compose down"
    echo "🗂️  Manual backup:     cd ${INSTALL_PATH} && ./backup-mediabutler.sh"
    echo "🔍 Check health:      cd ${INSTALL_PATH} && ./monitor-mediabutler.sh"
    echo "🆙 Update:            Re-run deployment script with same parameters"
    echo
    echo "📊 Current Container Status:"
    echo "════════════════════════════════════════════════════════════════"
    docker-compose ps
    echo
    echo "📈 Resource Usage:"
    echo "════════════════════════════════════════════════════════════════"
    docker stats --no-stream --format "table {{.Container}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.NetIO}}\t{{.BlockIO}}"
    echo
    info "🔗 Access your MediaButler instance at: http://${host_ip}:${PROXY_PORT}"
    echo
}

# =============================================================================
# MAIN EXECUTION
# =============================================================================

main() {
    show_banner

    # Validate inputs
    validate_parameters

    # Deployment steps
    check_requirements
    cleanup_deployment
    download_source
    create_docker_files
    create_compose_config
    setup_directories
    build_and_deploy
    create_monitoring
    show_deployment_info

    success "🚀 MediaButler QNAP deployment completed successfully!"

    # Log completion
    log "Deployment completed at $(date)"
    log "Installation path: $INSTALL_PATH"
    log "Configuration: API:$API_PORT, Web:$WEB_PORT, Proxy:$PROXY_PORT"
}

# Error handling
trap 'error "💥 Deployment failed at line $LINENO - Check logs at $LOG_FILE"' ERR

# Ensure log directory exists
mkdir -p "$(dirname "$LOG_FILE")" 2>/dev/null || true

# Execute main function
main "$@"