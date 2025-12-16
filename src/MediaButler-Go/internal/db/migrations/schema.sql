CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;

CREATE TABLE "ConfigurationSettings" (
    -- Unique configuration key identifier (e.g., 'ML.ConfidenceThreshold')
    "Key" varchar(200) NOT NULL CONSTRAINT "PK_ConfigurationSettings" PRIMARY KEY,

    -- Configuration value serialized as JSON
    "Value" text NOT NULL,

    -- Logical section for grouping related settings (e.g., 'ML', 'Paths')
    "Section" varchar(100) NOT NULL,

    -- Human-readable description of the setting's purpose
    "Description" varchar(500) NULL,

    -- Expected data type for value validation
    "DataType" integer NOT NULL DEFAULT 0,

    -- Indicates if application restart is required for changes to take effect
    "RequiresRestart" boolean NOT NULL DEFAULT 0,

    -- UTC timestamp when the entity was created
    "CreatedDate" datetime NOT NULL,

    -- UTC timestamp when the entity was last modified
    "LastUpdateDate" datetime NOT NULL,

    -- Optional contextual notes about the entity
    "Note" text NULL,

    -- Indicates if the entity is active (not soft-deleted)
    "IsActive" boolean NOT NULL DEFAULT 1,
    CONSTRAINT "CK_ConfigurationSettings_DataType_Valid" CHECK ([DataType] BETWEEN 0 AND 4),
    CONSTRAINT "CK_ConfigurationSettings_Key_Format" CHECK ([Key] LIKE '%.%'),
    CONSTRAINT "CK_ConfigurationSettings_Section_Format" CHECK ([Section] NOT LIKE '%[^A-Za-z0-9_-]%'),
    CONSTRAINT "CK_ConfigurationSettings_Value_Length" CHECK (LENGTH([Value]) <= 10000)
);

CREATE TABLE "TrackedFiles" (
    -- SHA256 hash of the file content, serves as unique identifier
    "Hash" varchar(64) NOT NULL CONSTRAINT "PK_TrackedFiles" PRIMARY KEY,

    -- Original filename including extension
    "FileName" varchar(500) NOT NULL,

    -- Full path where the file was originally discovered
    "OriginalPath" varchar(1000) NOT NULL,

    -- File size in bytes
    "FileSize" bigint NOT NULL,

    -- Current processing status of the file
    "Status" integer NOT NULL DEFAULT 0,

    -- Category suggested by ML classification
    "SuggestedCategory" varchar(200) NULL,

    -- ML classification confidence score (0.0 to 1.0)
    "Confidence" decimal(5,4) NOT NULL DEFAULT '0.0',

    -- Final category confirmed by user or system
    "Category" varchar(200) NULL,

    -- Target path for file organization
    "TargetPath" varchar(1000) NULL,

    -- UTC timestamp when ML classification was completed
    "ClassifiedAt" datetime NULL,

    -- UTC timestamp when file was successfully moved
    "MovedAt" datetime NULL,

    -- Most recent error message encountered during processing
    "LastError" text NULL,

    -- UTC timestamp of the most recent error
    "LastErrorAt" datetime NULL,

    -- Number of processing retry attempts made
    "RetryCount" integer NOT NULL DEFAULT 0,

    -- UTC timestamp when the entity was created
    "CreatedDate" datetime NOT NULL,

    -- UTC timestamp when the entity was last modified
    "LastUpdateDate" datetime NOT NULL,

    -- Optional contextual notes about the entity
    "Note" text NULL,

    -- Indicates if the entity is active (not soft-deleted)
    "IsActive" boolean NOT NULL DEFAULT 1
);

