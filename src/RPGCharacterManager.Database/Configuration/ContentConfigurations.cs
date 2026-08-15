using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Database.Configuration;

/// <summary>Конфигурация характеристик.</summary>
internal sealed class AttributeDefinitionConfiguration : ContentEntityConfiguration<AttributeDefinition>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<AttributeDefinition> builder)
    {
        builder.Property(entity => entity.Category).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Formula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.ModifierFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.Color).HasMaxLength(FieldLengths.ShortText);
        builder.HasIndex(entity => entity.Category);
    }
}

/// <summary>Конфигурация навыков.</summary>
internal sealed class SkillConfiguration : ContentEntityConfiguration<Skill>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Skill> builder)
    {
        builder.Property(entity => entity.Category).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Formula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.Requirements).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.Color).HasMaxLength(FieldLengths.ShortText);

        builder.HasOne(entity => entity.LinkedAttribute)
            .WithMany()
            .HasForeignKey(entity => entity.LinkedAttributeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(entity => entity.Category);
    }
}

/// <summary>Конфигурация классов персонажей.</summary>
internal sealed class CharacterClassConfiguration : ContentEntityConfiguration<CharacterClass>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<CharacterClass> builder)
    {
        builder.Property(entity => entity.HitDiceFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.Requirements).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.Role).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Color).HasMaxLength(FieldLengths.ShortText);

        builder.HasOne(entity => entity.PrimaryAttribute)
            .WithMany()
            .HasForeignKey(entity => entity.PrimaryAttributeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(entity => entity.Subclasses)
            .WithOne(subclass => subclass.Class)
            .HasForeignKey(subclass => subclass.ClassId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация подклассов.</summary>
internal sealed class SubclassConfiguration : ContentEntityConfiguration<Subclass>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Subclass> builder)
    {
        builder.Property(entity => entity.Requirements).HasMaxLength(FieldLengths.Expression);
        builder.HasIndex(entity => entity.ClassId);
    }
}

/// <summary>Конфигурация рас.</summary>
internal sealed class RaceConfiguration : ContentEntityConfiguration<Race>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Race> builder)
    {
        builder.Property(entity => entity.Size).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Languages).HasMaxLength(FieldLengths.MediumText);
        builder.Property(entity => entity.Requirements).HasMaxLength(FieldLengths.Expression);
    }
}

/// <summary>Конфигурация происхождений.</summary>
internal sealed class BackgroundConfiguration : ContentEntityConfiguration<Background>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Background> builder) =>
        builder.Property(entity => entity.Requirements).HasMaxLength(FieldLengths.Expression);
}

/// <summary>Конфигурация черт.</summary>
internal sealed class TraitConfiguration : ContentEntityConfiguration<Trait>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Trait> builder)
    {
        builder.Property(entity => entity.Category).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Requirements).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.Formula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.UsesFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.RechargeRule).HasMaxLength(FieldLengths.MediumText);
        builder.Property(entity => entity.ActivationCondition).HasMaxLength(FieldLengths.Expression);

        // Дерево развития черт: черта может требовать другую черту.
        builder.HasOne(entity => entity.RequiredTrait)
            .WithMany()
            .HasForeignKey(entity => entity.RequiredTraitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(entity => entity.Category);
    }
}

/// <summary>Конфигурация способностей.</summary>
internal sealed class AbilityConfiguration : ContentEntityConfiguration<Ability>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Ability> builder)
    {
        builder.Property(entity => entity.Category).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Formula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.ResourceCostFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.RechargeRule).HasMaxLength(FieldLengths.MediumText);
        builder.Property(entity => entity.Requirements).HasMaxLength(FieldLengths.Expression);

        builder.HasOne(entity => entity.Resource)
            .WithMany()
            .HasForeignKey(entity => entity.ResourceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Конфигурация ресурсов.</summary>
internal sealed class GameResourceConfiguration : ContentEntityConfiguration<GameResource>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<GameResource> builder)
    {
        builder.Property(entity => entity.Category).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.MaximumFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.StartingFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.RestoreRule).HasMaxLength(FieldLengths.MediumText);
        builder.Property(entity => entity.Color).HasMaxLength(FieldLengths.ShortText);
    }
}

