#!/usr/bin/env bash

# ==============================================================================
# Script: package-mediabutler-for-nas.sh
# Description: Cross-compiles MediaButler API and Web for QNAP/Synology NAS
#              directly on macOS (ARM64/Intel) and packages them into lightweight
#              tarballs. Completely bypasses compilation errors on NAS.
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

# Get script and workspace directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
DIST_DIR="${WORKSPACE_ROOT}/Delivery/dist"

cd "$WORKSPACE_ROOT"

echo "============================================================================="
echo "   MediaButler - NAS Packaging Suite (macOS Cross-Compiler)"
echo "============================================================================="
echo "This script compiles the codebase on your powerful Mac, targets your NAS architecture,"
echo "and packages them into ready-to-load .tar archives."
echo ""
echo "Select your NAS CPU Architecture:"
echo "1) ARM32 (QNAP TS-228A, TS-328, Synology DS218, DS118, etc.) - [linux/arm/v7]"
echo "2) ARM64 (QNAP TS-230, TS-130, Synology DS223, DS120j, etc.) - [linux/arm64]"
read -p "Select [1]: " -r arch_choice
arch_choice="${arch_choice:-1}"

DOCKER_PLATFORM="linux/arm/v7"
TAR_SUFFIX="arm32"

if [ "$arch_choice" = "2" ]; then
    DOCKER_PLATFORM="linux/arm64"
    TAR_SUFFIX="arm64"
fi

log_info "Target Platform selected: $DOCKER_PLATFORM"

# Create output distribution directory
mkdir -p "$DIST_DIR"

# Check Docker buildx support
if ! docker buildx version >/dev/null 2>&1; then
    log_error "Docker Buildx is required on Mac but not detected. Make sure Docker Desktop is running."
    exit 1
fi

echo ""
echo "============================================================================="
echo "  Step 1: Compiling & Building MediaButler API"
echo "============================================================================="
log_info "Building API Image for $DOCKER_PLATFORM..."

DOCKER_BUILDKIT=1 docker build \
    --platform "$DOCKER_PLATFORM" \
    -f Delivery/docker/api-minimal.dockerfile \
    -t "mediabutler_api_image:latest" \
    .

log_success "API Image built successfully!"

log_info "Saving API Image to ${DIST_DIR}/mediabutler_api_${TAR_SUFFIX}.tar..."
docker save -o "${DIST_DIR}/mediabutler_api_${TAR_SUFFIX}.tar" "mediabutler_api_image:latest"
log_success "API package saved!"

echo ""
echo "============================================================================="
echo "  Step 2: Compiling & Building MediaButler Web"
echo "============================================================================="
log_info "Building Web Image for $DOCKER_PLATFORM..."

DOCKER_BUILDKIT=1 docker build \
    --platform "$DOCKER_PLATFORM" \
    -f Delivery/docker/Dockerfile.webassembly \
    -t "mediabutler_web_image:latest" \
    .

log_success "Web Image built successfully!"

log_info "Saving Web Image to ${DIST_DIR}/mediabutler_web_${TAR_SUFFIX}.tar..."
docker save -o "${DIST_DIR}/mediabutler_web_${TAR_SUFFIX}.tar" "mediabutler_web_image:latest"
log_success "Web package saved!"

echo ""
echo "============================================================================="
echo "  🎉 Packaging Completed Successfully!"
echo "============================================================================="
echo "The following ready-to-load NAS packages are available in:"
echo "   $DIST_DIR"
echo ""
ls -lh "$DIST_DIR"
echo ""
echo "============================================================================="
echo "  🚀 INSTRUCTIONS FOR YOUR NAS"
echo "============================================================================="
echo "1. Copy the .tar files from your Mac to your NAS (via SMB, FTP, or File Station)"
echo "   Suggested destination: /share/Storage/Docker/mediabutler/delivery/"
echo ""
echo "2. SSH into your NAS and navigate to the folder where you copied the files."
echo ""
echo "3. Load the images directly into Docker on your NAS by running:"
echo -e "   ${GREEN}docker load -i mediabutler_api_${TAR_SUFFIX}.tar${NC}"
echo -e "   ${GREEN}docker load -i mediabutler_web_${TAR_SUFFIX}.tar${NC}"
echo ""
echo "4. Spawn the fully localized containers on the NAS:"
echo ""
echo "   API Container:"
echo "   -------------------------------------------------------------------------"
echo "   docker run -d --name mediabutler_api --restart always \\"
echo "     -p 30129:8080 \\"
echo "     -v /share/CACHEDEV1_DATA/Docker/mediabutler:/data \\"
echo "     -v /share/Download/Incoming:/watch \\"
echo "     -v /share/Video/Serie:/library \\"
echo "     -v /share/CACHEDEV1_DATA/Docker/mediabutler/logs:/app/logs \\"
echo "     -e \"ASPNETCORE_ENVIRONMENT=Production\" \\"
echo "     -e \"Security__ApiKey=mb-local-dev-key-8a9b2c\" \\"
echo "     -e \"Security__JwtSecret=mb-local-dev-jwt-secret-9x8y7z\" \\"
echo "     -e \"MediaButler__Paths__WatchFolder=/watch\" \\"
echo "     -e \"MediaButler__Paths__MediaLibrary=/library\" \\"
echo "     -e \"ConnectionStrings__DefaultConnection=Data Source=/data/mediabutler.db\" \\"
echo "     -e \"MediaButler__ML__MaxBatchSize=10\" \\"
echo "     -e \"MediaButler__FileDiscovery__ScanIntervalMinutes=5\" \\"
echo "     -e \"MediaButler__ARM32__MemoryThresholdMB=140\" \\"
echo "     -e \"MediaButler__ARM32__AutoGCTriggerMB=110\" \\"
echo "     --platform \"$DOCKER_PLATFORM\" \\"
echo "     mediabutler_api_image:latest"
echo ""
echo "   Web UI Container:"
echo "   -------------------------------------------------------------------------"
echo "   docker run -d --name mediabutler_web --restart always \\"
echo "     -p 30139:8080 \\"
echo "     -v /share/CACHEDEV1_DATA/Docker/mediabutler_web/logs:/var/log/nginx \\"
echo "     -e \"API_BASE_URL=http://localhost:30129/\" \\"
echo "     --platform \"$DOCKER_PLATFORM\" \\"
echo "     mediabutler_web_image:latest"
echo ""
echo "============================================================================="
