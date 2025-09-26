#!/bin/bash
# MediaButler Backup Script for QNAP NAS
# Comprehensive backup solution with rotation and verification
# Version: 1.0.0

set -euo pipefail

# =============================================================================
# CONFIGURATION
# =============================================================================

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_PATH="$(dirname "$SCRIPT_DIR")"
BACKUP_DIR="$INSTALL_PATH/backups"
LOG_FILE="$INSTALL_PATH/logs/backup.log"
CONFIG_FILE="$INSTALL_PATH/.env"

# Backup settings
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_PREFIX="mediabutler_backup"
BACKUP_FILE="${BACKUP_PREFIX}_${TIMESTAMP}.tar.gz"
MAX_BACKUPS=5
COMPRESSION_LEVEL=6

# Load environment configuration
if [[ -f "$CONFIG_FILE" ]]; then
    source "$CONFIG_FILE"
fi

# =============================================================================
# LOGGING
# =============================================================================

# Ensure log directory exists
mkdir -p "$(dirname "$LOG_FILE")" "$BACKUP_DIR"

# Logging function
log_backup() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

# =============================================================================
# BACKUP FUNCTIONS
# =============================================================================

# Create configuration backup
backup_configuration() {
    log_backup "Creating configuration backup..."

    local config_backup="$BACKUP_DIR/config_${TIMESTAMP}.tar.gz"

    tar -czf "$config_backup" \
        -C "$INSTALL_PATH" \
        --exclude='./logs' \
        --exclude='./backups' \
        --exclude='./data/temp' \
        .env \
        docker-compose.yml \
        nginx.conf \
        configs/ \
        2>/dev/null || true

    if [[ -f "$config_backup" ]]; then
        log_backup "Configuration backup created: $config_backup"
        echo "$config_backup"
    else
        log_backup "WARNING: Configuration backup failed"
        return 1
    fi
}

# Create data backup
backup_data() {
    log_backup "Creating data backup..."

    local data_backup="$BACKUP_DIR/data_${TIMESTAMP}.tar.gz"

    # Check if data directory exists and has content
    if [[ -d "$INSTALL_PATH/data" ]] && [[ $(find "$INSTALL_PATH/data" -type f | wc -l) -gt 0 ]]; then
        tar -czf "$data_backup" \
            -C "$INSTALL_PATH" \
            --exclude='./data/temp' \
            --exclude='./data/watch' \
            --exclude='./data/*.tmp' \
            data/ \
            2>/dev/null

        if [[ -f "$data_backup" ]]; then
            log_backup "Data backup created: $data_backup"
            echo "$data_backup"
        else
            log_backup "WARNING: Data backup failed"
            return 1
        fi
    else
        log_backup "INFO: No data to backup (empty or missing data directory)"
        return 0
    fi
}

# Create complete backup
create_full_backup() {
    log_backup "Creating complete backup: $BACKUP_FILE"

    local temp_dir=$(mktemp -d)
    local backup_path="$BACKUP_DIR/$BACKUP_FILE"

    # Create backup structure
    mkdir -p "$temp_dir/mediabutler"

    # Copy configuration files
    cp -r "$INSTALL_PATH"/{.env,docker-compose.yml,nginx.conf} "$temp_dir/mediabutler/" 2>/dev/null || true
    cp -r "$INSTALL_PATH/configs" "$temp_dir/mediabutler/" 2>/dev/null || true
    cp -r "$INSTALL_PATH/docker" "$temp_dir/mediabutler/" 2>/dev/null || true

    # Copy data (excluding temporary files)
    if [[ -d "$INSTALL_PATH/data" ]]; then
        mkdir -p "$temp_dir/mediabutler/data"
        rsync -av \
            --exclude='temp/' \
            --exclude='watch/' \
            --exclude='*.tmp' \
            --exclude='*.lock' \
            "$INSTALL_PATH/data/" "$temp_dir/mediabutler/data/" 2>/dev/null || true
    fi

    # Copy models
    if [[ -d "$INSTALL_PATH/models" ]]; then
        cp -r "$INSTALL_PATH/models" "$temp_dir/mediabutler/" 2>/dev/null || true
    fi

    # Create backup metadata
    cat > "$temp_dir/mediabutler/backup_info.txt" << EOF
MediaButler Backup Information
=============================
Backup Date: $(date)
Backup Version: 1.0.0
Source Path: $INSTALL_PATH
Backup Type: Full System Backup
Host: $(hostname)
Architecture: $(uname -m)
Docker Version: $(docker --version 2>/dev/null || echo "Unknown")

Included Components:
- Configuration files (.env, docker-compose.yml, nginx.conf)
- Application data (database, logs)
- ML models
- Docker configurations
- Custom configurations

Excluded Components:
- Temporary files
- Watch folder contents
- Log files
- Backup files
EOF

    # Create compressed backup
    if tar -czf "$backup_path" -C "$temp_dir" mediabutler/; then
        local backup_size=$(du -h "$backup_path" | cut -f1)
        log_backup "Full backup created successfully: $backup_path ($backup_size)"

        # Clean up temporary directory
        rm -rf "$temp_dir"

        # Verify backup
        if verify_backup "$backup_path"; then
            log_backup "Backup verification successful"
            echo "$backup_path"
        else
            log_backup "WARNING: Backup verification failed"
            return 1
        fi
    else
        log_backup "ERROR: Failed to create backup"
        rm -rf "$temp_dir"
        return 1
    fi
}

