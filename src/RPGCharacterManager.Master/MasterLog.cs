using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Master;

/// <summary>
/// Сообщения журнала режима мастера.
/// </summary>
internal static partial class MasterLog
{
    [LoggerMessage(
        EventId = 13001,
        Level = LogLevel.Information,
        Message = "Массовое изменение ресурса «{ResourceName}» на {Delta}: изменено персонажей — {Changed}.")]
    public static partial void ResourceChanged(
        ILogger logger,
        string resourceName,
        double delta,
        int changed);

    [LoggerMessage(
        EventId = 13002,
        Level = LogLevel.Information,
        Message = "Массовое наложение эффекта «{EffectName}»: изменено персонажей — {Changed}.")]
    public static partial void EffectApplied(ILogger logger, string effectName, int changed);

    [LoggerMessage(
        EventId = 13003,
        Level = LogLevel.Information,
        Message = "Массовое снятие эффекта «{EffectName}»: изменено персонажей — {Changed}.")]
    public static partial void EffectRemoved(ILogger logger, string effectName, int changed);

    [LoggerMessage(
        EventId = 13004,
        Level = LogLevel.Information,
        Message = "Брошена инициатива по формуле «{Formula}»: участников — {Count}.")]
    public static partial void InitiativeRolled(ILogger logger, string formula, int count);

    [LoggerMessage(
        EventId = 13005,
        Level = LogLevel.Information,
        Message = "Ход передан участнику «{CharacterName}», раунд {Round}.")]
    public static partial void TurnAdvanced(ILogger logger, string characterName, int round);

    [LoggerMessage(
        EventId = 13006,
        Level = LogLevel.Information,
        Message = "Бой завершён: очередь хода очищена.")]
    public static partial void CombatEnded(ILogger logger);

    [LoggerMessage(
        EventId = 13007,
        Level = LogLevel.Error,
        Message = "Не удалось выполнить действие режима мастера: {Action}.")]
    public static partial void ActionFailed(ILogger logger, Exception exception, string action);
}
