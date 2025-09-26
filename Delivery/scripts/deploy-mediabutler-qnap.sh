#!/bin/bash
set -euo pipefail

# MediaButler QNAP NAS Deployment Script
# Optimized for 1GB RAM ARM32/ARM64 NAS systems
# Version: 1.0.1
# Author: MediaButler Team

# =============================================================================
# CONFIGURATION - Customize these parameters before deployment
# =============================================================================

# Default Configuration (can be overridden via environment variables)
GITHUB_REPO_URL="${GITHUB_REPO_URL:-https://github.com/chim331u/MediaButler}"
GITHUB_BRANCH="${GITHUB_BRANCH:-main}"
API_PORT="${API_PORT:-30129}"
WEB_PORT="${WEB_PORT:-30139}"
PROXY_PORT="${PROXY_PORT:-8080}"
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

# Step counter for progress tracking
STEP_COUNT=0
step() {
    STEP_COUNT=$((STEP_COUNT + 1))
    echo
    echo -e "${PURPLE}==============================================================================${NC}"
    echo -e "${PURPLE}STEP $STEP_COUNT: $1${NC}"
    echo -e "${PURPLE}==============================================================================${NC}"
}

# =============================================================================
# SYSTEM REQUIREMENTS CHECK
# =============================================================================

check_requirements() {
    step "Checking system requirements..."

    # Check if running as root (optional but recommended)
    if [[ $EUID -eq 0 ]]; then
        warning "Running as root. Consider using a regular user with sudo privileges."
    fi

    # Check available memory
    local mem_total=$(grep MemTotal /proc/meminfo | awk '{print $2}')
    local mem_gb=$((mem_total / 1024 / 1024))

    if [[ $mem_gb -lt 1 ]]; then
        error "Insufficient memory. MediaButler requires at least 1GB RAM. Found: ${mem_gb}GB"
    fi
    success "Memory check passed: ${mem_gb}GB available"

    # Check Docker
    if ! command -v docker >/dev/null 2>&1; then
        error "Docker is not installed. Please install Container Station first."
    fi
    DOCKER_VERSION=$(docker --version | cut -d' ' -f3 | cut -d',' -f1)
    success "Docker found: $DOCKER_VERSION"

    # Check Docker Compose (modern Docker includes compose as a plugin)
    if docker compose version >/dev/null 2>&1; then
        COMPOSE_CMD="docker compose"
        COMPOSE_VERSION=$(docker compose version --short 2>/dev/null || docker compose version | grep -o 'v[0-9]\+\.[0-9]\+\.[0-9]\+' | head -1)
        success "Docker Compose (plugin) found: $COMPOSE_VERSION"
    elif command -v docker-compose >/dev/null 2>&1; then
        COMPOSE_CMD="docker-compose"
        COMPOSE_VERSION=$(docker-compose --version | cut -d' ' -f3 | cut -d',' -f1)
        success "Docker Compose (standalone) found: $COMPOSE_VERSION"
    else
        error "Docker Compose not found. Please install Container Station first."
    fi

    # Check architecture
    ARCH=$(uname -m)
    case "$ARCH" in
        "x86_64")
            DOCKER_PLATFORM="linux/amd64"
            success "Architecture: x86_64 (AMD64)"
            ;;
        "aarch64"|"arm64")
            DOCKER_PLATFORM="linux/arm64"
            success "Architecture: ARM64"
            ;;
        "armv7l"|"armv6l")
            DOCKER_PLATFORM="linux/arm/v7"
            success "Architecture: ARM32"
            ;;
        *)
            warning "Unknown architecture: $ARCH. Defaulting to linux/amd64"
            DOCKER_PLATFORM="linux/amd64"
            ;;
    esac
    export DOCKER_PLATFORM

    # Check available disk space (minimum 2GB)
    local available_space=$(df "$INSTALL_PATH" 2>/dev/null | tail -1 | awk '{print $4}' || echo 0)
    local space_gb=$((available_space / 1024 / 1024))

    if [[ $space_gb -lt 2 ]]; then
        error "Insufficient disk space. Need at least 2GB, found: ${space_gb}GB"
    fi
    success "Disk space check passed: ${space_gb}GB available"

    # Check internet connectivity
    if ! curl -s --max-time 10 https://github.com >/dev/null; then
        error "No internet connectivity. Cannot download MediaButler source."
    fi
    success "Internet connectivity verified"
}

# =============================================================================
# CLEANUP PREVIOUS INSTALLATION
# =============================================================================

