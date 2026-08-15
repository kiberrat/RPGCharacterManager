using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.Services;

/// <summary>
/// Всплывающие уведомления в правом верхнем углу главного окна.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private const int MaximumVisibleNotifications = 4;

    private static readonly TimeSpan NotificationLifetime = TimeSpan.FromSeconds(4);

    private WindowNotificationManager? _manager;

    /// <inheritdoc />
    public void Show(string message, NotificationKind kind = NotificationKind.Information)
    {
        Guard.NotNullOrWhiteSpace(message);

        if (Dispatcher.UIThread.CheckAccess())
        {
            ShowCore(message, kind);
        }
        else
        {
            Dispatcher.UIThread.Post(() => ShowCore(message, kind));
        }
    }

    private void ShowCore(string message, NotificationKind kind)
    {
        var manager = ResolveManager();
        manager?.Show(new Notification(ResolveTitle(kind), message, ToNotificationType(kind), NotificationLifetime));
    }

    private WindowNotificationManager? ResolveManager()
    {
        if (_manager is not null)
        {
            return _manager;
        }

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        var topLevel = desktop.MainWindow as TopLevel;
        if (topLevel is null)
        {
            return null;
        }

        _manager = new WindowNotificationManager(topLevel)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = MaximumVisibleNotifications,
        };

        return _manager;
    }

    private static string ResolveTitle(NotificationKind kind) => kind switch
    {
        NotificationKind.Success => "Готово",
        NotificationKind.Warning => "Предупреждение",
        NotificationKind.Error => "Ошибка",
        _ => "Сообщение",
    };

    private static NotificationType ToNotificationType(NotificationKind kind) => kind switch
    {
        NotificationKind.Success => NotificationType.Success,
        NotificationKind.Warning => NotificationType.Warning,
        NotificationKind.Error => NotificationType.Error,
        _ => NotificationType.Information,
    };
}
