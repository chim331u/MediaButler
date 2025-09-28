#!/bin/bash

#############################################################################
# MediaButler WEB Simple Local Deployment Script
# Direct .NET hosting approach - bypasses Docker WebAssembly issues
#
# This script performs:
# 1. Git clone from repository
# 2. Local .NET build and run
# 3. Direct Kestrel hosting (no Docker)
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
# SIMPLE LOCAL CONFIGURATION
#############################################################################

# Git Repository Configuration
GITHUB_REPO="${GITHUB_REPO:-https://github.com/chim331u/MediaButler.git}"
GIT_BRANCH="${GIT_BRANCH:-delploy}"
LOCAL_REPO_DIR="${LOCAL_REPO_DIR:-/tmp/MediaButler_Web_Simple}"

# Runtime Configuration
HOST_PORT="${HOST_PORT:-3019}"
API_BASE_URL="${API_BASE_URL:-http://192.168.1.5:30129/}"
API_TIMEOUT="${API_TIMEOUT:-30}"

# Process Management
PID_FILE="/tmp/mediabutler-web.pid"

#############################################################################
# HELPER FUNCTIONS
#############################################################################

print_banner() {
    echo "============================================================================="
    echo "  MediaButler WEB - Simple Local Deployment (Direct .NET)"
    echo "============================================================================="
    echo "Repository: $GITHUB_REPO"
    echo "Branch: $GIT_BRANCH"
    echo "Local URL: http://localhost:${HOST_PORT}"
    echo "API URL: $API_BASE_URL"
    echo "Mode: Direct Kestrel hosting (no Docker)"
    echo "============================================================================="
}

