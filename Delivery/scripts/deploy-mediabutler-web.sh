#!/usr/bin/env bash

# ==============================================================================
# Script: deploy-mediabutler-web.sh
# Description: Highly optimized, interactive, and resilient deployment script for
#              MediaButler Web (Blazor WebAssembly + Nginx). Tailored for low-memory
#              NAS (QNAP/Synology) and macOS.
# ==============================================================================

set -euo pipefail

# --- Colors for Output ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

log_info() { echo -e "${BLUE}[$(date +'%Y-%m-%d %H:%M:%S')] [INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[$(date +'%Y-%m-%d %H:%M:%S')] [SUCCESS]${NC} $1"; }
log_warning() { echo -e "${YELLOW}[$(date +'%Y-%m-%d %H:%M:%S')] [WARNING]${NC} $1"; }
log_error() { echo -e "${RED}[$(date +'%Y-%m-%d %H:%M:%S')] [ERROR]${NC} $1" >&2; }

detect_qnap_volume_root() {
    if [ -d "/share/CACHEDEV1_DATA" ]; then
        echo "/share/CACHEDEV1_DATA"
    elif [ -d "/share/CACHEDEV2_DATA" ]; then
        echo "/share/CACHEDEV2_DATA"
    else
        echo "/share/CACHEDEV1_DATA"
    fi
}

# --- Platform Detection ---
detect_platform() {
    local arch
    arch=$(uname -m)
    local os
    os=$(uname -s)

    if [[ "$os" == "Darwin" && "$arch" == "arm64" ]]; then
        echo "mac_arm64"
    elif [[ "$arch" == "armv7l" || "$arch" == "aarch64" ]]; then
        echo "qnap_arm"
    else
        echo "unknown"
    fi
}

# --- Config Variables ---
GITHUB_REPO="https://github.com/chim331u/MediaButler.git"
GIT_BRANCH="main"
LOCAL_REPO_DIR="/tmp/MediaButler_Web"

DOCKER_IMAGE_NAME="mediabutler_web_image"
DOCKER_IMAGE_TAG="latest"
CONTAINER_NAME="mediabutler_web"
DOCKERFILE_PATH="Delivery/docker/Dockerfile.webassembly"
BUILD_CONTEXT="."
HOST_PORT="30139"
API_BASE_URL="http://localhost:30129/"

# Default volume paths and resource settings (will be configured below)
LOGS_VOLUME=""
DOCKER_PLATFORM="linux/arm/v7"

# --- Main Configuration & Platform Setup ---
configure_platform() {
    local platform
    platform=$(detect_platform)
    
    echo "============================================================================="
    echo "  MediaButler WEB - Platform Specific Deployment"
    echo "============================================================================="
    
    if [ "$platform" = "mac_arm64" ]; then
        log_info "Detected Platform: MacBook ARM64 (Apple Silicon)"
        echo "1) MacBook ARM64 (Local Development)"
        echo "2) QNAP NAS (ARM32/ARM64 - Production)"
        read -p "Select Target Platform [1]: " -r choice
        choice="${choice:-1}"
    else
        log_info "Detected Platform: NAS / Embedded Device"
        echo "1) QNAP/Synology NAS (ARM32/ARM64 - Production)"
        echo "2) MacBook ARM64 (Local Development)"
        read -p "Select Target Platform [1]: " -r choice
        choice="${choice:-1}"
        # Swap values based on default selection for NAS
        if [ "$choice" = "1" ]; then choice="2"; else choice="1"; fi
    fi

    if [ "$choice" = "2" ] || [ "$platform" = "qnap_arm" ]; then
        # NAS Production Configuration
        local qnap_root
        qnap_root=$(detect_qnap_volume_root)
        log_info "Configuring for NAS Production deployment (Root: ${qnap_root})...."
        
        LOGS_VOLUME="${qnap_root}/Docker/mediabutler_web/logs:/var/log/nginx"
        DOCKER_PLATFORM="linux/arm/v7"
    else
        # Mac Local Development Configuration
        log_info "Configuring for Local macOS development..."
        
        mkdir -p "$HOME/mediabutler_web/logs"
        LOGS_VOLUME="$HOME/mediabutler_web/logs:/var/log/nginx"
        DOCKER_PLATFORM="linux/arm64"
    fi
}

interactive_prompts() {
    echo ""
    echo "============================================================================="
    echo "  Web Application & Backend Configuration"
    echo "============================================================================="
    
    # Prompt for Git Config
    read -p "Git Repository URL [$GITHUB_REPO]: " -r input_repo
    GITHUB_REPO="${input_repo:-$GITHUB_REPO}"
    read -p "Git Branch [$GIT_BRANCH]: " -r input_branch
    GIT_BRANCH="${input_branch:-$GIT_BRANCH}"

    # Prompt for Network Ports
    read -p "Host Port [$HOST_PORT]: " -r input_port
    HOST_PORT="${input_port:-$HOST_PORT}"

    # Prompt for API Base URL
    echo ""
    log_info "Configure the MediaButler API Base URL. This is the URL the browser will query."
    read -p "API Base URL [$API_BASE_URL]: " -r input_api_url
    API_BASE_URL="${input_api_url:-$API_BASE_URL}"
    
    # Ensure trailing slash for API URL
    if [[ ! "$API_BASE_URL" =~ /$ ]]; then
        API_BASE_URL="${API_BASE_URL}/"
    fi
    log_info "Using API Base URL: $API_BASE_URL"
    echo ""
}

