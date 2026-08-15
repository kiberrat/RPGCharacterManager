using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentBonuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Armor");

            migrationBuilder.AddColumn<Guid>(
                name: "EquipmentSlotId",
                table: "Items",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ItemBonuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Target = table.Column<int>(type: "INTEGER", nullable: false),
                    AttributeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Formula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Condition = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemBonuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemBonuses_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemBonuses_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemBonuses_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Items_EquipmentSlotId",
                table: "Items",
                column: "EquipmentSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBonuses_AttributeId",
                table: "ItemBonuses",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBonuses_ItemId",
                table: "ItemBonuses",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemBonuses_ResourceId",
                table: "ItemBonuses",
                column: "ResourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_EquipmentSlots_EquipmentSlotId",
                table: "Items",
                column: "EquipmentSlotId",
                principalTable: "EquipmentSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_EquipmentSlots_EquipmentSlotId",
                table: "Items");

            migrationBuilder.DropTable(
                name: "ItemBonuses");

            migrationBuilder.DropIndex(
                name: "IX_Items_EquipmentSlotId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "EquipmentSlotId",
                table: "Items");

            migrationBuilder.CreateTable(
                name: "Armor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SlotId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AddedHealthFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DefenceFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Armor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Armor_EquipmentSlots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "EquipmentSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Armor_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Armor_ItemId",
                table: "Armor",
                column: "ItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Armor_SlotId",
                table: "Armor",
                column: "SlotId");
        }
    }
}
