using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class CharacterResourceOverridesAndLocalItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerCharacterId",
                table: "Items",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MaximumOverride",
                table: "CharacterResources",
                type: "REAL",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_OwnerCharacterId",
                table: "Items",
                column: "OwnerCharacterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Characters_OwnerCharacterId",
                table: "Items",
                column: "OwnerCharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Characters_OwnerCharacterId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_OwnerCharacterId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "OwnerCharacterId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "MaximumOverride",
                table: "CharacterResources");
        }
    }
}
