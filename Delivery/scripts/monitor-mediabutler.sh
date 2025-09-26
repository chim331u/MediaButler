#!/bin/bash
# MediaButler Health Monitor for QNAP NAS
# Comprehensive monitoring script with automated recovery
# Version: 1.0.0

set -euo pipefail

# =============================================================================
# CONFIGURATION
# =============================================================================

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_PATH="$(dirname "$SCRIPT_DIR")"
LOG_FILE="$INSTALL_PATH/logs/monitor.log"
ALERT_LOG="$INSTALL_PATH/logs/alerts.log"
CONFIG_FILE="$INSTALL_PATH/.env"

# Load configuration
if [[ -f "$CONFIG_FILE" ]]; then
    source "$CONFIG_FILE"
fi

# Monitoring settings
MAX_LOG_SIZE_MB=10
MEMORY_THRESHOLD_MB=300
DISK_THRESHOLD_PERCENT=85
API_TIMEOUT=10
WEB_TIMEOUT=10
RESTART_COOLDOWN=60
MAX_RESTART_ATTEMPTS=3

# Health check URLs
API_PORT="${API_PORT:-5000}"
WEB_PORT="${WEB_PORT:-3000}"
PROXY_PORT="${PROXY_PORT:-80}"

# =============================================================================
# LOGGING FUNCTIONS
# =============================================================================

# Ensure log directories exist
mkdir -p "$(dirname "$LOG_FILE")" "$(dirname "$ALERT_LOG")"

# Logging functions
log_monitor() {
    local level="$1"
    shift
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] [$level] $*" | tee -a "$LOG_FILE"
}

log_info() { log_monitor "INFO" "$@"; }
log_warn() { log_monitor "WARN" "$@"; }
log_error() { log_monitor "ERROR" "$@"; }
log_alert() {
    log_monitor "ALERT" "$@"
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] ALERT: $*" >> "$ALERT_LOG"
}

# =============================================================================
# UTILITY FUNCTIONS
# =============================================================================

# Rotate logs if they get too large
rotate_logs() {
    for log_file in "$LOG_FILE" "$ALERT_LOG"; do
        if [[ -f "$log_file" ]]; then
            local size_bytes=$(stat -f%z "$log_file" 2>/dev/null || stat -c%s "$log_file" 2>/dev/null || echo 0)
            local size_mb=$((size_bytes / 1024 / 1024))

            if [[ $size_mb -gt $MAX_LOG_SIZE_MB ]]; then
                mv "$log_file" "${log_file}.old"
                touch "$log_file"
                log_info "Log rotated: $log_file (was ${size_mb}MB)"
            fi
        fi
    done
}

# Get container status
get_container_status() {
    local container_name="$1"
    docker inspect --format='{{.State.Status}}' "$container_name" 2>/dev/null || echo "not_found"
}

# Get container health
get_container_health() {
    local container_name="$1"
    docker inspect --format='{{.State.Health.Status}}' "$container_name" 2>/dev/null || echo "no_health_check"
}

# Check if service is responding
check_service_response() {
    local url="$1"
    local timeout="$2"

    if curl -sf --max-time "$timeout" "$url" >/dev/null 2>&1; then
        return 0
    else
        return 1
    fi
}

# =============================================================================
# HEALTH CHECK FUNCTIONS
# =============================================================================

# Check Docker daemon
check_docker() {
    if ! docker info >/dev/null 2>&1; then
        log_error "Docker daemon not responding"
        return 1
    fi
    return 0
}

