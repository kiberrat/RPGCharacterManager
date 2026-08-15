namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Пользовательский кубик.
///
/// Встроенные кубики — от d2 до d100 — доступны всегда и в базе данных не хранятся.
/// Здесь описаны кубики, которых нет среди привычных: «d3», «d50», «Кристалл судьбы d777».
/// Приложение не ограничивает количество граней, поэтому игровая система с любыми
/// костями работает без изменения кода.
/// </summary>
public class DieType : ContentEntity
{
    /// <summary>Количество граней кубика.</summary>
    public int Sides { get; set; } = 6;

    /// <summary>Цвет кубика в интерфейсе в виде <c>#RRGGBB</c>.</summary>
    public string? Color { get; set; }

    /// <summary>Порядок кубика среди кнопок панели бросков.</summary>
    public int SortOrder { get; set; }
}