# --- Repository Fetching with Git-less fallback ---
fetch_repository() {
    log_info "Fetching codebase..."
    rm -rf "$LOCAL_REPO_DIR"

    if command -v git >/dev/null 2>&1; then
        log_info "Git client detected. Cloning repository..."
        git clone -b "$GIT_BRANCH" "$GITHUB_REPO" "$LOCAL_REPO_DIR"
    else
        log_warning "Git client NOT detected. Falling back to ZIP download..."
        if ! command -v unzip >/dev/null 2>&1; then
            log_error "unzip command is missing. Cannot extract repository. Install unzip or git."
            exit 1
        fi
        
        local clean_url
        clean_url=$(echo "$GITHUB_REPO" | sed 's/\.git$//')
        local zip_url="${clean_url}/archive/refs/heads/${GIT_BRANCH}.zip"
        local zip_file="/tmp/mediabutler_web.zip"
        
        log_info "Downloading ZIP from: $zip_url"
        if command -v wget >/dev/null 2>&1; then
            wget -q -O "$zip_file" "$zip_url"
        elif command -v curl >/dev/null 2>&1; then
            curl -s -L -o "$zip_file" "$zip_url"
        else
            log_error "Neither wget nor curl detected. Cannot download repository."
            exit 1
        fi
        
        log_info "Extracting codebase..."
        unzip -q "$zip_file" -d "/tmp"
        local folder_name
        folder_name=$(basename "$clean_url")
        mv "/tmp/${folder_name}-${GIT_BRANCH}" "$LOCAL_REPO_DIR"
        rm -f "$zip_file"
    fi
    
    log_success "Codebase successfully downloaded to $LOCAL_REPO_DIR"
}

# --- Docker Operations ---
cleanup_infrastructure() {
    log_info "Performing environment cleanup and self-healing..."
    
    if docker ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
        log_warning "Container '$CONTAINER_NAME' already exists. Stopping and removing..."
        docker stop "$CONTAINER_NAME" || true
        docker rm -f "$CONTAINER_NAME" || true
    fi

    local full_image="${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"
    if docker images --format '{{.Repository}}:{{.Tag}}' | grep -q "^${full_image}$"; then
        log_warning "Image '$full_image' already exists. Deleting to release disk space..."
        docker rmi -f "$full_image" || true
    fi
}

build_image() {
    log_info "Building Web Docker image with multi-stage platform optimizations..."
    cd "$LOCAL_REPO_DIR"

    if [ ! -f "$DOCKERFILE_PATH" ]; then
        log_error "Dockerfile not found at $DOCKERFILE_PATH"
        exit 1
    fi

    # Execute build with platform specifications for the final stage
    # Compiler stage uses linux/amd64 explicitly inside the Dockerfile
    docker build \
        --platform "$DOCKER_PLATFORM" \
        -f "$DOCKERFILE_PATH" \
        -t "${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}" \
        "$BUILD_CONTEXT"

    log_success "Image built successfully!"
    local size
    size=$(docker images --format '{{.Size}}' "${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}" | head -n 1)
    log_info "Optimized Image Size: $size"
}

run_container() {
    log_info "Spawning optimized Web container (Non-Root on port 8080)..."
    
    # Resolve host logs folder from volumes for folder creation
    local host_logs
    host_logs=$(echo "$LOGS_VOLUME" | cut -d':' -f1)

    mkdir -p "$host_logs"
    # Ensure logs folder has permissions for non-root user (UID 1000)
    chmod -R 777 "$host_logs" || true

    # Start docker container
    docker run \
        --restart always \
        --name "$CONTAINER_NAME" \
        -d \
        -p "${HOST_PORT}:8080" \
        -v "$LOGS_VOLUME" \
        -e "API_BASE_URL=$API_BASE_URL" \
        --platform "$DOCKER_PLATFORM" \
        "${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"

    log_success "Web Container successfully spawned!"
}

# --- Post Deployment Verification & Statistics ---
verify_deploy() {
    log_info "Initiating deployment verification (waiting 8 seconds)..."
    sleep 8

    # Verify if container is up
    if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
        log_error "Container failed to start! Dumping last logs below:"
        docker logs "$CONTAINER_NAME"
        exit 1
    fi

    # Try health check
    local check_url="http://localhost:${HOST_PORT}/health"
    log_info "Running local Health Check on $check_url..."
    if command -v curl >/dev/null 2>&1; then
        if curl -s -f "$check_url" >/dev/null 2>&1; then
            log_success "Web Health Check: Connected successfully!"
        else
            log_warning "Web Health Check: Endpoint did not respond with healthy. It might still be starting."
        fi
    else
        log_warning "curl is not available. Skipping Web health check verification."
    fi

    echo ""
    echo "============================================================================="
    echo "  CONTAINER STATUS & MEMORY FOOTPRINT"
    echo "============================================================================="
    docker ps --filter "name=$CONTAINER_NAME" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
    echo ""
    docker stats "$CONTAINER_NAME" --no-stream
    echo ""
    log_info "Displaying last 20 lines of container logs:"
    echo "-----------------------------------------------------------------------------"
    docker logs --tail 20 "$CONTAINER_NAME"
    echo "-----------------------------------------------------------------------------"
}

cleanup_temp() {
    log_info "Cleaning up temporary build folder..."
    rm -rf "$LOCAL_REPO_DIR" || true
}

# --- Main Flow ---
main() {
    configure_platform
    interactive_prompts
    cleanup_infrastructure
    fetch_repository
    build_image
    run_container
    verify_deploy
}

# Trap to ensure cleanup occurs on exit
trap cleanup_temp EXIT
main "$@"