using System.Text.RegularExpressions;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Преобразует разделы выбранного подкласса D&amp;D 5e в отдельные способности.
///
/// Пакеты книг хранят текст подкласса одним блоком. В русских изданиях заголовок
/// каждой способности имеет устойчивый вид «Название N-й уровень, умение ...».
/// Каталог разбирает эти заголовки и выдаёт только способности достигнутых уровней.
/// </summary>
internal static class Dnd5eSubclassAbilityCatalog
{
    private const string Category = "Способности подкласса";
    private static readonly Guid Dnd5eSystemId = new("5bb844d5-4e9c-4cd8-97b5-1f2e978d3675");

    private static readonly Regex HeaderRegex = new(
        @"(?<name>[А-ЯЁ][А-Яа-яЁё0-9 ,:«»()'’\-]{1,120}?)\s+(?<!\d)(?<level>\d{1,2})-(?:й|я|е)\s+уровень,\s+умение\s+[а-яё][а-яё \-]+?(?=\s+[А-ЯЁ])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly IReadOnlyDictionary<string, int[]> FeatureLevels =
        new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["изобретатель"] = [3, 5, 9, 15],
            ["варвар"] = [3, 6, 10, 14],
            ["бард"] = [3, 6, 14],
            ["жрец"] = [1, 2, 6, 8, 17],
            ["друид"] = [2, 6, 10, 14],
            ["воин"] = [3, 7, 10, 15, 18],
            ["монах"] = [3, 6, 11, 17],
            ["паладин"] = [3, 7, 15, 20],
            ["следопыт"] = [3, 7, 11, 15],
            ["плут"] = [3, 9, 13, 17],
            ["чародей"] = [1, 6, 14, 18],
            ["колдун"] = [1, 6, 10, 14],
            ["волшебник"] = [2, 6, 10, 14],
        };

    // Подписи к иллюстрациям и таблицы в некоторых PDF стоят прямо перед заголовком.
    // Известное окончание позволяет сохранить только настоящее название способности.
    private static readonly string[] KnownNameEndings =
    [
        "Божественный канал: Сумеречное святилище",
        "Божественный канал: Требование порядка",
        "Божественный канал: Бальзам мира",
        "Шквал исцеления и повреждения",
        "Призыв духа дикого огня",
        "Расширенный список заклинаний",
        "Заразительное вдохновение",
        "Частица души усопшего",
        "Магическая встряска",
        "Укреплённая позиция",
        "Мистическая пушка",
        "Стихийный дар",
        "Заклинания домена",
        "Установление мира",
        "Звёздная форма",
        "Ореол спор",
        "Торс астрального тела",
        "Исцеляющая рука",
        "Повреждающая рука",
        "Заманивающий трюк",
        "Подкрепление фей",
        "Извивающаяся волна",
        "Могущественный рой",
    ];

    public static IReadOnlyList<Ability> GetAbilities(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);

        var subclass = character.Subclass;
        var className = character.Class?.SystemName;
        if (character.GameSystemId != Dnd5eSystemId || subclass is null ||
            string.IsNullOrWhiteSpace(subclass.Description) || string.IsNullOrWhiteSpace(className) ||
            !FeatureLevels.TryGetValue(className, out var allowedLevels))
        {
            return [];
        }

        var allowed = allowedLevels.ToHashSet();
        var matches = HeaderRegex.Matches(subclass.Description)
            .Where(match => int.TryParse(match.Groups["level"].Value, out var level) && allowed.Contains(level))
            .ToList();

        var result = new List<Ability>();
        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            var level = int.Parse(match.Groups["level"].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (level > character.Level)
            {
                continue;
            }

            var name = CleanName(match.Groups["name"].Value);
            var descriptionStart = match.Index + match.Length;
            var descriptionEnd = index + 1 < matches.Count ? matches[index + 1].Index : subclass.Description.Length;
            var description = CleanDescription(subclass.Description[descriptionStart..descriptionEnd]);

            result.Add(new Ability
            {
                Id = StableId(subclass.SystemName, level, name),
                Name = name,
                SystemName = $"{subclass.SystemName}_{level}_{name}",
                Description = description,
                Category = $"{Category} — {subclass.Name}",
                Requirements = $"подкласс = \"{subclass.SystemName}\" и уровень >= {level}",
                GameSystemId = character.GameSystemId,
                IsSystem = true,
                Source = subclass.Source,
            });
        }

        return result;
    }

    private static string CleanName(string value)
    {
        var name = Regex.Replace(value, @"\s+", " ").Trim();
        var note = name.LastIndexOf("ТАША ", StringComparison.OrdinalIgnoreCase);
        if (note >= 0)
        {
            name = name[(note + "ТАША ".Length)..].Trim();
        }

        foreach (var ending in KnownNameEndings)
        {
            if (name.EndsWith(ending, StringComparison.CurrentCultureIgnoreCase))
            {
                return ending;
            }
        }

        return name;
    }

    private static string CleanDescription(string value) =>
        Regex.Replace(value, @"\s+", " ").Trim(' ', ',', ':', ';', '—', '-');

    private static Guid StableId(string subclassName, int level, string name)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"dnd5e:subclass:{subclassName}:{level}:{name}"));
        return new Guid(bytes[..16]);
    }
}
