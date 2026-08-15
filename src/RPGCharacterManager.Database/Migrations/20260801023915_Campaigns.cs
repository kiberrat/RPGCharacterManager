using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class Campaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Campaigns_CampaignId",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Campaigns_CampaignId",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_Npcs_Campaigns_CampaignId",
                table: "Npcs");

            migrationBuilder.DropIndex(
                name: "IX_Characters_CampaignId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "Characters");

            // Портрет неигрового персонажа — то же самое, что изображение игрового
            // объекта, поэтому столбец переименовывается и портреты сохраняются.
            migrationBuilder.RenameColumn(
                name: "Portrait",
                table: "Npcs",
                newName: "Image");

            // Принадлежность кампании больше не хранится полем объекта: её задаёт
            // состав кампании. Столбцы именно удаляются, а не переименовываются
            // в новые ссылки: идентификатор кампании не является ни локацией,
            // ни игровой системой, и такой перенос создал бы битые ссылки.
            migrationBuilder.DropIndex(
                name: "IX_Npcs_CampaignId",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "Npcs");

            migrationBuilder.DropIndex(
                name: "IX_Locations_CampaignId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "Locations");

            migrationBuilder.AddColumn<string>(
                name: "Attitude",
                table: "Npcs",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Npcs",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "Npcs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GameSystemId",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContentPackId",
                table: "Npcs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GameSystemId",
                table: "Npcs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Npcs",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "Npcs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SystemName",
                table: "Npcs",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ContentPackId",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Locations",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "Locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Locations",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Locations",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemName",
                table: "Locations",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Campaigns",
                type: "TEXT",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartDate",
                table: "Campaigns",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "World",
                table: "Campaigns",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CampaignEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    GameDate = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignEvents_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ObjectKind = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ObjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignMembers_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Quests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Reward = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GiverId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LocationId = table.Column<Guid>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_Quests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quests_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Quests_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Quests_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Quests_Npcs_GiverId",
                        column: x => x.GiverId,
                        principalTable: "Npcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "QuestSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    IsDone = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestSteps_Quests_QuestId",
                        column: x => x.QuestId,
                        principalTable: "Quests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_ContentPackId",
                table: "Npcs",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_LocationId",
                table: "Npcs",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_GameSystemId",
                table: "Locations",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_GameSystemId",
                table: "Npcs",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_GameSystemId_SystemName",
                table: "Npcs",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ContentPackId",
                table: "Locations",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_GameSystemId_SystemName",
                table: "Locations",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignEvents_CampaignId_SortOrder",
                table: "CampaignEvents",
                columns: new[] { "CampaignId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_CampaignId_ObjectKind",
                table: "CampaignMembers",
                columns: new[] { "CampaignId", "ObjectKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_CampaignId_ObjectKind_ObjectId",
                table: "CampaignMembers",
                columns: new[] { "CampaignId", "ObjectKind", "ObjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quests_ContentPackId",
                table: "Quests",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_GameSystemId",
                table: "Quests",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_GameSystemId_SystemName",
                table: "Quests",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quests_GiverId",
                table: "Quests",
                column: "GiverId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_LocationId",
                table: "Quests",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_Name",
                table: "Quests",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_Status",
                table: "Quests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_QuestSteps_QuestId_SortOrder",
                table: "QuestSteps",
                columns: new[] { "QuestId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_ContentPacks_ContentPackId",
                table: "Locations",
                column: "ContentPackId",
                principalTable: "ContentPacks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_GameSystems_GameSystemId",
                table: "Locations",
                column: "GameSystemId",
                principalTable: "GameSystems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Npcs_ContentPacks_ContentPackId",
                table: "Npcs",
                column: "ContentPackId",
                principalTable: "ContentPacks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Npcs_GameSystems_GameSystemId",
                table: "Npcs",
                column: "GameSystemId",
                principalTable: "GameSystems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Npcs_Locations_LocationId",
                table: "Npcs",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Locations_ContentPacks_ContentPackId",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_GameSystems_GameSystemId",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_Npcs_ContentPacks_ContentPackId",
                table: "Npcs");

            migrationBuilder.DropForeignKey(
                name: "FK_Npcs_GameSystems_GameSystemId",
                table: "Npcs");

            migrationBuilder.DropForeignKey(
                name: "FK_Npcs_Locations_LocationId",
                table: "Npcs");

            migrationBuilder.DropTable(
                name: "CampaignEvents");

            migrationBuilder.DropTable(
                name: "CampaignMembers");

            migrationBuilder.DropTable(
                name: "QuestSteps");

            migrationBuilder.DropTable(
                name: "Quests");

            migrationBuilder.DropIndex(
                name: "IX_Npcs_ContentPackId",
                table: "Npcs");

            migrationBuilder.DropIndex(
                name: "IX_Npcs_LocationId",
                table: "Npcs");

            migrationBuilder.DropIndex(
                name: "IX_Locations_GameSystemId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Npcs_GameSystemId",
                table: "Npcs");

            migrationBuilder.DropIndex(
                name: "IX_Npcs_GameSystemId_SystemName",
                table: "Npcs");

            migrationBuilder.DropIndex(
                name: "IX_Locations_ContentPackId",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_GameSystemId_SystemName",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Attitude",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "ContentPackId",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "GameSystemId",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "SystemName",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "GameSystemId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "ContentPackId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "SystemName",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "World",
                table: "Campaigns");

            migrationBuilder.RenameColumn(
                name: "Image",
                table: "Npcs",
                newName: "Portrait");

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "Npcs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "Locations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_CampaignId",
                table: "Npcs",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_CampaignId",
                table: "Locations",
                column: "CampaignId");

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "Characters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Characters_CampaignId",
                table: "Characters",
                column: "CampaignId");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Campaigns_CampaignId",
                table: "Characters",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Campaigns_CampaignId",
                table: "Locations",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Npcs_Campaigns_CampaignId",
                table: "Npcs",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