/// <summary>Конфигурация заклинаний.</summary>
internal sealed class SpellConfiguration : ContentEntityConfiguration<Spell>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Spell> builder)
    {
        builder.Property(entity => entity.School).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Category).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.CastingTime).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Range).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.AreaOfEffect).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Target).HasMaxLength(FieldLengths.MediumText);
        builder.Property(entity => entity.Components).HasMaxLength(FieldLengths.MediumText);
        builder.Property(entity => entity.Duration).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Formula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.ScalingFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.ResourceCostFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.Requirements).HasMaxLength(FieldLengths.Expression);

        builder.HasOne(entity => entity.Resource)
            .WithMany()
            .HasForeignKey(entity => entity.ResourceId)
            .OnDelete(DeleteBehavior.SetNull);

        // Библиотеки заклинаний насчитывают десятки тысяч записей: фильтрация
        // по уровню и школе должна выполняться по индексу.
        builder.HasIndex(entity => entity.Level);
        builder.HasIndex(entity => entity.School);
    }
}

/// <summary>Конфигурация эффектов.</summary>
internal sealed class EffectConfiguration : ContentEntityConfiguration<Effect>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Effect> builder)
    {
        builder.Property(entity => entity.Category).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Formula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.DurationFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.DurationUnit).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.EndCondition).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.Area).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Color).HasMaxLength(FieldLengths.ShortText);

        // Бонусы принадлежат эффекту и удаляются вместе с ним.
        builder.HasMany(entity => entity.Bonuses)
            .WithOne(bonus => bonus.Effect)
            .HasForeignKey(bonus => bonus.EffectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Панель эффектов показывает их от большего приоритета к меньшему.
        builder.HasIndex(entity => entity.Priority);
    }
}

/// <summary>Конфигурация бонусов эффектов.</summary>
internal sealed class EffectBonusConfiguration : IEntityTypeConfiguration<EffectBonus>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EffectBonus> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Formula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.Condition).HasMaxLength(FieldLengths.Expression);

        // Бонусы всегда читаются вместе с эффектом.
        builder.HasIndex(entity => entity.EffectId);

        // Удаление характеристики или ресурса не удаляет эффект: бонус лишь
        // перестаёт указывать на цель, и пользователь видит это в редакторе.
        builder.HasOne(entity => entity.Attribute).WithMany()
            .HasForeignKey(entity => entity.AttributeId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(entity => entity.Resource).WithMany()
            .HasForeignKey(entity => entity.ResourceId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Конфигурация пользовательских кубиков.</summary>
internal sealed class DieTypeConfiguration : ContentEntityConfiguration<DieType>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<DieType> builder)
    {
        builder.Property(entity => entity.Color).HasMaxLength(FieldLengths.ShortText);

        // Панель бросков показывает кубики в заданном пользователем порядке.
        builder.HasIndex(entity => entity.SortOrder);
    }
}

/// <summary>Конфигурация видов отдыха.</summary>
internal sealed class RestTypeConfiguration : ContentEntityConfiguration<RestType>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<RestType> builder)
    {
        builder.Property(entity => entity.DurationUnit).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Requirements).HasMaxLength(FieldLengths.Expression);

        // Восстановления принадлежат виду отдыха и удаляются вместе с ним.
        builder.HasMany(entity => entity.Restores)
            .WithOne(restore => restore.RestType)
            .HasForeignKey(restore => restore.RestTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Вкладка отдыха показывает виды в заданном пользователем порядке.
        builder.HasIndex(entity => entity.SortOrder);
    }
}

/// <summary>Конфигурация восстановлений при отдыхе.</summary>
internal sealed class RestRestoreConfiguration : IEntityTypeConfiguration<RestRestore>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RestRestore> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Formula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.Condition).HasMaxLength(FieldLengths.Expression);

        // Восстановления всегда читаются вместе с видом отдыха.
        builder.HasIndex(entity => entity.RestTypeId);

        // Удаление ресурса не удаляет вид отдыха: восстановление лишь перестаёт
        // указывать на цель, и пользователь видит это в редакторе.
        builder.HasOne(entity => entity.Resource)
            .WithMany()
            .HasForeignKey(entity => entity.ResourceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Конфигурация монстров.</summary>
internal sealed class MonsterConfiguration : ContentEntityConfiguration<Monster>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Monster> builder)
    {
        builder.Property(entity => entity.Challenge).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.CreatureType).HasMaxLength(FieldLengths.Name);
        builder.HasIndex(entity => entity.Challenge);
    }
}

/// <summary>Конфигурация слотов экипировки.</summary>
internal sealed class EquipmentSlotConfiguration : ContentEntityConfiguration<EquipmentSlot>;

/// <summary>Конфигурация формул.</summary>
internal sealed class FormulaConfiguration : ContentEntityConfiguration<Formula>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Formula> builder)
    {
        builder.Property(entity => entity.Expression)
            .HasMaxLength(FieldLengths.Expression)
            .IsRequired();

        builder.Property(entity => entity.Category).HasMaxLength(FieldLengths.Name);
    }
}

