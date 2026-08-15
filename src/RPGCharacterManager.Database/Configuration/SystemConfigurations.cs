using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Database.Configuration;

/// <summary>Конфигурация игровых систем.</summary>
internal sealed class GameSystemConfiguration : IEntityTypeConfiguration<GameSystem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GameSystem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(entity => entity.SystemName).HasMaxLength(FieldLengths.SystemName).IsRequired();
        builder.Property(entity => entity.Version).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Author).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Description).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.Icon).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.CarryCapacityFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.WeightUnit).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.KnownSpellsFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.PreparedSpellsFormula).HasMaxLength(FieldLengths.Expression);
        builder.Property(entity => entity.InitiativeFormula).HasMaxLength(FieldLengths.Expression);

        builder.HasIndex(entity => entity.SystemName).IsUnique();
        builder.HasIndex(entity => entity.Name);

        builder.HasMany(entity => entity.ContentPacks)
            .WithOne(pack => pack.GameSystem)
            .HasForeignKey(pack => pack.GameSystemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Конфигурация контент-паков.</summary>
internal sealed class ContentPackConfiguration : IEntityTypeConfiguration<ContentPack>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContentPack> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(entity => entity.Version).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Author).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Description).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.License).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.RequiredVersion).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.DependenciesJson).HasMaxLength(FieldLengths.Description);

        // Установленный пак отыскивается по названию: обновление приходит
        // отдельным файлом, и связать его со старой версией больше нечем.
        builder.HasIndex(entity => entity.Name);
    }
}

/// <summary>Конфигурация дополнительных имён игровых объектов.</summary>
internal sealed class ContentAliasConfiguration : IEntityTypeConfiguration<ContentAlias>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ContentAlias> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ContentTypeId).HasMaxLength(FieldLengths.SystemName).IsRequired();
        builder.Property(entity => entity.TargetSystemName).HasMaxLength(FieldLengths.SystemName).IsRequired();
        builder.Property(entity => entity.Alias).HasMaxLength(FieldLengths.Name).IsRequired();

        builder.HasIndex(entity => entity.Alias);
        builder.HasIndex(entity => new
        {
            entity.ContentPackId,
            entity.GameSystemId,
            entity.ContentTypeId,
            entity.TargetSystemName,
            entity.Alias,
        }).IsUnique();

        builder.HasOne(entity => entity.ContentPack)
            .WithMany()
            .HasForeignKey(entity => entity.ContentPackId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entity => entity.GameSystem)
            .WithMany()
            .HasForeignKey(entity => entity.GameSystemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация значений пользовательских свойств.</summary>
internal sealed class PropertyValueConfiguration : IEntityTypeConfiguration<PropertyValue>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PropertyValue> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Value).HasMaxLength(FieldLengths.Description);

        // Значения выбираются по объекту, поэтому индекс по ObjectId обязателен:
        // без него загрузка карточки объекта выполняла бы полный перебор таблицы.
        builder.HasIndex(entity => entity.ObjectId);
        builder.HasIndex(entity => new { entity.ObjectId, entity.PropertyDefinitionId }).IsUnique();

        builder.HasOne(entity => entity.PropertyDefinition)
            .WithMany()
            .HasForeignKey(entity => entity.PropertyDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация кампаний.</summary>
internal sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.World).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.StartDate).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Notes).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.Image).HasMaxLength(FieldLengths.MediumText);
        builder.HasIndex(entity => entity.Name);

        builder.HasOne(entity => entity.GameSystem).WithMany()
            .HasForeignKey(entity => entity.GameSystemId).OnDelete(DeleteBehavior.SetNull);

        // Состав и хронология принадлежат кампании и удаляются вместе с ней.
        builder.HasMany(entity => entity.Members).WithOne(member => member.Campaign)
            .HasForeignKey(member => member.CampaignId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(entity => entity.Events).WithOne(item => item.Campaign)
            .HasForeignKey(item => item.CampaignId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация состава кампании.</summary>
internal sealed class CampaignMemberConfiguration : IEntityTypeConfiguration<CampaignMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CampaignMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ObjectKind).HasMaxLength(FieldLengths.SystemName).IsRequired();
        builder.Property(entity => entity.Role).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Notes).HasMaxLength(FieldLengths.Description);

        // Состав кампании читается целиком и группируется по виду объекта.
        builder.HasIndex(entity => new { entity.CampaignId, entity.ObjectKind });

        // Один объект входит в кампанию единожды: два одинаковых участника
        // означали бы, что мастер потерял из виду одного из них.
        builder.HasIndex(entity => new { entity.CampaignId, entity.ObjectKind, entity.ObjectId }).IsUnique();
    }
}

