using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class CharacterCustomAbilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CharacterCustomAbilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Formula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DependencyDescription = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterCustomAbilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterCustomAbilities_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterCustomAbilities_CharacterId",
                table: "CharacterCustomAbilities",
                column: "CharacterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterCustomAbilities");
        }
    }
}
