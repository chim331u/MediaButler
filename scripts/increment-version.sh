#!/usr/bin/env bash

# ==============================================================================
# Script: increment-version.sh
# Description: Automatically increments semantic versioning (Major, Minor, Patch)
#              in a specified .csproj file.
# Compatibility: macOS (BSD) and Linux (GNU)
# ==============================================================================

set -euo pipefail

# --- CONFIGURATION ---
# Path to your .csproj file (relative to repository root or absolute)
PROJECT_FILE="src/MediaButler.API/MediaButler.API.csproj"
# ---------------------

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

log_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Get repo root and resolve project file path
REPO_ROOT=$(git rev-parse --show-toplevel 2>/dev/null || pwd)
FULL_PATH="$REPO_ROOT/$PROJECT_FILE"

if [ ! -f "$FULL_PATH" ]; then
    log_error "Project file not found at: $FULL_PATH"
    exit 1
fi

log_info "Target project file: $PROJECT_FILE"

# Determine increment type (patch, minor, major) - default is patch
INCREMENT_TYPE="${1:-patch}"
if [[ ! "$INCREMENT_TYPE" =~ ^(patch|minor|major)$ ]]; then
    log_error "Invalid increment type: '$INCREMENT_TYPE'. Supported: patch, minor, major."
    exit 1
fi

# Ensure version tags exist in the .csproj
# If <Version> is missing, insert default 1.0.0 tags inside the first <PropertyGroup>
if ! grep -q "<Version>" "$FULL_PATH"; then
    log_warning "<Version> tag not found. Initializing with version 1.0.0..."
    
    # Cross-platform injection using a temp file for safety and absolute robustness
    TEMP_FILE=$(mktemp)
    
    # We locate the first <PropertyGroup> and inject the tags right after it
    awk '
    /\<PropertyGroup\>/ && !done {
        print $0
        print "    <Version>1.0.0</Version>"
        print "    <AssemblyVersion>1.0.0.0</AssemblyVersion>"
        print "    <FileVersion>1.0.0.0</FileVersion>"
        done = 1
        next
    }
    { print }
    ' "$FULL_PATH" > "$TEMP_FILE"
    
    mv "$TEMP_FILE" "$FULL_PATH"
    log_success "Initialized version tags to 1.0.0"
fi

# Extract current version
CURRENT_VERSION=$(grep -oE '<Version>[^<]+</Version>' "$FULL_PATH" | sed -E 's/<\/?Version>//g' | tr -d '[:space:]')
log_info "Current Version: $CURRENT_VERSION"

# Parse SemVer (handles prerelease tags like -beta.1)
BASE_VERSION=$(echo "$CURRENT_VERSION" | cut -d'-' -f1)
PRERELEASE=$(echo "$CURRENT_VERSION" | cut -d'-' -f2 -s)

IFS='.' read -r -a VERSION_PARTS <<< "$BASE_VERSION"
MAJOR="${VERSION_PARTS[0]:-0}"
MINOR="${VERSION_PARTS[1]:-0}"
PATCH="${VERSION_PARTS[2]:-0}"

# Calculate new version
case "$INCREMENT_TYPE" in
    major)
        MAJOR=$((MAJOR + 1))
        MINOR=0
        PATCH=0
        ;;
    minor)
        MINOR=$((MINOR + 1))
        PATCH=0
        ;;
    patch)
        PATCH=$((PATCH + 1))
        ;;
esac

NEW_VERSION="${MAJOR}.${MINOR}.${PATCH}"
if [ -n "$PRERELEASE" ]; then
    NEW_VERSION="${NEW_VERSION}-${PRERELEASE}"
fi

NEW_ASSEMBLY_VERSION="${MAJOR}.${MINOR}.${PATCH}.0"
NEW_FILE_VERSION="${MAJOR}.${MINOR}.${PATCH}.0"

log_info "Bumping version to: $NEW_VERSION (Assembly: $NEW_ASSEMBLY_VERSION, File: $NEW_FILE_VERSION)"

# Update CSPROJ using sed (compatible with both macOS BSD sed and Linux GNU sed via -i.bak)
sed -i.bak -E \
    -e "s|<Version>[^<]+</Version>|<Version>${NEW_VERSION}</Version>|" \
    -e "s|<AssemblyVersion>[^<]+</AssemblyVersion>|<AssemblyVersion>${NEW_ASSEMBLY_VERSION}</AssemblyVersion>|" \
    -e "s|<FileVersion>[^<]+</FileVersion>|<FileVersion>${NEW_FILE_VERSION}</FileVersion>|" \
    "$FULL_PATH"

# Clean up BSD/GNU sed backup file
rm -f "${FULL_PATH}.bak"

log_success "Successfully updated $PROJECT_FILE to version $NEW_VERSION"
