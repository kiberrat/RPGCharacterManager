using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class ContentAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentTypeId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TargetSystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Alias = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentAliases_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentAliases_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentAliases_Alias",
                table: "ContentAliases",
                column: "Alias");

            migrationBuilder.CreateIndex(
                name: "IX_ContentAliases_ContentPackId_GameSystemId_ContentTypeId_TargetSystemName_Alias",
                table: "ContentAliases",
                columns: new[] { "ContentPackId", "GameSystemId", "ContentTypeId", "TargetSystemName", "Alias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentAliases_GameSystemId",
                table: "ContentAliases",
                column: "GameSystemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentAliases");
        }
    }
}
