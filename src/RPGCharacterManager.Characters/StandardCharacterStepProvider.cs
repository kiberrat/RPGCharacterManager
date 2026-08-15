using System.Globalization;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Content;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Стандартные шаги мастера создания персонажа.
///
/// Шаги описаны данными: ни один из них не содержит правил конкретной игры.
/// Состав вариантов, требования и ограничения берутся из контента, созданного
/// пользователем, поэтому одна и та же последовательность шагов обслуживает
/// любую игровую систему. Игровая система добавляет собственные страницы
/// регистрацией дополнительного <see cref="ICharacterStepProvider"/>.
/// </summary>
public sealed class StandardCharacterStepProvider : ICharacterStepProvider
{
    private const int StepOrderStep = 10;

    /// <inheritdoc />
    public IEnumerable<CharacterStepDefinition> GetSteps()
    {
        yield return CreateGameSystemStep();
        yield return CreateBasicsStep();
        yield return CreateRaceStep();
        yield return CreateClassStep();
        yield return CreateSubclassStep();
        yield return CreateBackgroundStep();
        yield return CreateAttributesStep();
        yield return CreateSkillsStep();
        yield return CreateTraitsStep();
        yield return CreateSpellsStep();
        yield return CreateSummaryStep();
    }

    private static CharacterStepDefinition CreateGameSystemStep() => new()
    {
        Id = CharacterStepIds.GameSystem,
        Title = "Игровая система",
        Description =
            "Выберите игровую систему и источники, из которых мастер возьмёт расы, классы, "
            + "навыки и остальной контент. Правила создания персонажа определяются выбранной системой.",
        Kind = CharacterStepKind.GameSystem,
        Order = 0,
    };

    private static CharacterStepDefinition CreateBasicsStep() => new()
    {
        Id = CharacterStepIds.Basics,
        Title = "Основная информация",
        Description = "Заполните сведения о персонаже. Обязательным является только имя.",
        Kind = CharacterStepKind.Fields,
        Order = 1 * StepOrderStep,
        IsRequired = true,
        Fields = CreateBasicFields(),
    };

    private static CharacterStepDefinition CreateRaceStep() => new()
    {
        Id = CharacterStepIds.Race,
        Title = "Раса",
        Description = "Выберите расу, происхождение вида или его аналог в вашей игровой системе.",
        Kind = CharacterStepKind.SingleChoice,
        Order = 2 * StepOrderStep,
        OptionEntityType = typeof(Race),
        WriteSelection = (character, id) => character.RaceId = id,
        ReadSelection = character => character.RaceId,
        VariableName = CharacterVariables.Race,
        ReadRequirements = entity => ((Race)entity).Requirements,
        ReadDetails = entity => CollectDetails(
            Detail("Скорость", FormatNumber(((Race)entity).Speed)),
            Detail("Размер", ((Race)entity).Size),
            Detail("Языки", ((Race)entity).Languages)),
    };

    private static CharacterStepDefinition CreateClassStep() => new()
    {
        Id = CharacterStepIds.Class,
        Title = "Класс",
        Description = "Выберите класс, профессию, архетип или роль персонажа.",
        Kind = CharacterStepKind.SingleChoice,
        Order = 3 * StepOrderStep,
        OptionEntityType = typeof(CharacterClass),
        IncludePaths = [nameof(CharacterClass.PrimaryAttribute)],
        WriteSelection = (character, id) => character.ClassId = id,
        ReadSelection = character => character.ClassId,
        VariableName = CharacterVariables.Class,
        ReadRequirements = entity => ((CharacterClass)entity).Requirements,
        ReadDetails = entity => CollectDetails(
            Detail("Основная характеристика", ((CharacterClass)entity).PrimaryAttribute?.Name),
            Detail("Здоровье за уровень", ((CharacterClass)entity).HitDiceFormula),
            Detail("Роль", ((CharacterClass)entity).Role),
            Detail(
                "Уровни",
                $"{((CharacterClass)entity).StartingLevel.ToString(CultureInfo.CurrentCulture)}"
                + $" — {((CharacterClass)entity).MaximumLevel.ToString(CultureInfo.CurrentCulture)}")),
    };