# Check container status
check_containers() {
    local issues=0
    local containers=("mediabutler-api" "mediabutler-web" "mediabutler-proxy")

    cd "$INSTALL_PATH" || return 1

    for container in "${containers[@]}"; do
        local status=$(get_container_status "$container")
        local health=$(get_container_health "$container")

        case "$status" in
            "running")
                if [[ "$health" == "unhealthy" ]]; then
                    log_warn "Container $container is running but unhealthy"
                    ((issues++))
                elif [[ "$health" == "healthy" || "$health" == "no_health_check" ]]; then
                    log_info "Container $container is healthy"
                else
                    log_warn "Container $container health status: $health"
                    ((issues++))
                fi
                ;;
            "exited"|"dead")
                log_error "Container $container is not running (status: $status)"
                ((issues++))
                ;;
            "not_found")
                log_error "Container $container not found"
                ((issues++))
                ;;
            *)
                log_warn "Container $container has unexpected status: $status"
                ((issues++))
                ;;
        esac
    done

    return $issues
}

# Check service endpoints
check_endpoints() {
    local issues=0

    # Check API health endpoint
    if check_service_response "http://localhost:$API_PORT/health" "$API_TIMEOUT"; then
        log_info "API endpoint responding"
    else
        log_error "API health check failed (http://localhost:$API_PORT/health)"
        ((issues++))
    fi

    # Check Web endpoint
    if check_service_response "http://localhost:$WEB_PORT" "$WEB_TIMEOUT"; then
        log_info "Web endpoint responding"
    else
        log_error "Web health check failed (http://localhost:$WEB_PORT)"
        ((issues++))
    fi

    # Check Proxy endpoint
    if check_service_response "http://localhost:$PROXY_PORT/health" "$API_TIMEOUT"; then
        log_info "Proxy endpoint responding"
    else
        log_error "Proxy health check failed (http://localhost:$PROXY_PORT/health)"
        ((issues++))
    fi

    return $issues
}

# Check resource usage
check_resources() {
    local issues=0

    # Memory usage check
    local memory_usage
    if command -v docker >/dev/null 2>&1; then
        memory_usage=$(docker stats --no-stream --format "{{.MemUsage}}" 2>/dev/null | \
                      grep -oE '[0-9]+(\.[0-9]+)?MiB' | \
                      sed 's/MiB//' | \
                      awk '{sum+=$1} END {print int(sum)}')

        if [[ -n "$memory_usage" && $memory_usage -gt $MEMORY_THRESHOLD_MB ]]; then
            log_warn "High memory usage: ${memory_usage}MB (threshold: ${MEMORY_THRESHOLD_MB}MB)"
            ((issues++))
        else
            log_info "Memory usage: ${memory_usage:-unknown}MB"
        fi
    fi

    # Disk usage check
    local disk_usage
    disk_usage=$(df -h "$INSTALL_PATH" 2>/dev/null | awk 'NR==2 {print $5}' | sed 's/%//')

    if [[ -n "$disk_usage" && $disk_usage -gt $DISK_THRESHOLD_PERCENT ]]; then
        log_warn "High disk usage: ${disk_usage}% (threshold: ${DISK_THRESHOLD_PERCENT}%)"
        ((issues++))
    else
        log_info "Disk usage: ${disk_usage:-unknown}%"
    fi

    # Load average check (optional)
    local load_avg
    load_avg=$(uptime | grep -oE 'load average[s]?: [0-9]+(\.[0-9]+)?' | grep -oE '[0-9]+(\.[0-9]+)?')
    if [[ -n "$load_avg" ]]; then
        log_info "System load: $load_avg"

        # Alert if load is very high (above 2.0 on single core)
        if (( $(echo "$load_avg > 2.0" | bc -l 2>/dev/null || echo 0) )); then
            log_warn "High system load: $load_avg"
            ((issues++))
        fi
    fi

    return $issues
}

