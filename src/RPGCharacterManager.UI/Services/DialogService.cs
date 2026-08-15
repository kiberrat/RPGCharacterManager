using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.UI.ViewModels.Dialogs;
using RPGCharacterManager.UI.Views.Dialogs;

namespace RPGCharacterManager.UI.Services;

/// <summary>
/// Показ модальных диалоговых окон приложения.
/// </summary>
public sealed class DialogService : IDialogService
{
    /// <inheritdoc />
    public Task ShowInformationAsync(string title, string message) =>
        ShowAsync(new MessageDialogViewModel(MessageDialogKind.Information, title, message));

    /// <inheritdoc />
    public Task ShowErrorAsync(string title, string message, string? details = null) =>
        ShowAsync(new MessageDialogViewModel(MessageDialogKind.Error, title, message, details));

    /// <inheritdoc />
    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var result = await ShowAsync(new MessageDialogViewModel(MessageDialogKind.Confirmation, title, message))
            .ConfigureAwait(true);

        return result;
    }

    private static Task<bool> ShowAsync(MessageDialogViewModel viewModel)
    {
        // Диалог всегда открывается в потоке пользовательского интерфейса,
        // даже если запрос пришёл из фоновой операции.
        return Dispatcher.UIThread.CheckAccess()
            ? ShowCoreAsync(viewModel)
            : Dispatcher.UIThread.InvokeAsync(() => ShowCoreAsync(viewModel));
    }

    private static async Task<bool> ShowCoreAsync(MessageDialogViewModel viewModel)
    {
        var window = new MessageDialogWindow { DataContext = viewModel };
        var owner = ResolveOwnerWindow();

        if (owner is null)
        {
            // Главное окно ещё не создано: показываем диалог как самостоятельное окно,
            // чтобы ошибки этапа запуска не терялись.
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Show();
            return false;
        }

        return await window.ShowDialog<bool>(owner).ConfigureAwait(true);
    }

    private static Window? ResolveOwnerWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
