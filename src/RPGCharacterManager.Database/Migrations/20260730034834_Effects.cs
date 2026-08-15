using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class Effects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Генератор миграций принял удаление одних свойств и добавление других
            // за переименование. Это не переименования:
            //
            // IsStackable — признак «да/нет», Tone — окраска эффекта. Переименование
            // превратило бы каждый складывающийся эффект в отрицательный.
            //
            // Duration хранил целую фразу «1 минута», а DurationUnit хранит только
            // единицу «минута»: по ней сходятся таймеры. Старое значение в новом
            // поле сделало бы единицу неузнаваемой.
            migrationBuilder.DropColumn(
                name: "IsStackable",
                table: "Effects");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Effects");

            migrationBuilder.AddColumn<int>(
                name: "Tone",
                table: "Effects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DurationUnit",
                table: "Effects",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "Effects",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DurationFormula",
                table: "Effects",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaximumStacks",
                table: "Effects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Effects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Stacking",
                table: "Effects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EffectBonuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EffectId = table.Column<Guid>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_EffectBonuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EffectBonuses_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EffectBonuses_Effects_EffectId",
                        column: x => x.EffectId,
                        principalTable: "Effects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EffectBonuses_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Effects_Priority",
                table: "Effects",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_EffectBonuses_AttributeId",
                table: "EffectBonuses",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_EffectBonuses_EffectId",
                table: "EffectBonuses",
                column: "EffectId");

            migrationBuilder.CreateIndex(
                name: "IX_EffectBonuses_ResourceId",
                table: "EffectBonuses",
                column: "ResourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EffectBonuses");

            migrationBuilder.DropIndex(
                name: "IX_Effects_Priority",
                table: "Effects");

            migrationBuilder.DropColumn(
                name: "Area",
                table: "Effects");

            migrationBuilder.DropColumn(
                name: "DurationFormula",
                table: "Effects");

            migrationBuilder.DropColumn(
                name: "MaximumStacks",
                table: "Effects");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Effects");

            migrationBuilder.DropColumn(
                name: "Stacking",
                table: "Effects");

            migrationBuilder.DropColumn(
                name: "Tone",
                table: "Effects");

            migrationBuilder.DropColumn(
                name: "DurationUnit",
                table: "Effects");

            migrationBuilder.AddColumn<bool>(
                name: "IsStackable",
                table: "Effects",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Duration",
                table: "Effects",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }
    }
}
