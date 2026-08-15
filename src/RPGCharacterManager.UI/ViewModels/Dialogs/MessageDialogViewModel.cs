using CommunityToolkit.Mvvm.ComponentModel;

namespace RPGCharacterManager.UI.ViewModels.Dialogs;

/// <summary>
/// Назначение диалогового окна сообщения.
/// </summary>
public enum MessageDialogKind
{
    /// <summary>Информационное сообщение с единственной кнопкой закрытия.</summary>
    Information = 0,

    /// <summary>Сообщение об ошибке с блоком технических подробностей.</summary>
    Error = 1,

    /// <summary>Запрос подтверждения с кнопками согласия и отказа.</summary>
    Confirmation = 2,
}

/// <summary>
/// Модель представления единого диалогового окна сообщений приложения.
/// </summary>
public sealed partial class MessageDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _areDetailsExpanded;

    /// <summary>
    /// Создаёт модель представления диалогового окна.
    /// </summary>
    /// <param name="kind">Назначение окна.</param>
    /// <param name="title">Заголовок окна.</param>
    /// <param name="message">Основной текст сообщения.</param>
    /// <param name="details">Технические подробности, скрытые под раскрывающимся блоком.</param>
    public MessageDialogViewModel(MessageDialogKind kind, string title, string message, string? details = null)
    {
        Kind = kind;
        Title = title;
        Message = message;
        Details = details;
    }

    /// <summary>Назначение окна.</summary>
    public MessageDialogKind Kind { get; }

    /// <summary>Заголовок окна.</summary>
    public string Title { get; }

    /// <summary>Основной текст сообщения.</summary>
    public string Message { get; }

    /// <summary>Технические подробности или <see langword="null"/>, если их нет.</summary>
    public string? Details { get; }

    /// <summary>Блок технических подробностей доступен.</summary>
    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    /// <summary>Окно запрашивает подтверждение и показывает две кнопки.</summary>
    public bool IsConfirmation => Kind == MessageDialogKind.Confirmation;

    /// <summary>Окно сообщает об ошибке.</summary>
    public bool IsError => Kind == MessageDialogKind.Error;

    /// <summary>Текст основной кнопки.</summary>
    public string PrimaryButtonText => IsConfirmation ? "Да" : "Закрыть";
}
