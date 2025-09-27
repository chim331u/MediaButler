#!/bin/bash
set -euo pipefail

# MediaButler QNAP NAS Deployment Script - Orchestrator
# Optimized for 1GB RAM ARM32/ARM64 NAS systems
# Version: 2.0.0 - Separated API and WEB deployment
# Author: MediaButler Team

# =============================================================================
# CONFIGURATION - Customize these parameters before deployment
# =============================================================================

# Default Configuration (can be overridden via environment variables)
GITHUB_REPO_URL="${GITHUB_REPO_URL:-https://github.com/chim331u/MediaButler.git}"
GITHUB_BRANCH="${GITHUB_BRANCH:-delploy}"
API_PORT="${API_PORT:-30129}"
WEB_PORT="${WEB_PORT:-30139}"
INSTALL_PATH="${INSTALL_PATH:-/share/Container/mediabutler}"

# Deployment Options
DEPLOY_API="${DEPLOY_API:-true}"
DEPLOY_WEB="${DEPLOY_WEB:-true}"
SKIP_HEALTH_CHECK="${SKIP_HEALTH_CHECK:-false}"

# Advanced Configuration
DOCKER_REGISTRY="${DOCKER_REGISTRY:-}"
SSL_ENABLED="${SSL_ENABLED:-false}"
SSL_CERT_PATH="${SSL_CERT_PATH:-}"
SSL_KEY_PATH="${SSL_KEY_PATH:-}"
BACKUP_ENABLED="${BACKUP_ENABLED:-true}"
MONITORING_ENABLED="${MONITORING_ENABLED:-true}"

# Script Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DEPLOY_SCRIPT="${SCRIPT_DIR}/deploy-mediabutler-api.sh"
WEB_DEPLOY_SCRIPT="${SCRIPT_DIR}/deploy-mediabutler-web.sh"

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

    # Check available memory (with proper error handling)
    local mem_total=$(grep MemTotal /proc/meminfo 2>/dev/null | awk '{print $2}' || echo "0")

    if [[ "$mem_total" =~ ^[0-9]+$ ]] && [[ $mem_total -gt 0 ]]; then
        local mem_mb=$((mem_total / 1024))

        if [[ $mem_mb -lt 300 ]]; then
            warning "Low memory detected: ${mem_mb}MB. MediaButler requires at least 300MB RAM for deployment."
            warning "Deployment will continue with reduced memory limits."
            # Adjust memory limits for low-memory systems
            MEMORY_LIMIT_API="100m"
            MEMORY_LIMIT_WEB="75m"
            MEMORY_LIMIT_PROXY="15m"
        else
            success "Memory check passed: ${mem_mb}MB available"
        fi
    else
        warning "Unable to determine system memory. Proceeding with conservative memory limits."
        MEMORY_LIMIT_API="100m"
        MEMORY_LIMIT_WEB="75m"
        MEMORY_LIMIT_PROXY="15m"
    fi

    # Check Docker
    if ! command -v docker >/dev/null 2>&1; then
        error "Docker is not installed. Please install Container Station first."
    fi
    DOCKER_VERSION=$(docker --version | cut -d' ' -f3 | cut -d',' -f1)
    success "Docker found: $DOCKER_VERSION"

    # Check if individual deployment scripts exist
    if [[ "$DEPLOY_API" == "true" && ! -f "$API_DEPLOY_SCRIPT" ]]; then
        error "API deployment script not found: $API_DEPLOY_SCRIPT"
    fi

    if [[ "$DEPLOY_WEB" == "true" && ! -f "$WEB_DEPLOY_SCRIPT" ]]; then
        error "WEB deployment script not found: $WEB_DEPLOY_SCRIPT"
    fi

    # Make scripts executable
    if [[ "$DEPLOY_API" == "true" ]]; then
        chmod +x "$API_DEPLOY_SCRIPT" || warning "Failed to make API script executable"
        success "API deployment script found: $API_DEPLOY_SCRIPT"
    fi

    if [[ "$DEPLOY_WEB" == "true" ]]; then
        chmod +x "$WEB_DEPLOY_SCRIPT" || warning "Failed to make WEB script executable"
        success "WEB deployment script found: $WEB_DEPLOY_SCRIPT"
    fi

    # Check architecture
    ARCH=$(uname -m)
    case "$ARCH" in
        "x86_64")
            DOCKER_PLATFORM="linux/amd64"
            DOCKER_ARCH="amd64"
            success "Architecture: x86_64 (AMD64)"
            ;;
        "aarch64"|"arm64")
            DOCKER_PLATFORM="linux/arm64"
            DOCKER_ARCH="arm64"
            success "Architecture: ARM64"
            ;;
        "armv7l"|"armv6l")
            DOCKER_PLATFORM="linux/arm/v7"
            DOCKER_ARCH="arm"
            success "Architecture: ARM32"
            ;;
        *)
            warning "Unknown architecture: $ARCH. Defaulting to linux/amd64"
            DOCKER_PLATFORM="linux/amd64"
            DOCKER_ARCH="amd64"
            ;;
    esac
    export DOCKER_PLATFORM
    export DOCKER_ARCH

    info "System requirements check completed successfully"
}

