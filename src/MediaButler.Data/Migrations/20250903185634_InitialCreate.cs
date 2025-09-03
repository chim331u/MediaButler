using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaButler.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigurationSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, comment: "Unique configuration key identifier (e.g., 'ML.ConfidenceThreshold')"),
                    Value = table.Column<string>(type: "text", nullable: false, comment: "Configuration value serialized as JSON"),
                    Section = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "Logical section for grouping related settings (e.g., 'ML', 'Paths')"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, comment: "Human-readable description of the setting's purpose"),
                    DataType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Expected data type for value validation"),
                    RequiresRestart = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Indicates if application restart is required for changes to take effect"),
                    CreatedDate = table.Column<DateTime>(type: "datetime", nullable: false, comment: "UTC timestamp when the entity was created"),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime", nullable: false, comment: "UTC timestamp when the entity was last modified"),
                    Note = table.Column<string>(type: "text", nullable: true, comment: "Optional contextual notes about the entity"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Indicates if the entity is active (not soft-deleted)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationSettings", x => x.Key);
                    table.CheckConstraint("CK_ConfigurationSettings_DataType_Valid", "[DataType] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_ConfigurationSettings_Key_Format", "[Key] LIKE '%.%'");
                    table.CheckConstraint("CK_ConfigurationSettings_Section_Format", "[Section] NOT LIKE '%[^A-Za-z0-9_-]%'");
                    table.CheckConstraint("CK_ConfigurationSettings_Value_Length", "LENGTH([Value]) <= 10000");
                });

            migrationBuilder.CreateTable(
                name: "TrackedFiles",
                columns: table => new
                {
                    Hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, comment: "SHA256 hash of the file content, serves as unique identifier"),
                    FileName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, comment: "Original filename including extension"),
                    OriginalPath = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false, comment: "Full path where the file was originally discovered"),
                    FileSize = table.Column<long>(type: "bigint", nullable: false, comment: "File size in bytes"),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Current processing status of the file"),
                    SuggestedCategory = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true, comment: "Category suggested by ML classification"),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false, defaultValue: 0.0m, comment: "ML classification confidence score (0.0 to 1.0)"),
                    Category = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true, comment: "Final category confirmed by user or system"),
                    TargetPath = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true, comment: "Target path for file organization"),
                    ClassifiedAt = table.Column<DateTime>(type: "datetime", nullable: true, comment: "UTC timestamp when ML classification was completed"),
                    MovedAt = table.Column<DateTime>(type: "datetime", nullable: true, comment: "UTC timestamp when file was successfully moved"),
                    LastError = table.Column<string>(type: "text", nullable: true, comment: "Most recent error message encountered during processing"),
                    LastErrorAt = table.Column<DateTime>(type: "datetime", nullable: true, comment: "UTC timestamp of the most recent error"),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Number of processing retry attempts made"),
                    CreatedDate = table.Column<DateTime>(type: "datetime", nullable: false, comment: "UTC timestamp when the entity was created"),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime", nullable: false, comment: "UTC timestamp when the entity was last modified"),
                    Note = table.Column<string>(type: "text", nullable: true, comment: "Optional contextual notes about the entity"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Indicates if the entity is active (not soft-deleted)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedFiles", x => x.Hash);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, comment: "Unique identifier for this user preference"),
                    UserId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, defaultValue: "default", comment: "User identifier this preference belongs to (defaults to 'default' for single-user)"),
                    Key = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, comment: "Unique preference key identifier (e.g., 'theme', 'defaultView')"),
                    Value = table.Column<string>(type: "text", nullable: false, comment: "Preference value serialized as JSON for consistent storage"),
                    Category = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "Category for organizing related preferences (e.g., 'UI', 'Notifications')"),
                    CreatedDate = table.Column<DateTime>(type: "datetime", nullable: false, comment: "UTC timestamp when the entity was created"),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime", nullable: false, comment: "UTC timestamp when the entity was last modified"),
                    Note = table.Column<string>(type: "text", nullable: true, comment: "Optional contextual notes about the entity"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Indicates if the entity is active (not soft-deleted)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                    table.CheckConstraint("CK_UserPreferences_Category_Format", "[Category] NOT LIKE '%[^A-Za-z0-9_]%'");
                    table.CheckConstraint("CK_UserPreferences_Key_Format", "[Key] NOT LIKE '' AND [Key] NOT LIKE '% %'");
                    table.CheckConstraint("CK_UserPreferences_UserId_Format", "[UserId] NOT LIKE '%[^A-Za-z0-9_-]%'");
                    table.CheckConstraint("CK_UserPreferences_Value_Length", "LENGTH([Value]) <= 10000");
                });

            migrationBuilder.CreateTable(
                name: "ProcessingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, comment: "Unique identifier for this log entry"),
                    FileHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, comment: "SHA256 hash of the associated file"),
                    Level = table.Column<int>(type: "integer", nullable: false, comment: "Severity level of this log entry"),
                    Category = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "Functional category that generated this log entry"),
                    Message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false, comment: "Primary log message describing the event"),
                    Details = table.Column<string>(type: "text", nullable: true, comment: "Additional detailed information about the logged event"),
                    Exception = table.Column<string>(type: "text", nullable: true, comment: "Exception information including stack trace for debugging"),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true, comment: "Operation duration in milliseconds for performance monitoring"),
                    CreatedDate = table.Column<DateTime>(type: "datetime", nullable: false, comment: "UTC timestamp when the entity was created"),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime", nullable: false, comment: "UTC timestamp when the entity was last modified"),
                    Note = table.Column<string>(type: "text", nullable: true, comment: "Optional contextual notes about the entity"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Indicates if the entity is active (not soft-deleted)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingLogs_TrackedFiles",
                        column: x => x.FileHash,
                        principalTable: "TrackedFiles",
                        principalColumn: "Hash",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationSetting_CreatedDate",
                table: "ConfigurationSettings",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationSetting_IsActive",
                table: "ConfigurationSettings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationSetting_IsActive_LastUpdateDate",
                table: "ConfigurationSettings",
                columns: new[] { "IsActive", "LastUpdateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationSetting_LastUpdateDate",
                table: "ConfigurationSettings",
                column: "LastUpdateDate");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationSettings_DataType_Section",
                table: "ConfigurationSettings",
                columns: new[] { "DataType", "Section", "IsActive" },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationSettings_Recent_Changes",
                table: "ConfigurationSettings",
                columns: new[] { "LastUpdateDate", "Section" },
                descending: new[] { true, false },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationSettings_Restart_Changes",
                table: "ConfigurationSettings",
                columns: new[] { "RequiresRestart", "LastUpdateDate" },
                filter: "[RequiresRestart] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationSettings_Section_Active",
                table: "ConfigurationSettings",
                columns: new[] { "Section", "IsActive" },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLog_CreatedDate",
                table: "ProcessingLogs",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLog_IsActive",
                table: "ProcessingLogs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLog_IsActive_LastUpdateDate",
                table: "ProcessingLogs",
                columns: new[] { "IsActive", "LastUpdateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLog_LastUpdateDate",
                table: "ProcessingLogs",
                column: "LastUpdateDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLogs_Category_Timeline",
                table: "ProcessingLogs",
                columns: new[] { "Category", "CreatedDate", "IsActive" },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLogs_Comprehensive_Analysis",
                table: "ProcessingLogs",
                columns: new[] { "Level", "Category", "FileHash", "CreatedDate" },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLogs_Error_Monitoring",
                table: "ProcessingLogs",
                columns: new[] { "Level", "CreatedDate", "IsActive" },
                filter: "[Level] >= 4 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLogs_Exception_Tracking",
                table: "ProcessingLogs",
                columns: new[] { "Exception", "CreatedDate" },
                filter: "[Exception] IS NOT NULL AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLogs_File_Audit_Trail",
                table: "ProcessingLogs",
                columns: new[] { "FileHash", "CreatedDate", "Level" },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLogs_FileHash",
                table: "ProcessingLogs",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLogs_Performance_Analysis",
                table: "ProcessingLogs",
                columns: new[] { "Category", "DurationMs", "CreatedDate" },
                filter: "[DurationMs] IS NOT NULL AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingLogs_Recent_Activity",
                table: "ProcessingLogs",
                columns: new[] { "CreatedDate", "Level", "Category" },
                descending: new[] { true, false, false },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFile_CreatedDate",
                table: "TrackedFiles",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFile_IsActive",
                table: "TrackedFiles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFile_IsActive_LastUpdateDate",
                table: "TrackedFiles",
                columns: new[] { "IsActive", "LastUpdateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFile_LastUpdateDate",
                table: "TrackedFiles",
                column: "LastUpdateDate");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFiles_Category_Stats",
                table: "TrackedFiles",
                columns: new[] { "Category", "Status", "MovedAt" },
                filter: "[Category] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFiles_Classification_Workflow",
                table: "TrackedFiles",
                columns: new[] { "Status", "Confidence", "ClassifiedAt" },
                filter: "[Status] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFiles_Error_Monitoring",
                table: "TrackedFiles",
                columns: new[] { "Status", "RetryCount", "LastErrorAt" },
                filter: "[Status] IN (6, 7)");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFiles_Filename_Analysis",
                table: "TrackedFiles",
                columns: new[] { "FileName", "Category", "Confidence" },
                filter: "[Category] IS NOT NULL AND [Confidence] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFiles_Organization_Workflow",
                table: "TrackedFiles",
                columns: new[] { "Status", "Category", "MovedAt" },
                filter: "[Status] IN (3, 4, 5)");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFiles_OriginalPath",
                table: "TrackedFiles",
                column: "OriginalPath");

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFiles_Performance_Analytics",
                table: "TrackedFiles",
                columns: new[] { "FileSize", "Status", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackedFiles_Status_IsActive",
                table: "TrackedFiles",
                columns: new[] { "Status", "IsActive" },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreference_CreatedDate",
                table: "UserPreferences",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreference_IsActive",
                table: "UserPreferences",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreference_IsActive_LastUpdateDate",
                table: "UserPreferences",
                columns: new[] { "IsActive", "LastUpdateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPreference_LastUpdateDate",
                table: "UserPreferences",
                column: "LastUpdateDate");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_Category_User",
                table: "UserPreferences",
                columns: new[] { "Category", "UserId", "IsActive" },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_Key_Category",
                table: "UserPreferences",
                columns: new[] { "Key", "Category", "IsActive" },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_Recent_Changes",
                table: "UserPreferences",
                columns: new[] { "LastUpdateDate", "UserId", "Category" },
                descending: new[] { true, false, false },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_User_Active_Updated",
                table: "UserPreferences",
                columns: new[] { "UserId", "IsActive", "LastUpdateDate" },
                descending: new[] { false, false, true },
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_User_Key_Unique",
                table: "UserPreferences",
                columns: new[] { "UserId", "Key" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigurationSettings");

            migrationBuilder.DropTable(
                name: "ProcessingLogs");

            migrationBuilder.DropTable(
                name: "UserPreferences");

            migrationBuilder.DropTable(
                name: "TrackedFiles");
        }
    }
}
