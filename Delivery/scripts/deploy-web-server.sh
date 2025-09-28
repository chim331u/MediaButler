#!/bin/bash

#############################################################################
# MediaButler WEB Blazor Server Local Deployment Script
# Converts WebAssembly project to Blazor Server for local development
#
# This script performs:
# 1. Git clone from repository
# 2. Convert project to Blazor Server (avoids WebAssembly issues)
# 3. Local .NET build and run with Blazor Server
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
LOCAL_REPO_DIR="${LOCAL_REPO_DIR:-/tmp/MediaButler_Web_Server}"

# Runtime Configuration
HOST_PORT="${HOST_PORT:-3019}"
API_BASE_URL="${API_BASE_URL:-http://192.168.1.5:30129/}"
API_TIMEOUT="${API_TIMEOUT:-30}"

# Process Management
PID_FILE="/tmp/mediabutler-web-server.pid"

#############################################################################
# HELPER FUNCTIONS
#############################################################################

print_banner() {
    echo "============================================================================="
    echo "  MediaButler WEB - Blazor Server Local Development"
    echo "============================================================================="
    echo "Repository: $GITHUB_REPO"
    echo "Branch: $GIT_BRANCH"
    echo "Local URL: http://localhost:${HOST_PORT}"
    echo "API URL: $API_BASE_URL"
    echo "Mode: Blazor Server (converted from WebAssembly)"
    echo "============================================================================="
}

show_help() {
    cat << EOF
MediaButler WEB Blazor Server Local Deployment Script
============================================================================

DESCRIPTION:
    Converts MediaButler WebAssembly to Blazor Server for local development.
    This avoids all WebAssembly build and serving issues.

USAGE:
    $0 [OPTIONS]

QUICK START:
    $0 --api-url "http://192.168.1.5:30129/"

OPTIONS:
    -h, --help              Show this help message
    -p, --port PORT         Host port (default: 3019)
    --api-url URL           API base URL
    --stop                  Stop running instance
    --background            Run in background

EOF
}

#############################################################################
# SETUP FUNCTIONS
#############################################################################

validate_environment() {
    log "Validating environment..."

    if ! command -v dotnet >/dev/null 2>&1; then
        error ".NET SDK not found. Please install .NET 9 SDK"
        exit 1
    fi

    if ! command -v git >/dev/null 2>&1; then
        error "Git not found. Please install Git"
        exit 1
    fi

    success "Environment validation completed"
}

stop_existing_instance() {
    log "Checking for existing instance..."

    if [[ -f "$PID_FILE" ]]; then
        local pid=$(cat "$PID_FILE")
        if kill -0 "$pid" 2>/dev/null; then
            log "Stopping existing instance (PID: $pid)"
            kill "$pid"
            sleep 2
        fi
        rm -f "$PID_FILE"
    fi

    # Kill any remaining processes
    pkill -f "MediaButler.Web" || true
    success "Cleanup completed"
}

clone_repository() {
    log "Setting up repository..."

    if [[ -d "$LOCAL_REPO_DIR" ]]; then
        rm -rf "$LOCAL_REPO_DIR"
    fi

    git clone -b "$GIT_BRANCH" "$GITHUB_REPO" "$LOCAL_REPO_DIR"

    if [[ ! -f "$LOCAL_REPO_DIR/src/MediaButler.Web/MediaButler.Web.csproj" ]]; then
        error "MediaButler.Web project not found"
        exit 1
    fi

    success "Repository setup completed"
}

convert_to_blazor_server() {
    log "Converting project to Blazor Server..."

    cd "$LOCAL_REPO_DIR"
    local project_file="src/MediaButler.Web/MediaButler.Web.csproj"

    # Backup original
    cp "$project_file" "${project_file}.backup"

    # Create Blazor Server version
    cat > "$project_file" << 'EOF'
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="9.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="9.0.0" />
    <PackageReference Include="Radzen.Blazor" Version="7.3.5" />
  </ItemGroup>

  <ItemGroup>
    <_ContentIncludedByDefault Remove="wwwroot\sample-data\weather.json" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MediaButler.Core\MediaButler.Core.csproj" />
  </ItemGroup>

</Project>
EOF

    # Create Program.cs for Blazor Server
    cat > "src/MediaButler.Web/Program.cs" << 'EOF'
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add HTTP client services
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
EOF

    # Create _Host.cshtml
    mkdir -p "src/MediaButler.Web/Pages"
    cat > "src/MediaButler.Web/Pages/_Host.cshtml" << 'EOF'
@page "/"
@namespace MediaButler.Web.Pages
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@{
    Layout = "_Layout";
}

<component type="typeof(App)" render-mode="ServerPrerendered" />
EOF

    # Create _Layout.cshtml
    cat > "src/MediaButler.Web/Pages/Shared/_Layout.cshtml" << 'EOF'
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>MediaButler</title>
    <base href="~/" />
    <link href="css/bootstrap/bootstrap.min.css" rel="stylesheet" />
    <link href="css/app.css" rel="stylesheet" />
    <link href="_content/Radzen.Blazor/css/material-base.css" rel="stylesheet" />
    <HeadOutlet />
</head>
<body>
    @RenderBody()

    <div id="blazor-error-ui">
        <environment include="Staging,Production">
            An error has occurred. This application may no longer respond until reloaded.
        </environment>
        <environment include="Development">
            An unhandled exception has occurred. See browser dev tools for details.
        </environment>
        <a href="" class="reload">Reload</a>
        <a class="dismiss">🗙</a>
    </div>

    <script src="_framework/blazor.server.js"></script>
    <script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
</body>
</html>
EOF

    success "Project converted to Blazor Server"
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
# BUILD AND RUN
#############################################################################

build_and_run() {
    log "Building and running Blazor Server..."

    cd "$LOCAL_REPO_DIR/src/MediaButler.Web"

    export ASPNETCORE_URLS="http://localhost:${HOST_PORT}"
    export ASPNETCORE_ENVIRONMENT="Development"

    log "Restoring packages..."
    dotnet restore

    log "Starting Blazor Server on http://localhost:${HOST_PORT}"
    dotnet run --urls "http://localhost:${HOST_PORT}" &

    echo $! > "$PID_FILE"
    local app_pid=$!

    success "Blazor Server started!"
    log "PID: $app_pid"
    log "Access at: http://localhost:${HOST_PORT}"

    # Wait for the process
    wait $app_pid
    rm -f "$PID_FILE"
}

#############################################################################
# ARGUMENT PARSING
#############################################################################

STOP_ONLY=false
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
            --stop)
                STOP_ONLY=true
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
            kill "$pid" 2>/dev/null || true
        fi
        rm -f "$PID_FILE"
    fi

    if [[ -d "$LOCAL_REPO_DIR" ]]; then
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
        stop_existing_instance
        exit 0
    fi

    print_banner

    validate_environment
    stop_existing_instance
    clone_repository
    convert_to_blazor_server
    generate_appsettings
    build_and_run
}

main "$@"