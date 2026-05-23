#!/usr/bin/env bash

# ==============================================================================
# Script: deploy-mediabutler-api.sh
# Description: Highly optimized, interactive, and resilient deployment script for
#              MediaButler API. Tailored for low-memory NAS (QNAP/Synology) and macOS.
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

# --- Security Fallback Generators ---
generate_api_key() {
    if command -v openssl >/dev/null 2>&1; then
        openssl rand -hex 16
    else
        echo -n "$(date +%s%N)${RANDOM}" | md5sum | cut -d' ' -f1 | head -c 32
    fi
}

generate_jwt_secret() {
    if command -v openssl >/dev/null 2>&1; then
        openssl rand -base64 32
    else
        echo -n "$(date +%s%N)${RANDOM}${RANDOM}" | base64 | head -c 44
    fi
}

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
LOCAL_REPO_DIR="/tmp/MediaButler_API"

DOCKER_IMAGE_NAME="mediabutler_api_image"
DOCKER_IMAGE_TAG="latest"
CONTAINER_NAME="mediabutler_api"
DOCKERFILE_PATH="Delivery/docker/api-minimal.dockerfile"
BUILD_CONTEXT="."
HOST_PORT="30129"

# Default volume paths and resource settings (will be configured below)
DATA_VOLUME=""
WATCH_VOLUME=""
LIBRARY_VOLUME=""
LOGS_VOLUME=""

MAX_BATCH_SIZE="10"
SCAN_INTERVAL_MINUTES="5"
MEMORY_THRESHOLD_MB="140"
AUTO_GC_TRIGGER_MB="110"
DOCKER_PLATFORM="linux/arm/v7"
ASPNETCORE_ENVIRONMENT="Production"
LOG_LEVEL="Information"

WATCHFOLDER_PATH="/watch"
LIBRARY_PATH="/library"
DATABASE_PATH="/data/mediabutler.db"

# --- Main Configuration & Platform Setup ---
configure_platform() {
    local platform
    platform=$(detect_platform)
    
    echo "============================================================================="
    echo "  MediaButler API - Platform Specific Deployment"
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
        log_info "Configuring for NAS Production deployment (Root: ${qnap_root})..."
        
        DATA_VOLUME="${qnap_root}/Docker/mediabutler:/data"
        WATCH_VOLUME="/share/Download/Incoming:/watch"
        LIBRARY_VOLUME="/share/Video/Serie:/library"
        LOGS_VOLUME="${qnap_root}/Docker/mediabutler/logs:/app/logs"
        
        DOCKER_PLATFORM="linux/arm/v7"
        MAX_BATCH_SIZE="10"
        SCAN_INTERVAL_MINUTES="5"
        MEMORY_THRESHOLD_MB="140"
        AUTO_GC_TRIGGER_MB="110"
        ASPNETCORE_ENVIRONMENT="Production"
    else
        # Mac Local Development Configuration
        log_info "Configuring for Local macOS development..."
        
        mkdir -p "$HOME/mediabutler/data" "$HOME/mediabutler/watch" "$HOME/mediabutler/library" "$HOME/mediabutler/logs"
        DATA_VOLUME="$HOME/mediabutler/data:/data"
        WATCH_VOLUME="$HOME/mediabutler/watch:/watch"
        LIBRARY_VOLUME="$HOME/mediabutler/library:/library"
        LOGS_VOLUME="$HOME/mediabutler/logs:/app/logs"
        
        DOCKER_PLATFORM="linux/arm64"
        MAX_BATCH_SIZE="50"
        SCAN_INTERVAL_MINUTES="2"
        MEMORY_THRESHOLD_MB="1000"
        AUTO_GC_TRIGGER_MB="800"
        ASPNETCORE_ENVIRONMENT="Development"
    fi
}