    private static CharacterStepDefinition CreateSubclassStep() => new()
    {
        Id = CharacterStepIds.Subclass,
        Title = "Подкласс",
        Description =
            "Выберите специализацию внутри класса. Показываются только подклассы выбранного класса, "
            + "доступные на текущем уровне.",
        Kind = CharacterStepKind.SingleChoice,
        Order = 4 * StepOrderStep,
        OptionEntityType = typeof(Subclass),
        ParentStepId = CharacterStepIds.Class,
        ParentPropertyName = nameof(Subclass.ClassId),
        WriteSelection = (character, id) => character.SubclassId = id,
        ReadSelection = character => character.SubclassId,
        VariableName = CharacterVariables.Subclass,
        ReadRequirements = entity => CombineRequirements(
            ((Subclass)entity).AvailableAtLevel > 1
                ? $"{CharacterVariables.Level} >= "
                  + ((Subclass)entity).AvailableAtLevel.ToString(CultureInfo.InvariantCulture)
                : null,
            ((Subclass)entity).Requirements),
        ReadDetails = entity => CollectDetails(
            Detail(
                "Доступен с уровня",
                ((Subclass)entity).AvailableAtLevel.ToString(CultureInfo.CurrentCulture))),
    };

    private static CharacterStepDefinition CreateBackgroundStep() => new()
    {
        Id = CharacterStepIds.Background,
        Title = "Происхождение",
        Description =
            "Выберите происхождение, предысторию, культуру, профессию, клан или фракцию — "
            + "всё, что игровая система описывает происхождением персонажа.",
        Kind = CharacterStepKind.SingleChoice,
        Order = 5 * StepOrderStep,
        OptionEntityType = typeof(Background),
        WriteSelection = (character, id) => character.BackgroundId = id,
        ReadSelection = character => character.BackgroundId,
        VariableName = CharacterVariables.Background,
        ReadRequirements = entity => ((Background)entity).Requirements,
    };

    private static CharacterStepDefinition CreateAttributesStep() => new()
    {
        Id = CharacterStepIds.Attributes,
        Title = "Характеристики",
        Description =
            "Распределите значения характеристик. Способ распределения и его параметры "
            + "задаёт игровая система; изменённые значения сразу пересчитываются.",
        Kind = CharacterStepKind.Attributes,
        Order = 6 * StepOrderStep,
        AttributeOptions = new AttributeStepOptions(),
    };

    private static CharacterStepDefinition CreateSkillsStep() => new()
    {
        Id = CharacterStepIds.Skills,
        Title = "Навыки",
        Description = "Отметьте навыки, которыми владеет персонаж.",
        Kind = CharacterStepKind.MultipleChoice,
        Order = 7 * StepOrderStep,
        OptionEntityType = typeof(Skill),
        IncludePaths = [nameof(Skill.LinkedAttribute)],
        ReadRequirements = entity => ((Skill)entity).Requirements,
        ReadDetails = entity => CollectDetails(
            Detail("Характеристика", ((Skill)entity).LinkedAttribute?.Name),
            Detail("Категория", ((Skill)entity).Category)),
        WriteSelections = WriteSkills,
        ReadSelections = character => character.Skills.Select(skill => skill.SkillId),
    };

    private static CharacterStepDefinition CreateTraitsStep() => new()
    {
        Id = CharacterStepIds.Traits,
        Title = "Черты",
        Description =
            "Выберите черты, особенности и таланты персонажа. Недоступные варианты "
            + "показываются с указанием причины.",
        Kind = CharacterStepKind.MultipleChoice,
        Order = 8 * StepOrderStep,
        OptionEntityType = typeof(Trait),
        ReadRequirements = entity => ((Trait)entity).Requirements,
        ReadRequiredOption = entity => ((Trait)entity).RequiredTraitId,
        ReadDetails = entity => CollectDetails(
            Detail("Категория", ((Trait)entity).Category),
            Detail("Ступень", ((Trait)entity).Level > 0
                ? ((Trait)entity).Level.ToString(CultureInfo.CurrentCulture)
                : null),
            Detail("Эффект", ((Trait)entity).Formula)),
        WriteSelections = WriteTraits,
        ReadSelections = character => character.Traits.Select(trait => trait.TraitId),
    };

