#!/bin/bash

#############################################################################
# MediaButler WEB Deployment Script for QNAP ARM32 NAS
# Optimized for ARM32 architecture with 1GB RAM
#
# This script performs:
# 1. Git clone from specified repository and branch
# 2. Docker build optimized for ARM32 (.NET 9 Blazor WebAssembly + Nginx)
# 3. Docker run with configurable parameters and nginx static file serving
#############################################################################

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging function
log() {
    echo -e "${BLUE}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1" >&2
}

success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

#############################################################################
# CONFIGURATION PARAMETERS
# These can be modified or set via environment variables
# Based on MediaButler architecture - adapted for .NET 9 Blazor WebAssembly
#############################################################################

# Git Repository Configuration
GITHUB_REPO="${GITHUB_REPO:-https://github.com/chim331u/MediaButler.git}"
GIT_BRANCH="${GIT_BRANCH:-delploy}"  # Default branch for MediaButler (current: delploy)
LOCAL_REPO_DIR="${LOCAL_REPO_DIR:-/tmp/MediaButler_Web}"

# Docker Configuration
DOCKER_IMAGE_NAME="${DOCKER_IMAGE_NAME:-mediabutler_web_image}"
DOCKER_IMAGE_TAG="${DOCKER_IMAGE_TAG:-latest}"
CONTAINER_NAME="${CONTAINER_NAME:-mediabutler_web}"

# Docker Build Configuration
DOCKERFILE_PATH="${DOCKERFILE_PATH:-Delivery/docker/Dockerfile.webassembly}"
BUILD_CONTEXT="${BUILD_CONTEXT:-.}"

# Container Runtime Configuration
HOST_PORT="${HOST_PORT:-30139}"
CONTAINER_PORT="${CONTAINER_PORT:-80}"
HOST_HTTPS_PORT="${HOST_HTTPS_PORT:-30443}"
CONTAINER_HTTPS_PORT="${CONTAINER_HTTPS_PORT:-443}"

# Volume Mappings (QNAP specific paths for nginx logs and SSL certificates)
# Customize these paths according to your QNAP NAS setup
NGINX_LOG_VOLUME="${NGINX_LOG_VOLUME:-/share/CACHEDEV2_DATA/Storage/Docker/mediabutler_web/logs:/var/log/nginx}"
SSL_CERT_VOLUME="${SSL_CERT_VOLUME:-/share/CACHEDEV2_DATA/Storage/Docker/mediabutler_web/certs:/etc/nginx/ssl}"

# Application Configuration
# API Base URL - Points to your MediaButler API container
API_BASE_URL="${API_BASE_URL:-http://localhost:30129/}"
API_TIMEOUT="${API_TIMEOUT:-30}"

#############################################################################
# ADDITIONAL OPTIONAL SETTINGS
# These settings provide fine-tuned control over the deployment
#############################################################################

# Nginx Configuration
NGINX_WORKER_PROCESSES="${NGINX_WORKER_PROCESSES:-auto}"
NGINX_WORKER_CONNECTIONS="${NGINX_WORKER_CONNECTIONS:-1024}"
NGINX_KEEPALIVE_TIMEOUT="${NGINX_KEEPALIVE_TIMEOUT:-65}"

# Compression Settings
ENABLE_GZIP="${ENABLE_GZIP:-true}"
GZIP_COMP_LEVEL="${GZIP_COMP_LEVEL:-6}"

# Cache Settings
STATIC_CACHE_DURATION="${STATIC_CACHE_DURATION:-1d}"
API_CACHE_DURATION="${API_CACHE_DURATION:-no-cache}"

#############################################################################
# ARM32 NAS OPTIMIZATION SETTINGS
# These settings are automatically applied in the Docker container
#############################################################################

# Nginx optimizations for ARM32
NGINX_WORKER_PROCESSES_ARM32="${NGINX_WORKER_PROCESSES_ARM32:-1}"  # Single worker for ARM32
NGINX_CLIENT_MAX_BODY_SIZE="${NGINX_CLIENT_MAX_BODY_SIZE:-10m}"
NGINX_CLIENT_BODY_TIMEOUT="${NGINX_CLIENT_BODY_TIMEOUT:-60s}"

