using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class Inventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Capacity",
                table: "Items",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Items",
                type: "TEXT",
                nullable: true);

            // Доля веса содержимого по умолчанию равна единице: обычный контейнер
            // передаёт носителю весь вес того, что в нём лежит. Значение 0, которое
            // подставляет генератор миграций, сделало бы каждый существующий
            // контейнер безразмерной сумкой.
            migrationBuilder.AddColumn<double>(
                name: "ContentWeightFactor",
                table: "Items",
                type: "REAL",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<bool>(
                name: "IsContainer",
                table: "Items",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "UseCost",
                table: "Items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CarryCapacityFormula",
                table: "GameSystems",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeightUnit",
                table: "GameSystems",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ItemCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
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
                    table.PrimaryKey("PK_ItemCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemCategories_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemCategories_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemCategories_ItemCategories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ItemCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ItemUseEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Formula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemUseEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemUseEffects_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemUseEffects_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Items_CategoryId",
                table: "Items",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_ContentPackId",
                table: "ItemCategories",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_GameSystemId",
                table: "ItemCategories",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_GameSystemId_SystemName",
                table: "ItemCategories",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_Name",
                table: "ItemCategories",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_ParentId",
                table: "ItemCategories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemUseEffects_ItemId",
                table: "ItemUseEffects",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemUseEffects_ResourceId",
                table: "ItemUseEffects",
                column: "ResourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_ItemCategories_CategoryId",
                table: "Items",
                column: "CategoryId",
                principalTable: "ItemCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_ItemCategories_CategoryId",
                table: "Items");

            migrationBuilder.DropTable(
                name: "ItemCategories");

            migrationBuilder.DropTable(
                name: "ItemUseEffects");

            migrationBuilder.DropIndex(
                name: "IX_Items_CategoryId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ContentWeightFactor",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsContainer",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "UseCost",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CarryCapacityFormula",
                table: "GameSystems");

            migrationBuilder.DropColumn(
                name: "WeightUnit",
                table: "GameSystems");
        }
    }
}