    private static CharacterStepDefinition CreateSpellsStep() => new()
    {
        Id = CharacterStepIds.Spells,
        Title = "Заклинания",
        Description =
            "Выберите заклинания, техники или ритуалы, известные персонажу.",
        Kind = CharacterStepKind.MultipleChoice,
        Order = 9 * StepOrderStep,
        OptionEntityType = typeof(Spell),
        IncludePaths = [nameof(Spell.Resource)],
        ReadRequirements = entity => ((Spell)entity).Requirements,
        ReadDetails = entity => CollectDetails(
            Detail("Уровень", ((Spell)entity).Level.ToString(CultureInfo.CurrentCulture)),
            Detail("Школа", ((Spell)entity).School),
            Detail("Ресурс", ((Spell)entity).Resource?.Name),
            Detail("Эффект", ((Spell)entity).Formula)),
        WriteSelections = WriteSpells,
        ReadSelections = character => character.Spells.Select(spell => spell.SpellId),
    };

    private static CharacterStepDefinition CreateSummaryStep() => new()
    {
        Id = CharacterStepIds.Summary,
        Title = "Проверка и создание",
        Description =
            "Проверьте готового персонажа. Замечания можно устранить, вернувшись на нужный шаг.",
        Kind = CharacterStepKind.Summary,
        Order = 10 * StepOrderStep,
    };

    /// <summary>
    /// Создаёт поля основной информации о персонаже.
    /// Поля описываются теми же средствами, что и поля редактора контента,
    /// поэтому мастер отображает их без отдельной разметки.
    /// </summary>
    /// <returns>Список полей формы.</returns>
    private static IReadOnlyList<IContentField> CreateBasicFields() =>
    [
        new ContentField<Character>(
            nameof(Character.Name),
            "Имя",
            ContentFieldKind.Text,
            character => character.Name,
            (character, value) => character.Name = value as string ?? string.Empty)
        {
            IsRequired = true,
            Hint = "Как персонажа называют за столом.",
        },
        new ContentField<Character>(
            nameof(Character.FullName),
            "Полное имя",
            ContentFieldKind.Text,
            character => character.FullName,
            (character, value) => character.FullName = value as string),
        new ContentField<Character>(
            nameof(Character.Gender),
            "Пол",
            ContentFieldKind.Text,
            character => character.Gender,
            (character, value) => character.Gender = value as string),
        new ContentField<Character>(
            nameof(Character.Age),
            "Возраст",
            ContentFieldKind.Text,
            character => character.Age,
            (character, value) => character.Age = value as string),
        new ContentField<Character>(
            nameof(Character.Height),
            "Рост",
            ContentFieldKind.Text,
            character => character.Height,
            (character, value) => character.Height = value as string),
        new ContentField<Character>(
            nameof(Character.Weight),
            "Вес",
            ContentFieldKind.Text,
            character => character.Weight,
            (character, value) => character.Weight = value as string),
        new ContentField<Character>(
            nameof(Character.Alignment),
            "Мировоззрение",
            ContentFieldKind.Text,
            character => character.Alignment,
            (character, value) => character.Alignment = value as string)
        {
            Hint = "Мировоззрение, кодекс, убеждения — как это называет ваша игровая система.",
        },
        new ContentField<Character>(
            nameof(Character.Languages),
            "Языки",
            ContentFieldKind.Text,
            character => character.Languages,
            (character, value) => character.Languages = value as string),
        new ContentField<Character>(
            nameof(Character.Level),
            "Уровень",
            ContentFieldKind.WholeNumber,
            character => character.Level,
            (character, value) => character.Level = value is int level && level > 0 ? level : 1)
        {
            Group = ContentFieldGroups.Rules,
            Hint = "Формулы и требования используют уровень как переменную «уровень».",
        },
        new ContentField<Character>(
            nameof(Character.Portrait),
            "Портрет",
            ContentFieldKind.Image,
            character => character.Portrait,
            (character, value) => character.Portrait = value as string)
        {
            Group = ContentFieldGroups.Appearance,
            Hint = "Путь к файлу изображения.",
        },
        new ContentField<Character>(
            nameof(Character.Description),
            "Краткое описание",
            ContentFieldKind.LongText,
            character => character.Description,
            (character, value) => character.Description = value as string)
        {
            Group = ContentFieldGroups.Appearance,
        },
        new ContentField<Character>(
            nameof(Character.Biography),
            "Биография",
            ContentFieldKind.LongText,
            character => character.Biography,
            (character, value) => character.Biography = value as string)
        {
            Group = ContentFieldGroups.Appearance,
        },
        new ContentField<Character>(
            nameof(Character.Notes),
            "Заметки",
            ContentFieldKind.LongText,
            character => character.Notes,
            (character, value) => character.Notes = value as string)
        {
            Group = ContentFieldGroups.Appearance,
        },
    ];

