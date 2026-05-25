#!/usr/bin/env bash

# ==============================================================================
# MediaButler QNAP NAS Deploy Automation Script
# ==============================================================================
# Automates the local multi-platform Docker compilation, tar packaging,
# dynamic QNAP storage volume detection, and container service loading.
#
# Usage:
#   Local (Mac): ./scripts/deploy-qnap.sh build
#   On QNAP NAS: ./qnap-service-run.sh (copied or run directly as 'run-nas')
# ==============================================================================

set -euo pipefail

# Text colors for clean formatting
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

show_help() {
    echo "MediaButler QNAP Deployment Automation Utility"
    echo ""
    echo "Usage:"
    echo "  $0 build          - Local: Multi-platform Docker ARM32v7 build & package to tar"
    echo "  $0 run-nas        - QNAP: Detect active storage volume, load tarball, and launch Compose"
    echo "  $0 help           - Show this help screen"
    echo ""
}

# 1. Local Building Phase
local_build() {
    log_info "Initiating Local Docker ARM32v7 Cross-Compilation Phase..."
    
    # Check if docker is installed
    if ! command -v docker &> /dev/null; then
        log_error "Docker is not installed on this host. Docker is required for local building."
        exit 1
    fi

    # Ensure Buildx is available
    if ! docker buildx version &> /dev/null; then
        log_error "Docker Buildx is not available or enabled. Multi-platform builds require Buildx."
        exit 1
    fi

    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
    OUT_DIR="${REPO_ROOT}/build"
    TAR_OUT="${OUT_DIR}/mediabutler-qnap-arm32.tar"

    mkdir -p "${OUT_DIR}"

    log_info "Creating/Checking multi-platform docker builder instance..."
    if ! docker buildx inspect mediabutler-builder &> /dev/null; then
        log_info "Creating new buildx builder 'mediabutler-builder'..."
        docker buildx create --name mediabutler-builder --use
    else
        docker buildx use mediabutler-builder
    fi

    log_info "Building Docker image for linux/arm/v7 (ARM32v7)..."
    log_info "Context: ${REPO_ROOT}"
    
    # Run Docker Buildx to build and package image locally to docker cache
    # Note: We use --load to load the resulting image into our local docker daemon
    docker buildx build \
        --platform linux/arm/v7 \
        -f "${REPO_ROOT}/docker/Dockerfile.arm32" \
        -t mediabutler:qnap-arm32 \
        --load \
        "${REPO_ROOT}"

    log_success "Docker image successfully cross-compiled: mediabutler:qnap-arm32 (ARM32v7)"

    log_info "Exporting Docker image to uncompressed tarball: ${TAR_OUT}..."
    docker save mediabutler:qnap-arm32 -o "${TAR_OUT}"

    log_success "Tar package built successfully: ${TAR_OUT}"
    
    # Get size of the generated tar file
    if command -v du &> /dev/null; then
        log_info "Tarball package size: $(du -sh "${TAR_OUT}" | cut -f1)"
    fi

    echo -e "\n=============================================================================="
    log_success "PREPARATION COMPLETED SUCCESSFULLY!"
    echo -e "Follow these steps to deploy on your QNAP NAS:"
    echo -e ""
    echo -e "1. Copy the tarball and docker-compose.qnap.yml to your NAS:"
    echo -e "   ${YELLOW}scp build/mediabutler-qnap-arm32.tar docker-compose.qnap.yml admin@<NAS_IP>:/share/Public/${NC}"
    echo -e ""
    echo -e "2. Copy this script to the NAS and run it to perform dynamic volume mapping and loading:"
    echo -e "   ${YELLOW}scp scripts/deploy-qnap.sh admin@<NAS_IP>:/share/Public/${NC}"
    echo -e "   ${YELLOW}ssh admin@<NAS_IP> 'bash /share/Public/deploy-qnap.sh run-nas'${NC}"
    echo -e "==============================================================================\n"
}

