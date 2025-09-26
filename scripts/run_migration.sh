#!/bin/bash
# MediaButler FileCat Migration Script
# This script provides options to migrate data from FileCat.db to MediaButler

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOURCE_DB="$SCRIPT_DIR/../temp/Import/FileCat.db"
TARGET_DB="$SCRIPT_DIR/../temp/mediabutler.dev.db"
SQL_SCRIPT="$SCRIPT_DIR/migrate_filecat_data.sql"
CS_TOOL="$SCRIPT_DIR/FileCatMigrationTool.cs"

echo "=== MediaButler FileCat Migration Tool ==="
echo "Source Database: $SOURCE_DB"
echo "Target Database: $TARGET_DB"
echo

# Check if databases exist
if [ ! -f "$SOURCE_DB" ]; then
    echo "Error: Source database not found at $SOURCE_DB"
    exit 1
fi

if [ ! -f "$TARGET_DB" ]; then
    echo "Error: Target database not found at $TARGET_DB"
    echo "Please ensure MediaButler API has been run at least once to create the database."
    exit 1
fi

# Function to show menu
show_menu() {
    echo "Choose migration method:"
    echo "1) SQL Script Migration (Quick, basic hash generation)"
    echo "2) C# Tool Migration (Robust, proper SHA256 hashes)"
    echo "3) Analyze source data only"
    echo "4) Show current target database status"
    echo "5) Exit"
}

# Function to run SQL migration
run_sql_migration() {
    echo "Running SQL migration..."
    echo "Note: This generates pseudo-hashes. Files will need to be re-scanned for proper hashes."

    # Create backup of target database
    BACKUP_FILE="$TARGET_DB.backup.$(date +%Y%m%d_%H%M%S)"
    cp "$TARGET_DB" "$BACKUP_FILE"
    echo "Created backup: $BACKUP_FILE"

    # Update the SQL script with correct paths
    sed "s|/Users/luca/GitHub/mediabutler/MediaButler/temp/Import/FileCat.db|$SOURCE_DB|g" "$SQL_SCRIPT" > "$SQL_SCRIPT.tmp"

    # Run migration
    sqlite3 "$TARGET_DB" < "$SQL_SCRIPT.tmp"
    rm "$SQL_SCRIPT.tmp"

    echo "SQL migration completed!"
}

# Function to run C# migration
run_cs_migration() {
    echo "Running C# migration tool..."

    # Create temporary project file for the migration tool
    cat > "$SCRIPT_DIR/MigrationTool.csproj" << EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Data.SQLite" Version="1.0.118" />
  </ItemGroup>
</Project>
EOF

    # Copy the C# file to Program.cs
    cp "$CS_TOOL" "$SCRIPT_DIR/Program.cs"

    echo "First, let's run a dry run to see what would be migrated..."
    cd "$SCRIPT_DIR"
    dotnet run -- --dry-run

    echo
    read -p "Do you want to proceed with the live migration? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "Running live migration..."
        dotnet run -- --live
    else
        echo "Migration cancelled."
    fi

    # Cleanup
    rm -f "$SCRIPT_DIR/MigrationTool.csproj" "$SCRIPT_DIR/Program.cs"
    rm -rf "$SCRIPT_DIR/bin" "$SCRIPT_DIR/obj"
}

# Function to analyze source data
analyze_source() {
    echo "Analyzing source database..."
    sqlite3 "$SOURCE_DB" << EOF
.mode column
.headers on
SELECT 'Source Database Analysis' as info;
SELECT
    COUNT(*) as total_records,
    COUNT(CASE WHEN FileCategory IS NOT NULL AND FileCategory != '' THEN 1 END) as with_category,
    COUNT(CASE WHEN IsDeleted = 1 THEN 1 END) as deleted,
    COUNT(CASE WHEN IsToCategorize = 1 THEN 1 END) as to_categorize,
    COUNT(CASE WHEN IsNotToMove = 1 THEN 1 END) as not_to_move,
    COUNT(CASE WHEN IsActive = 1 THEN 1 END) as active
FROM FilesDetail;

SELECT 'Top Categories' as info;
SELECT FileCategory, COUNT(*) as count
FROM FilesDetail
WHERE FileCategory IS NOT NULL AND FileCategory != ''
GROUP BY FileCategory
ORDER BY count DESC
LIMIT 10;

SELECT 'Sample Records' as info;
SELECT substr(Name, 1, 40) as filename, FileCategory, Status, CreatedDate
FROM (
    SELECT Name, FileCategory,
           CASE WHEN IsDeleted = 1 THEN 'Deleted'
                WHEN IsNotToMove = 1 THEN 'NotToMove'
                WHEN FileCategory IS NOT NULL THEN 'Categorized'
                WHEN IsToCategorize = 1 THEN 'ToCategorize'
                ELSE 'Unknown' END as Status,
           CreatedDate
    FROM FilesDetail
    ORDER BY Id DESC
    LIMIT 5
);
EOF
}

# Function to show target database status
show_target_status() {
    echo "Current target database status..."
    sqlite3 "$TARGET_DB" << EOF
.mode column
.headers on
SELECT 'Target Database Status' as info;
SELECT COUNT(*) as total_tracked_files FROM TrackedFiles;

SELECT 'Status Distribution' as info;
SELECT
    CASE Status
        WHEN 0 THEN 'New'
        WHEN 1 THEN 'Discovered'
        WHEN 2 THEN 'Classified'
        WHEN 3 THEN 'ConfirmedCategory'
        WHEN 4 THEN 'Queued'
        WHEN 5 THEN 'Moved'
        WHEN 6 THEN 'Error'
        WHEN 7 THEN 'Failed'
        ELSE 'Unknown'
    END as status_name,
    COUNT(*) as count
FROM TrackedFiles
GROUP BY Status
ORDER BY Status;

SELECT 'Recent Files' as info;
SELECT substr(FileName, 1, 40) as filename, Category, Status, CreatedDate
FROM TrackedFiles
ORDER BY CreatedDate DESC
LIMIT 5;
EOF
}

# Main menu loop
while true; do
    show_menu
    read -p "Enter your choice (1-5): " choice

    case $choice in
        1)
            run_sql_migration
            ;;
        2)
            run_cs_migration
            ;;
        3)
            analyze_source
            ;;
        4)
            show_target_status
            ;;
        5)
            echo "Goodbye!"
            exit 0
            ;;
        *)
            echo "Invalid option. Please choose 1-5."
            ;;
    esac

    echo
    read -p "Press Enter to continue..."
    echo
done