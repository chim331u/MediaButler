#!/bin/bash
# Apply ML schema migration to MediaButler database
# Creates tables for Go Naive Bayes training system

set -e  # Exit on error

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Read database path from config.json
CONFIG_FILE="${PROJECT_ROOT}/configs/config.json"
DB_PATH_FROM_CONFIG=$(grep -A 1 '"database"' "$CONFIG_FILE" | grep '"path"' | sed 's/.*"path": *"\([^"]*\)".*/\1/' | head -1)

# Resolve relative path from PROJECT_ROOT
DB_PATH="${PROJECT_ROOT}/${DB_PATH_FROM_CONFIG}"
SCHEMA_FILE="${PROJECT_ROOT}/pkg/ml/db/migrations/001_ml_schema.sql"

echo "========================================"
echo "MediaButler Go API - ML Schema Migration"
echo "========================================"
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

# Check if ML schema is already applied
SCHEMA_EXISTS=$(sqlite3 "$DB_PATH" "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ml_samples';" 2>/dev/null || echo "0")

if [ "$SCHEMA_EXISTS" = "1" ]; then
    echo "ℹ️  ML schema already applied. Checking version..."

    VERSION=$(sqlite3 "$DB_PATH" "SELECT version FROM ml_schema_version ORDER BY version DESC LIMIT 1;" 2>/dev/null || echo "0")
    echo "   Current schema version: ${VERSION}"
    echo ""
    read -p "Do you want to re-apply the schema? (y/N): " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Skipping migration."
        exit 0
    fi
fi

echo "📋 Applying ML schema migration..."
echo ""

# Apply schema
if sqlite3 "$DB_PATH" < "$SCHEMA_FILE" 2>&1; then
    echo ""
    echo "✅ ML schema applied successfully!"
    echo ""
    echo "Tables created:"
    sqlite3 "$DB_PATH" "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'ml_%' ORDER BY name;" | while read table; do
        COUNT=$(sqlite3 "$DB_PATH" "SELECT COUNT(*) FROM $table;")
        echo "  - $table ($COUNT rows)"
    done
    echo ""
    echo "Views created:"
    sqlite3 "$DB_PATH" "SELECT name FROM sqlite_master WHERE type='view' AND name LIKE 'v_ml_%' ORDER BY name;" | while read view; do
        echo "  - $view"
    done
    echo ""
    echo "Triggers created:"
    sqlite3 "$DB_PATH" "SELECT name FROM sqlite_master WHERE type='trigger' AND name LIKE 'trg_ml_%' ORDER BY name;" | while read trigger; do
        echo "  - $trigger"
    done
else
    echo ""
    echo "❌ Failed to apply ML schema"
    exit 1
fi

echo ""
echo "Database ready for ML training! 🚀"
echo ""
echo "Next steps:"
echo "  1. Import training data: go run cmd/ml-trainer/main.go --import data/train_split.csv"
echo "  2. Train model: go run cmd/ml-trainer/main.go --train"
echo "  3. Evaluate model: go run cmd/ml-evaluator/main.go"
