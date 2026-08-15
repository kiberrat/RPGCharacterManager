using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database.Configuration;

namespace RPGCharacterManager.Database;

/// <summary>
/// Контекст базы данных приложения.
///
/// Конфигурации сущностей подключаются автоматически из текущей сборки, поэтому
/// добавление новой таблицы не требует изменения этого класса — достаточно создать
/// класс конфигурации <see cref="Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{TEntity}"/>.
/// </summary>
public class RpgDbContext : DbContext
{
    /// <summary>
    /// Создаёт контекст базы данных.
    /// </summary>
    /// <param name="options">Параметры контекста, задаваемые контейнером зависимостей.</param>
    public RpgDbContext(DbContextOptions<RpgDbContext> options)
        : base(options)
    {
    }

    // ---------- Системные данные ----------

    /// <summary>Игровые системы.</summary>
    public DbSet<GameSystem> GameSystems => Set<GameSystem>();

    /// <summary>Контент-паки.</summary>
    public DbSet<ContentPack> ContentPacks => Set<ContentPack>();

    /// <summary>Дополнительные имена объектов из подключенных пакетов.</summary>
    public DbSet<ContentAlias> ContentAliases => Set<ContentAlias>();

    // ---------- Характеристики и навыки ----------

    /// <summary>Характеристики игровых систем.</summary>
    public DbSet<AttributeDefinition> Attributes => Set<AttributeDefinition>();

    /// <summary>Навыки.</summary>
    public DbSet<Skill> Skills => Set<Skill>();

    // ---------- Развитие персонажа ----------

    /// <summary>Классы персонажей.</summary>
    public DbSet<CharacterClass> Classes => Set<CharacterClass>();

    /// <summary>Подклассы.</summary>
    public DbSet<Subclass> Subclasses => Set<Subclass>();

    /// <summary>Расы.</summary>
    public DbSet<Race> Races => Set<Race>();

    /// <summary>Происхождения.</summary>
    public DbSet<Background> Backgrounds => Set<Background>();

    /// <summary>Черты.</summary>
    public DbSet<Trait> Traits => Set<Trait>();

    /// <summary>Способности.</summary>
    public DbSet<Ability> Abilities => Set<Ability>();

    // ---------- Магия, ресурсы и эффекты ----------

    /// <summary>Заклинания.</summary>
    public DbSet<Spell> Spells => Set<Spell>();

    /// <summary>Ресурсы.</summary>
    public DbSet<GameResource> Resources => Set<GameResource>();

    /// <summary>Эффекты.</summary>
    public DbSet<Effect> Effects => Set<Effect>();

    /// <summary>Пользовательские кубики.</summary>
    public DbSet<DieType> DieTypes => Set<DieType>();

    /// <summary>Виды отдыха.</summary>
    public DbSet<RestType> RestTypes => Set<RestType>();

    /// <summary>Восстановления ресурсов при отдыхе.</summary>
    public DbSet<RestRestore> RestRestores => Set<RestRestore>();

    // ---------- Предметы ----------

    /// <summary>Предметы.</summary>
    public DbSet<Item> Items => Set<Item>();

    /// <summary>Оружейные свойства предметов.</summary>
    public DbSet<Weapon> Weapons => Set<Weapon>();

    /// <summary>Бонусы, которые предметы дают персонажу.</summary>
    public DbSet<ItemBonus> ItemBonuses => Set<ItemBonus>();

    /// <summary>Действия, происходящие при использовании предметов.</summary>
    public DbSet<ItemUseEffect> ItemUseEffects => Set<ItemUseEffect>();

    /// <summary>Бонусы, которые эффекты дают персонажу.</summary>
    public DbSet<EffectBonus> EffectBonuses => Set<EffectBonus>();

    /// <summary>Категории предметов инвентаря.</summary>
    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();

    /// <summary>Слоты экипировки.</summary>
    public DbSet<EquipmentSlot> EquipmentSlots => Set<EquipmentSlot>();

    // ---------- Персонажи ----------

    /// <summary>Персонажи.</summary>
    public DbSet<Character> Characters => Set<Character>();

    /// <summary>Значения характеристик персонажей.</summary>
    public DbSet<CharacterAttributeValue> CharacterAttributes => Set<CharacterAttributeValue>();

    /// <summary>Владение навыками.</summary>
    public DbSet<CharacterSkill> CharacterSkills => Set<CharacterSkill>();

    /// <summary>Полученные черты.</summary>
    public DbSet<CharacterTrait> CharacterTraits => Set<CharacterTrait>();

    /// <summary>Авторские способности персонажей.</summary>
    public DbSet<CharacterCustomAbility> CharacterCustomAbilities => Set<CharacterCustomAbility>();