/// <summary>Конфигурация событий кампании.</summary>
internal sealed class CampaignEventConfiguration : IEntityTypeConfiguration<CampaignEvent>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CampaignEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.GameDate).HasMaxLength(FieldLengths.Name);

        builder.HasIndex(entity => new { entity.CampaignId, entity.SortOrder });
    }
}

/// <summary>Конфигурация макроса.</summary>
internal sealed class MacroConfiguration : ContentEntityConfiguration<Macro>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Macro> builder)
    {
        builder.Property(entity => entity.Category).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Hotkey).HasMaxLength(FieldLengths.ShortText);
        // Условия и действия хранятся деревом в формате JSON — так же, как у правил.
        builder.Property(entity => entity.Condition).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.ActionsJson).HasMaxLength(FieldLengths.Description).IsRequired();
        builder.Property(entity => entity.Version).HasMaxLength(FieldLengths.ShortText);
        builder.Property(entity => entity.Author).HasMaxLength(FieldLengths.Name);

        // Сочетание клавиш ищется при каждом нажатии, а список макросов
        // выстраивается по порядку отображения.
        builder.HasIndex(entity => entity.Hotkey);
        builder.HasIndex(entity => entity.SortOrder);

        // Макрос персонажа исчезает вместе с ним: выполнять его будет не над кем.
        builder.HasOne<Character>().WithMany()
            .HasForeignKey(entity => entity.CharacterId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация макета листа персонажа.</summary>
internal sealed class SheetLayoutConfiguration : IEntityTypeConfiguration<SheetLayout>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SheetLayout> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.HasIndex(entity => entity.Name);

        // Применяемый макет ищется по этому признаку при каждом открытии листа.
        builder.HasIndex(entity => entity.IsDefault);

        builder.HasMany(entity => entity.Tabs).WithOne(tab => tab.Layout)
            .HasForeignKey(tab => tab.LayoutId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация вкладки макета.</summary>
internal sealed class SheetLayoutTabConfiguration : IEntityTypeConfiguration<SheetLayoutTab>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SheetLayoutTab> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.HasIndex(entity => new { entity.LayoutId, entity.SortOrder });

        builder.HasMany(entity => entity.Panels).WithOne(panel => panel.Tab)
            .HasForeignKey(panel => panel.TabId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация панели макета.</summary>
internal sealed class SheetLayoutPanelConfiguration : IEntityTypeConfiguration<SheetLayoutPanel>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SheetLayoutPanel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.PanelId).HasMaxLength(FieldLengths.SystemName).IsRequired();
        builder.HasIndex(entity => new { entity.TabId, entity.SortOrder });
    }
}