show_help() {
    cat << EOF
MediaButler WEB Simple Local Deployment Script
============================================================================

DESCRIPTION:
    Runs MediaButler Blazor WebAssembly directly using .NET Kestrel server.
    No Docker involved - pure local development setup.

USAGE:
    $0 [OPTIONS]

QUICK START:
    # Run with your API server
    $0 --api-url "http://192.168.1.5:30129/"

    # Stop running instance
    $0 --stop

OPTIONS:
    -h, --help              Show this help message
    -p, --port PORT         Host port for Web UI (default: 3019)
    --api-url URL           API base URL (default: http://192.168.1.5:30129/)
    -r, --repo URL          Git repository URL
    -b, --branch NAME       Git branch name (default: delploy)
    --stop                  Stop running MediaButler Web instance
    --clean                 Clean build before starting

EXAMPLES:
    # Basic run
    $0 --api-url "http://192.168.1.5:30129/"

    # Different port
    $0 --port 8080 --api-url "http://192.168.1.5:30129/"

    # Stop running instance
    $0 --stop

REQUIREMENTS:
    - .NET 9 SDK installed
    - Git available

EOF
}

#############################################################################
# VALIDATION AND SETUP
#############################################################################

validate_environment() {
    log "Validating environment..."

    # Check .NET SDK
    if ! command -v dotnet >/dev/null 2>&1; then
        error ".NET SDK not found. Please install .NET 9 SDK from https://dotnet.microsoft.com/download"
        exit 1
    fi

    DOTNET_VERSION=$(dotnet --version 2>/dev/null)
    log ".NET version: $DOTNET_VERSION"

    # Check Git
    if ! command -v git >/dev/null 2>&1; then
        error "Git not found. Please install Git."
        exit 1
    fi

    success "Environment validation completed"
}

stop_existing_instance() {
    log "Checking for existing MediaButler Web instance..."

    if [[ -f "$PID_FILE" ]]; then
        local pid=$(cat "$PID_FILE")
        if kill -0 "$pid" 2>/dev/null; then
            log "Stopping existing instance (PID: $pid)"
            kill "$pid"
            sleep 2
            if kill -0 "$pid" 2>/dev/null; then
                warning "Process still running, force killing..."
                kill -9 "$pid"
            fi
        fi
        rm -f "$PID_FILE"
        success "Existing instance stopped"
    else
        log "No existing instance found"
    fi

    # Also check for any dotnet processes running the Web project
    local web_pids=$(pgrep -f "MediaButler.Web" || true)
    if [[ -n "$web_pids" ]]; then
        log "Found additional MediaButler.Web processes: $web_pids"
        echo "$web_pids" | xargs -r kill
        success "Additional processes stopped"
    fi
}

clone_repository() {
    log "Setting up repository..."

    # Remove existing directory
    if [[ -d "$LOCAL_REPO_DIR" ]]; then
        log "Removing existing repository: $LOCAL_REPO_DIR"
        rm -rf "$LOCAL_REPO_DIR"
    fi

    # Clone repository
    log "Cloning repository: $GITHUB_REPO (branch: $GIT_BRANCH)"
    git clone -b "$GIT_BRANCH" "$GITHUB_REPO" "$LOCAL_REPO_DIR"

    # Verify
    if [[ ! -f "$LOCAL_REPO_DIR/src/MediaButler.Web/MediaButler.Web.csproj" ]]; then
        error "MediaButler.Web project not found in repository"
        exit 1
    fi

    success "Repository setup completed"
}

generate_appsettings() {
    log "Generating appsettings.json..."

    local appsettings_file="$LOCAL_REPO_DIR/src/MediaButler.Web/wwwroot/appsettings.json"
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

    success "Configuration generated with API URL: $API_BASE_URL"
}

#############################################################################
# BUILD AND RUN
#############################################################################

build_and_run() {
    log "Building and running MediaButler Web..."

    cd "$LOCAL_REPO_DIR"
    generate_appsettings

    # Set environment variables for Kestrel
    export ASPNETCORE_URLS="http://localhost:${HOST_PORT}"
    export ASPNETCORE_ENVIRONMENT="Development"

    log "Starting MediaButler Web on http://localhost:${HOST_PORT}"
    log "API connection: $API_BASE_URL"
    log "Press Ctrl+C to stop the server"

    # Run the project directly
    cd src/MediaButler.Web

    if [[ "$CLEAN_BUILD" == "true" ]]; then
        log "Cleaning previous build..."
        dotnet clean
    fi

    log "Restoring packages..."
    dotnet restore

    log "Starting application..."
    dotnet run --urls "http://localhost:${HOST_PORT}" &

    # Save PID for later cleanup
    echo $! > "$PID_FILE"
    local app_pid=$!

    success "MediaButler Web started successfully!"
    log "PID: $app_pid"
    log "Access at: http://localhost:${HOST_PORT}"

    # Wait for the process
    wait $app_pid
    rm -f "$PID_FILE"
}

run_in_background() {
    log "Starting MediaButler Web in background..."

    cd "$LOCAL_REPO_DIR"
    generate_appsettings

    # Set environment variables
    export ASPNETCORE_URLS="http://localhost:${HOST_PORT}"
    export ASPNETCORE_ENVIRONMENT="Development"

    cd src/MediaButler.Web

    if [[ "$CLEAN_BUILD" == "true" ]]; then
        log "Cleaning previous build..."
        dotnet clean
    fi

    log "Restoring packages..."
    dotnet restore

    log "Starting application in background..."
    nohup dotnet run --urls "http://localhost:${HOST_PORT}" > /tmp/mediabutler-web.log 2>&1 &

    local app_pid=$!
    echo $app_pid > "$PID_FILE"

    # Wait a moment and check if it started
    sleep 3
    if kill -0 "$app_pid" 2>/dev/null; then
        success "MediaButler Web started in background!"
        log "PID: $app_pid"
        log "Access at: http://localhost:${HOST_PORT}"
        log "Logs: tail -f /tmp/mediabutler-web.log"
    else
        error "Failed to start MediaButler Web"
        cat /tmp/mediabutler-web.log
        exit 1
    fi
}

#############################################################################
# ARGUMENT PARSING
#############################################################################

STOP_ONLY=false
CLEAN_BUILD=false
BACKGROUND_MODE=false

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
            -r|--repo)
                GITHUB_REPO="$2"
                shift 2
                ;;
            -b|--branch)
                GIT_BRANCH="$2"
                shift 2
                ;;
            --stop)
                STOP_ONLY=true
                shift
                ;;
            --clean)
                CLEAN_BUILD=true
                shift
                ;;
            --background)
                BACKGROUND_MODE=true
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
    if [[ -f "$PID_FILE" ]]; then
        local pid=$(cat "$PID_FILE" 2>/dev/null || echo "")
        if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
            log "Cleaning up process $pid"
            kill "$pid" 2>/dev/null || true
        fi
        rm -f "$PID_FILE"
    fi

    if [[ -d "$LOCAL_REPO_DIR" ]]; then
        log "Cleaning up repository: $LOCAL_REPO_DIR"
        rm -rf "$LOCAL_REPO_DIR"
    fi
}

# Trap cleanup on exit
trap cleanup EXIT

#############################################################################
# MAIN EXECUTION
#############################################################################

main() {
    parse_arguments "$@"

    if [[ "$STOP_ONLY" == "true" ]]; then
        stop_existing_instance
        exit 0
    fi

    print_banner

    # Setup
    validate_environment
    stop_existing_instance
    clone_repository

    # Run
    if [[ "$BACKGROUND_MODE" == "true" ]]; then
        run_in_background
    else
        build_and_run
    fi
}

# Execute main function
main "$@"