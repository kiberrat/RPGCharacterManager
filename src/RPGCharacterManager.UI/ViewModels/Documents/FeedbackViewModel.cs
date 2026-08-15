using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Distribution;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>Документ отправки обратной связи разработчику.</summary>
public sealed partial class FeedbackViewModel : DocumentViewModelBase
{
    /// <summary>Максимальная длина сообщения.</summary>
    public const int MaximumMessageLength = 5000;

    private readonly IFeedbackService _feedback;
    private readonly INotificationService _notifications;

    [ObservableProperty]
    private FeedbackKind _selectedKind = FeedbackKind.Suggestion;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _contact = string.Empty;

    [ObservableProperty]
    private bool _includeTechnicalInformation = true;

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    private string _statusText;

    /// <summary>Создаёт документ обратной связи.</summary>
    /// <param name="feedback">Служба отправки.</param>
    /// <param name="notifications">Служба уведомлений.</param>
    public FeedbackViewModel(IFeedbackService feedback, INotificationService notifications)
        : base(CoreShellContributor.FeedbackDocumentId, "Обратная связь", "ЗначокОбратнаяСвязь")
    {
        _feedback = Guard.NotNull(feedback);
        _notifications = Guard.NotNull(notifications);
        _statusText = feedback.IsConfigured
            ? "Адрес получателя скрыт. Сообщение отправляется разработчику через защищённый обработчик."
            : "Локальная тестовая сборка: форма готова, отправка включится при публикации.";
    }

    /// <summary>Варианты категории сообщения.</summary>
    public IReadOnlyList<FeedbackKindOption> KindOptions { get; } =
    [
        new(FeedbackKind.Suggestion, "Предложение"),
        new(FeedbackKind.Bug, "Ошибка в приложении"),
        new(FeedbackKind.Question, "Вопрос"),
        new(FeedbackKind.Other, "Другое"),
    ];

    /// <summary>Число введённых символов.</summary>
    public string CharacterCount => $"{Message.Length} / {MaximumMessageLength}";

    /// <summary>Доступна ли отправка.</summary>
    public bool CanSend => _feedback.IsConfigured
        && !IsSending
        && Message.Trim().Length is >= 3 and <= MaximumMessageLength
        && Contact.Length <= 200;

    /// <summary>Отправляет сообщение.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача отправки.</returns>
    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        IsSending = true;
        StatusText = "Отправляем…";
        SendCommand.NotifyCanExecuteChanged();

        try
        {
            var technicalInformation = IncludeTechnicalInformation
                ? $"ОС: {RuntimeInformation.OSDescription}; платформа: {RuntimeInformation.OSArchitecture}; .NET: {Environment.Version}"
                : null;
            var version = typeof(FeedbackViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
            var result = await _feedback.SendAsync(
                new FeedbackMessage(SelectedKind, Message, Contact, version, technicalInformation),
                cancellationToken).ConfigureAwait(true);

            if (result.IsFailure)
            {
                StatusText = result.Error!;
                _notifications.Show(result.Error!, NotificationKind.Warning);
                return;
            }

            Message = string.Empty;
            StatusText = "Спасибо! Сообщение отправлено разработчику.";
            _notifications.Show("Обратная связь отправлена", NotificationKind.Success);
        }
        finally
        {
            IsSending = false;
            SendCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnMessageChanged(string value)
    {
        OnPropertyChanged(nameof(CharacterCount));
        OnPropertyChanged(nameof(CanSend));
        SendCommand.NotifyCanExecuteChanged();
    }

    partial void OnContactChanged(string value)
    {
        OnPropertyChanged(nameof(CanSend));
        SendCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSendingChanged(bool value) => OnPropertyChanged(nameof(CanSend));

    /// <summary>Отображаемый вариант категории.</summary>
    /// <param name="Value">Значение.</param>
    /// <param name="Title">Название.</param>
    public sealed record FeedbackKindOption(FeedbackKind Value, string Title);
}