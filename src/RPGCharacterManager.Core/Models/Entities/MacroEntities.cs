namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Макрос: последовательность действий, которую запускает человек.
///
/// Условия и действия хранятся теми же структурами, что у игрового правила,
/// и выполняются тем же движком. Разница между правилом и макросом одна:
/// правило запускает событие, а макрос — нажатие (решение Р-97).
/// </summary>
public class Macro : ContentEntity
{
    /// <summary>Категория макроса: бой, персонаж, магия, предметы.</summary>
    public string? Category { get; set; }

    /// <summary>
    /// Сочетание клавиш, запускающее макрос, например <c>Ctrl+1</c>.
    /// Пустое значение означает, что макрос запускается только из раздела.
    /// </summary>
    public string? Hotkey { get; set; }

    /// <summary>Дерево условий в формате JSON. Пусто — макрос выполняется всегда.</summary>
    public string? Condition { get; set; }

    /// <summary>Описание выполняемых действий в формате JSON.</summary>
    public string ActionsJson { get; set; } = "[]";

    /// <summary>Порядок отображения в списке макросов.</summary>
    public int SortOrder { get; set; }

    /// <summary>Макрос доступен к запуску.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Идентификатор персонажа, если макрос принадлежит только ему.</summary>
    public Guid? CharacterId { get; set; }

    /// <summary>Автор макроса.</summary>
    public string? Author { get; set; }

    /// <summary>Версия макроса.</summary>
    public string Version { get; set; } = "1.0";
}
