using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RPGCharacterManager.Database;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(RpgDbContext))]
    [Migration("20260807000000_CharacterAttributeOverrides")]
    public partial class CharacterAttributeOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "OverrideValue",
                table: "CharacterAttributes",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverrideValue",
                table: "CharacterAttributes");
        }
    }
}