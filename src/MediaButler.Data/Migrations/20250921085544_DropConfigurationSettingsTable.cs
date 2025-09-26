using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaButler.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropConfigurationSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigurationSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigurationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedDate = table.Column<DateTime>(type: "datetime", nullable: false, comment: "UTC timestamp when the entity was created"),
                    DataType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Expected data type for value validation"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true, comment: "Human-readable description of the setting's purpose"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Indicates if the entity is active (not soft-deleted)"),
                    Key = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, comment: "Configuration key identifier (can have duplicates)"),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime", nullable: false, comment: "UTC timestamp when the entity was last modified"),
                    Note = table.Column<string>(type: "text", nullable: true, comment: "Optional contextual notes about the entity"),
                    RequiresRestart = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false, comment: "Indicates if application restart is required for changes to take effect"),
                    Section = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, comment: "Logical section for grouping related settings (e.g., 'ML', 'Paths')"),
                    Value = table.Column<string>(type: "text", nullable: false, comment: "Configuration value serialized as JSON")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationSettings", x => x.Id);
                    table.CheckConstraint("CK_ConfigurationSettings_DataType_Valid", "[DataType] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_ConfigurationSettings_Section_Valid", "[Section] IN ('Path', 'General', 'Future', 'WatchPath')");
                    table.CheckConstraint("CK_ConfigurationSettings_Value_Length", "LENGTH([Value]) <= 10000");
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
        }
    }
}