#############################################################################
# DEPLOYMENT FUNCTIONS
#############################################################################

deploy_api() {
    step "Deploying MediaButler API"

    if [[ "$DEPLOY_API" != "true" ]]; then
        info "API deployment skipped (DEPLOY_API=false)"
        return 0
    fi

    log "Starting API deployment using separate script..."

    # Set environment variables for API deployment
    export GITHUB_REPO="$GITHUB_REPO_URL"
    export GIT_BRANCH="$GITHUB_BRANCH"
    export HOST_PORT="$API_PORT"
    export CONTAINER_NAME="mediabutler_api"

    # Run API deployment script
    if ! "$API_DEPLOY_SCRIPT"; then
        error "API deployment failed"
        return 1
    fi

    success "API deployment completed successfully"
}

deploy_web() {
    step "Deploying MediaButler WEB"

    if [[ "$DEPLOY_WEB" != "true" ]]; then
        info "WEB deployment skipped (DEPLOY_WEB=false)"
        return 0
    fi

    log "Starting WEB deployment using separate script..."

    # Set environment variables for WEB deployment
    export GITHUB_REPO="$GITHUB_REPO_URL"
    export GIT_BRANCH="$GITHUB_BRANCH"
    export HOST_PORT="$WEB_PORT"
    export CONTAINER_NAME="mediabutler_web"
    export API_BASE_URL="http://localhost:${API_PORT}/"

    # Run WEB deployment script
    if ! "$WEB_DEPLOY_SCRIPT"; then
        error "WEB deployment failed"
        return 1
    fi

    success "WEB deployment completed successfully"
}

verify_deployment() {
    step "Verifying deployment"

    local api_healthy=false
    local web_healthy=false

    if [[ "$SKIP_HEALTH_CHECK" == "true" ]]; then
        warning "Health check skipped (SKIP_HEALTH_CHECK=true)"
        return 0
    fi

    # Check API health
    if [[ "$DEPLOY_API" == "true" ]]; then
        log "Checking API health..."
        local api_url="http://localhost:${API_PORT}/health"

        if command -v curl >/dev/null 2>&1; then
            for i in {1..5}; do
                if curl -f -s "$api_url" >/dev/null 2>&1; then
                    success "API is responding at $api_url"
                    api_healthy=true
                    break
                else
                    warning "API health check attempt $i/5 failed, retrying in 10s..."
                    sleep 10
                fi
            done

            if [[ "$api_healthy" != "true" ]]; then
                warning "API health check failed after 5 attempts"
                log "Check API logs: docker logs mediabutler_api"
            fi
        else
            warning "curl not available for API health check"
        fi
    fi

    # Check WEB health
    if [[ "$DEPLOY_WEB" == "true" ]]; then
        log "Checking WEB health..."
        local web_url="http://localhost:${WEB_PORT}/"

        if command -v curl >/dev/null 2>&1; then
            for i in {1..3}; do
                if curl -f -s "$web_url" >/dev/null 2>&1; then
                    success "WEB is responding at $web_url"
                    web_healthy=true
                    break
                else
                    warning "WEB health check attempt $i/3 failed, retrying in 5s..."
                    sleep 5
                fi
            done

            if [[ "$web_healthy" != "true" ]]; then
                warning "WEB health check failed after 3 attempts"
                log "Check WEB logs: docker logs mediabutler_web"
            fi
        else
            warning "curl not available for WEB health check"
        fi
    fi

    # Overall health status
    if [[ "$DEPLOY_API" == "true" && "$api_healthy" != "true" ]]; then
        warning "API deployment may have issues"
    fi

    if [[ "$DEPLOY_WEB" == "true" && "$web_healthy" != "true" ]]; then
        warning "WEB deployment may have issues"
    fi

    success "Deployment verification completed"
}

#############################################################################
# MAIN DEPLOYMENT PROCESS
#############################################################################