# Check log file sizes and errors
check_logs() {
    local issues=0
    local log_dirs=("$INSTALL_PATH/logs/api" "$INSTALL_PATH/logs/web" "$INSTALL_PATH/logs/nginx")

    for log_dir in "${log_dirs[@]}"; do
        if [[ -d "$log_dir" ]]; then
            # Check for recent errors in logs
            local error_count
            error_count=$(find "$log_dir" -name "*.log" -mtime -1 -exec grep -i "error\|exception\|fatal" {} \; 2>/dev/null | wc -l)

            if [[ $error_count -gt 10 ]]; then
                log_warn "High error count in $log_dir: $error_count errors in last 24h"
                ((issues++))
            fi

            # Check log file sizes
            local large_logs
            large_logs=$(find "$log_dir" -name "*.log" -size +50M 2>/dev/null | wc -l)

            if [[ $large_logs -gt 0 ]]; then
                log_warn "Large log files detected in $log_dir: $large_logs files over 50MB"
                ((issues++))
            fi
        fi
    done

    return $issues
}

# =============================================================================
# RECOVERY FUNCTIONS
# =============================================================================

# Restart specific container
restart_container() {
    local container="$1"
    log_info "Restarting container: $container"

    cd "$INSTALL_PATH" || return 1

    if docker-compose restart "$container"; then
        log_info "Successfully restarted $container"
        return 0
    else
        log_error "Failed to restart $container"
        return 1
    fi
}

# Restart all services
restart_all_services() {
    log_info "Restarting all MediaButler services"

    cd "$INSTALL_PATH" || return 1

    if docker-compose restart; then
        log_info "Successfully restarted all services"
        return 0
    else
        log_error "Failed to restart services"
        return 1
    fi
}

# Clean up resources
cleanup_resources() {
    log_info "Performing resource cleanup"

    # Clean up Docker resources
    docker system prune -f >/dev/null 2>&1 || true

    # Clean up old log files
    find "$INSTALL_PATH/logs" -name "*.log.*" -mtime +7 -delete 2>/dev/null || true

    # Clean up temporary files
    find /tmp -name "*mediabutler*" -mtime +1 -delete 2>/dev/null || true

    log_info "Resource cleanup completed"
}

# Emergency recovery
emergency_recovery() {
    log_alert "Initiating emergency recovery"

    cd "$INSTALL_PATH" || return 1

    # Stop all services
    docker-compose down --remove-orphans 2>/dev/null || true

    # Clean up resources
    cleanup_resources

    # Wait before restart
    sleep 10

    # Start services
    if docker-compose up -d; then
        log_info "Emergency recovery completed successfully"
        return 0
    else
        log_error "Emergency recovery failed"
        return 1
    fi
}

# =============================================================================
# NOTIFICATION FUNCTIONS
# =============================================================================

# Send alert notification (placeholder for future webhook/email integration)
send_alert() {
    local severity="$1"
    local message="$2"

    log_alert "[$severity] $message"

    # Future: Add webhook notification
    # curl -X POST "https://hooks.slack.com/..." -d "{\"text\":\"MediaButler Alert: $message\"}"

    # Future: Add email notification
    # echo "$message" | mail -s "MediaButler Alert" admin@domain.com
}

# =============================================================================
# MAIN MONITORING FUNCTION
# =============================================================================

# Track restart attempts
RESTART_ATTEMPTS_FILE="$INSTALL_PATH/logs/.restart_attempts"

get_restart_attempts() {
    if [[ -f "$RESTART_ATTEMPTS_FILE" ]]; then
        local last_restart=$(cat "$RESTART_ATTEMPTS_FILE")
        local current_time=$(date +%s)
        local time_diff=$((current_time - last_restart))

        # Reset counter if more than 1 hour has passed
        if [[ $time_diff -gt 3600 ]]; then
            echo 0
        else
            local attempts_today=$(grep -c "$(date +%Y-%m-%d)" "$LOG_FILE" 2>/dev/null | grep "Restarting" || echo 0)
            echo "$attempts_today"
        fi
    else
        echo 0
    fi
}

record_restart_attempt() {
    date +%s > "$RESTART_ATTEMPTS_FILE"
}