cleanup_previous() {
    step "Cleaning up previous installation..."

    # Change to install directory if it exists
    if [[ -d "$INSTALL_PATH" ]]; then
        cd "$INSTALL_PATH" || true

        # Stop existing containers
        if [[ -f "docker-compose.yml" ]]; then
            $COMPOSE_CMD down --remove-orphans 2>/dev/null || true
            success "Stopped existing containers"
        fi

        # Remove old images (keep last version as backup)
        local old_images=$(docker images --filter "reference=mediabutler/*" --format "{{.Repository}}:{{.Tag}}" | grep -v "latest" | head -5)
        if [[ -n "$old_images" ]]; then
            echo "$old_images" | xargs docker rmi 2>/dev/null || true
            success "Cleaned up old Docker images"
        fi

        # Create backup of current configuration
        if [[ -f ".env" ]]; then
            cp .env ".env.backup.$(date +%Y%m%d_%H%M%S)" 2>/dev/null || true
            success "Backed up existing configuration"
        fi
    fi

    # Create installation directory
    mkdir -p "$INSTALL_PATH"/{data,logs,models,configs,temp}
    chmod 755 "$INSTALL_PATH"
    success "Prepared installation directory: $INSTALL_PATH"
}

# =============================================================================
# DOWNLOAD SOURCE CODE
# =============================================================================