CREATE TABLE "UserPreferences" (
    -- Unique identifier for this user preference
    "Id" char(36) NOT NULL CONSTRAINT "PK_UserPreferences" PRIMARY KEY,

    -- User identifier this preference belongs to (defaults to 'default' for single-user)
    "UserId" varchar(100) NOT NULL DEFAULT 'default',

    -- Unique preference key identifier (e.g., 'theme', 'defaultView')
    "Key" varchar(200) NOT NULL,

    -- Preference value serialized as JSON for consistent storage
    "Value" text NOT NULL,

    -- Category for organizing related preferences (e.g., 'UI', 'Notifications')
    "Category" varchar(100) NOT NULL,

    -- UTC timestamp when the entity was created
    "CreatedDate" datetime NOT NULL,

    -- UTC timestamp when the entity was last modified
    "LastUpdateDate" datetime NOT NULL,

    -- Optional contextual notes about the entity
    "Note" text NULL,

    -- Indicates if the entity is active (not soft-deleted)
    "IsActive" boolean NOT NULL DEFAULT 1,
    CONSTRAINT "CK_UserPreferences_Category_Format" CHECK ([Category] NOT LIKE '%[^A-Za-z0-9_]%'),
    CONSTRAINT "CK_UserPreferences_Key_Format" CHECK ([Key] NOT LIKE '' AND [Key] NOT LIKE '% %'),
    CONSTRAINT "CK_UserPreferences_UserId_Format" CHECK ([UserId] NOT LIKE '%[^A-Za-z0-9_-]%'),
    CONSTRAINT "CK_UserPreferences_Value_Length" CHECK (LENGTH([Value]) <= 10000)
);

CREATE TABLE "ProcessingLogs" (
    -- Unique identifier for this log entry
    "Id" char(36) NOT NULL CONSTRAINT "PK_ProcessingLogs" PRIMARY KEY,

    -- SHA256 hash of the associated file
    "FileHash" varchar(64) NOT NULL,

    -- Severity level of this log entry
    "Level" integer NOT NULL,

    -- Functional category that generated this log entry
    "Category" varchar(100) NOT NULL,

    -- Primary log message describing the event
    "Message" varchar(1000) NOT NULL,

    -- Additional detailed information about the logged event
    "Details" text NULL,

    -- Exception information including stack trace for debugging
    "Exception" text NULL,

    -- Operation duration in milliseconds for performance monitoring
    "DurationMs" bigint NULL,

    -- UTC timestamp when the entity was created
    "CreatedDate" datetime NOT NULL,

    -- UTC timestamp when the entity was last modified
    "LastUpdateDate" datetime NOT NULL,

    -- Optional contextual notes about the entity
    "Note" text NULL,

    -- Indicates if the entity is active (not soft-deleted)
    "IsActive" boolean NOT NULL DEFAULT 1,
    CONSTRAINT "FK_ProcessingLogs_TrackedFiles" FOREIGN KEY ("FileHash") REFERENCES "TrackedFiles" ("Hash") ON DELETE RESTRICT
);

CREATE INDEX "IX_ConfigurationSetting_CreatedDate" ON "ConfigurationSettings" ("CreatedDate");

CREATE INDEX "IX_ConfigurationSetting_IsActive" ON "ConfigurationSettings" ("IsActive");

CREATE INDEX "IX_ConfigurationSetting_IsActive_LastUpdateDate" ON "ConfigurationSettings" ("IsActive", "LastUpdateDate");

CREATE INDEX "IX_ConfigurationSetting_LastUpdateDate" ON "ConfigurationSettings" ("LastUpdateDate");

CREATE INDEX "IX_ConfigurationSettings_DataType_Section" ON "ConfigurationSettings" ("DataType", "Section", "IsActive") WHERE [IsActive] = 1;

CREATE INDEX "IX_ConfigurationSettings_Recent_Changes" ON "ConfigurationSettings" ("LastUpdateDate" DESC, "Section") WHERE [IsActive] = 1;

CREATE INDEX "IX_ConfigurationSettings_Restart_Changes" ON "ConfigurationSettings" ("RequiresRestart", "LastUpdateDate") WHERE [RequiresRestart] = 1 AND [IsActive] = 1;

CREATE INDEX "IX_ConfigurationSettings_Section_Active" ON "ConfigurationSettings" ("Section", "IsActive") WHERE [IsActive] = 1;

CREATE INDEX "IX_ProcessingLog_CreatedDate" ON "ProcessingLogs" ("CreatedDate");