# Main monitoring function
main() {
    local start_time=$(date +%s)
    rotate_logs

    log_info "Starting health check"

    local total_issues=0
    local critical_issues=0

    # Check Docker daemon
    if ! check_docker; then
        log_alert "Docker daemon issues detected"
        send_alert "CRITICAL" "Docker daemon not responding"
        return 1
    fi

    # Check containers
    if ! check_containers; then
        local container_issues=$?
        total_issues=$((total_issues + container_issues))
        if [[ $container_issues -gt 1 ]]; then
            critical_issues=$((critical_issues + 1))
        fi
    fi

    # Check service endpoints
    if ! check_endpoints; then
        local endpoint_issues=$?
        total_issues=$((total_issues + endpoint_issues))
        if [[ $endpoint_issues -gt 1 ]]; then
            critical_issues=$((critical_issues + 1))
        fi
    fi

    # Check resources
    if ! check_resources; then
        local resource_issues=$?
        total_issues=$((total_issues + resource_issues))
    fi

    # Check logs
    if ! check_logs; then
        local log_issues=$?
        total_issues=$((total_issues + log_issues))
    fi

    # Determine recovery action
    if [[ $total_issues -eq 0 ]]; then
        log_info "All services healthy - no action required"
    else
        log_warn "Detected $total_issues issues ($critical_issues critical)"

        local restart_attempts=$(get_restart_attempts)

        if [[ $restart_attempts -ge $MAX_RESTART_ATTEMPTS ]]; then
            log_alert "Maximum restart attempts reached ($restart_attempts) - manual intervention required"
            send_alert "CRITICAL" "MediaButler requires manual intervention - max restart attempts exceeded"
        elif [[ $critical_issues -gt 1 ]]; then
            log_alert "Multiple critical issues detected - initiating emergency recovery"
            send_alert "CRITICAL" "MediaButler emergency recovery initiated"

            if emergency_recovery; then
                record_restart_attempt
                sleep 30

                # Recheck after recovery
                if check_endpoints; then
                    log_info "Emergency recovery successful"
                    send_alert "INFO" "MediaButler emergency recovery completed successfully"
                else
                    log_error "Emergency recovery failed"
                    send_alert "CRITICAL" "MediaButler emergency recovery failed - manual intervention required"
                fi
            fi
        elif [[ $total_issues -gt 0 ]]; then
            log_info "Attempting service restart"

            if restart_all_services; then
                record_restart_attempt
                sleep $RESTART_COOLDOWN

                # Recheck after restart
                if check_endpoints; then
                    log_info "Service restart successful"
                    send_alert "INFO" "MediaButler services restarted successfully"
                else
                    log_error "Service restart failed to resolve issues"
                    send_alert "WARNING" "MediaButler restart completed but issues persist"
                fi
            else
                log_error "Service restart failed"
                send_alert "CRITICAL" "MediaButler service restart failed"
            fi
        fi
    fi

    local end_time=$(date +%s)
    local duration=$((end_time - start_time))
    log_info "Health check completed in ${duration}s"

    return $total_issues
}

# =============================================================================
# SCRIPT EXECUTION
# =============================================================================

# Handle script arguments
case "${1:-}" in
    --status)
        cd "$INSTALL_PATH"
        echo "MediaButler Status Report"
        echo "========================="
        docker-compose ps
        echo
        echo "Resource Usage:"
        docker stats --no-stream --format "table {{.Container}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.NetIO}}"
        ;;
    --logs)
        tail -f "$LOG_FILE"
        ;;
    --alerts)
        tail -f "$ALERT_LOG"
        ;;
    --cleanup)
        cleanup_resources
        ;;
    --help)
        echo "MediaButler Health Monitor"
        echo "Usage: $0 [--status|--logs|--alerts|--cleanup|--help]"
        echo
        echo "Options:"
        echo "  --status   Show current service status"
        echo "  --logs     Show monitoring logs (tail)"
        echo "  --alerts   Show alert logs (tail)"
        echo "  --cleanup  Perform resource cleanup"
        echo "  --help     Show this help message"
        echo
        echo "When run without arguments, performs health check and recovery if needed"
        ;;
    *)
        # Run main monitoring
        main
        ;;
esac