using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Core.Abstractions.Extensions;

/// <summary>
/// Расширение, без которого другое не работает.
/// </summary>
/// <param name="Name">Название требуемого расширения.</param>
/// <param name="Version">Наименьшая подходящая версия; пусто — любая.</param>
public sealed record ExtensionDependency(string Name, string? Version = null)
{
    /// <summary>Требование в виде строки: «Киберпанк: ядро (не ниже 1.2)».</summary>
    public string Summary => string.IsNullOrWhiteSpace(Version)
        ? Name
        : $"{Name} (не ниже {Version})";
}

/// <summary>
/// Описание расширения: всё, что известно о нём до установки.
/// </summary>
/// <param name="Name">Название расширения.</param>
/// <param name="Version">Версия расширения.</param>
/// <param name="Author">Автор расширения.</param>
/// <param name="Description">Описание расширения.</param>
/// <param name="License">Лицензия, на условиях которой оно распространяется.</param>
/// <param name="GameSystem">Название игровой системы, к которой оно относится.</param>
/// <param name="RequiredVersion">Наименьшая версия приложения.</param>
/// <param name="CreatedAt">Момент создания файла.</param>
/// <param name="FormatVersion">Версия формата пакета.</param>
/// <param name="Dependencies">Расширения, без которых это не работает.</param>
public sealed record ExtensionManifest(
    string Name,
    string Version = "1.0",
    string? Author = null,
    string? Description = null,
    string? License = null,
    string? GameSystem = null,
    string? RequiredVersion = null,
    DateTimeOffset? CreatedAt = null,
    string FormatVersion = ExtensionPackage.FormatVersion,
    IReadOnlyList<ExtensionDependency>? Dependencies = null)
{
    /// <summary>Расширения, без которых это не работает.</summary>
    public IReadOnlyList<ExtensionDependency> Dependencies { get; init; } = Dependencies ?? [];

    /// <summary>Название с версией: «Тёмное фэнтези 1.2».</summary>
    public string Title => $"{Name} {Version}";
}

/// <summary>
/// Состояние установленного расширения.
/// </summary>
public enum ExtensionState
{
    /// <summary>Расширение включено и его содержимое доступно.</summary>
    Active = 0,

    /// <summary>Расширение отключено пользователем.</summary>
    Disabled = 1,

    /// <summary>Расширению нужна более новая версия приложения.</summary>
    Incompatible = 2,

    /// <summary>Не установлено расширение, без которого это не работает.</summary>
    MissingDependency = 3,
}

/// <summary>
/// Раздел содержимого расширения: сколько объектов какого вида в нём лежит.
/// </summary>
/// <param name="TypeId">Идентификатор вида содержимого.</param>
/// <param name="Title">Название раздела для интерфейса.</param>
/// <param name="Count">Количество объектов.</param>
public sealed record ExtensionSection(string TypeId, string Title, int Count);

/// <summary>
/// Установленное расширение.
/// </summary>
/// <param name="Id">Идентификатор записи в базе данных.</param>
/// <param name="Manifest">Описание расширения.</param>
/// <param name="State">Состояние расширения.</param>
/// <param name="InstalledAt">Момент установки.</param>
/// <param name="ObjectCount">Количество установленных объектов.</param>
/// <param name="Problems">Что мешает расширению работать.</param>
public sealed record ExtensionItem(
    Guid Id,
    ExtensionManifest Manifest,
    ExtensionState State,
    DateTimeOffset InstalledAt,
    int ObjectCount,
    IReadOnlyList<string> Problems)
{
    /// <summary>Расширение работает.</summary>
    public bool IsActive => State == ExtensionState.Active;

    /// <summary>Расширению что-то мешает работать.</summary>
    public bool HasProblems => Problems.Count > 0;

    /// <summary>Расширение включено пользователем.</summary>
    public bool IsEnabled => State != ExtensionState.Disabled;

    /// <summary>Название состояния для интерфейса.</summary>
    public string StateText => State switch
    {
        ExtensionState.Active => "Активно",
        ExtensionState.Disabled => "Отключено",
        ExtensionState.Incompatible => "Несовместимо",
        _ => "Нет зависимости",
    };

    /// <summary>Краткое описание состава: «объектов: 128».</summary>
    public string Summary => $"объектов: {ObjectCount}";
}