CREATE INDEX "IX_ProcessingLog_IsActive" ON "ProcessingLogs" ("IsActive");

CREATE INDEX "IX_ProcessingLog_IsActive_LastUpdateDate" ON "ProcessingLogs" ("IsActive", "LastUpdateDate");

CREATE INDEX "IX_ProcessingLog_LastUpdateDate" ON "ProcessingLogs" ("LastUpdateDate");

CREATE INDEX "IX_ProcessingLogs_Category_Timeline" ON "ProcessingLogs" ("Category", "CreatedDate", "IsActive") WHERE [IsActive] = 1;

CREATE INDEX "IX_ProcessingLogs_Comprehensive_Analysis" ON "ProcessingLogs" ("Level", "Category", "FileHash", "CreatedDate") WHERE [IsActive] = 1;

CREATE INDEX "IX_ProcessingLogs_Error_Monitoring" ON "ProcessingLogs" ("Level", "CreatedDate", "IsActive") WHERE [Level] >= 4 AND [IsActive] = 1;

CREATE INDEX "IX_ProcessingLogs_Exception_Tracking" ON "ProcessingLogs" ("Exception", "CreatedDate") WHERE [Exception] IS NOT NULL AND [IsActive] = 1;

CREATE INDEX "IX_ProcessingLogs_File_Audit_Trail" ON "ProcessingLogs" ("FileHash", "CreatedDate", "Level") WHERE [IsActive] = 1;

CREATE INDEX "IX_ProcessingLogs_FileHash" ON "ProcessingLogs" ("FileHash");

CREATE INDEX "IX_ProcessingLogs_Performance_Analysis" ON "ProcessingLogs" ("Category", "DurationMs", "CreatedDate") WHERE [DurationMs] IS NOT NULL AND [IsActive] = 1;

CREATE INDEX "IX_ProcessingLogs_Recent_Activity" ON "ProcessingLogs" ("CreatedDate" DESC, "Level", "Category") WHERE [IsActive] = 1;

CREATE INDEX "IX_TrackedFile_CreatedDate" ON "TrackedFiles" ("CreatedDate");

CREATE INDEX "IX_TrackedFile_IsActive" ON "TrackedFiles" ("IsActive");

CREATE INDEX "IX_TrackedFile_IsActive_LastUpdateDate" ON "TrackedFiles" ("IsActive", "LastUpdateDate");

CREATE INDEX "IX_TrackedFile_LastUpdateDate" ON "TrackedFiles" ("LastUpdateDate");

CREATE INDEX "IX_TrackedFiles_Category_Stats" ON "TrackedFiles" ("Category", "Status", "MovedAt") WHERE [Category] IS NOT NULL;

CREATE INDEX "IX_TrackedFiles_Classification_Workflow" ON "TrackedFiles" ("Status", "Confidence", "ClassifiedAt") WHERE [Status] = 2;

CREATE INDEX "IX_TrackedFiles_Error_Monitoring" ON "TrackedFiles" ("Status", "RetryCount", "LastErrorAt") WHERE [Status] IN (6, 7);

CREATE INDEX "IX_TrackedFiles_Filename_Analysis" ON "TrackedFiles" ("FileName", "Category", "Confidence") WHERE [Category] IS NOT NULL AND [Confidence] > 0;

CREATE INDEX "IX_TrackedFiles_Organization_Workflow" ON "TrackedFiles" ("Status", "Category", "MovedAt") WHERE [Status] IN (3, 4, 5);

CREATE INDEX "IX_TrackedFiles_OriginalPath" ON "TrackedFiles" ("OriginalPath");

CREATE INDEX "IX_TrackedFiles_Performance_Analytics" ON "TrackedFiles" ("FileSize", "Status", "CreatedDate");

CREATE INDEX "IX_TrackedFiles_Status_IsActive" ON "TrackedFiles" ("Status", "IsActive") WHERE [IsActive] = 1;

CREATE INDEX "IX_UserPreference_CreatedDate" ON "UserPreferences" ("CreatedDate");

