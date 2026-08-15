using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class Dice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DieTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sides = table.Column<int>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("PK_DieTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DieTypes_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DieTypes_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DieTypes_ContentPackId",
                table: "DieTypes",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_DieTypes_GameSystemId",
                table: "DieTypes",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_DieTypes_GameSystemId_SystemName",
                table: "DieTypes",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DieTypes_Name",
                table: "DieTypes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DieTypes_SortOrder",
                table: "DieTypes",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DieTypes");
        }
    }
}