print_banner() {
    echo
    echo "============================================================================="
    echo "  MediaButler QNAP NAS Deployment - Orchestrator Script"
    echo "============================================================================="
    echo "Repository: $GITHUB_REPO_URL"
    echo "Branch: $GITHUB_BRANCH"
    echo "API Port: $API_PORT"
    echo "WEB Port: $WEB_PORT"
    echo ""
    echo "Deployment Options:"
    echo "  Deploy API: $DEPLOY_API"
    echo "  Deploy WEB: $DEPLOY_WEB"
    echo "  Skip Health Check: $SKIP_HEALTH_CHECK"
    echo ""
    echo "Architecture: $(uname -m)"
    echo "============================================================================="
}

print_summary() {
    echo ""
    echo "============================================================================="
    echo "  MEDIABUTLER DEPLOYMENT COMPLETED"
    echo "============================================================================="

    if [[ "$DEPLOY_API" == "true" ]]; then
        echo "API URL: http://localhost:${API_PORT}"
        echo "API Health: http://localhost:${API_PORT}/health"
        echo "API Swagger: http://localhost:${API_PORT}/swagger"
    fi

    if [[ "$DEPLOY_WEB" == "true" ]]; then
        echo "WEB URL: http://localhost:${WEB_PORT}"
    fi

    echo ""
    echo "Useful commands:"
    if [[ "$DEPLOY_API" == "true" ]]; then
        echo "  docker logs mediabutler_api              # View API logs"
        echo "  docker restart mediabutler_api           # Restart API"
        echo "  docker stats mediabutler_api             # View API resource usage"
    fi

    if [[ "$DEPLOY_WEB" == "true" ]]; then
        echo "  docker logs mediabutler_web              # View WEB logs"
        echo "  docker restart mediabutler_web           # Restart WEB"
        echo "  docker stats mediabutler_web             # View WEB resource usage"
    fi

    echo ""
    echo "Next steps:"
    echo "  1. Configure your watch folders and library paths"
    echo "  2. Access the web interface to start organizing your media"
    echo "  3. Monitor logs for any issues"
    echo "============================================================================="
}

show_help() {
    cat << EOF
MediaButler QNAP NAS Deployment Script - Orchestrator
============================================================================

DESCRIPTION:
    This orchestrator script deploys MediaButler components (API and/or WEB)
    on QNAP ARM32/ARM64 NAS systems using separate optimized deployment scripts.

USAGE:
    $0 [OPTIONS]

OPTIONS:
    -h, --help              Show this help message
    --api-only              Deploy only the API component
    --web-only              Deploy only the WEB component
    --skip-health-check     Skip health check verification
    -r, --repo URL          Git repository URL
    -b, --branch NAME       Git branch name (default: main)
    --api-port PORT         API port (default: 30129)
    --web-port PORT         WEB port (default: 30139)

ENVIRONMENT VARIABLES:
    DEPLOY_API              Deploy API component (default: true)
    DEPLOY_WEB              Deploy WEB component (default: true)
    SKIP_HEALTH_CHECK       Skip health verification (default: false)
    GITHUB_REPO_URL         Repository URL
    GITHUB_BRANCH           Branch to deploy
    API_PORT                API port number
    WEB_PORT                WEB port number

EXAMPLES:
    # Deploy both API and WEB (default)
    $0

    # Deploy only API
    $0 --api-only

    # Deploy only WEB (requires API to be already running)
    $0 --web-only

    # Deploy with custom ports
    $0 --api-port 8080 --web-port 8081

    # Deploy from different branch
    $0 -b develop

REQUIREMENTS:
    - QNAP NAS with Container Station enabled
    - deploy-mediabutler-api.sh script (for API deployment)
    - deploy-mediabutler-web.sh script (for WEB deployment)
    - Docker available via Container Station

ARCHITECTURE:
    This script acts as an orchestrator that calls specialized deployment
    scripts for each component, following "Simple Made Easy" principles
    by composing independent deployment tasks.

EOF
}

parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -h|--help)
                show_help
                exit 0
                ;;
            --api-only)
                DEPLOY_API="true"
                DEPLOY_WEB="false"
                shift
                ;;
            --web-only)
                DEPLOY_API="false"
                DEPLOY_WEB="true"
                shift
                ;;
            --skip-health-check)
                SKIP_HEALTH_CHECK="true"
                shift
                ;;
            -r|--repo)
                GITHUB_REPO_URL="$2"
                shift 2
                ;;
            -b|--branch)
                GITHUB_BRANCH="$2"
                shift 2
                ;;
            --api-port)
                API_PORT="$2"
                shift 2
                ;;
            --web-port)
                WEB_PORT="$2"
                shift 2
                ;;
            *)
                error "Unknown option: $1"
                show_help
                exit 1
                ;;
        esac
    done
}

