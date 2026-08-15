using Microsoft.Extensions.Logging;
using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Abstractions.Rules;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.GameRules.Serialization;
using RPGCharacterManager.Shared.Extensions;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.Shared.Results;

namespace RPGCharacterManager.GameRules;

/// <summary>
/// Хранение игровых правил в базе данных.
///
/// Условия и действия сохраняются в текстовых полях записи <see cref="GameRule"/>,
/// как описывает документ 004_База_данных.md.
/// </summary>
public sealed class RuleService : IRuleService
{
    private readonly IRepository<GameRule> _repository;
    private readonly ILogger<RuleService> _logger;

    /// <summary>
    /// Создаёт службу хранения правил.
    /// </summary>
    /// <param name="repository">Хранилище записей правил.</param>
    /// <param name="logger">Журналировщик.</param>
    public RuleService(IRepository<GameRule> repository, ILogger<RuleService> logger)
    {
        _repository = Guard.NotNull(repository);
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuleDefinition>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.ListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return entities
            .Select(ToDefinition)
            .OrderBy(rule => rule.Category, StringComparer.CurrentCulture)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuleDefinition>> GetByTriggerAsync(
        string trigger,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(trigger);

        var entities = await _repository
            .ListAsync(entity => entity.Enabled && entity.Trigger == trigger, cancellationToken)
            .ConfigureAwait(false);

        return entities
            .Select(ToDefinition)
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Result> SaveAsync(RuleDefinition rule, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(rule);

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            return Result.Failure("Не задано название правила.");
        }

        try
        {
            var existing = await _repository.GetByIdAsync(rule.Id, cancellationToken).ConfigureAwait(false);

            if (existing is null)
            {
                await _repository.AddAsync(ToEntity(rule, new GameRule { Id = rule.Id }), cancellationToken)
                    .ConfigureAwait(false);

                RuleLog.RuleCreated(_logger, rule.Name);
            }
            else
            {
                await _repository.UpdateAsync(ToEntity(rule, existing), cancellationToken).ConfigureAwait(false);
                RuleLog.RuleUpdated(_logger, rule.Name);
            }

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RuleLog.RuleSaveFailed(_logger, exception, rule.Name);
            return Result.Failure($"Не удалось сохранить правило: {exception.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var deleted = await _repository.DeleteAsync(ruleId, cancellationToken).ConfigureAwait(false);

        if (deleted)
        {
            RuleLog.RuleDeleted(_logger, ruleId);
        }

        return deleted;
    }

    /// <summary>
    /// Преобразует хранимую запись в выполняемое правило.
    /// </summary>
    /// <param name="entity">Запись базы данных.</param>
    /// <returns>Правило в выполняемом виде.</returns>
    public static RuleDefinition ToDefinition(GameRule entity)
    {
        Guard.NotNull(entity);

        var definition = new RuleDefinition
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Category = string.IsNullOrWhiteSpace(entity.Category) ? RuleCategories.Custom : entity.Category,
            Trigger = entity.Trigger,
            Priority = entity.Priority,
            Enabled = entity.Enabled,
            Condition = RuleSerializer.DeserializeCondition(entity.Condition),
            GameSystemId = entity.GameSystemId,
            CharacterId = entity.CharacterId,
            CampaignId = entity.CampaignId,
            Version = entity.Version,
            Author = entity.Author,
        };

        foreach (var action in RuleSerializer.DeserializeActions(entity.ActionsJson))
        {
            definition.Actions.Add(action);
        }

        return definition;
    }

    /// <summary>
    /// Переносит выполняемое правило в хранимую запись.
    /// </summary>
    /// <param name="rule">Правило.</param>
    /// <param name="entity">Изменяемая запись базы данных.</param>
    /// <returns>Та же запись с заполненными полями.</returns>
    public static GameRule ToEntity(RuleDefinition rule, GameRule entity)
    {
        Guard.NotNull(rule);
        Guard.NotNull(entity);

        entity.Name = rule.Name;
        entity.SystemName = string.IsNullOrWhiteSpace(entity.SystemName)
            ? rule.Name.ToSystemName()
            : entity.SystemName;
        entity.Description = rule.Description;
        entity.Category = rule.Category;
        entity.Trigger = rule.Trigger;
        entity.Priority = rule.Priority;
        entity.Enabled = rule.Enabled;
        entity.Condition = RuleSerializer.SerializeCondition(rule.Condition);
        entity.ActionsJson = RuleSerializer.SerializeActions(rule.Actions);
        entity.GameSystemId = rule.GameSystemId;
        entity.CharacterId = rule.CharacterId;
        entity.CampaignId = rule.CampaignId;
        entity.Version = rule.Version;
        entity.Author = rule.Author;

        return entity;
    }
}

/// <summary>
/// Сообщения журнала подсистемы игровых правил.
/// </summary>
internal static partial class RuleLog
{
    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Создано правило «{Name}».")]
    public static partial void RuleCreated(ILogger logger, string name);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Изменено правило «{Name}».")]
    public static partial void RuleUpdated(ILogger logger, string name);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Information, Message = "Удалено правило {RuleId}.")]
    public static partial void RuleDeleted(ILogger logger, Guid ruleId);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Error, Message = "Не удалось сохранить правило «{Name}».")]
    public static partial void RuleSaveFailed(ILogger logger, Exception exception, string name);
}
