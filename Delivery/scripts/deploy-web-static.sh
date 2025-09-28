#!/bin/bash

#############################################################################
# MediaButler WebAssembly Static Deployment Script
# Following the pattern from collabnix.com WebAssembly Docker tutorial
#
# Process:
# 1. Build WebAssembly locally (avoiding Docker build issues)
# 2. Copy static files to simple nginx container
# 3. Serve with nginx (like the tutorial example)
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
# CONFIGURATION
#############################################################################

# Git Repository Configuration
GITHUB_REPO="${GITHUB_REPO:-https://github.com/chim331u/MediaButler.git}"
GIT_BRANCH="${GIT_BRANCH:-delploy}"
LOCAL_REPO_DIR="${LOCAL_REPO_DIR:-/tmp/MediaButler_Web_Static}"

# Docker Configuration
DOCKER_IMAGE_NAME="${DOCKER_IMAGE_NAME:-mediabutler-web-static}"
DOCKER_IMAGE_TAG="${DOCKER_IMAGE_TAG:-latest}"
CONTAINER_NAME="${CONTAINER_NAME:-mediabutler-web-static}"

# Runtime Configuration
HOST_PORT="${HOST_PORT:-3019}"
CONTAINER_PORT="${CONTAINER_PORT:-80}"
API_BASE_URL="${API_BASE_URL:-http://192.168.1.5:30129/}"
API_TIMEOUT="${API_TIMEOUT:-30}"

# Build directories
BUILD_DIR="${BUILD_DIR:-build}"
WWWROOT_DIR="${WWWROOT_DIR:-wwwroot}"

#############################################################################
# HELPER FUNCTIONS
#############################################################################

print_banner() {
    echo "============================================================================="
    echo "  MediaButler WebAssembly - Static File Deployment"
    echo "============================================================================="
    echo "Repository: $GITHUB_REPO"
    echo "Branch: $GIT_BRANCH"
    echo "Image: ${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"
    echo "Container: $CONTAINER_NAME"
    echo "Local URL: http://localhost:${HOST_PORT}"
    echo "API URL: $API_BASE_URL"
    echo "Approach: Build locally + Static Docker serving"
    echo "============================================================================="
}

show_help() {
    cat << EOF
MediaButler WebAssembly Static Deployment Script
============================================================================

DESCRIPTION:
    Builds WebAssembly locally then serves via simple nginx Docker container.
    Follows the pattern from collabnix.com WebAssembly Docker tutorial.

USAGE:
    $0 [OPTIONS]

QUICK START:
    $0 --api-url "http://192.168.1.5:30129/"

OPTIONS:
    -h, --help              Show this help message
    -p, --port PORT         Host port (default: 3019)
    --api-url URL           API base URL
    --clean                 Clean build before starting
    --stop                  Stop and remove container

EOF
}

#############################################################################
# VALIDATION
#############################################################################

validate_environment() {
    log "Validating environment..."

    # Check .NET SDK
    if ! command -v dotnet >/dev/null 2>&1; then
        error ".NET SDK not found. Please install .NET 9 SDK"
        exit 1
    fi

    # Check Docker
    if ! command -v docker >/dev/null 2>&1; then
        error "Docker not found. Please install Docker Desktop"
        exit 1
    fi

    if ! docker info >/dev/null 2>&1; then
        error "Docker is not running. Please start Docker Desktop"
        exit 1
    fi

    # Check Git
    if ! command -v git >/dev/null 2>&1; then
        error "Git not found. Please install Git"
        exit 1
    fi

    success "Environment validation completed"
}

#############################################################################
# SETUP FUNCTIONS
#############################################################################

cleanup_existing() {
    log "Cleaning up existing container and image..."

    # Stop and remove container
    if docker ps -a --format "table {{.Names}}" | grep -q "^${CONTAINER_NAME}$"; then
        log "Stopping existing container: $CONTAINER_NAME"
        docker stop "$CONTAINER_NAME" >/dev/null 2>&1 || true
        docker rm "$CONTAINER_NAME" >/dev/null 2>&1 || true
    fi

    # Remove image if requested
    if [[ "$CLEAN_BUILD" == "true" ]]; then
        local image_full_name="${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"
        if docker images --format "table {{.Repository}}:{{.Tag}}" | grep -q "^${image_full_name}$"; then
            log "Removing existing image: $image_full_name"
            docker rmi "$image_full_name" >/dev/null 2>&1 || true
        fi
    fi

    success "Cleanup completed"
}

clone_repository() {
    log "Setting up repository..."

    if [[ -d "$LOCAL_REPO_DIR" ]]; then
        rm -rf "$LOCAL_REPO_DIR"
    fi

    log "Cloning: $GITHUB_REPO (branch: $GIT_BRANCH)"
    git clone -b "$GIT_BRANCH" "$GITHUB_REPO" "$LOCAL_REPO_DIR"

    if [[ ! -f "$LOCAL_REPO_DIR/src/MediaButler.Web/MediaButler.Web.csproj" ]]; then
        error "MediaButler.Web project not found"
        exit 1
    fi

    success "Repository cloned successfully"
}