# 2. QNAP NAS Execution Phase
qnap_nas_run() {
    log_info "Initiating QNAP NAS Execution & Deployment Phase..."
    
    # A. Dynamic storage volume detection
    log_info "Exploring QNAP storage volumes..."
    
    QNAP_VOLUME=""
    if [ -d "/share/CACHEDEV1_DATA" ]; then
        QNAP_VOLUME="/share/CACHEDEV1_DATA"
    elif [ -d "/share/CACHEDEV2_DATA" ]; then
        QNAP_VOLUME="/share/CACHEDEV2_DATA"
    elif [ -d "/share/CACHEDEV3_DATA" ]; then
        QNAP_VOLUME="/share/CACHEDEV3_DATA"
    elif [ -d "/share/CACHEDEV4_DATA" ]; then
        QNAP_VOLUME="/share/CACHEDEV4_DATA"
    else
        # Fallback to general shared space
        log_warn "Active CACHEDEV volume not found in standard paths."
        QNAP_VOLUME="/share/CACHEDEV1_DATA"
    fi
    
    export QNAP_DATA_DIR="${QNAP_VOLUME}/Docker/mediabutler"
    log_success "Dynamic active storage volume located: ${QNAP_VOLUME}"
    log_success "Exported QNAP_DATA_DIR: ${QNAP_DATA_DIR}"
    
    # B. Ensure directories are created
    log_info "Creating local folders on QNAP host..."
    mkdir -p "${QNAP_DATA_DIR}"
    mkdir -p "/share/Download/Incoming"
    mkdir -p "/share/Video/Serie"
    log_success "Operational folders ready."

    # C. Check docker availability on NAS
    if ! command -v docker &> /dev/null; then
        log_error "Docker was not found on this QNAP NAS. Please install Container Station."
        exit 1
    fi

    # D. Load the Docker Image from tar
    # Locate tarball in local folder
    TAR_PATH="./mediabutler-qnap-arm32.tar"
    if [ ! -f "${TAR_PATH}" ]; then
        TAR_PATH="/share/Public/mediabutler-qnap-arm32.tar"
    fi

    if [ -f "${TAR_PATH}" ]; then
        log_info "Loading container image from archive: ${TAR_PATH}..."
        docker load -i "${TAR_PATH}"
        log_success "Docker image loaded successfully into Container Station!"
    else
        log_warn "Docker image tarball not found at current dir or /share/Public/."
        log_warn "Assuming the image 'mediabutler:qnap-arm32' is already present or was cached."
    fi

    # E. Find compose tool
    COMPOSE_CMD="docker compose"
    if ! docker compose version &> /dev/null; then
        if command -v docker-compose &> /dev/null; then
            COMPOSE_CMD="docker-compose"
        else
            log_error "Docker Compose was not found. Please install docker-compose via QNAP QPKG or CLI."
            exit 1
        fi
    fi

    # F. Fire up compose
    COMPOSE_FILE="./docker-compose.qnap.yml"
    if [ ! -f "${COMPOSE_FILE}" ]; then
        COMPOSE_FILE="/share/Public/docker-compose.qnap.yml"
    fi

    if [ ! -f "${COMPOSE_FILE}" ]; then
        log_error "Docker Compose configuration file 'docker-compose.qnap.yml' not found in current directory or /share/Public/."
        exit 1
    fi

    log_info "Launching MediaButler container service via ${COMPOSE_CMD}..."
    ${COMPOSE_CMD} -f "${COMPOSE_FILE}" up -d

    log_success "MediaButler container stack is running in background!"
    echo -e "\n=============================================================================="
    log_success "DEPLOYMENT COMPLETED!"
    echo -e "Your MediaButler service is active:"
    echo -e "   - ${BLUE}API Endpoint Access:${NC}  http://<NAS_IP>:30139/api"
    echo -e "   - ${BLUE}Web UI Access:${NC}       http://<NAS_IP>:30149/"
    echo -e "   - ${BLUE}Health Check:${NC}       http://<NAS_IP>:30139/health"
    echo -e "==============================================================================\n"
}

# Main routing logic
if [ $# -lt 1 ]; then
    show_help
    exit 1
fi

case "$1" in
    build)
        local_build
        ;;
    run-nas)
        qnap_nas_run
        ;;
    help)
        show_help
        ;;
    *)
        log_error "Unknown argument: $1"
        show_help
        exit 1
        ;;
esac