interactive_prompts() {
    echo ""
    echo "============================================================================="
    echo "  Security & Configuration Prompts"
    echo "============================================================================="
    
    # Prompt for Git Config
    read -p "Git Repository URL [$GITHUB_REPO]: " -r input_repo
    GITHUB_REPO="${input_repo:-$GITHUB_REPO}"
    read -p "Git Branch [$GIT_BRANCH]: " -r input_branch
    GIT_BRANCH="${input_branch:-$GIT_BRANCH}"

    # Prompt for Network Ports
    read -p "Host Port [$HOST_PORT]: " -r input_port
    HOST_PORT="${input_port:-$HOST_PORT}"

    # Guided Security setup
    echo ""
    log_info "Setting up Security Credentials. Leave empty to use local development defaults."
    
    local default_key="mb-local-dev-key-8a9b2c"
    read -p "API Key (Security:ApiKey) [$default_key]: " -r input_api_key
    API_KEY="${input_api_key:-$default_key}"
    log_info "Using API Key: $API_KEY"

    read -p "JWT Secret (Security:JwtSecret) [Auto-Generate]: " -r input_jwt_secret
    if [ -z "$input_jwt_secret" ]; then
        JWT_SECRET=$(generate_jwt_secret)
        log_warning "Auto-generated secure JWT Secret: $JWT_SECRET"
    else
        JWT_SECRET="$input_jwt_secret"
    fi
    echo ""
}

