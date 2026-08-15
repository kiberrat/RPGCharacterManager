using RPGCharacterManager.Characters;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.Core.Models.Entities;

namespace RPGCharacterManager.Tests.Characters;

/// <summary>
/// Шаг мастера, добавляемый игровой системой.
///
/// Проверяет требование документа 006: игровая система должна уметь добавлять
/// собственные страницы конструктора без изменения кода мастера.
/// </summary>
internal sealed class MutationStepProvider : ICharacterStepProvider
{
    /// <summary>Идентификатор добавляемого шага.</summary>
    public const string StepId = "test.step.mutations";

    /// <inheritdoc />
    public IEnumerable<CharacterStepDefinition> GetSteps()
    {
        yield return new CharacterStepDefinition
        {
            Id = StepId,
            Title = "Мутации",
            Description = "Собственная страница игровой системы.",
            Kind = CharacterStepKind.MultipleChoice,
            Order = 55,
            OptionEntityType = typeof(Trait),

            // Количество мутаций ограничено уровнем персонажа.
            SelectionLimitFormula = CharacterVariables.Level,
            ReadRequirements = entity => ((Trait)entity).Requirements,
            WriteSelections = (character, selected) =>
            {
                var existing = character.Traits.Select(trait => trait.TraitId).ToHashSet();

                foreach (var id in selected.Where(id => !existing.Contains(id)))
                {
                    character.Traits.Add(new CharacterTrait
                    {
                        CharacterId = character.Id,
                        TraitId = id,
                        Source = "Мутации",
                    });
                }
            },
        };
    }
}

/// <summary>
/// Проверка расширяемости мастера: игровая система добавляет собственный шаг
/// регистрацией описания, мастер при этом не изменяется.
/// </summary>
public sealed class CharacterStepExtensibilityTests
{
    private const int OptionLimit = 100;

    private static Task<CharacterTestContext> CreateAsync() =>
        CharacterTestContext.CreateAsync(new StandardCharacterStepProvider(), new MutationStepProvider());

    [Fact]
    public async Task СобственныйШаг_ПоявляетсяВМастереВСвоёмПорядке()
    {
        await using var context = await CreateAsync();

        var identifiers = context.Builder.Steps.Select(step => step.Id).ToList();
        var position = identifiers.IndexOf(MutationStepProvider.StepId);

        Assert.True(position > 0);
        Assert.Equal(CharacterStepIds.Summary, identifiers[^1]);
    }

    [Fact]
    public async Task ОграничениеВыбора_ВычисляетсяФормулойШага()
    {
        await using var context = await CreateAsync();

        var draft = new CharacterDraft { Level = 3 };

        var limit = await context.Builder.GetSelectionLimitAsync(
            context.Step(MutationStepProvider.StepId),
            draft);

        Assert.Equal(3, limit);
    }

    [Fact]
    public async Task ПревышениеОграничения_ПрепятствуетСозданию()
    {
        await using var context = await CreateAsync();

        var first = CharacterContent.Trait("Ночное зрение", "ночное_зрение");
        var second = CharacterContent.Trait("Толстая кожа", "толстая_кожа");
        await context.AddAsync(first, second);

        var draft = new CharacterDraft { Level = 1 };
        draft.Character.Name = "Мутант";

        var selections = draft.GetSelections(MutationStepProvider.StepId);
        selections.Add(first.Id);
        selections.Add(second.Id);

        var result = await context.Builder.CreateAsync(draft);

        Assert.True(result.IsFailure);
        Assert.Contains("Мутации", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task СобственныйШаг_СохраняетВыборВПерсонаже()
    {
        await using var context = await CreateAsync();

        var mutation = CharacterContent.Trait("Ночное зрение", "ночное_зрение");
        await context.AddAsync(mutation);

        var draft = new CharacterDraft { Level = 2 };
        draft.Character.Name = "Мутант";
        draft.GetSelections(MutationStepProvider.StepId).Add(mutation.Id);

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        var character = await context.LoadCharacterAsync(result.Value);
        var trait = Assert.Single(character.Traits);

        Assert.Equal(mutation.Id, trait.TraitId);
        Assert.Equal("Мутации", trait.Source);
    }

    [Fact]
    public async Task ДваШага_ПишущиеВОдинСписок_НеСтираютВыборДругДруга()
    {
        await using var context = await CreateAsync();

        var mutation = CharacterContent.Trait("Ночное зрение", "ночное_зрение");
        var feat = CharacterContent.Trait("Меткий стрелок", "меткий_стрелок");
        await context.AddAsync(mutation, feat);

        var draft = new CharacterDraft { Level = 2 };
        draft.Character.Name = "Странник";
        draft.GetSelections(MutationStepProvider.StepId).Add(mutation.Id);
        draft.GetSelections(CharacterStepIds.Traits).Add(feat.Id);

        var result = await context.Builder.CreateAsync(draft);
        Assert.True(result.IsSuccess, result.Error);

        var character = await context.LoadCharacterAsync(result.Value);

        Assert.Equal(2, character.Traits.Count);
        Assert.Contains(character.Traits, trait => trait.Source == "Мутации");
        Assert.Contains(character.Traits, trait => trait.Source == "Создание персонажа");
    }

    [Fact]
    public async Task СобственныйШаг_ПроверяетТребованияВариантов()
    {
        await using var context = await CreateAsync();

        var strength = CharacterContent.Attribute("Сила", "сила", defaultValue: 10);
        await context.AddAsync(strength);

        await context.AddAsync(
            CharacterContent.Trait("Костяная броня", "костяная_броня", requirements: "сила >= 14"));

        var draft = new CharacterDraft();

        var page = await context.Builder.GetOptionsAsync(
            context.Step(MutationStepProvider.StepId),
            draft,
            search: null,
            includeUnavailable: true,
            OptionLimit);

        Assert.False(Assert.Single(page.Options).IsAvailable);
    }
}
