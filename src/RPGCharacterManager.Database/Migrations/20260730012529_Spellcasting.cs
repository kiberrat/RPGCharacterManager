using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class Spellcasting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KnownSpellsFormula",
                table: "GameSystems",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreparedSpellsFormula",
                table: "GameSystems",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConcentrating",
                table: "CharacterSpells",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KnownSpellsFormula",
                table: "GameSystems");

            migrationBuilder.DropColumn(
                name: "PreparedSpellsFormula",
                table: "GameSystems");

            migrationBuilder.DropColumn(
                name: "IsConcentrating",
                table: "CharacterSpells");
        }
    }
}
