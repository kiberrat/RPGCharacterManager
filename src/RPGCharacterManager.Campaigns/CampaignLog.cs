using Microsoft.Extensions.Logging;

namespace RPGCharacterManager.Campaigns;

/// <summary>
/// Сообщения журнала подсистемы кампаний.
/// </summary>
internal static partial class CampaignLog
{
    [LoggerMessage(
        EventId = 12001,
        Level = LogLevel.Information,
        Message = "Сохранена кампания «{Name}» ({CampaignId}).")]
    public static partial void CampaignSaved(ILogger logger, string name, Guid campaignId);

    [LoggerMessage(
        EventId = 12002,
        Level = LogLevel.Information,
        Message = "Удалена кампания «{Name}» ({CampaignId}).")]
    public static partial void CampaignDeleted(ILogger logger, string name, Guid campaignId);

    [LoggerMessage(
        EventId = 12003,
        Level = LogLevel.Information,
        Message = "В кампанию «{CampaignName}» добавлен участник «{ObjectName}» ({KindName}).")]
    public static partial void MemberAdded(
        ILogger logger,
        string objectName,
        string kindName,
        string campaignName);

    [LoggerMessage(
        EventId = 12004,
        Level = LogLevel.Information,
        Message = "Из состава кампании убран участник: {ObjectKind} {ObjectId}.")]
    public static partial void MemberRemoved(ILogger logger, string objectKind, Guid objectId);

    [LoggerMessage(
        EventId = 12005,
        Level = LogLevel.Information,
        Message = "Сохранено событие кампании «{Title}» ({CampaignId}).")]
    public static partial void EventSaved(ILogger logger, string title, Guid campaignId);

    [LoggerMessage(
        EventId = 12006,
        Level = LogLevel.Information,
        Message = "Удалено событие кампании «{Title}» ({CampaignId}).")]
    public static partial void EventDeleted(ILogger logger, string title, Guid campaignId);

    [LoggerMessage(EventId = 12010, Level = LogLevel.Error, Message = "Не удалось прочитать список кампаний.")]
    public static partial void CampaignsReadFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 12011, Level = LogLevel.Error, Message = "Не удалось прочитать кампанию {CampaignId}.")]
    public static partial void CampaignReadFailed(ILogger logger, Exception exception, Guid campaignId);

    [LoggerMessage(EventId = 12012, Level = LogLevel.Error, Message = "Не удалось сохранить кампанию «{Name}».")]
    public static partial void CampaignSaveFailed(ILogger logger, Exception exception, string name);

    [LoggerMessage(EventId = 12013, Level = LogLevel.Error, Message = "Не удалось удалить кампанию {CampaignId}.")]
    public static partial void CampaignDeleteFailed(ILogger logger, Exception exception, Guid campaignId);

    [LoggerMessage(
        EventId = 12014,
        Level = LogLevel.Error,
        Message = "Не удалось добавить участника «{ObjectName}» в кампанию {CampaignId}.")]
    public static partial void MemberAddFailed(
        ILogger logger,
        Exception exception,
        string objectName,
        Guid campaignId);

    [LoggerMessage(EventId = 12015, Level = LogLevel.Error, Message = "Не удалось сохранить участника {MemberId}.")]
    public static partial void MemberSaveFailed(ILogger logger, Exception exception, Guid memberId);

    [LoggerMessage(EventId = 12016, Level = LogLevel.Error, Message = "Не удалось убрать участника {MemberId}.")]
    public static partial void MemberRemoveFailed(ILogger logger, Exception exception, Guid memberId);

    [LoggerMessage(EventId = 12017, Level = LogLevel.Error, Message = "Не удалось сохранить событие «{Title}».")]
    public static partial void EventSaveFailed(ILogger logger, Exception exception, string title);

    [LoggerMessage(EventId = 12018, Level = LogLevel.Error, Message = "Не удалось переместить событие {EventId}.")]
    public static partial void EventMoveFailed(ILogger logger, Exception exception, Guid eventId);

    [LoggerMessage(EventId = 12019, Level = LogLevel.Error, Message = "Не удалось удалить событие {EventId}.")]
    public static partial void EventDeleteFailed(ILogger logger, Exception exception, Guid eventId);
}