CREATE INDEX "IX_UserPreference_IsActive" ON "UserPreferences" ("IsActive");

CREATE INDEX "IX_UserPreference_IsActive_LastUpdateDate" ON "UserPreferences" ("IsActive", "LastUpdateDate");

CREATE INDEX "IX_UserPreference_LastUpdateDate" ON "UserPreferences" ("LastUpdateDate");

CREATE INDEX "IX_UserPreferences_Category_User" ON "UserPreferences" ("Category", "UserId", "IsActive") WHERE [IsActive] = 1;

CREATE INDEX "IX_UserPreferences_Key_Category" ON "UserPreferences" ("Key", "Category", "IsActive") WHERE [IsActive] = 1;

CREATE INDEX "IX_UserPreferences_Recent_Changes" ON "UserPreferences" ("LastUpdateDate" DESC, "UserId", "Category") WHERE [IsActive] = 1;

CREATE INDEX "IX_UserPreferences_User_Active_Updated" ON "UserPreferences" ("UserId", "IsActive", "LastUpdateDate" DESC) WHERE [IsActive] = 1;

CREATE UNIQUE INDEX "IX_UserPreferences_User_Key_Unique" ON "UserPreferences" ("UserId", "Key") WHERE [IsActive] = 1;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250903185634_InitialCreate', '8.0.0');

COMMIT;

BEGIN TRANSACTION;

ALTER TABLE "TrackedFiles" ADD "MovedToPath" TEXT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250917194452_UpdateMigration', '8.0.0');

COMMIT;

BEGIN TRANSACTION;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250918194714_AddMovedToPathColumn', '8.0.0');

COMMIT;

BEGIN TRANSACTION;

ALTER TABLE "ConfigurationSettings" ADD "Id" INTEGER NOT NULL DEFAULT 0;

CREATE TABLE "ef_temp_ConfigurationSettings" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ConfigurationSettings" PRIMARY KEY AUTOINCREMENT,

    -- UTC timestamp when the entity was created
    "CreatedDate" datetime NOT NULL,

    -- Expected data type for value validation
    "DataType" integer NOT NULL DEFAULT 0,

    -- Human-readable description of the setting's purpose
    "Description" varchar(500) NULL,

    -- Indicates if the entity is active (not soft-deleted)
    "IsActive" boolean NOT NULL DEFAULT 1,

    -- Configuration key identifier (can have duplicates)
    "Key" varchar(200) NOT NULL,

    -- UTC timestamp when the entity was last modified
    "LastUpdateDate" datetime NOT NULL,

    -- Optional contextual notes about the entity
    "Note" text NULL,

    -- Indicates if application restart is required for changes to take effect
    "RequiresRestart" boolean NOT NULL DEFAULT 0,

    -- Logical section for grouping related settings (e.g., 'ML', 'Paths')
    "Section" varchar(100) NOT NULL,

    -- Configuration value serialized as JSON
    "Value" text NOT NULL,
    CONSTRAINT "CK_ConfigurationSettings_DataType_Valid" CHECK ([DataType] BETWEEN 0 AND 4),
    CONSTRAINT "CK_ConfigurationSettings_Section_Valid" CHECK ([Section] IN ('Path', 'General', 'Future', 'WatchPath')),
    CONSTRAINT "CK_ConfigurationSettings_Value_Length" CHECK (LENGTH([Value]) <= 10000)
);

INSERT INTO "ef_temp_ConfigurationSettings" ("Id", "CreatedDate", "DataType", "Description", "IsActive", "Key", "LastUpdateDate", "Note", "RequiresRestart", "Section", "Value")
SELECT "Id", "CreatedDate", "DataType", "Description", "IsActive", "Key", "LastUpdateDate", "Note", "RequiresRestart", "Section", "Value"
FROM "ConfigurationSettings";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;

DROP TABLE "ConfigurationSettings";

ALTER TABLE "ef_temp_ConfigurationSettings" RENAME TO "ConfigurationSettings";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;

CREATE INDEX "IX_ConfigurationSetting_CreatedDate" ON "ConfigurationSettings" ("CreatedDate");

