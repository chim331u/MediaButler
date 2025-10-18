#!/bin/bash

#############################################################################
# MediaButler WEB Local Development Deployment Script
# Optimized for local development machine with Docker Desktop
#
# This script performs:
# 1. Local Docker build of Blazor WebAssembly
# 2. Run container locally for development/testing
# 3. Configurable API connection for local or remote API
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
# LOCAL DEVELOPMENT CONFIGURATION
#############################################################################

# Git Repository Configuration
GITHUB_REPO="${GITHUB_REPO:-https://github.com/chim331u/MediaButler.git}"
GIT_BRANCH="${GIT_BRANCH:-delploy}"
LOCAL_REPO_DIR="${LOCAL_REPO_DIR:-/tmp/MediaButler_Web_Local}"

# Docker Configuration
DOCKER_IMAGE_NAME="${DOCKER_IMAGE_NAME:-mediabutler-web-local}"
DOCKER_IMAGE_TAG="${DOCKER_IMAGE_TAG:-dev}"
CONTAINER_NAME="${CONTAINER_NAME:-mediabutler-web-local}"

# Docker Build Configuration
DOCKERFILE_PATH="${DOCKERFILE_PATH:-Delivery/docker/Dockerfile.local}"
BUILD_CONTEXT="${BUILD_CONTEXT:-.}"

# Local Build Configuration
USE_LOCAL_BUILD="${USE_LOCAL_BUILD:-true}"
DIST_DIR="${DIST_DIR:-dist}"

# Container Runtime Configuration
HOST_PORT="${HOST_PORT:-5109}"
CONTAINER_PORT="${CONTAINER_PORT:-8080}"

# API Configuration - IMPORTANT: Configure this for your setup
API_BASE_URL="${API_BASE_URL:-http://192.168.1.5:30129/}"
API_TIMEOUT="${API_TIMEOUT:-30}"

# Development settings
ENABLE_HOT_RELOAD="${ENABLE_HOT_RELOAD:-false}"
DOCKER_PLATFORM="${DOCKER_PLATFORM:-linux/arm64}"  # Apple Silicon default

#############################################################################
# HELPER FUNCTIONS
#############################################################################

print_banner() {
    echo "============================================================================="
    echo "  MediaButler WEB - Local Development Deployment (macOS)"
    echo "============================================================================="
    echo "Repository: $GITHUB_REPO"
    echo "Branch: $GIT_BRANCH"
    echo "Local Clone: $LOCAL_REPO_DIR"
    echo "Image: ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"
    echo "Container: $CONTAINER_NAME"
    echo "Local URL: http://localhost:${HOST_PORT}"
    echo "API URL: $API_BASE_URL"
    echo "Platform: $DOCKER_PLATFORM"
    echo "============================================================================="
}

