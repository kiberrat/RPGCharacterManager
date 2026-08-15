using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class CharacterMana : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Mana",
                table: "Characters",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ManaMaximum",
                table: "Characters",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mana",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ManaMaximum",
                table: "Characters");
        }
    }
}
