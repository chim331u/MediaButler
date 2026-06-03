#!/usr/bin/env bash

# ==============================================================================
# MediaButler QNAP NAS Deploy Automation Script
# ==============================================================================
# Automates the local multi-platform Docker compilation, tar packaging,
# dynamic QNAP storage volume detection, SSH multiplexed copy, and container loading.
#
# Usage:
#   Local (Mac) Build Only:  ./scripts/deploy-qnap.sh build
#   QNAP NAS Remote Deploy:  ./scripts/deploy-qnap.sh deploy
#   QNAP NAS Local Load:     ./deploy-qnap.sh run-nas
# ==============================================================================

set -euo pipefail

# Text colors for clean formatting
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default SSH Configuration
NAS_IP="192.168.1.5"
NAS_USER="admin"
NAS_PORT="22"
NAS_PATH="/share/Storage/Docker/mediabutler/delivery"

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
    echo "  $0 [options] <command>"
    echo ""
    echo "Commands:"
    echo "  build             - Local: Multi-platform Docker ARM32v7 build & package to tar"
    echo "  deploy            - Local to Remote: Compile local image, copy over SSH, and run deploy on QNAP"
    echo "  run-nas           - QNAP: Detect active storage volume, load tarball, and launch Compose"
    echo "  help              - Show this help screen"
    echo ""
    echo "Options (for deploy/build):"
    echo "  --ip <ip>         - IP address of the QNAP NAS (default: ${NAS_IP})"
    echo "  --user <user>     - SSH username of the QNAP NAS (default: ${NAS_USER})"
    echo "  --port <port>     - SSH port of the QNAP NAS (default: ${NAS_PORT})"
    echo "  --path <path>     - Target path on QNAP NAS for delivery files (default: ${NAS_PATH})"
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
    # Ensure our custom NAS_PATH is created too
    mkdir -p "${NAS_PATH}"
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

# 3. Remote Deployment Phase
remote_deploy() {
    log_info "Starting Automated Remote Deployment to QNAP NAS..."
    
    # Run local compilation & tar packaging
    local_build

    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
    TAR_OUT="${REPO_ROOT}/build/mediabutler-qnap-arm32.tar"
    COMPOSE_FILE="${REPO_ROOT}/docker-compose.qnap.yml"
    DEPLOY_SCRIPT="${REPO_ROOT}/scripts/deploy-qnap.sh"

    if [ ! -f "${TAR_OUT}" ]; then
        log_error "Local build tarball does not exist: ${TAR_OUT}"
        exit 1
    fi

    # MUX Socket for SSH multiplexing
    MUX_SOCKET="/tmp/ssh_mux_mediabutler_${NAS_IP}_${NAS_PORT}"

    # Setup exit trap to close MUX session
    cleanup() {
        if [ -S "${MUX_SOCKET}" ]; then
            echo ""
            log_info "Closing SSH Multiplexed Master Connection..."
            ssh -p "${NAS_PORT}" -S "${MUX_SOCKET}" -O exit "${NAS_USER}@${NAS_IP}" 2>/dev/null || true
        fi
    }
    trap cleanup EXIT

    log_info "Establishing Master SSH Connection to NAS (${NAS_IP}:${NAS_PORT})..."
    log_warn "👉 Please enter the password for QNAP user '${NAS_USER}' (required ONCE):"
    ssh -p "${NAS_PORT}" -M -S "${MUX_SOCKET}" -fN "${NAS_USER}@${NAS_IP}"

    log_info "Creating delivery folder remotely on QNAP: ${NAS_PATH}..."
    ssh -S "${MUX_SOCKET}" -p "${NAS_PORT}" "${NAS_USER}@${NAS_IP}" "mkdir -p ${NAS_PATH}"

    log_info "Copying image package and configurations via SCP..."
    scp -o ControlPath="${MUX_SOCKET}" -P "${NAS_PORT}" "${TAR_OUT}" "${COMPOSE_FILE}" "${DEPLOY_SCRIPT}" "${NAS_USER}@${NAS_IP}:${NAS_PATH}/"
    log_success "Files successfully transferred to QNAP NAS."

    log_info "Triggering remote script execution via SSH..."
    ssh -S "${MUX_SOCKET}" -p "${NAS_PORT}" "${NAS_USER}@${NAS_IP}" << EOF
      # Add QNAP and Container Station paths to PATH
      for qpath in /share/*/.qpkg/container-station/bin /share/*/.qpkg/container-station/sbin /usr/local/bin /usr/local/sbin; do
        if [ -d "\$qpath" ]; then
          export PATH="\$qpath:\$PATH"
        fi
      done

      cd "${NAS_PATH}"
      # Redefine log helpers for the remote shell execution context
      log_info() { echo -e "\033[0;34m[INFO]\033[0m \$1"; }
      log_success() { echo -e "\033[0;32m[SUCCESS]\033[0m \$1"; }
      log_warn() { echo -e "\033[1;33m[WARN]\033[0m \$1"; }
      log_error() { echo -e "\033[0;31m[ERROR]\033[0m \$1"; }
      export NAS_PATH="${NAS_PATH}"

      # Run deployment script phase on QNAP
      bash deploy-qnap.sh run-nas
EOF

    log_success "Automated Deployment Pipeline Completed Successfully!"
}

# Main argument routing
COMMAND=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --ip)
            NAS_IP="$2"
            shift 2
            ;;
        --user)
            NAS_USER="$2"
            shift 2
            ;;
        --port)
            NAS_PORT="$2"
            shift 2
            ;;
        --path)
            NAS_PATH="$2"
            shift 2
            ;;
        build|run-nas|deploy|help)
            COMMAND="$1"
            shift
            ;;
        *)
            log_error "Unknown argument: $1"
            show_help
            exit 1
            ;;
    esac
done

if [ -z "${COMMAND}" ] || [ "${COMMAND}" = "help" ]; then
    show_help
    exit 0
fi

case "${COMMAND}" in
    build)
        local_build
        ;;
    run-nas)
        qnap_nas_run
        ;;
    deploy)
        remote_deploy
        ;;
esac