    /// <summary>
    /// Переносит выбранные навыки в персонажа.
    ///
    /// Запись только добавляет недостающие записи и никогда не удаляет чужие:
    /// в один и тот же список персонажа могут писать несколько шагов, включая
    /// собственные шаги игровой системы.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="selected">Идентификаторы выбранных навыков.</param>
    private static void WriteSkills(Character character, IReadOnlyCollection<Guid> selected)
    {
        var existing = character.Skills.Select(skill => skill.SkillId).ToHashSet();

        foreach (var added in selected.Where(id => !existing.Contains(id)))
        {
            character.Skills.Add(new CharacterSkill
            {
                CharacterId = character.Id,
                SkillId = added,
                ProficiencyLevel = 1,
            });
        }
    }

    /// <summary>
    /// Переносит выбранные черты в персонажа, не затрагивая черты, полученные
    /// из других источников.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="selected">Идентификаторы выбранных черт.</param>
    private static void WriteTraits(Character character, IReadOnlyCollection<Guid> selected)
    {
        var existing = character.Traits.Select(trait => trait.TraitId).ToHashSet();

        foreach (var added in selected.Where(id => !existing.Contains(id)))
        {
            character.Traits.Add(new CharacterTrait
            {
                CharacterId = character.Id,
                TraitId = added,
                Source = "Создание персонажа",
            });
        }
    }

    /// <summary>
    /// Переносит выбранные заклинания в персонажа, не затрагивая заклинания,
    /// полученные из других источников.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="selected">Идентификаторы выбранных заклинаний.</param>
    private static void WriteSpells(Character character, IReadOnlyCollection<Guid> selected)
    {
        var existing = character.Spells.Select(spell => spell.SpellId).ToHashSet();

        foreach (var added in selected.Where(id => !existing.Contains(id)))
        {
            character.Spells.Add(new CharacterSpell
            {
                CharacterId = character.Id,
                SpellId = added,
                Source = "Создание персонажа",
            });
        }
    }

    /// <summary>
    /// Объединяет два выражения требований логическим «и».
    /// </summary>
    /// <param name="first">Первое выражение.</param>
    /// <param name="second">Второе выражение.</param>
    /// <returns>Объединённое выражение либо то из них, которое задано.</returns>
    private static string? CombineRequirements(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second;
        }

        return string.IsNullOrWhiteSpace(second) ? first : $"({first}) и ({second})";
    }

    private static CharacterOptionDetail? Detail(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new CharacterOptionDetail(label, value);

    private static List<CharacterOptionDetail> CollectDetails(
        params CharacterOptionDetail?[] details) =>
        details.Where(detail => detail is not null).Select(detail => detail!).ToList();

    private static string FormatNumber(double value) =>
        Math.Abs(value) < double.Epsilon ? string.Empty : value.ToString("0.####", CultureInfo.CurrentCulture);
}
