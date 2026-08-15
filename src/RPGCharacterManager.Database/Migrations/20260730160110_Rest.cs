using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class Rest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Duration = table.Column<double>(type: "REAL", nullable: true),
                    DurationUnit = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_RestTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestTypes_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RestTypes_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RestRestores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RestTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    Formula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Condition = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestRestores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestRestores_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RestRestores_RestTypes_RestTypeId",
                        column: x => x.RestTypeId,
                        principalTable: "RestTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestRestores_ResourceId",
                table: "RestRestores",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_RestRestores_RestTypeId",
                table: "RestRestores",
                column: "RestTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RestTypes_ContentPackId",
                table: "RestTypes",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_RestTypes_GameSystemId",
                table: "RestTypes",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_RestTypes_GameSystemId_SystemName",
                table: "RestTypes",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestTypes_Name",
                table: "RestTypes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RestTypes_SortOrder",
                table: "RestTypes",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestRestores");

            migrationBuilder.DropTable(
                name: "RestTypes");
        }
    }
}
