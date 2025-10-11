using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaButler.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileOrganizationStateEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileOrganizationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, comment: "SHA256 hash of the file being organized"),
                    State = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Current state of the organization operation"),
                    StateContext = table.Column<string>(type: "text", nullable: true, comment: "Optional additional context about the current state"),
                    StateUpdatedAt = table.Column<DateTime>(type: "datetime", nullable: false, comment: "UTC timestamp when the state was last updated"),
                    CreatedDate = table.Column<DateTime>(type: "datetime", nullable: false, comment: "UTC timestamp when the entity was created"),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime", nullable: false, comment: "UTC timestamp when the entity was last modified"),
                    Note = table.Column<string>(type: "text", nullable: true, comment: "Optional contextual notes about the entity"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Indicates if the entity is active (not soft-deleted)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileOrganizationStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileOrganizationStateEntity_CreatedDate",
                table: "FileOrganizationStates",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_FileOrganizationStateEntity_IsActive",
                table: "FileOrganizationStates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FileOrganizationStateEntity_IsActive_LastUpdateDate",
                table: "FileOrganizationStates",
                columns: new[] { "IsActive", "LastUpdateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FileOrganizationStateEntity_LastUpdateDate",
                table: "FileOrganizationStates",
                column: "LastUpdateDate");

            migrationBuilder.CreateIndex(
                name: "IX_FileOrganizationStates_FileHash_Unique",
                table: "FileOrganizationStates",
                column: "FileHash",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_FileOrganizationStates_StaleCleanup",
                table: "FileOrganizationStates",
                columns: new[] { "State", "StateUpdatedAt", "IsActive" },
                filter: "[State] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_FileOrganizationStates_State_UpdatedAt",
                table: "FileOrganizationStates",
                columns: new[] { "State", "StateUpdatedAt" },
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileOrganizationStates");
        }
    }
}
