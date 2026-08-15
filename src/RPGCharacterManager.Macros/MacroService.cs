using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Macros;
using RPGCharacterManager.Core.Events;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Database;
using RPGCharacterManager.GameRules.Serialization;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.Macros;

/// <summary>
/// Макросы: хранение и выполнение последовательностей действий.
///
/// Собственного движка у службы нет. Условие проверяет и действия выполняет
/// движок правил, а запись персонажа — подсистема персонажей. Здесь только
/// хранение макроса и передача его состава на выполнение (решение Р-97).
/// </summary>
public sealed class MacroService : IMacroService
{
    /// <summary>Начало ключа события, под которым выполняется макрос.</summary>
    public const string TriggerPrefix = "макрос.";

    private readonly IDbContextFactory<RpgDbContext> _contextFactory;
    private readonly ICharacterProgressionService _progression;
    private readonly IEventBus _eventBus;
    private readonly ILogger<MacroService> _logger;

    /// <summary>
    /// Создаёт службу макросов.
    /// </summary>
    /// <param name="contextFactory">Фабрика контекстов базы данных.</param>
    /// <param name="progression">Служба развития персонажа: применяет действия и сохраняет результат.</param>
    /// <param name="eventBus">Шина событий приложения.</param>
    /// <param name="logger">Журналировщик.</param>
    public MacroService(
        IDbContextFactory<RpgDbContext> contextFactory,
        ICharacterProgressionService progression,
        IEventBus eventBus,
        ILogger<MacroService> logger)
    {
        _contextFactory = Guard.NotNull(contextFactory);
        _progression = Guard.NotNull(progression);
        _eventBus = Guard.NotNull(eventBus);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<MacroListItem>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var macros = await context.Macros.AsNoTracking()
                .OrderBy(macro => macro.SortOrder)
                .ThenBy(macro => macro.Name)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success<IReadOnlyList<MacroListItem>>([.. macros.Select(Describe)]);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MacroLog.ActionFailed(_logger, exception, "чтение списка макросов");
            return Result.Failure<IReadOnlyList<MacroListItem>>("Не удалось прочитать макросы.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<MacroDefinition>> GetAsync(
        Guid macroId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var macro = await context.Macros.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == macroId, cancellationToken)
                .ConfigureAwait(false);

            if (macro is null)
            {
                return Result.Failure<MacroDefinition>("Макрос не найден: возможно, он был удалён.");
            }

            var definition = new MacroDefinition
            {
                Id = macro.Id,
                Name = macro.Name,
                Description = macro.Description,
                Category = macro.Category,
                Hotkey = macro.Hotkey,
                Condition = RuleSerializer.DeserializeCondition(macro.Condition),
                Enabled = macro.Enabled,
                CharacterId = macro.CharacterId,
                GameSystemId = macro.GameSystemId,
            };

            foreach (var action in RuleSerializer.DeserializeActions(macro.ActionsJson))
            {
                definition.Actions.Add(action);
            }

            return Result.Success(definition);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MacroLog.ActionFailed(_logger, exception, "чтение макроса");
            return Result.Failure<MacroDefinition>("Не удалось прочитать макрос.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> SaveAsync(
        MacroDefinition macro,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(macro);

        if (string.IsNullOrWhiteSpace(macro.Name))
        {
            return Result.Failure<Guid>("Не задано название макроса.");
        }

        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var hotkey = string.IsNullOrWhiteSpace(macro.Hotkey) ? null : macro.Hotkey.Trim();

            if (hotkey is not null)
            {
                // Одно сочетание клавиш на два макроса означало бы, что нажатие
                // выполняет неизвестно который из них.
                var taken = await context.Macros.AsNoTracking()
                    .AnyAsync(
                        item => item.Id != macro.Id && item.Hotkey == hotkey,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (taken)
                {
                    return Result.Failure<Guid>($"Сочетание клавиш «{hotkey}» уже занято другим макросом.");
                }
            }

            var entity = macro.Id == Guid.Empty
                ? null
                : await context.Macros
                    .FirstOrDefaultAsync(item => item.Id == macro.Id, cancellationToken)
                    .ConfigureAwait(false);

            var created = entity is null;

            if (created)
            {
                entity = new Macro { SortOrder = await NextOrderAsync(context, cancellationToken).ConfigureAwait(false) };
                context.Add(entity);
            }

            entity!.Name = macro.Name.Trim();
            entity.SystemName = entity.Name;
            entity.Description = macro.Description;
            entity.Category = macro.Category;
            entity.Hotkey = hotkey;
            entity.Condition = RuleSerializer.SerializeCondition(macro.Condition);
            entity.ActionsJson = RuleSerializer.SerializeActions(macro.Actions);
            entity.Enabled = macro.Enabled;
            entity.CharacterId = macro.CharacterId;
            entity.GameSystemId = macro.GameSystemId;

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            MacroLog.MacroSaved(_logger, entity.Name, entity.Id);

            // Сочетания клавиш принадлежат макросам: главное окно должно узнать
            // о новом сочетании сразу, а не при следующем запуске.
            await _eventBus.PublishAsync(new MacrosChangedEvent(), cancellationToken).ConfigureAwait(false);

            return Result.Success(entity.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MacroLog.ActionFailed(_logger, exception, "сохранение макроса");
            return Result.Failure<Guid>("Не удалось сохранить макрос.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid macroId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var macro = await context.Macros
                .FirstOrDefaultAsync(item => item.Id == macroId, cancellationToken)
                .ConfigureAwait(false);

            if (macro is null)
            {
                return Result.Failure("Макрос не найден.");
            }

            context.Remove(macro);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            MacroLog.MacroDeleted(_logger, macro.Name, macro.Id);

            await _eventBus.PublishAsync(new MacrosChangedEvent(), cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MacroLog.ActionFailed(_logger, exception, "удаление макроса");
            return Result.Failure("Не удалось удалить макрос.");
        }
    }

    /// <inheritdoc />
    public async Task<Result<MacroRunReport>> RunAsync(
        Guid macroId,
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        var loaded = await GetAsync(macroId, cancellationToken).ConfigureAwait(false);

        if (loaded.IsFailure)
        {
            return Result.Failure<MacroRunReport>(loaded.Error!);
        }

        var macro = loaded.Value;

        if (!macro.Enabled)
        {
            return Result.Failure<MacroRunReport>($"Макрос «{macro.Name}» выключен.");
        }

        if (macro.Actions.Count == 0)
        {
            return Result.Failure<MacroRunReport>($"У макроса «{macro.Name}» нет ни одного действия.");
        }

        if (macro.CharacterId is { } owner && owner != characterId)
        {
            return Result.Failure<MacroRunReport>(
                $"Макрос «{macro.Name}» принадлежит другому персонажу.");
        }

        var applied = await _progression
            .ApplyActionsAsync(
                characterId,
                macro.Name,
                TriggerPrefix + macro.Name,
                macro.Condition,
                [.. macro.Actions],
                cancellationToken)
            .ConfigureAwait(false);

        if (applied.IsFailure)
        {
            return Result.Failure<MacroRunReport>(applied.Error!);
        }

        var report = applied.Value;

        // Движок пропускает набор действий, если условие не выполнено, поэтому
        // отсутствие имени макроса среди применённых и означает невыполненное условие.
        var conditionMet = report.AppliedRules.Contains(macro.Name, StringComparer.Ordinal);

        MacroLog.MacroExecuted(_logger, macro.Name, report.CharacterName, report.Changes.Count);

        return Result.Success(new MacroRunReport(
            macro.Name,
            report.CharacterName,
            conditionMet,
            report.Changes,
            [.. report.Issues.Select(issue => issue.Message)]));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MacroListItem>> GetHotkeysAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var macros = await context.Macros.AsNoTracking()
            .Where(macro => macro.Enabled && macro.Hotkey != null && macro.Hotkey != string.Empty)
            .OrderBy(macro => macro.SortOrder)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return [.. macros.Select(Describe)];
    }

    /// <summary>
    /// Переводит макрос в строку списка.
    /// </summary>
    /// <param name="macro">Макрос из базы данных.</param>
    /// <returns>Строка списка.</returns>
    private static MacroListItem Describe(Macro macro) => new(
        macro.Id,
        macro.Name,
        macro.Description,
        macro.Category,
        macro.Hotkey,
        RuleSerializer.DeserializeActions(macro.ActionsJson).Count,
        !string.IsNullOrWhiteSpace(macro.Condition),
        macro.Enabled);

    /// <summary>
    /// Возвращает порядок для нового макроса.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Порядок отображения.</returns>
    private static async Task<int> NextOrderAsync(RpgDbContext context, CancellationToken cancellationToken) =>
        await context.Macros.AnyAsync(cancellationToken).ConfigureAwait(false)
            ? await context.Macros.MaxAsync(macro => macro.SortOrder, cancellationToken)
                .ConfigureAwait(false) + 1
            : 0;
}
