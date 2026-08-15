using System.Windows.Input;

namespace RPGCharacterManager.Core.Models.Shell;

/// <summary>
/// Описание команды приложения, у которой нет собственного раздела.
///
/// Раздел открывается единственным способом — выбором в панели навигации, поэтому
/// команды описывают только действия: изменить масштаб, открыть папку данных,
/// показать сведения о программе. Команда доступна с клавиатуры, а подсистема
/// добавляет свои команды, не изменяя главное окно.
/// </summary>
public sealed class CommandContribution
{
    /// <summary>
    /// Создаёт описание команды.
    /// </summary>
    /// <param name="id">Уникальный идентификатор команды.</param>
    /// <param name="title">Название команды для пользователя.</param>
    /// <param name="command">Выполняемая команда.</param>
    public CommandContribution(string id, string title, ICommand command)
    {
        Id = id;
        Title = title;
        Command = command;
    }

    /// <summary>Уникальный идентификатор команды.</summary>
    public string Id { get; }

    /// <summary>Название команды для пользователя.</summary>
    public string Title { get; }

    /// <summary>Выполняемая команда.</summary>
    public ICommand Command { get; }

    /// <summary>Порядок сортировки. Меньшее значение отображается раньше.</summary>
    public int Order { get; init; }

    /// <summary>
    /// Сочетание клавиш в записи Avalonia, например <c>Ctrl+OemPlus</c>.
    /// Пустое значение означает, что горячей клавиши нет.
    /// </summary>
    public string? GestureText { get; init; }
}
