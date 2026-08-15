using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class Macros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Macros",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Hotkey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Condition = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    ActionsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Author = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Version = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Macros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Macros_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Macros_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Macros_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Macros_CharacterId",
                table: "Macros",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Macros_ContentPackId",
                table: "Macros",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Macros_GameSystemId",
                table: "Macros",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Macros_GameSystemId_SystemName",
                table: "Macros",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Macros_Hotkey",
                table: "Macros",
                column: "Hotkey");

            migrationBuilder.CreateIndex(
                name: "IX_Macros_Name",
                table: "Macros",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Macros_SortOrder",
                table: "Macros",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Macros");
        }
    }
}