/// <summary>
/// Разбор файла расширения до установки.
/// </summary>
/// <param name="Manifest">Описание расширения.</param>
/// <param name="Sections">Состав расширения по видам объектов.</param>
/// <param name="Problems">Причины, по которым установка невозможна.</param>
/// <param name="Warnings">То, о чём нужно предупредить, но что не мешает установке.</param>
/// <param name="ReplacesVersion">Версия уже установленного расширения с тем же названием.</param>
public sealed record ExtensionPreview(
    ExtensionManifest Manifest,
    IReadOnlyList<ExtensionSection> Sections,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Warnings,
    string? ReplacesVersion)
{
    /// <summary>Установка возможна.</summary>
    public bool CanInstall => Problems.Count == 0;

    /// <summary>Расширение с таким названием уже установлено.</summary>
    public bool IsUpdate => ReplacesVersion is not null;

    /// <summary>Общее количество объектов в расширении.</summary>
    public int ObjectCount => Sections.Sum(section => section.Count);
}

/// <summary>
/// Что и куда выгружать в файл расширения.
/// </summary>
/// <param name="Path">Полный путь создаваемого файла.</param>
/// <param name="Manifest">Описание создаваемого расширения.</param>
/// <param name="GameSystemId">Игровая система, содержимое которой выгружается.</param>
/// <param name="ContentPackId">Установленное расширение, содержимое которого выгружается.</param>
public sealed record ExtensionExportRequest(
    string Path,
    ExtensionManifest Manifest,
    Guid? GameSystemId = null,
    Guid? ContentPackId = null);

/// <summary>
/// Итог выгрузки расширения в файл.
/// </summary>
/// <param name="Path">Полный путь созданного файла.</param>
/// <param name="Sections">Состав выгруженного расширения.</param>
/// <param name="SizeInBytes">Размер созданного файла.</param>
public sealed record ExtensionExportResult(
    string Path,
    IReadOnlyList<ExtensionSection> Sections,
    long SizeInBytes)
{
    /// <summary>Общее количество выгруженных объектов.</summary>
    public int ObjectCount => Sections.Sum(section => section.Count);
}

/// <summary>
/// Источник выгрузки: игровая система или установленное расширение.
/// </summary>
/// <param name="Id">Идентификатор источника.</param>
/// <param name="Name">Название источника.</param>
/// <param name="IsGameSystem">Источник — игровая система.</param>
public sealed record ExtensionSource(Guid Id, string Name, bool IsGameSystem)
{
    /// <summary>Название источника с указанием его вида.</summary>
    public string Title => IsGameSystem ? $"Игровая система: {Name}" : $"Расширение: {Name}";
}

/// <summary>
/// Расширения приложения: установка, включение, удаление и выгрузка.
///
/// Расширение — это набор объектов самого приложения, а не программа
/// (решение Р-102). Поэтому подсистема не загружает и не выполняет чужой код:
/// она читает файл, проверяет его и складывает содержимое в те же таблицы,
/// в которые пишет пользователь, отмечая принадлежность к расширению.
/// Отсюда же следует, что удалить расширение — значит убрать ровно его объекты.
/// </summary>
public interface IExtensionService
{
    /// <summary>
    /// Возвращает установленные расширения.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Расширения в порядке названий.</returns>
    Task<Result<IReadOnlyList<ExtensionItem>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Читает файл расширения и проверяет его, ничего не устанавливая.
    /// </summary>
    /// <param name="path">Полный путь к файлу.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Разбор файла либо описание ошибки чтения.</returns>
    Task<Result<ExtensionPreview>> InspectAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Устанавливает расширение из файла.
    ///
    /// Расширение с тем же названием заменяется: его прежние объекты убираются,
    /// а на их место встают новые. Иначе обновление оставляло бы в базе два
    /// набора одного и того же содержимого.
    /// </summary>
    /// <param name="path">Полный путь к файлу.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Установленное расширение либо описание ошибки.</returns>
    Task<Result<ExtensionItem>> InstallAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Включает или отключает установленное расширение.
    /// </summary>
    /// <param name="id">Идентификатор расширения.</param>
    /// <param name="enabled">Расширение должно быть включено.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Изменённое расширение либо описание ошибки.</returns>
    Task<Result<ExtensionItem>> SetEnabledAsync(
        Guid id,
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет расширение вместе с его объектами.
    /// </summary>
    /// <param name="id">Идентификатор расширения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Количество удалённых объектов либо описание ошибки.</returns>
    Task<Result<int>> RemoveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает то, что можно выгрузить в файл: игровые системы и расширения.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Источники выгрузки.</returns>
    Task<Result<IReadOnlyList<ExtensionSource>>> GetSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Выгружает содержимое в файл расширения.
    /// </summary>
    /// <param name="request">Что и куда выгружать.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итог выгрузки либо описание ошибки.</returns>
    Task<Result<ExtensionExportResult>> ExportAsync(
        ExtensionExportRequest request,
        CancellationToken cancellationToken = default);
}
