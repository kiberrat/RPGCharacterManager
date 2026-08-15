using System.Collections.ObjectModel;
using RPGCharacterManager.Core.Abstractions.Ai;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Кто написал сообщение беседы.
/// </summary>
public enum AiAuthor
{
    /// <summary>Пользователь.</summary>
    User = 0,

    /// <summary>Помощник.</summary>
    Assistant = 1,

    /// <summary>Приложение: сообщение об ошибке или о ходе работы.</summary>
    Application = 2,
}

/// <summary>
/// Сообщение беседы, показываемое в разделе «AI».
/// </summary>
public sealed class AiMessageViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт сообщение беседы.
    /// </summary>
    /// <param name="author">Автор сообщения.</param>
    /// <param name="text">Текст сообщения.</param>
    /// <param name="steps">Что помощник сделал, отвечая на вопрос.</param>
    public AiMessageViewModel(AiAuthor author, string text, IReadOnlyList<string>? steps = null)
    {
        Author = author;
        Text = text;
        Steps = steps ?? [];
    }

    /// <summary>Автор сообщения.</summary>
    public AiAuthor Author { get; }

    /// <summary>Текст сообщения.</summary>
    public string Text { get; }

    /// <summary>Что помощник сделал, отвечая на вопрос.</summary>
    public IReadOnlyList<string> Steps { get; }

    /// <summary>Подпись автора сообщения.</summary>
    public string Title => Author switch
    {
        AiAuthor.User => "Вы",
        AiAuthor.Assistant => "Помощник",
        _ => "Приложение",
    };

    /// <summary>Сообщение написано пользователем.</summary>
    public bool IsUser => Author == AiAuthor.User;

    /// <summary>Сообщение сообщает об ошибке приложения.</summary>
    public bool IsApplication => Author == AiAuthor.Application;

    /// <summary>Сообщение содержит перечень выполненных действий.</summary>
    public bool HasSteps => Steps.Count > 0;
}

/// <summary>
/// Предложение помощника в списке действий.
///
/// Пока предложение не подтверждено, в базе ничего не изменилось: строка
/// показывает будущие значения полей и ждёт решения пользователя.
/// </summary>
public sealed class AiProposalViewModel : ViewModelBase
{
    /// <summary>
    /// Создаёт строку предложения.
    /// </summary>
    /// <param name="proposal">Предложение помощника.</param>
    public AiProposalViewModel(AiProposal proposal)
    {
        Proposal = Guard.NotNull(proposal);

        Changes = new ObservableCollection<string>(proposal.Changes.Select(Describe));
    }

    /// <summary>Предложение помощника.</summary>
    public AiProposal Proposal { get; }

    /// <summary>Изменения полей в виде «Поле: было → станет».</summary>
    public ObservableCollection<string> Changes { get; }

    /// <summary>Краткое описание действия.</summary>
    public string Summary => Proposal.Summary;

    /// <summary>Предложение ждёт решения пользователя.</summary>
    public bool IsPending => Proposal.State == AiProposalState.Pending;

    /// <summary>Предложение применено к базе данных.</summary>
    public bool IsApplied => Proposal.State == AiProposalState.Applied;

    /// <summary>Состояние предложения словами.</summary>
    public string StateText => Proposal.State switch
    {
        AiProposalState.Applied => "Применено",
        AiProposalState.Rejected => "Отклонено",
        AiProposalState.Failed => Proposal.Error ?? "Применить не удалось",
        _ => "Ждёт подтверждения",
    };

    /// <summary>Предложение не удалось применить.</summary>
    public bool IsFailed => Proposal.State == AiProposalState.Failed;

    /// <summary>
    /// Сообщает представлению, что состояние предложения изменилось.
    /// </summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsApplied));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(StateText));
    }

    private static string Describe(AiProposalChange change) => change.OldValue is null
        ? $"{change.Field}: {change.NewValue}"
        : $"{change.Field}: {change.OldValue} → {change.NewValue}";
}

/// <summary>
/// Персонаж в списке выбора раздела «AI».
/// </summary>
/// <param name="Id">Идентификатор персонажа; <see langword="null"/> — персонаж не выбран.</param>
/// <param name="Name">Имя персонажа.</param>
public sealed record AiCharacterOption(Guid? Id, string Name);
