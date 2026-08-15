using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Core.Models.History;
using RPGCharacterManager.Database;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Characters;

/// <summary>
/// Развитие персонажа: повышение уровня и автоматическое обновление параметров.
///
/// Повышение уровня не содержит правил конкретной игры: увеличивается значение
/// уровня, применяются правила события «повышение уровня», после чего все
/// зависящие от уровня формулы вычисляются заново.
/// </summary>
public sealed class CharacterProgressionService : ICharacterProgressionService
{
    /// <summary>Наибольшее количество уровней, добавляемых за одну операцию.</summary>
    public const int MaximumLevelStep = 100;

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ICharacterBuilderService _builder;
    private readonly IEventBus _eventBus;
    private readonly ILogger<CharacterProgressionService> _logger;

    /// <summary>
    /// Создаёт службу развития персонажа.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="builder">Мастер создания персонажа, выполняющий расчёт.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="logger">Журналировщик.</param>
    public CharacterProgressionService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ICharacterBuilderService builder,
        IEventBus eventBus,
        ILogger<CharacterProgressionService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _builder = Guard.NotNull(builder);
        _eventBus = Guard.NotNull(eventBus);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public Task<Result<CharacterUpdateReport>> LevelUpAsync(
        Guid characterId,
        int levels = 1,
        CancellationToken cancellationToken = default)
    {
        if (levels is < 1 or > MaximumLevelStep)
        {
            return Task.FromResult(Result.Failure<CharacterUpdateReport>(
                $"Количество уровней должно находиться в диапазоне от 1 до "
                + $"{MaximumLevelStep.ToString(CultureInfo.CurrentCulture)}."));
        }

        return UpdateAsync(characterId, levels, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<CharacterUpdateReport>> RecalculateAsync(
        Guid characterId,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(characterId, levels: 0, cancellationToken);

    /// <summary>
    /// Изменяет уровень персонажа и пересчитывает его параметры.
    /// </summary>
    /// <param name="characterId">Идентификатор персонажа.</param>
    /// <param name="levels">Количество добавляемых уровней. Ноль означает только пересчёт.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Отчёт о произошедших изменениях.</returns>
    private async Task<Result<CharacterUpdateReport>> UpdateAsync(
        Guid characterId,
        int levels,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var character = await CharacterService
                .LoadWithRelatedData(context.Characters)
                .FirstOrDefaultAsync(item => item.Id == characterId, cancellationToken)
                .ConfigureAwait(false);

            if (character is null)
            {
                return Result.Failure<CharacterUpdateReport>(
                    "Персонаж не найден: возможно, он был удалён.");
            }

            var previousLevel = character.Level;
            character.Level += levels;

            var draft = _builder.CreateDraft(character);

            // Правила повышения уровня применяются только при действительном
            // повышении и изменяют базовые значения: пересчёт не должен выдавать
            // награду за уровень повторно и не должен её терять.
            var eventRules = levels > 0
                ? await _builder
                    .ApplyEventAsync(draft, RuleTriggers.CharacterLevelUp, cancellationToken)
                    .ConfigureAwait(false)
                : [];

            var calculation = await _builder.CalculateAsync(draft, cancellationToken).ConfigureAwait(false);

            // Записи для характеристик и ресурсов, появившихся после создания
            // персонажа, передаются контексту явно: иначе он считает их изменёнными.
            var added = new List<object>();
            var changes = CharacterWriter.ApplyCalculation(character, calculation, added);

            context.AddRange(added);
            context.History.Add(CreateHistoryEntry(character, previousLevel, levels, changes));

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (levels > 0)
            {
                CharacterLog.CharacterLevelChanged(_logger, character.Name, previousLevel, character.Level);
            }
            else
            {
                CharacterLog.CharacterRecalculated(_logger, character.Name);
            }

            await _eventBus
                .PublishAsync(
                    new CharacterChangedEvent(
                        characterId,
                        levels > 0 ? CharacterChangeKind.LevelChanged : CharacterChangeKind.Recalculated),
                    cancellationToken)
                .ConfigureAwait(false);

            return Result.Success(new CharacterUpdateReport(
                character.Name,
                previousLevel,
                character.Level,
                changes,
                [.. eventRules, .. calculation.AppliedRules],
                calculation.Issues));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.CharacterUpdateFailed(_logger, exception, characterId);

            return Result.Failure<CharacterUpdateReport>(
                $"Не удалось обновить персонажа: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<CharacterUpdateReport>> ApplyActionsAsync(
        Guid characterId,
        string name,
        string trigger,
        RuleCondition? condition,
        IReadOnlyList<RuleAction> actions,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(name);
        Guard.NotNullOrWhiteSpace(trigger);
        Guard.NotNull(actions);

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var character = await CharacterService
                .LoadWithRelatedData(context.Characters)
                .FirstOrDefaultAsync(item => item.Id == characterId, cancellationToken)
                .ConfigureAwait(false);

            if (character is null)
            {
                return Result.Failure<CharacterUpdateReport>(
                    "Персонаж не найден: возможно, он был удалён.");
            }

            var draft = _builder.CreateDraft(character);

            // Действия описаны как правило: движок сам проверит условие
            // и пропустит набор, если оно не выполнено.
            var rule = new RuleDefinition
            {
                Name = name,
                Trigger = trigger,
                Condition = condition,
            };

            foreach (var action in actions)
            {
                rule.Actions.Add(action);
            }

            var applied = await _builder
                .ApplyRulesAsync(draft, trigger, [rule], cancellationToken).ConfigureAwait(false);

            var calculation = await _builder.CalculateAsync(draft, cancellationToken).ConfigureAwait(false);

            var added = new List<object>();
            var changes = CharacterWriter.ApplyCalculation(character, calculation, added);

            context.AddRange(added);
            context.History.Add(new HistoryEntry
            {
                CharacterId = character.Id,
                Action = HistoryActions.Recalculated,
                Subject = name,
                Description = $"Выполнено «{name}» для персонажа «{character.Name}».",
                NewValue = changes.Count > 0 ? string.Join("; ", changes) : null,
            });

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            CharacterLog.CharacterRecalculated(_logger, character.Name);

            await _eventBus
                .PublishAsync(
                    new CharacterChangedEvent(characterId, CharacterChangeKind.Recalculated),
                    cancellationToken)
                .ConfigureAwait(false);

            return Result.Success(new CharacterUpdateReport(
                character.Name,
                character.Level,
                character.Level,
                changes,
                [.. applied, .. calculation.AppliedRules],
                calculation.Issues));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            CharacterLog.CharacterUpdateFailed(_logger, exception, characterId);

            return Result.Failure<CharacterUpdateReport>(
                $"Не удалось выполнить действия: {exception.Message}");
        }
    }

    /// <summary>
    /// Создаёт запись журнала об изменении персонажа.
    /// </summary>
    /// <param name="character">Персонаж.</param>
    /// <param name="previousLevel">Уровень до изменения.</param>
    /// <param name="levels">Количество добавленных уровней.</param>
    /// <param name="changes">Описания изменившихся значений.</param>
    /// <returns>Запись журнала.</returns>
    private static HistoryEntry CreateHistoryEntry(
        Character character,
        int previousLevel,
        int levels,
        IReadOnlyList<string> changes) => new()
        {
            CharacterId = character.Id,
            Action = levels > 0 ? HistoryActions.LevelGained : HistoryActions.Recalculated,
            Description = levels > 0
                ? $"Персонаж «{character.Name}» повышен до уровня "
                  + character.Level.ToString(CultureInfo.CurrentCulture) + "."
                : $"Пересчитаны параметры персонажа «{character.Name}».",
            OldValue = previousLevel.ToString(CultureInfo.CurrentCulture),
            NewValue = changes.Count > 0
                ? string.Join("; ", changes)
                : character.Level.ToString(CultureInfo.CurrentCulture),
        };
}
