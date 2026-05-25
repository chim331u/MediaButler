#!/usr/bin/env bash

# ==============================================================================
# MediaButler Native Service Controller for QNAP QTS
# ==============================================================================
# A lightweight init-like controller to manage the MediaButler binary natively.
# Designed for QTS busybox environments where systemd is not present.
# ==============================================================================

# Configurations
SERVICE_NAME="MediaButler"
BINARY_PATH="./build/mediabutler-qnap" # Adjust to absolute path on QNAP if needed
LOG_FILE="./data/mediabutler.log"
PID_FILE="./data/mediabutler.pid"

# Export Environment Configurations (CGO-free execution)
export PORT=8080
export DATABASE_PATH="./data/mediabutler.db"
export DEST_FOLDER="./dest"
export WATCH_FOLDERS="./watch"
export LOG_LEVEL=info
export ML_THRESHOLD=0.85

# Ensure directory structure exists
mkdir -p "$(dirname "${LOG_FILE}")"
mkdir -p "${DEST_FOLDER}"
mkdir -p "${WATCH_FOLDERS}"

log_msg() {
    echo -e "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

start_service() {
    if [ -f "${PID_FILE}" ]; then
        PID=$(cat "${PID_FILE}")
        if kill -0 "${PID}" 2>/dev/null; then
            log_msg "${SERVICE_NAME} is already running with PID ${PID}."
            return 0
        else
            log_msg "Found stale PID file, removing."
            rm -f "${PID_FILE}"
        fi
    fi

    if [ ! -f "${BINARY_PATH}" ]; then
        log_msg "Error: Binary not found at ${BINARY_PATH}."
        log_msg "Please make sure you have compiled the binary and set the correct BINARY_PATH."
        return 1
    fi

    # Make sure binary is executable
    chmod +x "${BINARY_PATH}"

    log_msg "Starting ${SERVICE_NAME} in background..."
    nohup "${BINARY_PATH}" >> "${LOG_FILE}" 2>&1 &
    
    PID=$!
    echo "${PID}" > "${PID_FILE}"
    
    # Confirm startup
    sleep 1
    if kill -0 "${PID}" 2>/dev/null; then
        log_msg "${SERVICE_NAME} started successfully (PID: ${PID})."
        log_msg "Logs are written to: ${LOG_FILE}"
    else
        log_msg "Error: Service failed to start. Check logs at ${LOG_FILE}."
        rm -f "${PID_FILE}"
        return 1
    fi
}

stop_service() {
    if [ -f "${PID_FILE}" ]; then
        PID=$(cat "${PID_FILE}")
        if kill -0 "${PID}" 2>/dev/null; then
            log_msg "Stopping ${SERVICE_NAME} (PID: ${PID})..."
            kill -15 "${PID}" # Send SIGTERM for graceful shutdown
            
            # Wait for shutdown
            for i in {1..10}; do
                if ! kill -0 "${PID}" 2>/dev/null; then
                    break
                fi
                sleep 0.5
            done
            
            if kill -0 "${PID}" 2>/dev/null; then
                log_msg "Service did not stop, forcing termination..."
                kill -9 "${PID}"
            fi
            
            log_msg "${SERVICE_NAME} stopped."
        else
            log_msg "${SERVICE_NAME} is not running, but PID file exists. Cleaning up."
        fi
        rm -f "${PID_FILE}"
    else
        log_msg "${SERVICE_NAME} is not running."
    fi
}

status_service() {
    if [ -f "${PID_FILE}" ]; then
        PID=$(cat "${PID_FILE}")
        if kill -0 "${PID}" 2>/dev/null; then
            log_msg "${SERVICE_NAME} is running (PID: ${PID})."
            return 0
        else
            log_msg "${SERVICE_NAME} is stopped (stale PID file exists)."
            return 1
        fi
    else
        log_msg "${SERVICE_NAME} is stopped."
        return 3
    fi
}

case "$1" in
    start)
        start_service
        ;;
    stop)
        stop_service
        ;;
    restart)
        stop_service
        start_service
        ;;
    status)
        status_service
        ;;
    *)
        echo "Usage: $0 {start|stop|restart|status}"
        exit 1
        ;;
esac