# Static file optimization
ENABLE_ETAG="${ENABLE_ETAG:-on}"
ENABLE_SENDFILE="${ENABLE_SENDFILE:-on}"
TCP_NOPUSH="${TCP_NOPUSH:-on}"
TCP_NODELAY="${TCP_NODELAY:-on}"

# Log retention settings
ACCESS_LOG_RETENTION="${ACCESS_LOG_RETENTION:-7}"  # days
ERROR_LOG_RETENTION="${ERROR_LOG_RETENTION:-30}"   # days

#############################################################################
# PARAMETER VALIDATION
#############################################################################

validate_parameters() {
    log "Validating deployment parameters..."

    # Validate repository URL
    if [[ -z "$GITHUB_REPO" ]]; then
        error "GITHUB_REPO is required"
        exit 1
    fi

    if [[ ! "$GITHUB_REPO" =~ ^https?://github\.com/.+\.git$ ]]; then
        warning "GITHUB_REPO format may be invalid: $GITHUB_REPO"
        warning "Expected format: https://github.com/user/repository.git"
    fi

    # Validate branch name
    if [[ -z "$GIT_BRANCH" ]]; then
        error "GIT_BRANCH is required"
        exit 1
    fi

    # Check for potentially problematic characters in branch name
    if [[ "$GIT_BRANCH" =~ [[:space:]] ]]; then
        error "GIT_BRANCH contains spaces: '$GIT_BRANCH'"
        exit 1
    fi

    # Validate API base URL
    if [[ ! "$API_BASE_URL" =~ ^https?:// ]]; then
        error "API_BASE_URL must be a valid HTTP/HTTPS URL: $API_BASE_URL"
        exit 1
    fi

    # Validate ports
    if ! [[ "$HOST_PORT" =~ ^[0-9]+$ ]] || [[ "$HOST_PORT" -lt 1 || "$HOST_PORT" -gt 65535 ]]; then
        error "HOST_PORT must be a valid port number (1-65535): $HOST_PORT"
        exit 1
    fi

    log "Repository: $GITHUB_REPO"
    log "Branch: $GIT_BRANCH"
    log "Container: $CONTAINER_NAME"
    log "Port: $HOST_PORT:$CONTAINER_PORT"
    log "API URL: $API_BASE_URL"

    success "Parameter validation completed"
}

#############################################################################
# QNAP NAS OPTIMIZATION CHECKS
#############################################################################

check_qnap_environment() {
    log "Checking QNAP NAS environment..."

    # Check if running on ARM32 architecture
    ARCH=$(uname -m)
    if [[ "$ARCH" != "armv7l" ]]; then
        warning "Not running on ARM32 architecture (detected: $ARCH)"
    else
        success "ARM32 architecture detected"
    fi

    # Check available memory
    TOTAL_MEM=$(free -m | awk 'NR==2{printf "%.0f", $2}')
    if [[ $TOTAL_MEM -lt 1000 ]]; then
        warning "Available memory is low: ${TOTAL_MEM}MB"
        log "Nginx worker processes will be limited to 1 for ARM32 optimization"
    fi

    # Check download tools
    if command -v git >/dev/null 2>&1; then
        success "Git available - will use git clone"
    elif command -v wget >/dev/null 2>&1; then
        success "wget available - will use ZIP download"
    elif command -v curl >/dev/null 2>&1; then
        success "curl available - will use ZIP download"
    else
        error "Neither git, wget, nor curl found. Cannot download repository."
        exit 1
    fi

    # Check extraction tools (needed for ZIP method)
    if ! command -v git >/dev/null 2>&1; then
        if ! command -v unzip >/dev/null 2>&1; then
            error "unzip command not found. Required for ZIP extraction."
            log "Please install unzip: opkg install unzip"
            exit 1
        else
            success "unzip available for ZIP extraction"
        fi
    fi

    # Check Docker
    if ! command -v docker >/dev/null 2>&1; then
        error "Docker not found. Please enable Container Station."
        exit 1
    else
        success "Docker available"
    fi

    # Check if required directories exist
    for dir in "/share/CACHEDEV2_DATA" "/share/Download" "/share/Video"; do
        if [[ ! -d "$dir" ]]; then
            warning "Directory $dir does not exist"
        fi
    done
}

#############################################################################
# DOCKER MANAGEMENT FUNCTIONS
#############################################################################

cleanup_existing_container() {
    log "Checking for existing container: $CONTAINER_NAME"

    if docker ps -a --format "table {{.Names}}" | grep -q "^${CONTAINER_NAME}$"; then
        log "Stopping and removing existing container: $CONTAINER_NAME"
        docker stop "$CONTAINER_NAME" 2>/dev/null || true
        docker rm "$CONTAINER_NAME" 2>/dev/null || true
        success "Existing container removed"
    else
        log "No existing container found"
    fi
}

cleanup_existing_image() {
    local image_full_name="${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"

    log "Checking for existing image: $image_full_name"

    if docker images --format "table {{.Repository}}:{{.Tag}}" | grep -q "^${image_full_name}$"; then
        log "Removing existing image: $image_full_name"
        docker rmi "$image_full_name" 2>/dev/null || true
        success "Existing image removed"
    else
        log "No existing image found"
    fi
}

#############################################################################
# GIT OPERATIONS
#############################################################################

download_repository_zip() {
    log "Downloading repository as ZIP archive..."

    # Convert GitHub repo URL to ZIP download URL
    # From: https://github.com/user/repo.git
    # To:   https://github.com/user/repo/archive/refs/heads/branch.zip
    local repo_base=$(echo "$GITHUB_REPO" | sed 's/\.git$//')
    local zip_url="${repo_base}/archive/refs/heads/${GIT_BRANCH}.zip"
    local zip_file="/tmp/repo_${GIT_BRANCH}_web.zip"

    log "ZIP URL: $zip_url"
    log "ZIP file: $zip_file"

    # Download ZIP file
    if command -v wget >/dev/null 2>&1; then
        log "Using wget to download..."
        wget -O "$zip_file" "$zip_url"
    elif command -v curl >/dev/null 2>&1; then
        log "Using curl to download..."
        curl -L -o "$zip_file" "$zip_url"
    else
        error "Neither wget nor curl found. Cannot download repository."
        exit 1
    fi

    # Verify download
    if [[ ! -f "$zip_file" ]]; then
        error "Failed to download repository ZIP"
        exit 1
    fi

    # Extract ZIP file
    log "Extracting ZIP archive..."
    if command -v unzip >/dev/null 2>&1; then
        unzip -q "$zip_file" -d "/tmp/"

        # GitHub ZIP extracts to folder named "repo-branch"
        local repo_name=$(basename "$GITHUB_REPO" .git)
        local extracted_dir="/tmp/${repo_name}-${GIT_BRANCH}"

        if [[ -d "$extracted_dir" ]]; then
            mv "$extracted_dir" "$LOCAL_REPO_DIR"
            success "Repository extracted to: $LOCAL_REPO_DIR"
        else
            error "Extracted directory not found: $extracted_dir"
            exit 1
        fi
    else
        error "unzip command not found. Cannot extract repository."
        exit 1
    fi

    # Cleanup
    rm -f "$zip_file"
}

clone_repository() {
    log "Downloading repository: $GITHUB_REPO (branch: $GIT_BRANCH)"

    # Remove existing local repository if exists
    if [[ -d "$LOCAL_REPO_DIR" ]]; then
        log "Removing existing local repository: $LOCAL_REPO_DIR"
        rm -rf "$LOCAL_REPO_DIR"
    fi

    # Check if git is available
    if command -v git >/dev/null 2>&1; then
        log "Git found - using git clone"
        # Clone repository with specific branch
        if [[ "$GIT_BRANCH" == "main" ]] || [[ "$GIT_BRANCH" == "master" ]]; then
            log "Cloning default branch: git clone \"$GITHUB_REPO\" \"$LOCAL_REPO_DIR\""
            git clone "$GITHUB_REPO" "$LOCAL_REPO_DIR"
        else
            log "Cloning specific branch: git clone -b \"$GIT_BRANCH\" \"$GITHUB_REPO\" \"$LOCAL_REPO_DIR\""
            git clone -b "$GIT_BRANCH" "$GITHUB_REPO" "$LOCAL_REPO_DIR"
        fi
    else
        log "Git not found - using wget/curl to download ZIP archive"
        download_repository_zip
    fi

    # Verify download success and find Dockerfile
    if [[ ! -d "$LOCAL_REPO_DIR" ]]; then
        error "Repository download failed - directory not found: $LOCAL_REPO_DIR"
        exit 1
    fi

    cd "$LOCAL_REPO_DIR"

    # Search for Dockerfile in multiple possible locations (order matters - most optimized first)
    POSSIBLE_DOCKERFILES=(
        "$DOCKERFILE_PATH"
        "Delivery/docker/Dockerfile.webassembly"
        "docker/Dockerfile.webassembly"
        "web.dockerfile"
        "Dockerfile.web"
        "Dockerfile"
    )

    FOUND_DOCKERFILE=""
    for dockerfile in "${POSSIBLE_DOCKERFILES[@]}"; do
        if [[ -f "$dockerfile" ]]; then
            FOUND_DOCKERFILE="$dockerfile"
            log "Found Dockerfile at: $dockerfile"
            break
        fi
    done

    # If not found, do a recursive search
    if [[ -z "$FOUND_DOCKERFILE" ]]; then
        log "Dockerfile not found in expected locations, searching recursively..."

        # Search for webassembly dockerfile anywhere in the repository
        RECURSIVE_SEARCH=$(find . -name "Dockerfile.webassembly" -type f | head -1)
        if [[ -n "$RECURSIVE_SEARCH" ]]; then
            FOUND_DOCKERFILE="$RECURSIVE_SEARCH"
            log "Found Dockerfile recursively at: $RECURSIVE_SEARCH"
        else
            # Search for any dockerfile
            RECURSIVE_SEARCH=$(find . -name "*dockerfile*" -type f | head -1)
            if [[ -n "$RECURSIVE_SEARCH" ]]; then
                FOUND_DOCKERFILE="$RECURSIVE_SEARCH"
                log "Found alternative Dockerfile at: $RECURSIVE_SEARCH"
            fi
        fi
    fi

    if [[ -z "$FOUND_DOCKERFILE" ]]; then
        error "Dockerfile not found. Searched locations:"
        for dockerfile in "${POSSIBLE_DOCKERFILES[@]}"; do
            error "  - $dockerfile"
        done
        log "Current directory: $(pwd)"
        log "Repository directory structure:"
        ls -la
        log "All files in repository:"
        find . -type f | head -20
        log "Searching for dockerfile recursively:"
        find . -name "*dockerfile*" -o -name "Dockerfile*"
        log "Searching for Delivery directory:"
        find . -type d -name "Delivery" -o -name "delivery"
        exit 1
    fi

    # Update DOCKERFILE_PATH to the found location
    DOCKERFILE_PATH="$FOUND_DOCKERFILE"
    success "Repository downloaded successfully"

    # Show current info
    if command -v git >/dev/null 2>&1 && [[ -d ".git" ]]; then
        CURRENT_COMMIT=$(git rev-parse --short HEAD 2>/dev/null)
        CURRENT_BRANCH=$(git branch --show-current 2>/dev/null)
        log "Current commit: $CURRENT_COMMIT on branch: $CURRENT_BRANCH"
    else
        log "Downloaded as ZIP archive (no git history)"
    fi
}

#############################################################################
# APPLICATION CONFIGURATION GENERATION
#############################################################################

generate_appsettings() {
    log "Generating appsettings.json for production..."

    local appsettings_file="$LOCAL_REPO_DIR/src/MediaButler.Web/wwwroot/appsettings.json"

    # Create directory if it doesn't exist
    mkdir -p "$(dirname "$appsettings_file")"

    # Create production configuration
    cat > "$appsettings_file" << EOF
{
  "MediaButlerApi": {
    "BaseUrl": "$API_BASE_URL",
    "Timeout": "00:00:$API_TIMEOUT"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
EOF

    success "Created appsettings.json with API URL: $API_BASE_URL"
}

#############################################################################
# DOCKER BUILD OPERATIONS
#############################################################################

build_docker_image() {
    log "Building Docker image for .NET 9 Blazor WebAssembly with nginx..."

    cd "$LOCAL_REPO_DIR"

    # Generate application configuration
    generate_appsettings

    # Verify Dockerfile exists (should already be verified, but double-check)
    if [[ ! -f "$DOCKERFILE_PATH" ]]; then
        error "Dockerfile not found at: $DOCKERFILE_PATH"
        log "Current directory contents:"
        ls -la
        exit 1
    fi

    log "Using Dockerfile: $DOCKERFILE_PATH"

    local image_full_name="${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"

    # Build Docker image with ARM32 platform specification
    log "Building image: $image_full_name"
    log "Dockerfile: $DOCKERFILE_PATH"
    log "Build context: $BUILD_CONTEXT"

    # Try to build with platform specification first
    log "Attempting build with --platform linux/arm/v7"
    if ! docker build \
        --platform linux/arm/v7 \
        -f "$DOCKERFILE_PATH" \
        -t "$image_full_name" \
        "$BUILD_CONTEXT"; then

        warning "Build with --platform failed, trying without platform specification"

        # Fallback: build without platform specification
        if ! docker build \
            -f "$DOCKERFILE_PATH" \
            -t "$image_full_name" \
            "$BUILD_CONTEXT"; then

            error "Docker build failed with both platform and no-platform approaches"
            exit 1
        fi
    fi

    # Verify image was created
    if ! docker images --format "table {{.Repository}}:{{.Tag}}" | grep -q "^${image_full_name}$"; then
        error "Docker image build failed"
        exit 1
    fi

    success "Docker image built successfully: $image_full_name"

    # Show image size for ARM32 optimization verification
    IMAGE_SIZE=$(docker images --format "table {{.Size}}" "$image_full_name" | tail -n +2)
    log "Image size: $IMAGE_SIZE"
}

#############################################################################
# DOCKER RUN OPERATIONS
#############################################################################

create_data_directories() {
    log "Creating required data directories..."

    # Extract host paths from volume mappings
    local nginx_log_host_path=$(echo "$NGINX_LOG_VOLUME" | cut -d':' -f1)
    local ssl_cert_host_path=$(echo "$SSL_CERT_VOLUME" | cut -d':' -f1)

    for dir in "$nginx_log_host_path" "$ssl_cert_host_path"; do
        if [[ ! -d "$dir" ]]; then
            log "Creating directory: $dir"
            mkdir -p "$dir" || warning "Failed to create directory: $dir"
        else
            log "Directory already exists: $dir"
        fi
    done

    # Set proper permissions for nginx
    if [[ -d "$nginx_log_host_path" ]]; then
        chmod 755 "$nginx_log_host_path" || warning "Failed to set permissions on $nginx_log_host_path"
    fi
}

run_docker_container() {
    log "Starting Docker container: $CONTAINER_NAME"

    local image_full_name="${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"

    # Create required directories
    create_data_directories

    # Build docker run command with conditional volumes
    local docker_cmd="docker run \
        --restart always \
        --name \"$CONTAINER_NAME\" \
        -d \
        -p \"${HOST_PORT}:${CONTAINER_PORT}\""

    # Add HTTPS port if different from HTTP
    if [[ "$HOST_HTTPS_PORT" != "$HOST_PORT" ]]; then
        docker_cmd="$docker_cmd -p \"${HOST_HTTPS_PORT}:${CONTAINER_HTTPS_PORT}\""
    fi

    # Add volume mappings
    docker_cmd="$docker_cmd \
        -v \"$NGINX_LOG_VOLUME\" \
        -v \"$SSL_CERT_VOLUME\""

    # Add environment variables for nginx optimization
    docker_cmd="$docker_cmd \
        -e \"NGINX_WORKER_PROCESSES=$NGINX_WORKER_PROCESSES_ARM32\" \
        -e \"NGINX_WORKER_CONNECTIONS=$NGINX_WORKER_CONNECTIONS\" \
        -e \"ENABLE_GZIP=$ENABLE_GZIP\" \
        -e \"GZIP_COMP_LEVEL=$GZIP_COMP_LEVEL\""

    # Add platform and image
    docker_cmd="$docker_cmd \
        --platform linux/arm/v7 \
        \"$image_full_name\""

    # Execute docker run command
    eval $docker_cmd

    # Verify container is running
    sleep 5
    if ! docker ps --format "table {{.Names}}" | grep -q "^${CONTAINER_NAME}$"; then
        error "Container failed to start"
        log "Container logs:"
        docker logs "$CONTAINER_NAME"
        exit 1
    fi

    success "Container started successfully: $CONTAINER_NAME"

    # Show container status and resource usage
    log "Container status:"
    docker ps --filter "name=$CONTAINER_NAME" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

    # Show container resource usage (ARM32 optimization check)
    log "Container resource usage:"
    docker stats "$CONTAINER_NAME" --no-stream --format "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}"
}

#############################################################################
# DEPLOYMENT VERIFICATION
#############################################################################

verify_deployment() {
    log "Verifying deployment..."

    # Wait for application to start
    sleep 10

    # Check if container is still running
    if ! docker ps --format "table {{.Names}}" | grep -q "^${CONTAINER_NAME}$"; then
        error "Container is not running"
        return 1
    fi

    # Try to connect to the web endpoint
    local web_url="http://localhost:${HOST_PORT}/"
    log "Testing Web endpoint: $web_url"

    if command -v curl >/dev/null 2>&1; then
        if curl -f -s "$web_url" >/dev/null; then
            success "Web endpoint is responding"
        else
            warning "Web endpoint is not responding yet (this may be normal during startup)"
        fi
    else
        warning "curl not available for endpoint testing"
    fi

    # Show recent logs
    log "Recent container logs:"
    docker logs --tail 20 "$CONTAINER_NAME"
}

#############################################################################
# CLEANUP FUNCTIONS
#############################################################################

cleanup_temp_files() {
    if [[ -d "$LOCAL_REPO_DIR" ]]; then
        log "Cleaning up temporary repository: $LOCAL_REPO_DIR"
        rm -rf "$LOCAL_REPO_DIR"
    fi
}

#############################################################################
# MAIN DEPLOYMENT PROCESS
#############################################################################

print_banner() {
    echo "============================================================================="
    echo "  MediaButler WEB - QNAP ARM32 NAS Deployment Script"
    echo "============================================================================="
    echo "Repository: $GITHUB_REPO"
    echo "Branch: $GIT_BRANCH"
    echo "Image: ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"
    echo "Container: $CONTAINER_NAME"
    echo "Port: $HOST_PORT:$CONTAINER_PORT"
    echo ""
    echo "Volume Mappings:"
    echo "  Nginx Logs: $(echo $NGINX_LOG_VOLUME | cut -d: -f1) → /var/log/nginx"
    echo "  SSL Certs: $(echo $SSL_CERT_VOLUME | cut -d: -f1) → /etc/nginx/ssl"
    echo ""
    echo "Configuration: All parameters integrated in script (no .env file needed)"
    echo "API Base URL: $API_BASE_URL"
    echo "============================================================================="
}

print_summary() {
    echo ""
    echo "============================================================================="
    echo "  DEPLOYMENT COMPLETED SUCCESSFULLY"
    echo "============================================================================="
    echo "Container Name: $CONTAINER_NAME"
    echo "Image: ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"
    echo "Web URL: http://localhost:${HOST_PORT}"
    if [[ "$HOST_HTTPS_PORT" != "$HOST_PORT" ]]; then
        echo "HTTPS URL: https://localhost:${HOST_HTTPS_PORT}"
    fi
    echo "API Connection: $API_BASE_URL"
    echo ""
    echo "Useful commands:"
    echo "  docker logs $CONTAINER_NAME              # View container logs"
    echo "  docker restart $CONTAINER_NAME           # Restart container"
    echo "  docker stop $CONTAINER_NAME              # Stop container"
    echo "  docker stats $CONTAINER_NAME             # View resource usage"
    echo ""
    echo "Nginx Configuration:"
    echo "  Log Volume: $(echo $NGINX_LOG_VOLUME | cut -d: -f1)"
    echo "  SSL Volume: $(echo $SSL_CERT_VOLUME | cut -d: -f1)"
    echo "============================================================================="
}

show_help() {
    cat << EOF
MediaButler WEB Deployment Script for QNAP ARM32 NAS
============================================================================

DESCRIPTION:
    This script performs complete deployment of MediaButler Web
    (Blazor WebAssembly) on QNAP ARM32 NAS with nginx static file serving
    and ARM32 optimization. All configuration parameters are integrated
    in the script with sensible defaults.

USAGE:
    $0 [OPTIONS]

QUICK START:
    # 1. Edit configuration section in the script (lines 30-70)
    # 2. Run deployment
    $0

OPTIONS:
    -h, --help              Show this help message
    -r, --repo URL          Git repository URL
    -b, --branch NAME       Git branch name (default: main)
    -p, --port PORT         Host port for Web (default: 30139)
    --https-port PORT       Host HTTPS port (default: 30443)
    -n, --name NAME         Container name (default: mediabutler_web)
    -i, --image NAME        Docker image name (default: mediabutler_web_image)
    --api-url URL           API base URL (default: http://localhost:30129/)

CONFIGURATION:
    All parameters are configured in the script header (lines 30-70):

    # Main Configuration
    GITHUB_REPO             Git repository URL
    GIT_BRANCH              Git branch to deploy (default: main)
    HOST_PORT               Host port for Web (default: 30139)
    HOST_HTTPS_PORT         Host HTTPS port (default: 30443)
    CONTAINER_NAME          Docker container name (default: mediabutler_web)

    # API Integration
    API_BASE_URL            MediaButler API URL (REQUIRED)
    API_TIMEOUT             API timeout in seconds (default: 30)

    # QNAP Volume Paths
    NGINX_LOG_VOLUME        Nginx log files (default: /share/CACHEDEV2_DATA/...)
    SSL_CERT_VOLUME         SSL certificates (default: /share/CACHEDEV2_DATA/...)

ENVIRONMENT VARIABLE OVERRIDE:
    You can still override any parameter with environment variables:

    export API_BASE_URL="http://your-nas-ip:30129/"
    export HOST_PORT="8080"
    $0

EXAMPLES:
    # Basic deployment (edit script configuration first)
    $0

    # Override specific parameters
    $0 --port 8080 --api-url "http://192.168.1.100:30129/"

    # Different repository/branch
    $0 -r https://github.com/myuser/MediaButler.git -b main

    # Custom container name
    $0 --name my_mediabutler_web

REQUIREMENTS:
    - QNAP NAS ARM32 with Container Station enabled
    - MediaButler API already deployed and running
    - wget/curl (for repository download if Git unavailable)
    - unzip (for ZIP extraction if Git unavailable)
    - Docker available via Container Station

INTEGRATION:
    This Web deployment is designed to work with MediaButler API.
    Make sure to:
    1. Deploy MediaButler API first (using deploy-mediabutler-api.sh)
    2. Configure API_BASE_URL to point to your API container
    3. Ensure both containers can communicate (same Docker network)

EOF
}

#############################################################################
# COMMAND LINE ARGUMENT PARSING
#############################################################################

parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -h|--help)
                show_help
                exit 0
                ;;
            -r|--repo)
                GITHUB_REPO="$2"
                shift 2
                ;;
            -b|--branch)
                GIT_BRANCH="$2"
                shift 2
                ;;
            -p|--port)
                HOST_PORT="$2"
                shift 2
                ;;
            --https-port)
                HOST_HTTPS_PORT="$2"
                shift 2
                ;;
            -n|--name)
                CONTAINER_NAME="$2"
                shift 2
                ;;
            -i|--image)
                DOCKER_IMAGE_NAME="$2"
                shift 2
                ;;
            --api-url)
                API_BASE_URL="$2"
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

#############################################################################
# MAIN EXECUTION
#############################################################################

main() {
    parse_arguments "$@"
    print_banner

    # Pre-deployment checks
    validate_parameters
    check_qnap_environment

    # Deployment process
    cleanup_existing_container
    cleanup_existing_image
    clone_repository
    build_docker_image
    run_docker_container
    verify_deployment

    # Cleanup
    cleanup_temp_files

    print_summary
    success "MediaButler Web deployed successfully on QNAP ARM32 NAS!"
}

# Trap to cleanup on script exit
trap cleanup_temp_files EXIT

# Execute main function with all arguments
main "$@"