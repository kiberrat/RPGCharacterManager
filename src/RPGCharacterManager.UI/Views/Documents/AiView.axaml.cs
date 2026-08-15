using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RPGCharacterManager.UI.ViewModels.Documents;

namespace RPGCharacterManager.UI.Views.Documents;

/// <summary>
/// Представление раздела помощника.
///
/// Единственная задача кода представления — прокручивать беседу к последнему
/// сообщению. Это поведение отображения, а не игровая логика, поэтому его место
/// здесь, а не в модели представления.
/// </summary>
public partial class AiView : UserControl
{
    private INotifyCollectionChanged? _messages;

    /// <summary>
    /// Создаёт представление раздела помощника.
    /// </summary>
    public AiView()
    {
        AvaloniaXamlLoader.Load(this);

        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        Unsubscribe();

        if (DataContext is not AiViewModel model)
        {
            return;
        }

        _messages = model.Messages;
        _messages.CollectionChanged += OnMessagesChanged;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action == NotifyCollectionChangedAction.Add)
        {
            this.FindControl<ScrollViewer>("ЛентаБеседы")?.ScrollToEnd();
        }
    }

    private void Unsubscribe()
    {
        if (_messages is not null)
        {
            _messages.CollectionChanged -= OnMessagesChanged;
            _messages = null;
        }
    }
}
