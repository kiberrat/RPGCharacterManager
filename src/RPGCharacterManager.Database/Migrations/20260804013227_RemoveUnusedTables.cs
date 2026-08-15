using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiHistory");

            migrationBuilder.DropTable(
                name: "AiImports");

            migrationBuilder.DropTable(
                name: "AutoSaves");

            migrationBuilder.DropTable(
                name: "Exports");

            migrationBuilder.DropTable(
                name: "Imports");

            migrationBuilder.DropTable(
                name: "Plugins");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropColumn(
                name: "IsOfficial",
                table: "GameSystems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOfficial",
                table: "GameSystems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AiHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChangesApplied = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Prompt = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    Response = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DetectedObjectsJson = table.Column<string>(type: "TEXT", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    IsConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoSaves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    SizeInBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoSaves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Exports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    ExportType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Imports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedObjectCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    FileType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    SkippedDuplicateCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Imports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Avatar = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastOpenedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiHistory_CreatedAt",
                table: "AiHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiImports_CreatedAt",
                table: "AiImports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AutoSaves_CharacterId_CreatedAt",
                table: "AutoSaves",
                columns: new[] { "CharacterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Exports_CreatedAt",
                table: "Exports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Imports_CreatedAt",
                table: "Imports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Name",
                table: "Plugins",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Name",
                table: "Users",
                column: "Name",
                unique: true);
        }
    }
}
