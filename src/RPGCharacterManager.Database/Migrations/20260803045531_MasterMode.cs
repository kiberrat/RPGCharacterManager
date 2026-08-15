using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class MasterMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InitiativeFormula",
                table: "GameSystems",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InitiativeTrackers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InitiativeTrackers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InitiativeTrackers_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InitiativeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrackerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InitiativeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InitiativeEntries_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InitiativeEntries_InitiativeTrackers_TrackerId",
                        column: x => x.TrackerId,
                        principalTable: "InitiativeTrackers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InitiativeEntries_CharacterId",
                table: "InitiativeEntries",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_InitiativeEntries_TrackerId_CharacterId",
                table: "InitiativeEntries",
                columns: new[] { "TrackerId", "CharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InitiativeEntries_TrackerId_SortOrder",
                table: "InitiativeEntries",
                columns: new[] { "TrackerId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_InitiativeTrackers_CampaignId",
                table: "InitiativeTrackers",
                column: "CampaignId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InitiativeEntries");

            migrationBuilder.DropTable(
                name: "InitiativeTrackers");

            migrationBuilder.DropColumn(
                name: "InitiativeFormula",
                table: "GameSystems");
        }
    }
}