generate_appsettings() {
    log "Generating appsettings.json..."

    cd "$LOCAL_REPO_DIR"
    local appsettings_file="src/MediaButler.Web/wwwroot/appsettings.json"
    mkdir -p "$(dirname "$appsettings_file")"

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

    success "Configuration generated"
}

#############################################################################
# BUILD FUNCTIONS
#############################################################################

build_webassembly_locally() {
    log "Building WebAssembly locally (following collabnix.com pattern)..."

    cd "$LOCAL_REPO_DIR"

    # Generate configuration
    generate_appsettings

    # Clean previous build
    if [[ -d "$BUILD_DIR" ]]; then
        rm -rf "$BUILD_DIR"
    fi
    mkdir -p "$BUILD_DIR"

    log "Step 1: Restoring NuGet packages..."
    dotnet restore src/MediaButler.Web/MediaButler.Web.csproj

    log "Step 2: Publishing WebAssembly to local directory..."
    # Use dotnet publish to build WebAssembly files
    dotnet publish src/MediaButler.Web/MediaButler.Web.csproj \
        -c Release \
        -o "$BUILD_DIR" \
        --nologo \
        --verbosity minimal

    # Verify the build output
    if [[ ! -d "$BUILD_DIR/wwwroot" ]]; then
        error "WebAssembly build failed - wwwroot not found"
        exit 1
    fi

    # Copy wwwroot to our staging area
    if [[ -d "$WWWROOT_DIR" ]]; then
        rm -rf "$WWWROOT_DIR"
    fi
    cp -r "$BUILD_DIR/wwwroot" "$WWWROOT_DIR"

    # Show what we built
    local size=$(du -sh "$WWWROOT_DIR" | cut -f1)
    log "WebAssembly build completed - Output size: $size"
    log "Files in wwwroot:"
    ls -la "$WWWROOT_DIR/" | head -10

    success "Local WebAssembly build completed"
}

build_docker_image() {
    log "Building Docker image with static WebAssembly files..."

    cd "$LOCAL_REPO_DIR"

    # Verify we have the wwwroot directory
    if [[ ! -d "$WWWROOT_DIR" ]]; then
        error "wwwroot directory not found. Build WebAssembly first."
        exit 1
    fi

    local image_full_name="${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"
    local dockerfile_path="Delivery/docker/Dockerfile.static"

    if [[ ! -f "$dockerfile_path" ]]; then
        error "Dockerfile not found: $dockerfile_path"
        exit 1
    fi

    log "Building Docker image: $image_full_name"
    log "Using Dockerfile: $dockerfile_path"
    log "WebAssembly files: $WWWROOT_DIR"

    # Build the image (similar to collabnix.com example)
    docker build \
        -f "$dockerfile_path" \
        -t "$image_full_name" \
        .

    # Verify image was created
    if ! docker images --format "table {{.Repository}}:{{.Tag}}" | grep -q "^${image_full_name}$"; then
        error "Docker image build failed"
        exit 1
    fi

    success "Docker image built successfully: $image_full_name"
}

run_container() {
    log "Running container with static WebAssembly files..."

    local image_full_name="${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"

    # Run container (following collabnix.com pattern)
    docker run \
        -d \
        --name "$CONTAINER_NAME" \
        -p "${HOST_PORT}:${CONTAINER_PORT}" \
        "$image_full_name"

    # Verify container started
    sleep 2
    if ! docker ps --format "table {{.Names}}" | grep -q "^${CONTAINER_NAME}$"; then
        error "Container failed to start"
        log "Container logs:"
        docker logs "$CONTAINER_NAME"
        exit 1
    fi

    success "Container started successfully"
    log "WebAssembly app available at: http://localhost:${HOST_PORT}"
}

verify_deployment() {
    log "Verifying deployment..."

    # Wait for nginx to start
    sleep 3

    local web_url="http://localhost:${HOST_PORT}/"
    log "Testing endpoint: $web_url"

    if command -v curl >/dev/null 2>&1; then
        if curl -f -s "$web_url" >/dev/null; then
            success "WebAssembly app is responding"
        else
            warning "App not responding yet (may still be starting)"
        fi
    fi

    log "Container status:"
    docker ps --filter "name=$CONTAINER_NAME" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
}

#############################################################################
# ARGUMENT PARSING
#############################################################################

CLEAN_BUILD=false
STOP_ONLY=false

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
            --clean)
                CLEAN_BUILD=true
                shift
                ;;
            --stop)
                STOP_ONLY=true
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
# CLEANUP
#############################################################################

cleanup() {
    if [[ -d "$LOCAL_REPO_DIR" ]]; then
        log "Cleaning up repository: $LOCAL_REPO_DIR"
        rm -rf "$LOCAL_REPO_DIR"
    fi
}

trap cleanup EXIT

#############################################################################
# MAIN EXECUTION
#############################################################################

main() {
    parse_arguments "$@"

    if [[ "$STOP_ONLY" == "true" ]]; then
        cleanup_existing
        exit 0
    fi

    print_banner

    # Setup and validation
    validate_environment
    cleanup_existing
    clone_repository

    # Build process (following collabnix.com approach)
    build_webassembly_locally
    build_docker_image
    run_container
    verify_deployment

    success "WebAssembly deployment completed!"
    log "Access your application at: http://localhost:${HOST_PORT}"
    log "API connection: $API_BASE_URL"
}

main "$@"