namespace RPGCharacterManager.Core.Models.Entities;

/// <summary>
/// Способ восстановления ресурса при отдыхе.
/// </summary>
public enum RestRestoreMode
{
    /// <summary>Ресурс восстанавливается до максимума.</summary>
    Full = 0,

    /// <summary>Восстанавливается величина, вычисленная формулой.</summary>
    Formula = 1,
}

/// <summary>
/// Вид отдыха.
///
/// Короткий отдых, длительный отдых и любой отдых, придуманный пользователем, —
/// это одна и та же запись с разными значениями: приложение не знает ни одного
/// вида отдыха заранее. Длительность задаётся в единицах самого отдыха, поэтому
/// «1 час» и «6 раундов» описываются одинаково, а переводить одно в другое
/// приложению не приходится.
/// </summary>
public class RestType : ContentEntity
{
    /// <summary>Длительность отдыха в единицах, указанных ниже.</summary>
    public double? Duration { get; set; }

    /// <summary>
    /// Единица длительности: час, минута, раунд, день. Перечень задаёт пользователь.
    /// </summary>
    public string? DurationUnit { get; set; }

    /// <summary>
    /// Требование, при котором отдых доступен.
    /// Пустое значение означает, что отдохнуть можно всегда.
    /// </summary>
    public string? Requirements { get; set; }

    /// <summary>Порядок вида отдыха в списке.</summary>
    public int SortOrder { get; set; }

    /// <summary>Что и насколько восстанавливает отдых.</summary>
    public ICollection<RestRestore> Restores { get; set; } = [];
}

/// <summary>
/// Восстановление ресурса при отдыхе.
/// </summary>
public class RestRestore : EntityBase
{
    /// <summary>Идентификатор вида отдыха.</summary>
    public Guid RestTypeId { get; set; }

    /// <summary>Вид отдыха.</summary>
    public RestType? RestType { get; set; }

    /// <summary>
    /// Восстанавливаемый ресурс.
    /// Пустое значение означает «все ресурсы персонажа»: длительный отдых
    /// в большинстве систем восстанавливает всё сразу, и перечислять ресурсы
    /// поимённо не нужно.
    /// </summary>
    public Guid? ResourceId { get; set; }

    /// <summary>Восстанавливаемый ресурс.</summary>
    public GameResource? Resource { get; set; }

    /// <summary>Способ восстановления.</summary>
    public RestRestoreMode Mode { get; set; }

    /// <summary>
    /// Формула восстанавливаемой величины. Применяется при способе
    /// <see cref="RestRestoreMode.Formula"/> и видит максимум и текущее
    /// значение ресурса.
    /// </summary>
    public string? Formula { get; set; }

    /// <summary>
    /// Условие, при котором восстановление происходит.
    /// Пустое значение означает «всегда».
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>Порядок применения восстановления.</summary>
    public int SortOrder { get; set; }
}