# Verify backup integrity
verify_backup() {
    local backup_file="$1"

    log_backup "Verifying backup: $(basename "$backup_file")"

    # Check if file exists and is not empty
    if [[ ! -f "$backup_file" ]] || [[ ! -s "$backup_file" ]]; then
        log_backup "ERROR: Backup file is missing or empty"
        return 1
    fi

    # Test archive integrity
    if tar -tzf "$backup_file" >/dev/null 2>&1; then
        log_backup "Archive integrity check passed"
    else
        log_backup "ERROR: Archive is corrupted"
        return 1
    fi

    # Check for essential files
    local essential_files=("mediabutler/.env" "mediabutler/docker-compose.yml")
    for file in "${essential_files[@]}"; do
        if tar -tzf "$backup_file" | grep -q "^$file$"; then
            log_backup "Essential file found: $file"
        else
            log_backup "WARNING: Essential file missing: $file"
        fi
    done

    return 0
}

# Clean up old backups
cleanup_old_backups() {
    log_backup "Cleaning up old backups (keeping $MAX_BACKUPS most recent)"

    cd "$BACKUP_DIR"

    # Remove old full backups
    if ls ${BACKUP_PREFIX}_*.tar.gz >/dev/null 2>&1; then
        ls -t ${BACKUP_PREFIX}_*.tar.gz | tail -n +$((MAX_BACKUPS + 1)) | xargs rm -f 2>/dev/null || true
    fi

    # Remove old config backups
    if ls config_*.tar.gz >/dev/null 2>&1; then
        ls -t config_*.tar.gz | tail -n +$((MAX_BACKUPS + 1)) | xargs rm -f 2>/dev/null || true
    fi

    # Remove old data backups
    if ls data_*.tar.gz >/dev/null 2>&1; then
        ls -t data_*.tar.gz | tail -n +$((MAX_BACKUPS + 1)) | xargs rm -f 2>/dev/null || true
    fi

    # Clean up any backups older than 30 days
    find "$BACKUP_DIR" -name "*.tar.gz" -mtime +30 -delete 2>/dev/null || true

    log_backup "Backup cleanup completed"
}

# =============================================================================
# RESTORE FUNCTIONS
# =============================================================================

# List available backups
list_backups() {
    echo "Available MediaButler Backups:"
    echo "============================="

    if [[ -d "$BACKUP_DIR" ]] && [[ $(find "$BACKUP_DIR" -name "*.tar.gz" | wc -l) -gt 0 ]]; then
        echo
        echo "Full System Backups:"
        ls -lah "$BACKUP_DIR"/${BACKUP_PREFIX}_*.tar.gz 2>/dev/null | awk '{print $9, "(" $5 ")", $6, $7, $8}' || echo "None found"

        echo
        echo "Configuration Backups:"
        ls -lah "$BACKUP_DIR"/config_*.tar.gz 2>/dev/null | awk '{print $9, "(" $5 ")", $6, $7, $8}' || echo "None found"

        echo
        echo "Data Backups:"
        ls -lah "$BACKUP_DIR"/data_*.tar.gz 2>/dev/null | awk '{print $9, "(" $5 ")", $6, $7, $8}' || echo "None found"
    else
        echo "No backups found in $BACKUP_DIR"
    fi
    echo
}

