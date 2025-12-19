#!/bin/bash
# Initialize batch processing tables in SQLite database
# This script creates the batch_jobs and batch_job_items tables if they don't exist

set -e  # Exit on error

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Read database path from config.json using grep/sed (portable)
CONFIG_FILE="${PROJECT_ROOT}/configs/config.json"
DB_PATH_FROM_CONFIG=$(grep -A 1 '"database"' "$CONFIG_FILE" | grep '"path"' | sed 's/.*"path": *"\([^"]*\)".*/\1/' | head -1)

# Resolve relative path from PROJECT_ROOT
DB_PATH="${PROJECT_ROOT}/${DB_PATH_FROM_CONFIG}"
SCHEMA_FILE="${PROJECT_ROOT}/internal/db/queries/schema_batch.sql"

echo "===================================="
echo "MediaButler Go API - Database Init"
echo "===================================="
echo ""
echo "Database: ${DB_PATH}"
echo "Schema:   ${SCHEMA_FILE}"
echo ""

# Check if database file exists
if [ ! -f "$DB_PATH" ]; then
    echo "❌ Database file not found: ${DB_PATH}"
    echo ""
    echo "Please ensure the .NET API has been run at least once to create the main database."
    exit 1
fi

# Check if schema file exists
if [ ! -f "$SCHEMA_FILE" ]; then
    echo "❌ Schema file not found: ${SCHEMA_FILE}"
    exit 1
fi

# Check if batch_jobs table already exists
TABLE_EXISTS=$(sqlite3 "$DB_PATH" "SELECT name FROM sqlite_master WHERE type='table' AND name='batch_jobs';" 2>/dev/null || echo "")

if [ -n "$TABLE_EXISTS" ]; then
    echo "✅ Batch processing tables already exist. No action needed."
    exit 0
fi

echo "📋 Creating batch processing tables..."
echo ""

# Execute schema
if sqlite3 "$DB_PATH" < "$SCHEMA_FILE" 2>&1; then
    echo ""
    echo "✅ Batch processing tables created successfully!"
    echo ""
    echo "Tables created:"
    echo "  - batch_jobs"
    echo "  - batch_job_items"
    echo ""
    echo "Indexes created:"
    echo "  - idx_batch_jobs_status_queued"
    echo "  - idx_batch_jobs_next_retry"
    echo "  - idx_batch_jobs_queued_at"
    echo "  - idx_batch_job_items_batch_id"
    echo "  - idx_batch_job_items_batch_status"
    echo "  - idx_batch_job_items_file_hash"
else
    echo ""
    echo "❌ Failed to create batch processing tables"
    exit 1
fi

echo ""
echo "Database initialization complete! 🚀"
