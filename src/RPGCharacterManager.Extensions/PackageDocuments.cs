using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace RPGCharacterManager.Extensions;

/// <summary>
/// Описание расширения — содержимое файла «манифест.json».
///
/// Ключи русские: файл читают и правят люди, а приложение говорит по-русски.
/// </summary>
internal sealed class PackageManifest
{
    /// <summary>Версия формата пакета.</summary>
    [JsonPropertyName("формат")]
    public string? Format { get; set; }

    /// <summary>Название расширения.</summary>
    [JsonPropertyName("название")]
    public string? Name { get; set; }

    /// <summary>Версия расширения.</summary>
    [JsonPropertyName("версия")]
    public string? Version { get; set; }

    /// <summary>Автор расширения.</summary>
    [JsonPropertyName("автор")]
    public string? Author { get; set; }

    /// <summary>Описание расширения.</summary>
    [JsonPropertyName("описание")]
    public string? Description { get; set; }

    /// <summary>Лицензия, на условиях которой распространяется расширение.</summary>
    [JsonPropertyName("лицензия")]
    public string? License { get; set; }

    /// <summary>Название игровой системы, к которой относится расширение.</summary>
    [JsonPropertyName("игровая_система")]
    public string? GameSystem { get; set; }

    /// <summary>Наименьшая версия приложения.</summary>
    [JsonPropertyName("требуемая_версия")]
    public string? RequiredVersion { get; set; }

    /// <summary>Момент создания файла.</summary>
    [JsonPropertyName("создан")]
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Расширения, без которых это не работает.</summary>
    [JsonPropertyName("зависимости")]
    public List<PackageDependency> Dependencies { get; set; } = [];
}

/// <summary>
/// Требуемое расширение в описании.
/// </summary>
internal sealed class PackageDependency
{
    /// <summary>Название требуемого расширения.</summary>
    [JsonPropertyName("название")]
    public string? Name { get; set; }

    /// <summary>Наименьшая подходящая версия.</summary>
    [JsonPropertyName("версия")]
    public string? Version { get; set; }
}

/// <summary>
/// Содержимое расширения — файл «содержимое.json».
///
/// Сами объекты записаны так, как устроены в приложении: имена полей взяты
/// из модели данных, а не описаны отдельно. Второе описание тех же полей
/// разошлось бы с моделью при первом же новом свойстве, и выгрузка молча
/// перестала бы его сохранять (решение Р-103).
/// </summary>
/// <summary>Дополнительное имя объекта из другого или текущего пакета.</summary>
internal sealed class PackageAlias
{
    /// <summary>Идентификатор вида контента.</summary>
    [JsonPropertyName("тип")]
    public string? ContentTypeId { get; set; }

    /// <summary>Внутреннее имя целевого объекта.</summary>
    [JsonPropertyName("внутреннее_имя")]
    public string? TargetSystemName { get; set; }

    /// <summary>Дополнительное имя для поиска.</summary>
    [JsonPropertyName("псевдоним")]
    public string? Alias { get; set; }
}

/// <summary>
/// Содержимое файла расширения.
/// </summary>
internal sealed class PackageContent
{
    /// <summary>Версия формата пакета.</summary>
    [JsonPropertyName("формат")]
    public string Format { get; set; } = Core.Abstractions.Extensions.ExtensionPackage.FormatVersion;

    /// <summary>Игровая система расширения.</summary>
    [JsonPropertyName("игровая_система")]
    public JsonNode? GameSystem { get; set; }

    /// <summary>Игровые объекты по видам контента.</summary>
    [JsonPropertyName("объекты")]
    public Dictionary<string, List<JsonNode>> Objects { get; set; } = [];

    /// <summary>Дополнительные имена объектов для многоязычного поиска.</summary>
    [JsonPropertyName("псевдонимы")]
    public List<PackageAlias> Aliases { get; set; } = [];

    /// <summary>Игровые правила.</summary>
    [JsonPropertyName("правила")]
    public List<JsonNode> Rules { get; set; } = [];

    /// <summary>Макросы.</summary>
    [JsonPropertyName("макросы")]
    public List<JsonNode> Macros { get; set; } = [];
}
