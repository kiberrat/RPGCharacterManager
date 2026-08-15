using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Database.Configuration;

/// <summary>Конфигурация персонажей.</summary>
internal sealed class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Character> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Name).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(entity => entity.FullName).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Portrait).HasMaxLength(FieldLengths.MediumText);
        builder.Property(entity => entity.Description).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.Biography).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.Notes).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.Alignment).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Age).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Height).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Weight).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Gender).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Languages).HasMaxLength(FieldLengths.MediumText);
        builder.Property(entity => entity.Mana).HasPrecision(18, 2);
        builder.Property(entity => entity.ManaMaximum).HasPrecision(18, 2);

        // Поиск персонажей выполняется по имени и игровой системе.
        builder.HasIndex(entity => entity.Name);
        builder.HasIndex(entity => entity.GameSystemId);

        // Удаление справочных объектов не должно удалять персонажа:
        // ссылка просто очищается, а пользователь выбирает замену.
        builder.HasOne(entity => entity.Race).WithMany()
            .HasForeignKey(entity => entity.RaceId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(entity => entity.Class).WithMany()
            .HasForeignKey(entity => entity.ClassId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(entity => entity.Subclass).WithMany()
            .HasForeignKey(entity => entity.SubclassId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(entity => entity.Background).WithMany()
            .HasForeignKey(entity => entity.BackgroundId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(entity => entity.GameSystem).WithMany()
            .HasForeignKey(entity => entity.GameSystemId).OnDelete(DeleteBehavior.SetNull);

        // Данные, принадлежащие персонажу, удаляются вместе с ним.
        builder.HasMany(entity => entity.Attributes).WithOne(value => value.Character)
            .HasForeignKey(value => value.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(entity => entity.Skills).WithOne(value => value.Character)
            .HasForeignKey(value => value.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(entity => entity.Traits).WithOne(value => value.Character)
            .HasForeignKey(value => value.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(entity => entity.CustomAbilities).WithOne(value => value.Character)
            .HasForeignKey(value => value.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(entity => entity.Currencies).WithOne(value => value.Character)
            .HasForeignKey(value => value.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(entity => entity.Resources).WithOne(value => value.Character)
            .HasForeignKey(value => value.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(entity => entity.Spells).WithOne(value => value.Character)
            .HasForeignKey(value => value.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(entity => entity.Inventory).WithOne(value => value.Character)
            .HasForeignKey(value => value.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(entity => entity.Equipment).WithOne(value => value.Character)
            .HasForeignKey(value => value.CharacterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(entity => entity.Effects).WithOne(value => value.Character)
            .HasForeignKey(value => value.CharacterId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация авторских способностей персонажа.</summary>
internal sealed class CharacterCustomAbilityConfiguration : IEntityTypeConfiguration<CharacterCustomAbility>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CharacterCustomAbility> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.Category).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Formula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.Requirements).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.DependencyDescription).HasMaxLength(FieldLengths.MediumText);
        builder.HasIndex(entity => entity.CharacterId);
    }
}

/// <summary>Конфигурация денег персонажа.</summary>
internal sealed class CharacterCurrencyConfiguration : IEntityTypeConfiguration<CharacterCurrency>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CharacterCurrency> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(entity => entity.Amount).HasPrecision(18, 2);
        builder.HasIndex(entity => entity.CharacterId);
    }
}

/// <summary>Конфигурация значений характеристик персонажа.</summary>
internal sealed class CharacterAttributeValueConfiguration : IEntityTypeConfiguration<CharacterAttributeValue>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CharacterAttributeValue> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);

        // Одна характеристика может быть назначена персонажу только один раз.
        builder.HasIndex(entity => new { entity.CharacterId, entity.AttributeId }).IsUnique();

        builder.HasOne(entity => entity.Attribute).WithMany()
            .HasForeignKey(entity => entity.AttributeId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация владения навыками.</summary>
internal sealed class CharacterSkillConfiguration : IEntityTypeConfiguration<CharacterSkill>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CharacterSkill> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => new { entity.CharacterId, entity.SkillId }).IsUnique();

        builder.HasOne(entity => entity.Skill).WithMany()
            .HasForeignKey(entity => entity.SkillId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация полученных черт.</summary>
internal sealed class CharacterTraitConfiguration : IEntityTypeConfiguration<CharacterTrait>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CharacterTrait> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Source).HasMaxLength(FieldLengths.MediumText);
        builder.HasIndex(entity => new { entity.CharacterId, entity.TraitId }).IsUnique();

        builder.HasOne(entity => entity.Trait).WithMany()
            .HasForeignKey(entity => entity.TraitId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация ресурсов персонажа.</summary>
internal sealed class CharacterResourceConfiguration : IEntityTypeConfiguration<CharacterResource>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CharacterResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => new { entity.CharacterId, entity.ResourceId }).IsUnique();

        builder.HasOne(entity => entity.Resource).WithMany()
            .HasForeignKey(entity => entity.ResourceId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация заклинаний персонажа.</summary>
internal sealed class CharacterSpellConfiguration : IEntityTypeConfiguration<CharacterSpell>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CharacterSpell> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Source).HasMaxLength(FieldLengths.MediumText);
        builder.HasIndex(entity => new { entity.CharacterId, entity.SpellId }).IsUnique();

        builder.HasOne(entity => entity.Spell).WithMany()
            .HasForeignKey(entity => entity.SpellId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация действующих эффектов.</summary>
internal sealed class CharacterEffectConfiguration : IEntityTypeConfiguration<CharacterEffect>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CharacterEffect> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Source).HasMaxLength(FieldLengths.MediumText);

        // Один эффект может быть наложен на персонажа несколько раз из разных
        // источников, поэтому уникальный индекс здесь неприменим.
        builder.HasIndex(entity => entity.CharacterId);

        builder.HasOne(entity => entity.Effect).WithMany()
            .HasForeignKey(entity => entity.EffectId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация записей инвентаря.</summary>
internal sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Note).HasMaxLength(FieldLengths.MediumText);
        builder.HasIndex(entity => entity.CharacterId);
        builder.HasIndex(entity => entity.ItemId);

        builder.HasOne(entity => entity.Item).WithMany()
            .HasForeignKey(entity => entity.ItemId).OnDelete(DeleteBehavior.Cascade);

        // Контейнеры: сумка может содержать другие записи инвентаря.
        // Удаление контейнера не удаляет его содержимое — предметы выпадают в общий список.
        builder.HasOne(entity => entity.Container)
            .WithMany()
            .HasForeignKey(entity => entity.ContainerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Конфигурация экипировки персонажа.</summary>
internal sealed class CharacterEquipmentConfiguration : IEntityTypeConfiguration<CharacterEquipment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CharacterEquipment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);

        // Один и тот же предмет не может быть экипирован дважды.
        builder.HasIndex(entity => new { entity.CharacterId, entity.InventoryItemId }).IsUnique();
        builder.HasIndex(entity => new { entity.CharacterId, entity.SlotId });

        builder.HasOne(entity => entity.Slot).WithMany()
            .HasForeignKey(entity => entity.SlotId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entity => entity.InventoryItem).WithMany()
            .HasForeignKey(entity => entity.InventoryItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация оружейных свойств.</summary>
internal sealed class WeaponConfiguration : IEntityTypeConfiguration<Weapon>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Weapon> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.AttackDiceFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.AttackFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.DamageFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.CriticalFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.Category).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.DamageType).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Range).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.ReloadTime).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Properties).HasMaxLength(FieldLengths.Description);
        builder.HasIndex(entity => entity.ItemId).IsUnique();

        // Удаление характеристики или навыка не должно удалять оружие: оно лишь
        // перестаёт масштабироваться и продолжает существовать.
        builder.HasOne(entity => entity.ScalingAttribute).WithMany()
            .HasForeignKey(entity => entity.ScalingAttributeId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(entity => entity.ProficiencySkill).WithMany()
            .HasForeignKey(entity => entity.ProficiencySkillId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Конфигурация бонусов предметов.</summary>
internal sealed class ItemBonusConfiguration : IEntityTypeConfiguration<ItemBonus>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ItemBonus> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Formula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.Condition).HasMaxLength(FieldLengths.Expression);

        // Бонусы всегда читаются вместе с предметом.
        builder.HasIndex(entity => entity.ItemId);

        // Удаление характеристики или ресурса не удаляет предмет: бонус лишь
        // перестаёт указывать на цель, и пользователь видит это в редакторе.
        builder.HasOne(entity => entity.Attribute).WithMany()
            .HasForeignKey(entity => entity.AttributeId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(entity => entity.Resource).WithMany()
            .HasForeignKey(entity => entity.ResourceId).OnDelete(DeleteBehavior.SetNull);
    }
}
