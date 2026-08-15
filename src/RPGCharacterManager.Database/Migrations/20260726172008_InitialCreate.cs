using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGCharacterManager.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Prompt = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    Response = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ChangesApplied = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    DetectedObjectsJson = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoSaves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SizeInBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoSaves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Backups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SizeInBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    IsAutomatic = table.Column<bool>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Backups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiceHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Formula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Result = table.Column<double>(type: "REAL", nullable: false),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: true),
                    HasAdvantage = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasDisadvantage = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiceHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Exports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ExportType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameSystems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    IsOfficial = table.Column<bool>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSystems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "History",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_History", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Imports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    FileType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedObjectCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedDuplicateCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Imports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Avatar = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    LastOpenedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Campaigns_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ContentPacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentPacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentPacks_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ParentLocationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Locations_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Locations_Locations_ParentLocationId",
                        column: x => x.ParentLocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Npcs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Portrait = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Npcs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Npcs_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ValueType = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultValue = table.Column<double>(type: "REAL", nullable: false),
                    MinimumValue = table.Column<double>(type: "REAL", nullable: true),
                    MaximumValue = table.Column<double>(type: "REAL", nullable: true),
                    Formula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ModifierFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attributes_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Attributes_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Backgrounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Backgrounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Backgrounds_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Backgrounds_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Effects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Formula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Duration = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    EndCondition = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsStackable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Effects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Effects_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Effects_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllowMultiple = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaximumItems = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentSlots_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EquipmentSlots_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Formulas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Expression = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ReturnType = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Formulas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Formulas_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Formulas_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Rarity = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Weight = table.Column<double>(type: "REAL", nullable: false),
                    Price = table.Column<double>(type: "REAL", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Stackable = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaximumStackSize = table.Column<int>(type: "INTEGER", nullable: true),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ChargesFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Items_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Monsters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Challenge = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatureType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    StatBlockJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Monsters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Monsters_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Monsters_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PropertyDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    DataType = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultValue = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Group = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEditable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Formula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ValidationRule = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ReferenceTargetType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    AllowedValues = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyDefinitions_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PropertyDefinitions_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Races",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Speed = table.Column<double>(type: "REAL", nullable: false),
                    Size = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Languages = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Races", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Races_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Races_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    MaximumFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    StartingFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RestoreRule = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resources_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Resources_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Condition = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ActionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Version = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rules_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Rules_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Traits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Formula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    UsesFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RechargeRule = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiredTraitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActivationCondition = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Traits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Traits_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Traits_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Traits_Traits_RequiredTraitId",
                        column: x => x.RequiredTraitId,
                        principalTable: "Traits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Classes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HitDiceFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    PrimaryAttributeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    StartingLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    MaximumLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Classes_Attributes_PrimaryAttributeId",
                        column: x => x.PrimaryAttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Classes_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Classes_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LinkedAttributeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Formula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    MaximumLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skills_Attributes_LinkedAttributeId",
                        column: x => x.LinkedAttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Skills_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Skills_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Armor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SlotId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AddedHealthFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DefenceFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "Weapons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttackFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DamageFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DamageType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CriticalFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CriticalThreshold = table.Column<int>(type: "INTEGER", nullable: true),
                    Range = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AmmunitionItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MagazineSize = table.Column<int>(type: "INTEGER", nullable: true),
                    ReloadTime = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PropertiesJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Weapons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Weapons_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ObjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyValues_PropertyDefinitions_PropertyDefinitionId",
                        column: x => x.PropertyDefinitionId,
                        principalTable: "PropertyDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Abilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Formula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResourceCostFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RechargeRule = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Abilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Abilities_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Abilities_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Abilities_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Spells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    School = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CastingTime = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Range = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AreaOfEffect = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Target = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Components = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Duration = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    RequiresConcentration = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRitual = table.Column<bool>(type: "INTEGER", nullable: false),
                    Formula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ScalingFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResourceCostFormula = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Spells_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Spells_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Spells_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Subclasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClassId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AvailableAtLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Requirements = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContentPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Image = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subclasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subclasses_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Subclasses_ContentPacks_ContentPackId",
                        column: x => x.ContentPackId,
                        principalTable: "ContentPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Subclasses_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Portrait = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Experience = table.Column<double>(type: "REAL", nullable: false),
                    RaceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClassId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SubclassId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BackgroundId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Alignment = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Age = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Height = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Weight = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Gender = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Languages = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Biography = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    GameSystemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsTemplate = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Characters_Backgrounds_BackgroundId",
                        column: x => x.BackgroundId,
                        principalTable: "Backgrounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Characters_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Characters_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Characters_GameSystems_GameSystemId",
                        column: x => x.GameSystemId,
                        principalTable: "GameSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Characters_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Characters_Subclasses_SubclassId",
                        column: x => x.SubclassId,
                        principalTable: "Subclasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CharacterAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttributeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BaseValue = table.Column<double>(type: "REAL", nullable: false),
                    CurrentValue = table.Column<double>(type: "REAL", nullable: false),
                    Modifier = table.Column<double>(type: "REAL", nullable: false),
                    TemporaryBonus = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterAttributes_Attributes_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "Attributes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterAttributes_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EffectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RemainingTime = table.Column<double>(type: "REAL", nullable: true),
                    Stacks = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterEffects_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterEffects_Effects_EffectId",
                        column: x => x.EffectId,
                        principalTable: "Effects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Current = table.Column<double>(type: "REAL", nullable: false),
                    Maximum = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterResources_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterResources_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SkillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProficiencyLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Bonus = table.Column<double>(type: "REAL", nullable: false),
                    CurrentValue = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterSkills_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterSkills_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSpells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SpellId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsPrepared = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    TimesUsed = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSpells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterSpells_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterSpells_Spells_SpellId",
                        column: x => x.SpellId,
                        principalTable: "Spells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterTraits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TraitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    RemainingUses = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterTraits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterTraits_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterTraits_Traits_TraitId",
                        column: x => x.TraitId,
                        principalTable: "Traits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    RemainingCharges = table.Column<int>(type: "INTEGER", nullable: true),
                    Durability = table.Column<double>(type: "REAL", nullable: true),
                    ContainerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inventory_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inventory_Inventory_ContainerId",
                        column: x => x.ContainerId,
                        principalTable: "Inventory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Inventory_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterEquipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SlotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterEquipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterEquipment_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterEquipment_EquipmentSlots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "EquipmentSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterEquipment_Inventory_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "Inventory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Abilities_ContentPackId",
                table: "Abilities",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Abilities_GameSystemId",
                table: "Abilities",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Abilities_GameSystemId_SystemName",
                table: "Abilities",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Abilities_Name",
                table: "Abilities",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Abilities_ResourceId",
                table: "Abilities",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AiHistory_CreatedAt",
                table: "AiHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiImports_CreatedAt",
                table: "AiImports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Armor_ItemId",
                table: "Armor",
                column: "ItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Armor_SlotId",
                table: "Armor",
                column: "SlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Attributes_Category",
                table: "Attributes",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Attributes_ContentPackId",
                table: "Attributes",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Attributes_GameSystemId",
                table: "Attributes",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Attributes_GameSystemId_SystemName",
                table: "Attributes",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attributes_Name",
                table: "Attributes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AutoSaves_CharacterId_CreatedAt",
                table: "AutoSaves",
                columns: new[] { "CharacterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Backgrounds_ContentPackId",
                table: "Backgrounds",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Backgrounds_GameSystemId",
                table: "Backgrounds",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Backgrounds_GameSystemId_SystemName",
                table: "Backgrounds",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Backgrounds_Name",
                table: "Backgrounds",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Backups_CreatedAt",
                table: "Backups",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_GameSystemId",
                table: "Campaigns",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_Name",
                table: "Campaigns",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterAttributes_AttributeId",
                table: "CharacterAttributes",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterAttributes_CharacterId_AttributeId",
                table: "CharacterAttributes",
                columns: new[] { "CharacterId", "AttributeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterEffects_CharacterId",
                table: "CharacterEffects",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterEffects_EffectId",
                table: "CharacterEffects",
                column: "EffectId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterEquipment_CharacterId_InventoryItemId",
                table: "CharacterEquipment",
                columns: new[] { "CharacterId", "InventoryItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterEquipment_CharacterId_SlotId",
                table: "CharacterEquipment",
                columns: new[] { "CharacterId", "SlotId" });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterEquipment_InventoryItemId",
                table: "CharacterEquipment",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterEquipment_SlotId",
                table: "CharacterEquipment",
                column: "SlotId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterResources_CharacterId_ResourceId",
                table: "CharacterResources",
                columns: new[] { "CharacterId", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterResources_ResourceId",
                table: "CharacterResources",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_BackgroundId",
                table: "Characters",
                column: "BackgroundId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_CampaignId",
                table: "Characters",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_ClassId",
                table: "Characters",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_GameSystemId",
                table: "Characters",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_Name",
                table: "Characters",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_RaceId",
                table: "Characters",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_SubclassId",
                table: "Characters",
                column: "SubclassId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSkills_CharacterId_SkillId",
                table: "CharacterSkills",
                columns: new[] { "CharacterId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSkills_SkillId",
                table: "CharacterSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSpells_CharacterId_SpellId",
                table: "CharacterSpells",
                columns: new[] { "CharacterId", "SpellId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSpells_SpellId",
                table: "CharacterSpells",
                column: "SpellId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterTraits_CharacterId_TraitId",
                table: "CharacterTraits",
                columns: new[] { "CharacterId", "TraitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterTraits_TraitId",
                table: "CharacterTraits",
                column: "TraitId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_ContentPackId",
                table: "Classes",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_GameSystemId",
                table: "Classes",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_GameSystemId_SystemName",
                table: "Classes",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Classes_Name",
                table: "Classes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_PrimaryAttributeId",
                table: "Classes",
                column: "PrimaryAttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentPacks_GameSystemId",
                table: "ContentPacks",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentPacks_Name",
                table: "ContentPacks",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DiceHistory_CharacterId_CreatedAt",
                table: "DiceHistory",
                columns: new[] { "CharacterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DiceHistory_CreatedAt",
                table: "DiceHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Effects_ContentPackId",
                table: "Effects",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Effects_GameSystemId",
                table: "Effects",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Effects_GameSystemId_SystemName",
                table: "Effects",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Effects_Name",
                table: "Effects",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSlots_ContentPackId",
                table: "EquipmentSlots",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSlots_GameSystemId",
                table: "EquipmentSlots",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSlots_GameSystemId_SystemName",
                table: "EquipmentSlots",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSlots_Name",
                table: "EquipmentSlots",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Exports_CreatedAt",
                table: "Exports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Formulas_ContentPackId",
                table: "Formulas",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Formulas_GameSystemId",
                table: "Formulas",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Formulas_GameSystemId_SystemName",
                table: "Formulas",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Formulas_Name",
                table: "Formulas",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_GameSystems_Name",
                table: "GameSystems",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_GameSystems_SystemName",
                table: "GameSystems",
                column: "SystemName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_History_Action",
                table: "History",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_History_CampaignId_CreatedAt",
                table: "History",
                columns: new[] { "CampaignId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_History_CharacterId_CreatedAt",
                table: "History",
                columns: new[] { "CharacterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Imports_CreatedAt",
                table: "Imports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_CharacterId",
                table: "Inventory",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_ContainerId",
                table: "Inventory",
                column: "ContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_ItemId",
                table: "Inventory",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ContentPackId",
                table: "Items",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_GameSystemId",
                table: "Items",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_GameSystemId_SystemName",
                table: "Items",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_ItemType",
                table: "Items",
                column: "ItemType");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Name",
                table: "Items",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Rarity",
                table: "Items",
                column: "Rarity");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_CampaignId",
                table: "Locations",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Name",
                table: "Locations",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ParentLocationId",
                table: "Locations",
                column: "ParentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Monsters_Challenge",
                table: "Monsters",
                column: "Challenge");

            migrationBuilder.CreateIndex(
                name: "IX_Monsters_ContentPackId",
                table: "Monsters",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Monsters_GameSystemId",
                table: "Monsters",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Monsters_GameSystemId_SystemName",
                table: "Monsters",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Monsters_Name",
                table: "Monsters",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_CampaignId",
                table: "Notes",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_CharacterId",
                table: "Notes",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_CampaignId",
                table: "Npcs",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_Name",
                table: "Npcs",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Name",
                table: "Plugins",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDefinitions_ContentPackId",
                table: "PropertyDefinitions",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDefinitions_GameSystemId",
                table: "PropertyDefinitions",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDefinitions_GameSystemId_SystemName",
                table: "PropertyDefinitions",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDefinitions_Name",
                table: "PropertyDefinitions",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDefinitions_TargetType",
                table: "PropertyDefinitions",
                column: "TargetType");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValues_ObjectId",
                table: "PropertyValues",
                column: "ObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValues_ObjectId_PropertyDefinitionId",
                table: "PropertyValues",
                columns: new[] { "ObjectId", "PropertyDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyValues_PropertyDefinitionId",
                table: "PropertyValues",
                column: "PropertyDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Races_ContentPackId",
                table: "Races",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Races_GameSystemId",
                table: "Races",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Races_GameSystemId_SystemName",
                table: "Races",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Races_Name",
                table: "Races",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_ContentPackId",
                table: "Resources",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_GameSystemId",
                table: "Resources",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_GameSystemId_SystemName",
                table: "Resources",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Name",
                table: "Resources",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_CampaignId",
                table: "Rules",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_CharacterId",
                table: "Rules",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_ContentPackId",
                table: "Rules",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_GameSystemId",
                table: "Rules",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_GameSystemId_SystemName",
                table: "Rules",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rules_Name",
                table: "Rules",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Rules_Trigger_Enabled",
                table: "Rules",
                columns: new[] { "Trigger", "Enabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Category",
                table: "Skills",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_ContentPackId",
                table: "Skills",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_GameSystemId",
                table: "Skills",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_GameSystemId_SystemName",
                table: "Skills",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_LinkedAttributeId",
                table: "Skills",
                column: "LinkedAttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name",
                table: "Skills",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Spells_ContentPackId",
                table: "Spells",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Spells_GameSystemId",
                table: "Spells",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Spells_GameSystemId_SystemName",
                table: "Spells",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Spells_Level",
                table: "Spells",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Spells_Name",
                table: "Spells",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Spells_ResourceId",
                table: "Spells",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Spells_School",
                table: "Spells",
                column: "School");

            migrationBuilder.CreateIndex(
                name: "IX_Subclasses_ClassId",
                table: "Subclasses",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Subclasses_ContentPackId",
                table: "Subclasses",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Subclasses_GameSystemId",
                table: "Subclasses",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Subclasses_GameSystemId_SystemName",
                table: "Subclasses",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subclasses_Name",
                table: "Subclasses",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Traits_Category",
                table: "Traits",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Traits_ContentPackId",
                table: "Traits",
                column: "ContentPackId");

            migrationBuilder.CreateIndex(
                name: "IX_Traits_GameSystemId",
                table: "Traits",
                column: "GameSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Traits_GameSystemId_SystemName",
                table: "Traits",
                columns: new[] { "GameSystemId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Traits_Name",
                table: "Traits",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Traits_RequiredTraitId",
                table: "Traits",
                column: "RequiredTraitId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Name",
                table: "Users",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_ItemId",
                table: "Weapons",
                column: "ItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Abilities");

            migrationBuilder.DropTable(
                name: "AiHistory");

            migrationBuilder.DropTable(
                name: "AiImports");

            migrationBuilder.DropTable(
                name: "Armor");

            migrationBuilder.DropTable(
                name: "AutoSaves");

            migrationBuilder.DropTable(
                name: "Backups");

            migrationBuilder.DropTable(
                name: "CharacterAttributes");

            migrationBuilder.DropTable(
                name: "CharacterEffects");

            migrationBuilder.DropTable(
                name: "CharacterEquipment");

            migrationBuilder.DropTable(
                name: "CharacterResources");

            migrationBuilder.DropTable(
                name: "CharacterSkills");

            migrationBuilder.DropTable(
                name: "CharacterSpells");

            migrationBuilder.DropTable(
                name: "CharacterTraits");

            migrationBuilder.DropTable(
                name: "DiceHistory");

            migrationBuilder.DropTable(
                name: "Exports");

            migrationBuilder.DropTable(
                name: "Formulas");

            migrationBuilder.DropTable(
                name: "History");

            migrationBuilder.DropTable(
                name: "Imports");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Monsters");

            migrationBuilder.DropTable(
                name: "Notes");

            migrationBuilder.DropTable(
                name: "Npcs");

            migrationBuilder.DropTable(
                name: "Plugins");

            migrationBuilder.DropTable(
                name: "PropertyValues");

            migrationBuilder.DropTable(
                name: "Rules");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Weapons");

            migrationBuilder.DropTable(
                name: "Effects");

            migrationBuilder.DropTable(
                name: "EquipmentSlots");

            migrationBuilder.DropTable(
                name: "Inventory");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "Spells");

            migrationBuilder.DropTable(
                name: "Traits");

            migrationBuilder.DropTable(
                name: "PropertyDefinitions");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropTable(
                name: "Backgrounds");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropTable(
                name: "Races");

            migrationBuilder.DropTable(
                name: "Subclasses");

            migrationBuilder.DropTable(
                name: "Classes");

            migrationBuilder.DropTable(
                name: "Attributes");

            migrationBuilder.DropTable(
                name: "ContentPacks");

            migrationBuilder.DropTable(
                name: "GameSystems");
        }
    }
}
