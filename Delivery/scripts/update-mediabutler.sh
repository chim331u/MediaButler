#!/bin/bash
# MediaButler Update Script for QNAP NAS
# Safe update mechanism with rollback capability
# Version: 1.0.0

set -euo pipefail

# =============================================================================
# CONFIGURATION
# =============================================================================

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_PATH="$(dirname "$SCRIPT_DIR")"
LOG_FILE="$INSTALL_PATH/logs/update.log"
CONFIG_FILE="$INSTALL_PATH/.env"

# Load configuration
if [[ -f "$CONFIG_FILE" ]]; then
    source "$CONFIG_FILE"
fi

# Update settings
GITHUB_REPO_URL="${GITHUB_REPO_URL:-https://github.com/luca/mediabutler}"
GITHUB_BRANCH="${GITHUB_BRANCH:-main}"
UPDATE_TIMEOUT=1800  # 30 minutes
ROLLBACK_TIMEOUT=300  # 5 minutes

# =============================================================================
# LOGGING
# =============================================================================

# Ensure log directory exists
mkdir -p "$(dirname "$LOG_FILE")"

# Logging functions
log_update() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

# =============================================================================
# UTILITY FUNCTIONS
# =============================================================================

# Check if services are healthy
check_services_health() {
    local max_attempts=30
    local attempt=1

    log_update "Checking service health..."

    while [[ $attempt -le $max_attempts ]]; do
        local healthy_count=0
        local total_services=3

        # Check API health
        if curl -sf --max-time 10 "http://localhost:${API_PORT:-5000}/health" >/dev/null 2>&1; then
            ((healthy_count++))
        fi

        # Check Web health
        if curl -sf --max-time 10 "http://localhost:${WEB_PORT:-3000}" >/dev/null 2>&1; then
            ((healthy_count++))
        fi

        # Check Proxy health
        if curl -sf --max-time 10 "http://localhost:${PROXY_PORT:-80}/health" >/dev/null 2>&1; then
            ((healthy_count++))
        fi

        if [[ $healthy_count -eq $total_services ]]; then
            log_update "All services are healthy"
            return 0
        fi

        log_update "Health check attempt $attempt/$max_attempts: $healthy_count/$total_services services healthy"
        sleep 10
        ((attempt++))
    done

    log_update "Health check failed: services not responding after $max_attempts attempts"
    return 1
}

# Get current version info
get_current_version() {
    if [[ -f "$INSTALL_PATH/.version" ]]; then
        cat "$INSTALL_PATH/.version"
    else
        echo "unknown"
    fi
}

# Get latest version from GitHub
get_latest_version() {
    local latest_commit
    latest_commit=$(curl -sf "https://api.github.com/repos/${GITHUB_REPO_URL##*/}/commits/${GITHUB_BRANCH}" | \
                    grep '"sha"' | head -1 | cut -d'"' -f4 | cut -c1-8 2>/dev/null || echo "unknown")
    echo "$latest_commit"
}

# Create pre-update backup
create_update_backup() {
    log_update "Creating pre-update backup..."

    local backup_script="$SCRIPT_DIR/backup-mediabutler.sh"
    local backup_file="$INSTALL_PATH/backups/pre_update_$(date +%Y%m%d_%H%M%S).tar.gz"

    if [[ -x "$backup_script" ]]; then
        if "$backup_script" --full; then
            log_update "Pre-update backup created successfully"
            return 0
        else
            log_update "WARNING: Pre-update backup failed"
            return 1
        fi
    else
        log_update "WARNING: Backup script not found or not executable"
        return 1
    fi
}

# =============================================================================
# UPDATE FUNCTIONS
# =============================================================================

# Download latest source
download_latest_source() {
    local temp_dir="$1"

    log_update "Downloading latest source from $GITHUB_REPO_URL (branch: $GITHUB_BRANCH)"

    local download_url="${GITHUB_REPO_URL}/archive/refs/heads/${GITHUB_BRANCH}.zip"

    if wget --progress=bar:force -O "$temp_dir/mediabutler-latest.zip" "$download_url"; then
        log_update "Source downloaded successfully"

        # Extract source
        if unzip -q "$temp_dir/mediabutler-latest.zip" -d "$temp_dir"; then
            log_update "Source extracted successfully"

            # Find extracted directory
            local extracted_dir=$(find "$temp_dir" -maxdepth 1 -type d -name "*mediabutler*" -o -name "*MediaButler*" | head -1)
            if [[ -n "$extracted_dir" ]]; then
                echo "$extracted_dir"
                return 0
            else
                log_update "ERROR: Could not find extracted source directory"
                return 1
            fi
        else
            log_update "ERROR: Failed to extract source"
            return 1
        fi
    else
        log_update "ERROR: Failed to download source"
        return 1
    fi
}

