#!/usr/bin/env bash

# ==============================================================================
# MediaButler QNAP ARM32 & Local Builder
# ==============================================================================
# Automates compiling the static Svelte Web UI and the pure Go API backend
# with CGO disabled and code stripping flags to optimize for low-spec NAS devices.
#
# Supported architectures:
#   - QNAP NAS (ARM32v7, CGO-free, optimized size and RAM footprint)
#   - Local Host (macOS or custom architecture for testing)
# ==============================================================================

set -euo pipefail

# Text colors for clean formatting
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Helper functions for structured output
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

# Print help/usage instructions
show_usage() {
    echo "Usage: $0 [options]"
    echo ""
    echo "Options:"
    echo "  -l, --local           Build for local host operating system (for Mac development/testing)"
    echo "  -q, --qnap            Build for QNAP ARM32v7 (GOOS=linux GOARCH=arm GOARM=7) [default]"
    echo "  --skip-frontend       Skip building Svelte Web UI (uses existing src/backend/dist if present)"
    echo "  -h, --help            Show this help message"
    echo ""
}

# Defaults
TARGET="qnap"
SKIP_FRONTEND=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -l|--local)
            TARGET="local"
            shift
            ;;
        -q|--qnap)
            TARGET="qnap"
            shift
            ;;
        --skip-frontend)
            SKIP_FRONTEND=true
            shift
            ;;
        -h|--help)
            show_usage
            exit 0
            ;;
        *)
            log_error "Unknown argument: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Resolve absolute paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
FRONTEND_DIR="${REPO_ROOT}/src/frontend"
BACKEND_DIR="${REPO_ROOT}/src/backend"
BUILD_OUT_DIR="${REPO_ROOT}/build"

log_info "Initializing MediaButler build pipeline..."
log_info "Repository root: ${REPO_ROOT}"

# Ensure build output directory exists
mkdir -p "${BUILD_OUT_DIR}"

# 1. Build Svelte Web UI
if [ "$SKIP_FRONTEND" = "false" ]; then
    log_info "Building static Svelte Web UI..."
    
    # Check for npm and node
    if ! command -v npm &> /dev/null; then
        log_error "Node Package Manager (npm) is not installed. Please install Node.js."
        exit 1
    fi
    
    cd "${FRONTEND_DIR}"
    
    if [ ! -d "node_modules" ]; then
        log_info "node_modules not found, running 'npm install'..."
        npm install
    fi
    
    log_info "Compiling frontend assets via Vite..."
    npm run build
    
    log_success "Frontend assets compiled successfully to src/backend/dist"
else
    log_warn "Skipping frontend build as requested by --skip-frontend"
    if [ ! -d "${BACKEND_DIR}/dist" ]; then
        log_error "Embedded directory 'src/backend/dist' does not exist. You must build the frontend at least once!"
        exit 1
    fi
fi

# 2. Compile Go Application
log_info "Preparing Go API Server build..."

# Check for Go
if ! command -v go &> /dev/null; then
    log_error "Go compiler (go) is not installed. Please install Go."
    exit 1
fi

cd "${BACKEND_DIR}"

# Setup target-specific variables
if [ "$TARGET" = "qnap" ]; then
    export GOOS=linux
    export GOARCH=arm
    export GOARM=7
    export CGO_ENABLED=0
    OUT_FILE="${BUILD_OUT_DIR}/mediabutler-qnap"
    log_info "Targeting: QNAP NAS (ARM32v7 - GOOS=linux GOARCH=arm GOARM=7)"
else
    export CGO_ENABLED=0
    # Let Go automatically resolve GOOS and GOARCH for the local machine
    LOCAL_OS=$(go env GOOS)
    LOCAL_ARCH=$(go env GOARCH)
    OUT_FILE="${BUILD_OUT_DIR}/mediabutler-local-${LOCAL_OS}-${LOCAL_ARCH}"
    log_info "Targeting: Local Host (${LOCAL_OS} ${LOCAL_ARCH})"
fi

log_info "Compiling with optimization flags: CGO_ENABLED=0, stripping symbols (-ldflags='-s -w') and trimming paths (-trimpath)..."

# Build the CGO-free, stripped binary
go build -ldflags="-s -w" -trimpath -o "${OUT_FILE}" .

log_success "Go binary built successfully!"
log_info "Binary location: ${OUT_FILE}"

# Print file details & size for validation
if command -v file &> /dev/null; then
    file "${OUT_FILE}" || true
fi
if command -v du &> /dev/null; then
    log_info "Binary size: $(du -sh "${OUT_FILE}" | cut -f1)"
fi

echo -e "\n=============================================================================="
if [ "$TARGET" = "qnap" ]; then
    log_success "QNAP ARM32v7 binary ready for deployment!"
    echo -e "You can copy '${OUT_FILE}' directly to your QNAP NAS and run it natively."
    echo -e "Or package it into a super-light Docker container using 'docker/Dockerfile.arm32'."
else
    log_success "Local test binary ready!"
    echo -e "Run it immediately with: ./build/$(basename "${OUT_FILE}")"
fi
echo -e "==============================================================================\n"
