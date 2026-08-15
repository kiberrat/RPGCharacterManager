using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Database.Configuration;

/// <summary>
/// Общая конфигурация игровых объектов, редактируемых пользователем.
///
/// Единая базовая конфигурация гарантирует, что все игровые объекты получают
/// одинаковые ограничения и индексы: поиск по названию и уникальность внутреннего
/// имени в пределах игровой системы. Добавление нового типа контента сводится
/// к наследованию от этого класса.
/// </summary>
/// <typeparam name="TEntity">Тип игрового объекта.</typeparam>
internal abstract class ContentEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : ContentEntity
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Name)
            .HasMaxLength(FieldLengths.Name)
            .IsRequired();

        builder.Property(entity => entity.SystemName)
            .HasMaxLength(FieldLengths.SystemName)
            .IsRequired();

        builder.Property(entity => entity.Description).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.Source).HasMaxLength(FieldLengths.MediumText);
        builder.Property(entity => entity.Image).HasMaxLength(FieldLengths.MediumText);
        builder.Property(entity => entity.Icon).HasMaxLength(FieldLengths.ShortText);

        // Индекс по названию обеспечивает быстрый поиск и сортировку в списках,
        // рассчитанных на сотни тысяч записей.
        builder.HasIndex(entity => entity.Name);
        builder.HasIndex(entity => entity.GameSystemId);

        // Внутреннее имя уникально в пределах игровой системы: формулы и правила
        // ссылаются на объекты именно по нему.
        builder.HasIndex(entity => new { entity.GameSystemId, entity.SystemName }).IsUnique();

        builder.HasOne(entity => entity.GameSystem)
            .WithMany()
            .HasForeignKey(entity => entity.GameSystemId)
            // Удаление игровой системы не должно удалять пользовательский контент.
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(entity => entity.ContentPack)
            .WithMany()
            .HasForeignKey(entity => entity.ContentPackId)
            .OnDelete(DeleteBehavior.SetNull);

        ConfigureEntity(builder);
    }

    /// <summary>
    /// Задаёт настройки, специфичные для конкретного типа игрового объекта.
    /// </summary>
    /// <param name="builder">Построитель конфигурации сущности.</param>
    protected virtual void ConfigureEntity(EntityTypeBuilder<TEntity> builder)
    {
    }
}