# Restore from backup
restore_backup() {
    local backup_file="$1"
    local restore_path="${2:-$INSTALL_PATH}"

    if [[ ! -f "$backup_file" ]]; then
        log_backup "ERROR: Backup file not found: $backup_file"
        return 1
    fi

    log_backup "Restoring from backup: $(basename "$backup_file")"
    log_backup "Restore destination: $restore_path"

    # Verify backup before restore
    if ! verify_backup "$backup_file"; then
        log_backup "ERROR: Backup verification failed - restore aborted"
        return 1
    fi

    # Stop services before restore
    log_backup "Stopping MediaButler services..."
    cd "$INSTALL_PATH"
    docker compose down --remove-orphans 2>/dev/null || true

    # Create restore point
    local restore_backup="$BACKUP_DIR/pre_restore_$(date +%Y%m%d_%H%M%S).tar.gz"
    log_backup "Creating restore point: $restore_backup"
    tar -czf "$restore_backup" -C "$(dirname "$INSTALL_PATH")" "$(basename "$INSTALL_PATH")" 2>/dev/null || true

    # Extract backup
    log_backup "Extracting backup..."
    if tar -xzf "$backup_file" -C "$(dirname "$restore_path")" --strip-components=1; then
        log_backup "Backup extracted successfully"

        # Restart services
        log_backup "Starting MediaButler services..."
        cd "$restore_path"
        if docker compose up -d; then
            log_backup "Restore completed successfully"
            return 0
        else
            log_backup "ERROR: Failed to start services after restore"
            return 1
        fi
    else
        log_backup "ERROR: Failed to extract backup"
        return 1
    fi
}

# =============================================================================
# MAIN FUNCTIONS
# =============================================================================

# Show backup status
show_status() {
    echo "MediaButler Backup Status"
    echo "========================"
    echo "Backup Directory: $BACKUP_DIR"
    echo "Log File: $LOG_FILE"
    echo "Max Backups Retained: $MAX_BACKUPS"
    echo

    if [[ -d "$BACKUP_DIR" ]]; then
        local backup_count=$(find "$BACKUP_DIR" -name "*.tar.gz" | wc -l)
        local total_size=$(du -sh "$BACKUP_DIR" 2>/dev/null | cut -f1 || echo "Unknown")

        echo "Total Backups: $backup_count"
        echo "Total Size: $total_size"
        echo

        if [[ $backup_count -gt 0 ]]; then
            echo "Recent Backups:"
            ls -lah "$BACKUP_DIR"/*.tar.gz 2>/dev/null | tail -5 | awk '{print $9, "(" $5 ")", $6, $7, $8}'
        fi
    else
        echo "Backup directory does not exist"
    fi
    echo
}

# Main backup function
main() {
    local backup_type="${1:-full}"

    log_backup "Starting MediaButler backup (type: $backup_type)"

    case "$backup_type" in
        "config")
            if backup_configuration; then
                log_backup "Configuration backup completed successfully"
            else
                log_backup "Configuration backup failed"
                return 1
            fi
            ;;
        "data")
            if backup_data; then
                log_backup "Data backup completed successfully"
            else
                log_backup "Data backup failed"
                return 1
            fi
            ;;
        "full"|*)
            if create_full_backup; then
                log_backup "Full backup completed successfully"
                cleanup_old_backups
            else
                log_backup "Full backup failed"
                return 1
            fi
            ;;
    esac

    log_backup "Backup operation completed"
}

# =============================================================================
# SCRIPT EXECUTION
# =============================================================================

# Handle command line arguments
case "${1:-}" in
    --config)
        main "config"
        ;;
    --data)
        main "data"
        ;;
    --full|"")
        main "full"
        ;;
    --list)
        list_backups
        ;;
    --status)
        show_status
        ;;
    --restore)
        if [[ -n "${2:-}" ]]; then
            restore_backup "$2" "${3:-}"
        else
            echo "Usage: $0 --restore <backup_file> [restore_path]"
            exit 1
        fi
        ;;
    --cleanup)
        cleanup_old_backups
        ;;
    --help)
        cat << EOF
MediaButler Backup Script
========================

Usage: $0 [OPTIONS]

Options:
  --full      Create full system backup (default)
  --config    Create configuration backup only
  --data      Create data backup only
  --list      List available backups
  --status    Show backup status and statistics
  --restore   Restore from backup file
  --cleanup   Clean up old backups
  --help      Show this help message

Examples:
  $0                                    # Create full backup
  $0 --config                          # Backup configuration only
  $0 --list                            # List all backups
  $0 --restore backup_file.tar.gz      # Restore from backup
  $0 --status                          # Show backup status

Backup Location: $BACKUP_DIR
Log File: $LOG_FILE
EOF
        ;;
    *)
        echo "Unknown option: $1"
        echo "Use --help for usage information"
        exit 1
        ;;
esac