    /// <summary>Деньги персонажей.</summary>
    public DbSet<CharacterCurrency> CharacterCurrencies => Set<CharacterCurrency>();

    /// <summary>Ресурсы персонажей.</summary>
    public DbSet<CharacterResource> CharacterResources => Set<CharacterResource>();

    /// <summary>Заклинания персонажей.</summary>
    public DbSet<CharacterSpell> CharacterSpells => Set<CharacterSpell>();

    /// <summary>Действующие эффекты.</summary>
    public DbSet<CharacterEffect> CharacterEffects => Set<CharacterEffect>();

    /// <summary>Записи инвентаря.</summary>
    public DbSet<InventoryItem> Inventory => Set<InventoryItem>();

    /// <summary>Экипировка персонажей.</summary>
    public DbSet<CharacterEquipment> CharacterEquipment => Set<CharacterEquipment>();

    // ---------- Правила и формулы ----------

    /// <summary>Именованные формулы.</summary>
    public DbSet<Formula> Formulas => Set<Formula>();

    /// <summary>Игровые правила.</summary>
    public DbSet<GameRule> Rules => Set<GameRule>();

    /// <summary>Описания пользовательских свойств.</summary>
    public DbSet<PropertyDefinition> PropertyDefinitions => Set<PropertyDefinition>();

    /// <summary>Значения пользовательских свойств.</summary>
    public DbSet<PropertyValue> PropertyValues => Set<PropertyValue>();

    // ---------- Кампании ----------

    /// <summary>Кампании.</summary>
    public DbSet<Campaign> Campaigns => Set<Campaign>();

    /// <summary>Состав кампаний.</summary>
    public DbSet<CampaignMember> CampaignMembers => Set<CampaignMember>();

    /// <summary>События кампаний.</summary>
    public DbSet<CampaignEvent> CampaignEvents => Set<CampaignEvent>();

    /// <summary>Макросы.</summary>
    public DbSet<Macro> Macros => Set<Macro>();

    /// <summary>Макеты листа персонажа.</summary>
    public DbSet<SheetLayout> SheetLayouts => Set<SheetLayout>();

    /// <summary>Вкладки макетов.</summary>
    public DbSet<SheetLayoutTab> SheetLayoutTabs => Set<SheetLayoutTab>();

    /// <summary>Панели макетов.</summary>
    public DbSet<SheetLayoutPanel> SheetLayoutPanels => Set<SheetLayoutPanel>();

    /// <summary>Очереди хода.</summary>
    public DbSet<InitiativeTracker> InitiativeTrackers => Set<InitiativeTracker>();

    /// <summary>Участники очередей хода.</summary>
    public DbSet<InitiativeEntry> InitiativeEntries => Set<InitiativeEntry>();

    /// <summary>Неигровые персонажи.</summary>
    public DbSet<Npc> Npcs => Set<Npc>();

    /// <summary>Монстры.</summary>
    public DbSet<Monster> Monsters => Set<Monster>();

    /// <summary>Локации.</summary>
    public DbSet<Location> Locations => Set<Location>();

    /// <summary>Квесты.</summary>
    public DbSet<Quest> Quests => Set<Quest>();

    /// <summary>Этапы квестов.</summary>
    public DbSet<QuestStep> QuestSteps => Set<QuestStep>();

    /// <summary>Заметки.</summary>
    public DbSet<Note> Notes => Set<Note>();

    // ---------- Журналы и операции ----------

    /// <summary>Журнал бросков кубиков.</summary>
    public DbSet<DiceRoll> DiceHistory => Set<DiceRoll>();

    /// <summary>Журнал действий.</summary>
    public DbSet<HistoryEntry> History => Set<HistoryEntry>();






    /// <summary>Сведения о резервных копиях базы данных.</summary>
    public DbSet<BackupRecord> Backups => Set<BackupRecord>();

    /// <inheritdoc />
    public override int SaveChanges()
    {
        ApplyTimestamps();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        base.ConfigureConventions(configurationBuilder);

        // Соглашение применяется ко всем свойствам модели, поэтому новые сущности
        // с отметками времени автоматически получают корректную сортировку.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    /// <summary>
    /// Проставляет отметки времени создания и изменения.
    /// Выполняется централизованно, чтобы каждая служба не заполняла эти поля вручную.
    /// </summary>
    private void ApplyTimestamps()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.ModifiedAt = now;

                    // Момент создания записи изменению не подлежит.
                    entry.Property(entity => entity.CreatedAt).IsModified = false;
                    break;

                default:
                    break;
            }
        }
    }
}