# Backup current installation
backup_current_installation() {
    local backup_dir="$1"

    log_update "Backing up current installation..."

    # Stop services
    cd "$INSTALL_PATH"
    docker compose down --remove-orphans 2>/dev/null || true

    # Create backup
    cp -r "$INSTALL_PATH" "$backup_dir/current_installation"

    log_update "Current installation backed up to $backup_dir/current_installation"
}

# Update source files
update_source_files() {
    local source_dir="$1"

    log_update "Updating source files..."

    # Preserve configuration files
    local temp_config_dir=$(mktemp -d)
    cp "$INSTALL_PATH/.env" "$temp_config_dir/" 2>/dev/null || true
    cp "$INSTALL_PATH/docker compose.yml" "$temp_config_dir/" 2>/dev/null || true
    cp "$INSTALL_PATH/nginx.conf" "$temp_config_dir/" 2>/dev/null || true
    cp -r "$INSTALL_PATH/configs" "$temp_config_dir/" 2>/dev/null || true

    # Update source files (excluding data and configuration)
    rsync -av \
        --exclude='data/' \
        --exclude='logs/' \
        --exclude='backups/' \
        --exclude='.env' \
        --exclude='docker compose.yml' \
        --exclude='nginx.conf' \
        --exclude='configs/' \
        "$source_dir/" "$INSTALL_PATH/"

    # Restore configuration files
    cp "$temp_config_dir/.env" "$INSTALL_PATH/" 2>/dev/null || true
    cp "$temp_config_dir/docker compose.yml" "$INSTALL_PATH/" 2>/dev/null || true
    cp "$temp_config_dir/nginx.conf" "$INSTALL_PATH/" 2>/dev/null || true
    cp -r "$temp_config_dir/configs" "$INSTALL_PATH/" 2>/dev/null || true

    # Clean up
    rm -rf "$temp_config_dir"

    log_update "Source files updated successfully"
}

# Rebuild containers
rebuild_containers() {
    log_update "Rebuilding containers..."

    cd "$INSTALL_PATH"

    # Pull latest base images
    docker compose pull 2>/dev/null || true

    # Rebuild with no cache
    if docker compose build --no-cache --progress=plain; then
        log_update "Containers rebuilt successfully"
        return 0
    else
        log_update "ERROR: Container rebuild failed"
        return 1
    fi
}

# Start updated services
start_updated_services() {
    log_update "Starting updated services..."

    cd "$INSTALL_PATH"

    if docker compose up -d; then
        log_update "Services started successfully"
        return 0
    else
        log_update "ERROR: Failed to start services"
        return 1
    fi
}

# =============================================================================
# ROLLBACK FUNCTIONS
# =============================================================================