# --- Repository Fetching with Git-less fallback ---
fetch_repository() {
    log_info "Fetching codebase..."
    rm -rf "$LOCAL_REPO_DIR"

    # Check if we are running from a local working directory
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    local local_root
    local_root="$(cd "${script_dir}/../.." && pwd)"

    if [ -f "${local_root}/MediaButler.sln" ]; then
        log_info "Detected local working directory at ${local_root}. Copying local files..."
        mkdir -p "$LOCAL_REPO_DIR"
        # Copy files excluding temporary and build artifacts
        if command -v rsync >/dev/null 2>&1; then
            rsync -a --exclude='bin/' --exclude='obj/' --exclude='.git/' --exclude='temp/' --exclude='logs/' --exclude='data/' --exclude='Delivery/logs/' --exclude='Delivery/data/' "${local_root}/" "$LOCAL_REPO_DIR/"
        else
            cp -R "${local_root}/"* "$LOCAL_REPO_DIR/"
            rm -rf "$LOCAL_REPO_DIR/bin" "$LOCAL_REPO_DIR/obj" "$LOCAL_REPO_DIR/.git" "$LOCAL_REPO_DIR/temp" "$LOCAL_REPO_DIR/logs" "$LOCAL_REPO_DIR/data"
        fi
    elif command -v git >/dev/null 2>&1; then
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
        local zip_file="/tmp/mediabutler_api.zip"
        
        log_info "Downloading ZIP from: $zip_url"
        local download_success=0
        if command -v wget >/dev/null 2>&1; then
            log_info "Using wget..."
            if wget --no-check-certificate -q -O "$zip_file" "$zip_url"; then
                download_success=1
            else
                log_warning "wget failed. Retrying with verbose output..."
                if wget --no-check-certificate -O "$zip_file" "$zip_url"; then
                    download_success=1
                fi
            fi
        elif command -v curl >/dev/null 2>&1; then
            log_info "Using curl..."
            if curl -k -s -L -o "$zip_file" "$zip_url"; then
                download_success=1
            else
                log_warning "curl failed. Retrying with verbose output..."
                if curl -k -L -o "$zip_file" "$zip_url"; then
                    download_success=1
                fi
            fi
        fi

        if [ "$download_success" -ne 1 ]; then
            log_error "Failed to download codebase ZIP from $zip_url."
            log_info "Please ensure the NAS has internet access, can resolve github.com, and the branch is correct."
            exit 1
        fi
        
        log_info "Extracting codebase..."
        if ! unzip -q "$zip_file" -d "/tmp"; then
            log_error "Failed to extract ZIP file. The download might be corrupted or incomplete."
            ls -lh "$zip_file" || true
            exit 1
        fi

        local folder_name
        folder_name=$(basename "$clean_url")
        
        # Dynamically locate the extracted folder to be robust against casing or branch name variations
        local extracted_dir
        extracted_dir=$(find /tmp -maxdepth 1 -type d -name "${folder_name}-*" -o -name "$(echo "$folder_name" | tr '[:upper:]' '[:lower:]')-*" | head -n 1)
        
        if [ -n "$extracted_dir" ]; then
            log_info "Moving extracted folder $extracted_dir to $LOCAL_REPO_DIR..."
            mv "$extracted_dir" "$LOCAL_REPO_DIR"
        else
            log_warning "Dynamic folder resolution fallback for ZIP structure..."
            if [ -d "/tmp/${folder_name}-${GIT_BRANCH}" ]; then
                mv "/tmp/${folder_name}-${GIT_BRANCH}" "$LOCAL_REPO_DIR"
            else
                log_error "Could not locate extracted repository folder in /tmp."
                exit 1
            fi
        fi
        rm -f "$zip_file"
    fi
    
    log_success "Codebase successfully prepared in $LOCAL_REPO_DIR"
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
    log_info "Building API Docker image with platform optimizations..."
    cd "$LOCAL_REPO_DIR"

    if [ ! -f "$DOCKERFILE_PATH" ]; then
        log_error "Dockerfile not found at $DOCKERFILE_PATH"
        exit 1
    fi

    # Execute build with BuildKit and platform specifications
    DOCKER_BUILDKIT=1 docker build \
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
    log_info "Spawning optimized API container..."
    
    # Resolve host folders from volumes for folder creation
    local host_data
    host_data=$(echo "$DATA_VOLUME" | cut -d':' -f1)
    local host_logs
    host_logs=$(echo "$LOGS_VOLUME" | cut -d':' -f1)

    mkdir -p "$host_data" "$host_logs"

    # Start docker container
    docker run \
        --restart always \
        --name "$CONTAINER_NAME" \
        -d \
        -p "${HOST_PORT}:8080" \
        -v "$DATA_VOLUME" \
        -v "$WATCH_VOLUME" \
        -v "$LIBRARY_VOLUME" \
        -v "$LOGS_VOLUME" \
        -e "ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT" \
        -e "Logging__LogLevel__Default=$LOG_LEVEL" \
        -e "Security__ApiKey=$API_KEY" \
        -e "Security__JwtSecret=$JWT_SECRET" \
        -e "MediaButler__Paths__WatchFolder=$WATCHFOLDER_PATH" \
        -e "MediaButler__Paths__MediaLibrary=$LIBRARY_PATH" \
        -e "ConnectionStrings__DefaultConnection=Data Source=$DATABASE_PATH" \
        -e "MediaButler__ML__MaxBatchSize=$MAX_BATCH_SIZE" \
        -e "MediaButler__FileDiscovery__ScanIntervalMinutes=$SCAN_INTERVAL_MINUTES" \
        -e "MediaButler__ARM32__MemoryThresholdMB=$MEMORY_THRESHOLD_MB" \
        -e "MediaButler__ARM32__AutoGCTriggerMB=$AUTO_GC_TRIGGER_MB" \
        --platform "$DOCKER_PLATFORM" \
        "${DOCKER_IMAGE_NAME}:${DOCKER_IMAGE_TAG}"

    log_success "API Container successfully spawned!"
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
    local check_url="http://localhost:${HOST_PORT}/"
    log_info "Running local Health Check on $check_url..."
    if command -v curl >/dev/null 2>&1; then
        if curl -s -f -I "$check_url" >/dev/null 2>&1; then
            log_success "API Health Check: Connected successfully!"
        else
            log_warning "API Health Check: Endpoint did not respond. It might still be starting."
        fi
    else
        log_warning "curl is not available. Skipping API health check verification."
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

trap cleanup_temp EXIT
main "$@"