using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class SheetLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SheetLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SheetLayouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SheetLayoutTabs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LayoutId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SheetLayoutTabs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SheetLayoutTabs_SheetLayouts_LayoutId",
                        column: x => x.LayoutId,
                        principalTable: "SheetLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SheetLayoutPanels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TabId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PanelId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Width = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SheetLayoutPanels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SheetLayoutPanels_SheetLayoutTabs_TabId",
                        column: x => x.TabId,
                        principalTable: "SheetLayoutTabs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SheetLayoutPanels_TabId_SortOrder",
                table: "SheetLayoutPanels",
                columns: new[] { "TabId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SheetLayouts_IsDefault",
                table: "SheetLayouts",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_SheetLayouts_Name",
                table: "SheetLayouts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SheetLayoutTabs_LayoutId_SortOrder",
                table: "SheetLayoutTabs",
                columns: new[] { "LayoutId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SheetLayoutPanels");

            migrationBuilder.DropTable(
                name: "SheetLayoutTabs");

            migrationBuilder.DropTable(
                name: "SheetLayouts");
        }
    }
}