# Rollback to previous version
rollback_update() {
    local backup_dir="$1"

    log_update "Rolling back to previous version..."

    # Stop current services
    cd "$INSTALL_PATH"
    docker compose down --remove-orphans 2>/dev/null || true

    # Restore from backup
    if [[ -d "$backup_dir/current_installation" ]]; then
        # Remove current installation (except data)
        find "$INSTALL_PATH" -mindepth 1 -maxdepth 1 ! -name "data" ! -name "logs" ! -name "backups" -exec rm -rf {} \; 2>/dev/null || true

        # Restore from backup
        cp -r "$backup_dir/current_installation"/* "$INSTALL_PATH/"

        # Start services
        cd "$INSTALL_PATH"
        if docker compose up -d; then
            log_update "Rollback completed successfully"

            # Verify rollback
            if check_services_health; then
                log_update "Rollback verification successful"
                return 0
            else
                log_update "WARNING: Rollback completed but services are not healthy"
                return 1
            fi
        else
            log_update "ERROR: Failed to start services after rollback"
            return 1
        fi
    else
        log_update "ERROR: Backup directory not found for rollback"
        return 1
    fi
}

# =============================================================================
# MAIN UPDATE FUNCTION
# =============================================================================

# Perform update with rollback capability
perform_update() {
    local force_update="${1:-false}"

    log_update "Starting MediaButler update process"

    # Check current version
    local current_version=$(get_current_version)
    local latest_version=$(get_latest_version)

    log_update "Current version: $current_version"
    log_update "Latest version: $latest_version"

    # Check if update is needed
    if [[ "$current_version" == "$latest_version" && "$force_update" != "true" ]]; then
        log_update "Already running latest version - no update needed"
        echo "MediaButler is already up to date (version: $current_version)"
        return 0
    fi

    # Create temporary directory for update
    local temp_dir=$(mktemp -d)
    local backup_dir=$(mktemp -d)

    # Cleanup function
    cleanup() {
        rm -rf "$temp_dir" "$backup_dir" 2>/dev/null || true
    }
    trap cleanup EXIT

    # Step 1: Create pre-update backup
    if ! create_update_backup; then
        log_update "WARNING: Pre-update backup failed - continuing anyway"
    fi

    # Step 2: Download latest source
    local source_dir
    if ! source_dir=$(download_latest_source "$temp_dir"); then
        log_update "ERROR: Failed to download latest source"
        return 1
    fi

    # Step 3: Backup current installation
    backup_current_installation "$backup_dir"

    # Step 4: Check current service health before update
    log_update "Checking current service health..."
    if ! check_services_health; then
        log_update "WARNING: Services are not healthy before update"
    fi

    # Step 5: Update source files
    if ! update_source_files "$source_dir"; then
        log_update "ERROR: Failed to update source files"
        return 1
    fi

    # Step 6: Rebuild containers
    if ! rebuild_containers; then
        log_update "ERROR: Container rebuild failed - initiating rollback"
        rollback_update "$backup_dir"
        return 1
    fi

    # Step 7: Start updated services
    if ! start_updated_services; then
        log_update "ERROR: Failed to start updated services - initiating rollback"
        rollback_update "$backup_dir"
        return 1
    fi

    # Step 8: Verify update
    log_update "Verifying update..."
    if check_services_health; then
        # Update version info
        echo "$latest_version" > "$INSTALL_PATH/.version"
        log_update "Update completed successfully!"
        log_update "Updated from version $current_version to $latest_version"

        # Clean up old Docker images
        docker image prune -f >/dev/null 2>&1 || true

        return 0
    else
        log_update "ERROR: Update verification failed - initiating rollback"
        if rollback_update "$backup_dir"; then
            log_update "Rollback completed successfully"
            return 1
        else
            log_update "CRITICAL: Rollback failed - manual intervention required"
            return 2
        fi
    fi
}

# =============================================================================
# INFORMATION FUNCTIONS
# =============================================================================

# Show update status
show_update_status() {
    echo "MediaButler Update Status"
    echo "========================"

    local current_version=$(get_current_version)
    local latest_version=$(get_latest_version)

    echo "Repository: $GITHUB_REPO_URL"
    echo "Branch: $GITHUB_BRANCH"
    echo "Current Version: $current_version"
    echo "Latest Version: $latest_version"
    echo

    if [[ "$current_version" == "$latest_version" ]]; then
        echo "✅ MediaButler is up to date"
    else
        echo "📦 Update available: $current_version → $latest_version"
    fi
    echo

    # Show recent update history
    if [[ -f "$LOG_FILE" ]]; then
        echo "Recent Update History:"
        echo "====================="
        grep "Update completed successfully\|Rollback completed successfully" "$LOG_FILE" | tail -5 || echo "No update history found"
    fi
    echo
}

# =============================================================================
# SCRIPT EXECUTION
# =============================================================================

# Handle command line arguments
case "${1:-}" in
    --check)
        show_update_status
        ;;
    --force)
        perform_update "true"
        ;;
    --rollback)
        if [[ -n "${2:-}" ]]; then
            rollback_update "$2"
        else
            echo "Usage: $0 --rollback <backup_directory>"
            exit 1
        fi
        ;;
    --status)
        show_update_status
        ;;
    --help)
        cat << EOF
MediaButler Update Script
========================

Usage: $0 [OPTIONS]

Options:
  --check     Check for available updates
  --force     Force update even if already up to date
  --rollback  Rollback to backup (requires backup directory)
  --status    Show current update status
  --help      Show this help message

Examples:
  $0                          # Check and perform update if available
  $0 --check                  # Check for updates only
  $0 --force                  # Force update regardless of current version
  $0 --status                 # Show update status

The update process includes:
- Pre-update backup creation
- Source code download and verification
- Container rebuilding with latest code
- Service health verification
- Automatic rollback on failure

Repository: $GITHUB_REPO_URL
Branch: $GITHUB_BRANCH
Log File: $LOG_FILE
EOF
        ;;
    "")
        perform_update "false"
        ;;
    *)
        echo "Unknown option: $1"
        echo "Use --help for usage information"
        exit 1
        ;;
esac