/// <summary>Конфигурация очереди хода.</summary>
internal sealed class InitiativeTrackerConfiguration : IEntityTypeConfiguration<InitiativeTracker>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InitiativeTracker> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);

        // У кампании ровно одна очередь хода, и ровно одна очередь существует
        // вне кампаний. В SQL значения NULL считаются различными, поэтому
        // единственность очереди без кампании обеспечивает сама служба.
        builder.HasIndex(entity => entity.CampaignId).IsUnique();

        builder.HasOne(entity => entity.Campaign).WithMany()
            .HasForeignKey(entity => entity.CampaignId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(entity => entity.Entries).WithOne(entry => entry.Tracker)
            .HasForeignKey(entry => entry.TrackerId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация участника очереди хода.</summary>
internal sealed class InitiativeEntryConfiguration : IEntityTypeConfiguration<InitiativeEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InitiativeEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => new { entity.TrackerId, entity.SortOrder });

        // Персонаж входит в очередь единожды: два его хода за раунд означали бы,
        // что мастер потерял счёт очереди.
        builder.HasIndex(entity => new { entity.TrackerId, entity.CharacterId }).IsUnique();

        // Удаление персонажа убирает его из очереди: ход того, кого нет, не наступит.
        builder.HasOne(entity => entity.Character).WithMany()
            .HasForeignKey(entity => entity.CharacterId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация неигровых персонажей.</summary>
internal sealed class NpcConfiguration : ContentEntityConfiguration<Npc>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Npc> builder)
    {
        builder.Property(entity => entity.Role).HasMaxLength(FieldLengths.Name);
        builder.Property(entity => entity.Attitude).HasMaxLength(FieldLengths.Name);

        // Удаление локации не должно удалять её жителей: ссылка очищается.
        builder.HasOne(entity => entity.Location).WithMany()
            .HasForeignKey(entity => entity.LocationId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Конфигурация локаций.</summary>
internal sealed class LocationConfiguration : ContentEntityConfiguration<Location>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Location> builder)
    {
        builder.Property(entity => entity.Kind).HasMaxLength(FieldLengths.Name);

        // Иерархия локаций: город содержит районы, район — здания.
        builder.HasOne(entity => entity.ParentLocation).WithMany()
            .HasForeignKey(entity => entity.ParentLocationId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Конфигурация квестов.</summary>
internal sealed class QuestConfiguration : ContentEntityConfiguration<Quest>
{
    /// <inheritdoc />
    protected override void ConfigureEntity(EntityTypeBuilder<Quest> builder)
    {
        builder.Property(entity => entity.Reward).HasMaxLength(FieldLengths.MediumText);
        builder.HasIndex(entity => entity.Status);

        builder.HasOne(entity => entity.Giver).WithMany()
            .HasForeignKey(entity => entity.GiverId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(entity => entity.Location).WithMany()
            .HasForeignKey(entity => entity.LocationId).OnDelete(DeleteBehavior.SetNull);

        // Этапы принадлежат заданию и удаляются вместе с ним.
        builder.HasMany(entity => entity.Steps).WithOne(step => step.Quest)
            .HasForeignKey(step => step.QuestId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Конфигурация этапов квеста.</summary>
internal sealed class QuestStepConfiguration : IEntityTypeConfiguration<QuestStep>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<QuestStep> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(FieldLengths.Description);

        builder.HasIndex(entity => new { entity.QuestId, entity.SortOrder });
    }
}

/// <summary>Конфигурация заметок.</summary>
internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(entity => entity.Text).HasMaxLength(FieldLengths.Description);
        builder.HasIndex(entity => entity.CharacterId);
        builder.HasIndex(entity => entity.CampaignId);
    }
}

/// <summary>Конфигурация журнала бросков кубиков.</summary>
internal sealed class DiceRollConfiguration : IEntityTypeConfiguration<DiceRoll>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DiceRoll> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Formula).HasMaxLength(FieldLengths.Expression).IsRequired();
        builder.Property(entity => entity.Label).HasMaxLength(FieldLengths.Name);

        // Журнал бросков рассчитан на миллионы записей и всегда читается
        // в порядке убывания времени для конкретного персонажа.
        builder.HasIndex(entity => new { entity.CharacterId, entity.CreatedAt });
        builder.HasIndex(entity => entity.CreatedAt);
    }
}

/// <summary>Конфигурация журнала действий.</summary>
internal sealed class HistoryEntryConfiguration : IEntityTypeConfiguration<HistoryEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<HistoryEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Action).HasMaxLength(FieldLengths.Name).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(FieldLengths.Description);
        builder.Property(entity => entity.OldValue).HasMaxLength(FieldLengths.MediumText);
        builder.Property(entity => entity.NewValue).HasMaxLength(FieldLengths.MediumText);
        builder.Property(entity => entity.Subject).HasMaxLength(FieldLengths.Name);

        builder.HasIndex(entity => new { entity.CharacterId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.CampaignId, entity.CreatedAt });
        builder.HasIndex(entity => entity.Action);

        // Статистика сводит события по коду действия и названию объекта:
        // «сколько раз применялось заклинание», «сколько потрачено ресурса».
        builder.HasIndex(entity => new { entity.Action, entity.Subject });
    }
}






/// <summary>Конфигурация сведений о резервных копиях.</summary>
internal sealed class BackupRecordConfiguration : IEntityTypeConfiguration<BackupRecord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BackupRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.FilePath).HasMaxLength(FieldLengths.MediumText).IsRequired();
        builder.Property(entity => entity.Comment).HasMaxLength(FieldLengths.MediumText);
        builder.HasIndex(entity => entity.CreatedAt);
    }
}