main() {
    parse_arguments "$@"
    print_banner

    # Pre-deployment checks
    check_requirements

    # Deployment process
    if [[ "$DEPLOY_API" == "true" ]]; then
        deploy_api
    fi

    if [[ "$DEPLOY_WEB" == "true" ]]; then
        deploy_web
    fi

    # Verification
    verify_deployment

    print_summary
    success "MediaButler deployment orchestration completed!"
}

# Execute main function with all arguments
main "$@"

    # Ensure the install path directory exists or can be created
    if ! mkdir -p "$(dirname "$INSTALL_PATH")" 2>/dev/null; then
        error "Cannot create installation directory: $INSTALL_PATH"
    fi

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
        # Check if envsubst is available, otherwise use a simpler sed-based approach
        if command -v envsubst >/dev/null 2>&1; then
            envsubst < config/docker-compose.template.yml > docker-compose.yml
        else
            # Fallback to manual substitution using a safer approach
            cp config/docker-compose.template.yml docker-compose.yml

            # Use a more robust substitution method to avoid sed escaping issues
            cat > substitute_vars.py << 'PYTHON_SCRIPT'
import sys
import os

# Read the template file
with open('docker-compose.yml', 'r') as f:
    content = f.read()

# Get environment variables
substitutions = {
    'API_PORT': os.environ.get('API_PORT', '30129'),
    'WEB_PORT': os.environ.get('WEB_PORT', '30139'),
    'PROXY_PORT': os.environ.get('PROXY_PORT', '8080'),
    'MEMORY_LIMIT_API': os.environ.get('MEMORY_LIMIT_API', '150m'),
    'MEMORY_LIMIT_WEB': os.environ.get('MEMORY_LIMIT_WEB', '100m'),
    'MEMORY_LIMIT_PROXY': os.environ.get('MEMORY_LIMIT_PROXY', '20m'),
    'DOCKER_PLATFORM': os.environ.get('DOCKER_PLATFORM', 'linux/amd64'),
    'DOCKER_ARCH': os.environ.get('DOCKER_ARCH', 'amd64'),
    'INSTALL_PATH': os.environ.get('INSTALL_PATH', '/tmp'),
    'PROXY_SSL_PORT': os.environ.get('PROXY_SSL_PORT', '443'),
    'SSL_ENABLED': os.environ.get('SSL_ENABLED', 'false'),
    'SSL_CERT_PATH': os.environ.get('SSL_CERT_PATH', '/dev/null'),
    'SSL_KEY_PATH': os.environ.get('SSL_KEY_PATH', '/dev/null'),
    'NGINX_HOST': os.environ.get('NGINX_HOST', '_')
}

# Apply substitutions
for var, value in substitutions.items():
    content = content.replace(f'${{{var}}}', value)

# Write back
with open('docker-compose.yml', 'w') as f:
    f.write(content)

print("Variable substitution completed")
PYTHON_SCRIPT

            python3 substitute_vars.py 2>/dev/null && rm -f substitute_vars.py || {
                # Python fallback failed, use simple sed with different delimiter
                rm -f substitute_vars.py
                sed -i "s|\${API_PORT}|$API_PORT|g" docker-compose.yml
                sed -i "s|\${WEB_PORT}|$WEB_PORT|g" docker-compose.yml
                sed -i "s|\${PROXY_PORT}|$PROXY_PORT|g" docker-compose.yml
                sed -i "s|\${PROXY_SSL_PORT}|${PROXY_SSL_PORT:-443}|g" docker-compose.yml
                sed -i "s|\${MEMORY_LIMIT_API}|$MEMORY_LIMIT_API|g" docker-compose.yml
                sed -i "s|\${MEMORY_LIMIT_WEB}|$MEMORY_LIMIT_WEB|g" docker-compose.yml
                sed -i "s|\${MEMORY_LIMIT_PROXY}|$MEMORY_LIMIT_PROXY|g" docker-compose.yml
                sed -i "s|\${DOCKER_PLATFORM}|$DOCKER_PLATFORM|g" docker-compose.yml
                sed -i "s|\${DOCKER_ARCH}|$DOCKER_ARCH|g" docker-compose.yml
                sed -i "s|\${SSL_ENABLED}|${SSL_ENABLED}|g" docker-compose.yml
                sed -i "s|\${SSL_CERT_PATH}|${SSL_CERT_PATH:-/dev/null}|g" docker-compose.yml
                sed -i "s|\${SSL_KEY_PATH}|${SSL_KEY_PATH:-/dev/null}|g" docker-compose.yml
                sed -i "s|\${NGINX_HOST}|${NGINX_HOST:-_}|g" docker-compose.yml
                sed -i "s|\${INSTALL_PATH}|$INSTALL_PATH|g" docker-compose.yml
            }
        fi
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