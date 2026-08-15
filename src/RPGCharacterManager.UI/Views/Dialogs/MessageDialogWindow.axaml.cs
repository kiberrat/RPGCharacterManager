using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace RPGCharacterManager.UI.Views.Dialogs;

/// <summary>
/// Единое диалоговое окно сообщений приложения.
///
/// Документ 003_UI_UX.md запрещает использование устаревших системных окон Windows,
/// поэтому все сообщения показываются этим окном в общем оформлении приложения.
/// </summary>
public partial class MessageDialogWindow : Window
{
    /// <summary>
    /// Создаёт диалоговое окно.
    /// </summary>
    public MessageDialogWindow() => AvaloniaXamlLoader.Load(this);

    private void OnPrimaryButtonClick(object? sender, RoutedEventArgs args) => Close(true);

    private void OnSecondaryButtonClick(object? sender, RoutedEventArgs args) => Close(false);
}