CREATE INDEX "IX_ConfigurationSetting_IsActive" ON "ConfigurationSettings" ("IsActive");

CREATE INDEX "IX_ConfigurationSetting_IsActive_LastUpdateDate" ON "ConfigurationSettings" ("IsActive", "LastUpdateDate");

CREATE INDEX "IX_ConfigurationSetting_LastUpdateDate" ON "ConfigurationSettings" ("LastUpdateDate");

CREATE INDEX "IX_ConfigurationSettings_DataType_Section" ON "ConfigurationSettings" ("DataType", "Section", "IsActive") WHERE [IsActive] = 1;

CREATE INDEX "IX_ConfigurationSettings_Recent_Changes" ON "ConfigurationSettings" ("LastUpdateDate" DESC, "Section") WHERE [IsActive] = 1;

CREATE INDEX "IX_ConfigurationSettings_Restart_Changes" ON "ConfigurationSettings" ("RequiresRestart", "LastUpdateDate") WHERE [RequiresRestart] = 1 AND [IsActive] = 1;

CREATE INDEX "IX_ConfigurationSettings_Section_Active" ON "ConfigurationSettings" ("Section", "IsActive") WHERE [IsActive] = 1;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250920055856_ConfigurationSettingsPrimaryKeyUpdate', '8.0.0');

COMMIT;

BEGIN TRANSACTION;

DROP TABLE "ConfigurationSettings";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250921085544_DropConfigurationSettingsTable', '8.0.0');

COMMIT;

BEGIN TRANSACTION;

CREATE INDEX "IX_TrackedFiles_MultiStatus_Query" ON "TrackedFiles" ("Status", "Category", "CreatedDate") WHERE [IsActive] = 1;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251001173409_AddMultiStatusQueryIndex', '8.0.0');

COMMIT;

BEGIN TRANSACTION;

CREATE TABLE "FileOrganizationStates" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_FileOrganizationStates" PRIMARY KEY AUTOINCREMENT,

    -- SHA256 hash of the file being organized
    "FileHash" varchar(64) NOT NULL,

    -- Current state of the organization operation
    "State" integer NOT NULL DEFAULT 0,

    -- Optional additional context about the current state
    "StateContext" text NULL,

    -- UTC timestamp when the state was last updated
    "StateUpdatedAt" datetime NOT NULL,

    -- UTC timestamp when the entity was created
    "CreatedDate" datetime NOT NULL,

    -- UTC timestamp when the entity was last modified
    "LastUpdateDate" datetime NOT NULL,

    -- Optional contextual notes about the entity
    "Note" text NULL,

    -- Indicates if the entity is active (not soft-deleted)
    "IsActive" boolean NOT NULL DEFAULT 1
);

CREATE INDEX "IX_FileOrganizationStateEntity_CreatedDate" ON "FileOrganizationStates" ("CreatedDate");

CREATE INDEX "IX_FileOrganizationStateEntity_IsActive" ON "FileOrganizationStates" ("IsActive");

CREATE INDEX "IX_FileOrganizationStateEntity_IsActive_LastUpdateDate" ON "FileOrganizationStates" ("IsActive", "LastUpdateDate");

CREATE INDEX "IX_FileOrganizationStateEntity_LastUpdateDate" ON "FileOrganizationStates" ("LastUpdateDate");

CREATE UNIQUE INDEX "IX_FileOrganizationStates_FileHash_Unique" ON "FileOrganizationStates" ("FileHash") WHERE [IsActive] = 1;

CREATE INDEX "IX_FileOrganizationStates_StaleCleanup" ON "FileOrganizationStates" ("State", "StateUpdatedAt", "IsActive") WHERE [State] = 1 AND [IsActive] = 1;

CREATE INDEX "IX_FileOrganizationStates_State_UpdatedAt" ON "FileOrganizationStates" ("State", "StateUpdatedAt") WHERE [IsActive] = 1;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251010185413_AddFileOrganizationStateEntity', '8.0.0');

COMMIT;

