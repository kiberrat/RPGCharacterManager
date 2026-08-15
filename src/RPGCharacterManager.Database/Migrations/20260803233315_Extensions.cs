using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class Extensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DependenciesJson",
                table: "ContentPacks",
                type: "TEXT",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "License",
                table: "ContentPacks",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequiredVersion",
                table: "ContentPacks",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DependenciesJson",
                table: "ContentPacks");

            migrationBuilder.DropColumn(
                name: "License",
                table: "ContentPacks");

            migrationBuilder.DropColumn(
                name: "RequiredVersion",
                table: "ContentPacks");
        }
    }
}
