using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class WeaponScalingAndAmmunition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Свойства оружия перестали храниться в JSON и стали списком названий,
            // поэтому прежний столбец удаляется, а не переименовывается: средство
            // создания миграций приняло замену за переименование.
            migrationBuilder.DropColumn(
                name: "PropertiesJson",
                table: "Weapons");

            migrationBuilder.AddColumn<Guid>(
                name: "ScalingAttributeId",
                table: "Weapons",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmmunitionPerShot",
                table: "Weapons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "AttackDiceFormula",
                table: "Weapons",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Weapons",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProficiencySkillId",
                table: "Weapons",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                table: "Weapons",
                type: "TEXT",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoadedAmmunition",
                table: "Inventory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_ProficiencySkillId",
                table: "Weapons",
                column: "ProficiencySkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_ScalingAttributeId",
                table: "Weapons",
                column: "ScalingAttributeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Weapons_Attributes_ScalingAttributeId",
                table: "Weapons",
                column: "ScalingAttributeId",
                principalTable: "Attributes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Weapons_Skills_ProficiencySkillId",
                table: "Weapons",
                column: "ProficiencySkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Weapons_Attributes_ScalingAttributeId",
                table: "Weapons");

            migrationBuilder.DropForeignKey(
                name: "FK_Weapons_Skills_ProficiencySkillId",
                table: "Weapons");

            migrationBuilder.DropIndex(
                name: "IX_Weapons_ProficiencySkillId",
                table: "Weapons");

            migrationBuilder.DropIndex(
                name: "IX_Weapons_ScalingAttributeId",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "AmmunitionPerShot",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "AttackDiceFormula",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "ProficiencySkillId",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "Properties",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "ScalingAttributeId",
                table: "Weapons");

            migrationBuilder.DropColumn(
                name: "LoadedAmmunition",
                table: "Inventory");

            migrationBuilder.AddColumn<string>(
                name: "PropertiesJson",
                table: "Weapons",
                type: "TEXT",
                nullable: true);
        }
    }
}