show_help() {
    cat << EOF
MediaButler WEB Local Development Deployment Script
============================================================================

DESCRIPTION:
    Deploys MediaButler Blazor WebAssembly locally using Docker for
    development and testing purposes.

USAGE:
    $0 [OPTIONS]

QUICK START:
    # Deploy with default settings (API at host.docker.internal:5000)
    $0

    # Deploy with custom API URL
    $0 --api-url "http://localhost:30129/"

    # Deploy on different port
    $0 --port 3000

OPTIONS:
    -h, --help              Show this help message
    -p, --port PORT         Host port for Web UI (default: 3019)
    --api-url URL           API base URL (default: http://your-server-ip:30129/)
    --platform PLATFORM    Docker platform (default: linux/arm64 for Apple Silicon)
    -n, --name NAME         Container name (default: mediabutler-web-local)
    -i, --image NAME        Docker image name (default: mediabutler-web-local)
    -r, --repo URL          Git repository URL (default: https://github.com/chim331u/MediaButler.git)
    -b, --branch NAME       Git branch name (default: delploy)
    --clean                 Remove existing container and image before building

CONFIGURATION:
    # Main Configuration
    HOST_PORT               Local port for Web UI (default: 8080)
    API_BASE_URL            MediaButler API URL (REQUIRED)
    DOCKER_PLATFORM         Docker platform (default: linux/amd64)

EXAMPLES:
    # Basic local deployment
    $0

    # Custom port and API URL
    $0 --port 3000 --api-url "http://localhost:5000/"

    # Clean deployment (remove existing containers)
    $0 --clean

    # Different platform (for Apple Silicon Macs)
    $0 --platform linux/arm64

REQUIREMENTS:
    - Docker Desktop installed and running
    - .NET 8+ SDK (project currently uses .NET 9)
    - MediaButler API running (locally or remotely)
    - Current directory should be MediaButler project root

NOTES:
    - Uses host.docker.internal to connect to host machine services
    - For local API, make sure it's accessible from Docker containers
    - Web UI will be available at http://localhost:PORT

EOF
}

#############################################################################
# VALIDATION FUNCTIONS
#############################################################################

validate_environment() {
    log "Validating local development environment..."

    # Check if git is available for cloning
    if ! command -v git >/dev/null 2>&1; then
        error "Git not found. Please install Git or Xcode Command Line Tools."
        exit 1
    fi

    # Check if .NET SDK is available for local builds
    if [[ "$USE_LOCAL_BUILD" == "true" ]]; then
        if ! command -v dotnet >/dev/null 2>&1; then
            error ".NET SDK not found. Please install .NET 8+ SDK or set USE_LOCAL_BUILD=false"
            exit 1
        fi

        # Check .NET version
        DOTNET_VERSION=$(dotnet --version 2>/dev/null | cut -d'.' -f1)
        if [[ "$DOTNET_VERSION" -lt 8 ]]; then
            error ".NET version $DOTNET_VERSION detected. .NET 8+ required for building WebAssembly projects"
            exit 1
        elif [[ "$DOTNET_VERSION" -eq 8 ]]; then
            log ".NET $DOTNET_VERSION detected - WebAssembly supported"
        elif [[ "$DOTNET_VERSION" -ge 9 ]]; then
            log ".NET $DOTNET_VERSION detected - Full WebAssembly features supported"
        fi
    fi

    # Check Docker
    if ! command -v docker >/dev/null 2>&1; then
        error "Docker not found. Please install Docker Desktop."
        exit 1
    fi

    # Check if Docker is running
    if ! docker info >/dev/null 2>&1; then
        error "Docker is not running. Please start Docker Desktop."
        exit 1
    fi

    # Validate repository URL
    if [[ -z "$GITHUB_REPO" ]]; then
        error "GITHUB_REPO is required"
        exit 1
    fi

    if [[ ! "$GITHUB_REPO" =~ ^https?://github\.com/.+\.git$ ]]; then
        warning "GITHUB_REPO format may be invalid: $GITHUB_REPO"
        warning "Expected format: https://github.com/user/repository.git"
    fi

    # Validate API URL format
    if [[ ! "$API_BASE_URL" =~ ^https?:// ]]; then
        error "API_BASE_URL must be a valid HTTP/HTTPS URL: $API_BASE_URL"
        exit 1
    fi

    # Validate port
    if ! [[ "$HOST_PORT" =~ ^[0-9]+$ ]] || [[ "$HOST_PORT" -lt 1 || "$HOST_PORT" -gt 65535 ]]; then
        error "HOST_PORT must be a valid port number (1-65535): $HOST_PORT"
        exit 1
    fi

    success "Environment validation completed"
}

#############################################################################
# GIT OPERATIONS
#############################################################################

clone_repository() {
    log "Cloning repository: $GITHUB_REPO (branch: $GIT_BRANCH)"

    # Remove existing local repository if exists
    if [[ -d "$LOCAL_REPO_DIR" ]]; then
        log "Removing existing local repository: $LOCAL_REPO_DIR"
        rm -rf "$LOCAL_REPO_DIR"
    fi

    # Clone repository with specific branch
    if [[ "$GIT_BRANCH" == "main" ]] || [[ "$GIT_BRANCH" == "master" ]]; then
        log "Cloning default branch: git clone \"$GITHUB_REPO\" \"$LOCAL_REPO_DIR\""
        git clone "$GITHUB_REPO" "$LOCAL_REPO_DIR"
    else
        log "Cloning specific branch: git clone -b \"$GIT_BRANCH\" \"$GITHUB_REPO\" \"$LOCAL_REPO_DIR\""
        git clone -b "$GIT_BRANCH" "$GITHUB_REPO" "$LOCAL_REPO_DIR"
    fi

    # Verify download success
    if [[ ! -d "$LOCAL_REPO_DIR" ]]; then
        error "Repository clone failed - directory not found: $LOCAL_REPO_DIR"
        exit 1
    fi

    # Change to repository directory
    cd "$LOCAL_REPO_DIR"

    # Verify MediaButler Web project exists
    if [[ ! -f "src/MediaButler.Web/MediaButler.Web.csproj" ]]; then
        error "MediaButler.Web project not found in cloned repository"
        exit 1
    fi

    # Check Dockerfile exists
    if [[ ! -f "$DOCKERFILE_PATH" ]]; then
        # Try fallback Dockerfiles
        if [[ -f "Delivery/docker/Dockerfile.local" ]]; then
            DOCKERFILE_PATH="Delivery/docker/Dockerfile.local"
            log "Using fallback Dockerfile: $DOCKERFILE_PATH"
        elif [[ -f "Delivery/docker/Dockerfile.webassembly" ]]; then
            DOCKERFILE_PATH="Delivery/docker/Dockerfile.webassembly"
            warning "Using WebAssembly Dockerfile (may fail on macOS): $DOCKERFILE_PATH"
        else
            error "No suitable Dockerfile found"
            exit 1
        fi
    fi

    success "Repository cloned successfully"

    # Show current info
    CURRENT_COMMIT=$(git rev-parse --short HEAD 2>/dev/null)
    CURRENT_BRANCH=$(git branch --show-current 2>/dev/null)
    log "Current commit: $CURRENT_COMMIT on branch: $CURRENT_BRANCH"
}

#############################################################################
# LOCAL BUILD OPERATIONS
#############################################################################

build_locally() {
    log "Building Blazor WebAssembly project locally..."

    # Change to repository directory
    cd "$LOCAL_REPO_DIR"

    # Generate application configuration first
    generate_appsettings

    # Clean previous build output
    if [[ -d "$DIST_DIR" ]]; then
        log "Cleaning previous build output: $DIST_DIR"
        rm -rf "$DIST_DIR"
    fi

    # Create dist directory
    mkdir -p "$DIST_DIR"

    log "Building project with .NET SDK..."
    log "Project: src/MediaButler.Web/MediaButler.Web.csproj"

    # First, try to restore packages explicitly
    log "Restoring NuGet packages..."
    if ! dotnet restore src/MediaButler.Web/MediaButler.Web.csproj; then
        error "Package restore failed"
        exit 1
    fi

    # Build the project locally with explicit WebAssembly settings for .NET 9
    log "Building with .NET 9 WebAssembly configuration..."

    # Build with .NET 9 WebAssembly settings
    if ! dotnet publish src/MediaButler.Web/MediaButler.Web.csproj \
        --configuration Release \
        --output "$DIST_DIR" \
        --verbosity normal \
        --no-restore; then

        error "Primary .NET 9 build failed"
        log "Attempting fallback build with minimal WebAssembly features..."

        # Fallback: try with disabled features (for compatibility)
        if ! dotnet publish src/MediaButler.Web/MediaButler.Web.csproj \
            --configuration Release \
            --output "$DIST_DIR" \
            --verbosity normal \
            --no-restore \
            /p:RunAOTCompilation=false \
            /p:WasmEnableWebcil=false; then

            error "Local .NET build failed with fallback settings"
            exit 1
        fi
    fi

    # Verify build output
    if [[ ! -d "$DIST_DIR/wwwroot" ]]; then
        error "Build completed but wwwroot directory not found in $DIST_DIR"
        log "Build output contents:"
        ls -la "$DIST_DIR"
        exit 1
    fi

    # Show build output info
    WWWROOT_SIZE=$(du -sh "$DIST_DIR/wwwroot" | cut -f1)
    log "Build completed successfully"
    log "WebAssembly output size: $WWWROOT_SIZE"
    log "Output location: $DIST_DIR/wwwroot"

    # Debug: Show the structure of the build output
    log "Build output structure:"
    ls -la "$DIST_DIR/wwwroot/" | head -10
    if [[ -d "$DIST_DIR/wwwroot/_framework" ]]; then
        log "_framework directory contents:"
        ls -la "$DIST_DIR/wwwroot/_framework/" | head -10

        # Check for any files with fingerprint patterns
        log "Checking for files with fingerprint patterns:"
        find "$DIST_DIR/wwwroot/_framework" -name "*fingerprint*" -o -name "*#*" | head -5
    fi

    # Check and fix index.html for WebAssembly fingerprinting
    if [[ -f "$DIST_DIR/wwwroot/index.html" ]]; then
        log "Checking index.html for template patterns:"
        if grep -n "fingerprint\|#\[" "$DIST_DIR/wwwroot/index.html" >/dev/null 2>&1; then
            warning "Found unprocessed template patterns in index.html:"
            grep -n "fingerprint\|#\[" "$DIST_DIR/wwwroot/index.html" | head -3
        else
            success "No unprocessed template patterns found in index.html"
        fi

        # Fix blazor.webassembly.js reference with fingerprinted version
        if grep -q "blazor.webassembly.js" "$DIST_DIR/wwwroot/index.html"; then
            # Find the actual fingerprinted blazor file
            local blazor_file=$(find "$DIST_DIR/wwwroot/_framework" -name "blazor.webassembly.*.js" -not -name "*.gz" -not -name "*.br" | head -1)
            if [[ -n "$blazor_file" ]]; then
                local blazor_filename=$(basename "$blazor_file")
                log "Updating index.html to reference fingerprinted blazor file: $blazor_filename"
                sed -i.bak "s|blazor.webassembly.js|$blazor_filename|g" "$DIST_DIR/wwwroot/index.html"
                rm -f "$DIST_DIR/wwwroot/index.html.bak"
                success "Updated blazor.webassembly.js reference to $blazor_filename"
            else
                warning "Could not find fingerprinted blazor.webassembly file"
            fi
        else
            log "blazor.webassembly.js reference not found (may already be fingerprinted)"
        fi

        log "First 20 lines of index.html:"
        head -20 "$DIST_DIR/wwwroot/index.html"
    fi

    success "Local build completed successfully"
}

#############################################################################
# DOCKER OPERATIONS
#############################################################################

cleanup_existing() {
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

generate_appsettings() {
    log "Generating appsettings.json for local development..."

    local appsettings_file="src/MediaButler.Web/wwwroot/appsettings.json"

    # Create directory if it doesn't exist
    mkdir -p "$(dirname "$appsettings_file")"

    # Create development configuration
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

build_docker_image() {
    log "Building Docker image with pre-built WebAssembly files..."

    # Change to repository directory
    cd "$LOCAL_REPO_DIR"

    local image_full_name="${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"

    # Verify pre-built files exist
    if [[ ! -d "$DIST_DIR/wwwroot" ]]; then
        error "Pre-built files not found at $DIST_DIR/wwwroot. Run local build first."
        exit 1
    fi

    log "Building image: $image_full_name"
    log "Dockerfile: $DOCKERFILE_PATH"
    log "Build context: $BUILD_CONTEXT"
    log "Platform: $DOCKER_PLATFORM"
    log "Using pre-built files from: $DIST_DIR/wwwroot"

    # Build Docker image with platform specification
    if ! docker build \
        --platform "$DOCKER_PLATFORM" \
        -f "$DOCKERFILE_PATH" \
        -t "$image_full_name" \
        "$BUILD_CONTEXT"; then

        error "Docker build failed"
        exit 1
    fi

    # Verify image was created
    if ! docker images --format "table {{.Repository}}:{{.Tag}}" | grep -q "^${image_full_name}$"; then
        error "Docker image build failed"
        exit 1
    fi

    success "Docker image built successfully: $image_full_name"

    # Show image size
    IMAGE_SIZE=$(docker images --format "table {{.Size}}" "$image_full_name" | tail -n +2)
    log "Image size: $IMAGE_SIZE"
}

run_container() {
    log "Starting Docker container: $CONTAINER_NAME"

    local image_full_name="${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"

    # Run container
    docker run \
        --restart unless-stopped \
        --name "$CONTAINER_NAME" \
        -d \
        -p "${HOST_PORT}:${CONTAINER_PORT}" \
        --platform "$DOCKER_PLATFORM" \
        "$image_full_name"

    # Verify container is running
    sleep 3
    if ! docker ps --format "table {{.Names}}" | grep -q "^${CONTAINER_NAME}$"; then
        error "Container failed to start"
        log "Container logs:"
        docker logs "$CONTAINER_NAME"
        exit 1
    fi

    success "Container started successfully: $CONTAINER_NAME"

    # Show container status
    log "Container status:"
    docker ps --filter "name=$CONTAINER_NAME" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
}

verify_deployment() {
    log "Verifying deployment..."

    # Wait for application to start
    sleep 5

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
    docker logs --tail 10 "$CONTAINER_NAME"
}

print_summary() {
    echo ""
    echo "============================================================================="
    echo "  LOCAL DEPLOYMENT COMPLETED SUCCESSFULLY"
    echo "============================================================================="
    echo "Container Name: $CONTAINER_NAME"
    echo "Image: ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"
    echo "Web URL: http://localhost:${HOST_PORT}"
    echo "API Connection: $API_BASE_URL"
    echo ""
    echo "Useful commands:"
    echo "  docker logs $CONTAINER_NAME              # View container logs"
    echo "  docker logs -f $CONTAINER_NAME           # Follow container logs"
    echo "  docker restart $CONTAINER_NAME           # Restart container"
    echo "  docker stop $CONTAINER_NAME              # Stop container"
    echo "  docker rm $CONTAINER_NAME                # Remove container"
    echo ""
    echo "Development notes:"
    echo "  - Web UI available at: http://localhost:${HOST_PORT}"
    echo "  - API connection: $API_BASE_URL"
    echo "  - Container platform: $DOCKER_PLATFORM"
    echo "============================================================================="
}

#############################################################################
# COMMAND LINE ARGUMENT PARSING
#############################################################################

CLEAN_DEPLOYMENT=false

parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -h|--help)
                show_help
                exit 0
                ;;
            -p|--port)
                HOST_PORT="$2"
                shift 2
                ;;
            --api-url)
                API_BASE_URL="$2"
                shift 2
                ;;
            --platform)
                DOCKER_PLATFORM="$2"
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
            -r|--repo)
                GITHUB_REPO="$2"
                shift 2
                ;;
            -b|--branch)
                GIT_BRANCH="$2"
                shift 2
                ;;
            --clean)
                CLEAN_DEPLOYMENT=true
                shift
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

    # Validation
    validate_environment

    # Repository operations
    clone_repository

    # Local build operations
    if [[ "$USE_LOCAL_BUILD" == "true" ]]; then
        build_locally
    fi

    # Deployment process
    cleanup_existing

    if [[ "$CLEAN_DEPLOYMENT" == "true" ]]; then
        cleanup_existing_image
    fi

    build_docker_image
    run_container
    verify_deployment

    print_summary
    success "MediaButler Web deployed successfully for local development!"
}

# Cleanup function for temporary files
cleanup_temp_files() {
    if [[ -d "$LOCAL_REPO_DIR" ]]; then
        log "Cleaning up temporary repository: $LOCAL_REPO_DIR"
        rm -rf "$LOCAL_REPO_DIR"
    fi
}

# Trap to cleanup on script exit
trap cleanup_temp_files EXIT

# Execute main function with all arguments
main "$@"