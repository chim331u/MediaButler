using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaButler.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurationSettingsPrimaryKeyUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ConfigurationSettings",
                table: "ConfigurationSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfigurationSettings_Key_Format",
                table: "ConfigurationSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfigurationSettings_Section_Format",
                table: "ConfigurationSettings");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "ConfigurationSettings",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                comment: "Configuration key identifier (can have duplicates)",
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200,
                oldComment: "Unique configuration key identifier (e.g., 'ML.ConfidenceThreshold')");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "ConfigurationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConfigurationSettings",
                table: "ConfigurationSettings",
                column: "Id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfigurationSettings_Section_Valid",
                table: "ConfigurationSettings",
                sql: "[Section] IN ('Path', 'General', 'Future', 'WatchPath)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ConfigurationSettings",
                table: "ConfigurationSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConfigurationSettings_Section_Valid",
                table: "ConfigurationSettings");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ConfigurationSettings");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "ConfigurationSettings",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                comment: "Unique configuration key identifier (e.g., 'ML.ConfidenceThreshold')",
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200,
                oldComment: "Configuration key identifier (can have duplicates)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConfigurationSettings",
                table: "ConfigurationSettings",
                column: "Key");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfigurationSettings_Key_Format",
                table: "ConfigurationSettings",
                sql: "[Key] LIKE '%.%'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConfigurationSettings_Section_Format",
                table: "ConfigurationSettings",
                sql: "[Section] NOT LIKE '%[^A-Za-z0-9_-]%'");
        }
    }
}
