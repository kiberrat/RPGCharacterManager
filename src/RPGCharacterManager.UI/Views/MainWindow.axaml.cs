using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using RPGCharacterManager.UI.ViewModels;
using RPGCharacterManager.UI.ViewModels.Shell;

namespace RPGCharacterManager.UI.Views;

/// <summary>
/// Главное окно приложения.
///
/// Код окна не содержит бизнес-логики: он лишь преобразует описания горячих клавиш,
/// объявленные моделью представления, в объекты <see cref="KeyBinding"/> платформы Avalonia,
/// поскольку коллекция <see cref="InputElement.KeyBindings"/> не поддерживает привязку данных.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Список сочетаний, за которым следит окно.</summary>
    private ObservableCollection<ShortcutDefinition>? _shortcuts;

    /// <summary>
    /// Создаёт главное окно.
    /// </summary>
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        if (_shortcuts is not null)
        {
            _shortcuts.CollectionChanged -= OnShortcutsChanged;
            _shortcuts = null;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            KeyBindings.Clear();
            return;
        }

        // Сочетания макросов приходят из базы данных и меняются во время работы,
        // поэтому окно следит за списком, а не читает его однажды.
        _shortcuts = viewModel.Shortcuts;
        _shortcuts.CollectionChanged += OnShortcutsChanged;

        RebuildKeyBindings();
    }

    private void OnShortcutsChanged(object? sender, NotifyCollectionChangedEventArgs args) =>
        RebuildKeyBindings();

    private void RebuildKeyBindings()
    {
        KeyBindings.Clear();

        if (_shortcuts is null)
        {
            return;
        }

        foreach (var shortcut in _shortcuts)
        {
            KeyBindings.Add(new KeyBinding
            {
                Gesture = shortcut.Gesture,
                Command = shortcut.Command,
            });
        }
    }
}