download_source() {
    step "Downloading MediaButler source code..."

    cd "$INSTALL_PATH" || error "Cannot access install directory"

    # Download source as ZIP (more reliable than git clone)
    local download_url="${GITHUB_REPO_URL}/archive/refs/heads/${GITHUB_BRANCH}.zip"
    local temp_file="/tmp/mediabutler-source.zip"

    log "Downloading from: $download_url"

    if ! curl -L -o "$temp_file" "$download_url"; then
        error "Failed to download source code from GitHub"
    fi

    # Extract source code
    log "Extracting source code..."
    if ! unzip -q "$temp_file" -d /tmp/; then
        error "Failed to extract source archive"
    fi

    # Move source files to installation directory
    local source_dir="/tmp/MediaButler-${GITHUB_BRANCH}"
    if [[ -d "$source_dir" ]]; then
        # Copy source files
        cp -r "$source_dir"/src ./

        # Copy Docker configuration files
        mkdir -p docker config
        [[ -d "$source_dir/Delivery/docker" ]] && cp -r "$source_dir"/Delivery/docker/* ./docker/
        [[ -d "$source_dir/Delivery/config" ]] && cp -r "$source_dir"/Delivery/config/* ./config/

        # Create missing directories and files
        mkdir -p models configs

        # Create empty model directory structure
        touch models/.gitkeep
        touch configs/.gitkeep

        # Copy project files to build context
        [[ -f "$source_dir/global.json" ]] && cp "$source_dir/global.json" ./
        [[ -f "$source_dir/NuGet.Config" ]] && cp "$source_dir/NuGet.Config" ./
        [[ -f "$source_dir/Directory.Build.props" ]] && cp "$source_dir/Directory.Build.props" ./

        success "Source code extracted successfully"
    else
        error "Source directory not found after extraction"
    fi

    # Cleanup
    rm -f "$temp_file"
    rm -rf "/tmp/MediaButler-${GITHUB_BRANCH}"
}

# =============================================================================
# CONFIGURATION SETUP
# =============================================================================

setup_configuration() {
    step "Setting up configuration files..."

    # Create .env file
    cat > .env << EOF
# MediaButler QNAP Deployment Configuration
# Generated on $(date)

# Network Configuration
API_PORT=$API_PORT
WEB_PORT=$WEB_PORT
PROXY_PORT=$PROXY_PORT
PROXY_SSL_PORT=443

# Paths
INSTALL_PATH=$INSTALL_PATH

# Resource Limits (optimized for 1GB RAM)
MEMORY_LIMIT_API=$MEMORY_LIMIT_API
MEMORY_LIMIT_WEB=$MEMORY_LIMIT_WEB
MEMORY_LIMIT_PROXY=$MEMORY_LIMIT_PROXY

# Docker Platform
DOCKER_PLATFORM=$DOCKER_PLATFORM

# SSL Configuration
SSL_ENABLED=$SSL_ENABLED
SSL_CERT_PATH=$SSL_CERT_PATH
SSL_KEY_PATH=$SSL_KEY_PATH

# Nginx Configuration
NGINX_HOST=_

EOF

    success "Environment configuration created"

    # Create docker-compose.yml from template
    if [[ -f "config/docker-compose.template.yml" ]]; then
        envsubst < config/docker-compose.template.yml > docker-compose.yml
        success "Docker Compose configuration generated"
    else
        error "Docker Compose template not found"
    fi

    # Copy Nginx configuration if it exists
    [[ -f "config/nginx.template.conf" ]] && cp config/nginx.template.conf nginx.conf

    success "Configuration setup completed"
}

# =============================================================================
# BUILD AND DEPLOY
# =============================================================================

build_and_deploy() {
    step "Building and deploying MediaButler..."

    cd "$INSTALL_PATH" || error "Cannot access install directory"

    # Enable BuildKit for better performance
    export DOCKER_BUILDKIT=1
    export COMPOSE_DOCKER_CLI_BUILD=1

    # Build images with detailed progress
    log "Building API container..."
    if ! $COMPOSE_CMD build --progress=plain mediabutler-api; then
        error "Failed to build API container"
    fi
    success "API container built successfully"

    log "Building Web container..."
    if ! $COMPOSE_CMD build --progress=plain mediabutler-web; then
        error "Failed to build Web container"
    fi
    success "Web container built successfully"

    # Start services
    log "Starting MediaButler services..."
    if ! $COMPOSE_CMD up -d; then
        error "Failed to start services"
    fi

    # Wait for services to become healthy
    wait_for_health

    success "MediaButler deployed successfully!"
}

# =============================================================================
# HEALTH MONITORING
# =============================================================================

wait_for_health() {
    log "Waiting for services to become healthy..."

    local max_attempts=60
    local attempt=1

    while [[ $attempt -le $max_attempts ]]; do
        local healthy_count=$($COMPOSE_CMD ps --filter health=healthy | wc -l)
        local total_services=3

        if [[ $healthy_count -ge $total_services ]]; then
            success "All services are healthy and running"
            return 0
        fi

        if [[ $((attempt % 6)) -eq 0 ]]; then
            log "Health check progress: $healthy_count/$total_services services healthy (attempt $attempt/$max_attempts)"
            $COMPOSE_CMD ps
        fi

        sleep 10
        ((attempt++))
    done

    warning "Services started but health check timeout reached"
    $COMPOSE_CMD ps
    $COMPOSE_CMD logs --tail=20
}

# =============================================================================
# MAIN EXECUTION
# =============================================================================

main() {
    echo
    echo -e "${BLUE}╔══════════════════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║                    MediaButler QNAP NAS Deployment                          ║${NC}"
    echo -e "${BLUE}║                         Optimized for 1GB RAM                               ║${NC}"
    echo -e "${BLUE}╚══════════════════════════════════════════════════════════════════════════════╝${NC}"
    echo

    # Configuration summary
    info "Deployment Configuration:"
    echo "  📍 Installation Path: $INSTALL_PATH"
    echo "  🌐 API Port: $API_PORT"
    echo "  🖥️  Web Port: $WEB_PORT"
    echo "  🔗 Proxy Port: $PROXY_PORT"
    echo "  🏗️  Architecture: ${DOCKER_PLATFORM:-auto-detect}"
    echo "  💾 Memory Limits: API=${MEMORY_LIMIT_API}, Web=${MEMORY_LIMIT_WEB}, Proxy=${MEMORY_LIMIT_PROXY}"
    echo

    # Execute deployment steps
    check_requirements
    cleanup_previous
    download_source
    setup_configuration
    build_and_deploy

    # Final status report
    echo
    echo -e "${GREEN}╔══════════════════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}║                          DEPLOYMENT COMPLETED                               ║${NC}"
    echo -e "${GREEN}╚══════════════════════════════════════════════════════════════════════════════╝${NC}"
    echo
    echo "🎉 MediaButler has been successfully deployed!"
    echo
    echo "🌐 Access URLs:"
    echo "════════════════════════════════════════════════════════════════"
    echo "📱 Web UI:            http://$(hostname -I | awk '{print $1}'):${PROXY_PORT}"
    echo "🔗 API Endpoint:      http://$(hostname -I | awk '{print $1}'):${API_PORT}"
    echo "📊 API Direct:        http://$(hostname -I | awk '{print $1}'):${API_PORT}/swagger"
    echo
    echo "🛠️  Management Commands:"
    echo "════════════════════════════════════════════════════════════════"
    echo "📊 View status:       cd ${INSTALL_PATH} && $COMPOSE_CMD ps"
    echo "📜 View logs:         cd ${INSTALL_PATH} && $COMPOSE_CMD logs -f"
    echo "🔄 Restart services:  cd ${INSTALL_PATH} && $COMPOSE_CMD restart"
    echo "⏹️  Stop services:     cd ${INSTALL_PATH} && $COMPOSE_CMD down"
    echo
    echo "📊 Current Container Status:"
    echo "════════════════════════════════════════════════════════════════"
    $COMPOSE_CMD ps
    echo
    echo "📈 Resource Usage:"
    echo "════════════════════════════════════════════════════════════════"
    docker stats --no-stream --format "table {{.Container}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.NetIO}}\t{{.BlockIO}}" 2>/dev/null || echo "Resource stats unavailable"
    echo
}

# Handle script interruption
trap 'echo; error "Deployment interrupted by user"' INT TERM

# Run main function
main "$@"