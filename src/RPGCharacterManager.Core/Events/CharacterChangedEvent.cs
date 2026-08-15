namespace RPGCharacterManager.Core.Events;

/// <summary>
/// Вид изменения, произошедшего с персонажем.
/// </summary>
public enum CharacterChangeKind
{
    /// <summary>Персонаж создан.</summary>
    Created = 0,

    /// <summary>Уровень персонажа изменён.</summary>
    LevelChanged = 1,

    /// <summary>Параметры персонажа пересчитаны.</summary>
    Recalculated = 2,

    /// <summary>Персонаж удалён.</summary>
    Deleted = 3,
}

/// <summary>
/// Персонаж создан, изменён или удалён.
///
/// Событие позволяет открытым разделам приложения обновиться, не зная друг о друге:
/// список персонажей перечитывает данные, когда мастер создаёт нового персонажа.
/// </summary>
/// <param name="CharacterId">Идентификатор персонажа.</param>
/// <param name="Kind">Вид изменения.</param>
public sealed record CharacterChangedEvent(Guid CharacterId, CharacterChangeKind Kind);