/// <summary>Конфигурация игровых правил.</summary>
internal sealed class GameRuleConfiguration : ContentEntityConfiguration<GameRule>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<GameRule> builder)
    {
        builder.Property(entity => entity.Category).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Trigger).HasMaxLength(FieldLengths.Name).IsRequired();

        // Условия и действия хранятся деревом в формате JSON: у сложного правила
        // с несколькими вложенными группами длина превышает размер одного выражения.
        builder.Property(entity => entity.Condition).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.ActionsJson).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.Version).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Author).HasMaxLength(FieldLengths.Name);

        // Движок правил выбирает правила по событию и упорядочивает их по приоритету.
        builder.HasIndex(entity => new { entity.Trigger, entity.Enabled });
        builder.HasIndex(entity => entity.CharacterId);
        builder.HasIndex(entity => entity.CampaignId);
    }
}

/// <summary>Конфигурация описаний пользовательских свойств.</summary>
internal sealed class PropertyDefinitionConfiguration : ContentEntityConfiguration<PropertyDefinition>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<PropertyDefinition> builder)
    {
        builder.Property(entity => entity.DisplayName).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(entity => entity.TargetType).HasMaxLength(FieldLengths.SystemName).IsRequired();
        builder.Property(entity => entity.ReferenceTargetType).HasMaxLength(FieldLengths.SystemName);
        builder.Property(entity => entity.DefaultValue).HasMaxLength(FieldLengths.MediumText);
        builder.Property(entity => entity.Category).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Group).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Formula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.ValidationRule).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.AllowedValues).HasMaxLength(FieldLengths.Description);

        // Свойства выбираются по типу целевого объекта при построении форм.
        builder.HasIndex(entity => entity.TargetType);
    }
}

/// <summary>Конфигурация предметов.</summary>
internal sealed class ItemConfiguration : ContentEntityConfiguration<Item>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Item> builder)
    {
        builder.Property(entity => entity.ItemType).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Rarity).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Currency).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Requirements).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.ChargesFormula).HasMaxLength(FieldLengths.Expression);

        builder.HasOne(entity => entity.Weapon)
            .WithOne(weapon => weapon.Item)
            .HasForeignKey<Weapon>(weapon => weapon.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Бонусы принадлежат предмету и удаляются вместе с ним.
        builder.HasMany(entity => entity.Bonuses)
            .WithOne(bonus => bonus.Item)
            .HasForeignKey(bonus => bonus.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Действия использования принадлежат предмету и удаляются вместе с ним.
        builder.HasMany(entity => entity.UseEffects)
            .WithOne(effect => effect.Item)
            .HasForeignKey(effect => effect.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Удаление слота не удаляет предмет: он просто перестаёт надеваться.
        builder.HasOne(entity => entity.EquipmentSlot)
            .WithMany()
            .HasForeignKey(entity => entity.EquipmentSlotId)
            .OnDelete(DeleteBehavior.SetNull);

        // Удаление категории не удаляет предметы: они переходят в «Без категории».
        builder.HasOne(entity => entity.Category)
            .WithMany()
            .HasForeignKey(entity => entity.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(entity => entity.ItemType);
        builder.HasIndex(entity => entity.Rarity);
        builder.HasIndex(entity => entity.EquipmentSlotId);

        // Инвентарь отбирает предметы по категории при каждом переключении раздела.
        builder.HasIndex(entity => entity.CategoryId);
    }
}

/// <summary>Конфигурация категорий предметов.</summary>
internal sealed class ItemCategoryConfiguration : ContentEntityConfiguration<ItemCategory>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<ItemCategory> builder)
    {
        // Удаление категории поднимает вложенные категории на уровень выше,
        // а не уничтожает целую ветвь вместе с ними.
        builder.HasOne(entity => entity.Parent)
            .WithMany(entity => entity.Children)
            .HasForeignKey(entity => entity.ParentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(entity => entity.ParentId);
    }
}

/// <summary>Конфигурация действий использования предметов.</summary>
internal sealed class ItemUseEffectConfiguration : IEntityTypeConfiguration<ItemUseEffect>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ItemUseEffect> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Formula).HasMaxLength(FieldLengths.Expression);

        // Действия всегда читаются вместе с предметом.
        builder.HasIndex(entity => entity.ItemId);

        // Удаление ресурса не удаляет предмет: действие лишь перестаёт
        // указывать на цель, и пользователь видит это в редакторе.
        builder.HasOne(entity => entity.Resource)
            .WithMany()
            .HasForeignKey(entity => entity.ResourceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
