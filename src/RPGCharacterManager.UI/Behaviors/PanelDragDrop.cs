using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.ViewModels.Documents;

namespace RPGCharacterManager.UI.Behaviors;

/// <summary>
/// Перетаскивание панелей между вкладками макета.
///
/// Поведение подключается к общему предку всех столбцов-вкладок и целиком
/// относится к слою представления: оно следит за указателем и вычисляет место
/// вставки, а сам перенос выполняет модель представления, получая
/// <see cref="PanelDropRequest"/>.
///
/// Перенос сделан на обычных событиях указателя, а не на встроенном механизме
/// переноса: тот передаёт данные между приложениями и потому работает
/// с текстом и файлами, тогда как здесь панель не покидает окна.
/// </summary>
public static class PanelDragDrop
{
    /// <summary>
    /// Расстояние, которое указатель должен пройти, прежде чем нажатие
    /// будет истолковано как перетаскивание. Без порога любой щелчок
    /// по панели считался бы переносом.
    /// </summary>
    private const double DragThreshold = 5.0;

    /// <summary>Класс оформления, которым отмечается перетаскиваемая панель.</summary>
    private const string DraggingClass = "Перетаскивается";

    /// <summary>
    /// Команда, выполняющая перенос панели.
    /// Получает <see cref="PanelDropRequest"/> с панелью, вкладкой и позицией.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("Command", typeof(PanelDragDrop));

    /// <summary>Состояние текущего переноса. Хранится у самого предка, а не глобально.</summary>
    private static readonly AttachedProperty<DragState?> StateProperty =
        AvaloniaProperty.RegisterAttached<Control, DragState?>("State", typeof(PanelDragDrop));

    static PanelDragDrop() => CommandProperty.Changed.AddClassHandler<Control>(OnCommandChanged);

    /// <summary>
    /// Возвращает команду переноса панели.
    /// </summary>
    /// <param name="control">Предок столбцов-вкладок.</param>
    /// <returns>Команда переноса или <see langword="null"/>.</returns>
    public static ICommand? GetCommand(Control control) =>
        Guard.NotNull(control).GetValue(CommandProperty);

    /// <summary>
    /// Задаёт команду переноса панели.
    /// </summary>
    /// <param name="control">Предок столбцов-вкладок.</param>
    /// <param name="value">Команда переноса.</param>
    public static void SetCommand(Control control, ICommand? value) =>
        Guard.NotNull(control).SetValue(CommandProperty, value);

    private static void OnCommandChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        control.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        control.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
        control.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);

        if (args.NewValue is null)
        {
            return;
        }

        control.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        control.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        control.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is not Control root || !args.GetCurrentPoint(root).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = args.GetPosition(root);
        var item = FindPanelItem(root, position);

        if (item is { DataContext: LayoutPanelRowViewModel panel })
        {
            root.SetValue(StateProperty, new DragState(panel, item, position));
        }
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (sender is not Control root || root.GetValue(StateProperty) is not { } state)
        {
            return;
        }

        if (state.IsDragging)
        {
            return;
        }

        var position = args.GetPosition(root);

        if (Math.Abs(position.X - state.Origin.X) < DragThreshold
            && Math.Abs(position.Y - state.Origin.Y) < DragThreshold)
        {
            return;
        }

        state.IsDragging = true;
        state.Item.Classes.Add(DraggingClass);
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (sender is not Control root || root.GetValue(StateProperty) is not { } state)
        {
            return;
        }

        root.SetValue(StateProperty, null);
        state.Item.Classes.Remove(DraggingClass);

        if (!state.IsDragging || GetCommand(root) is not { } command)
        {
            return;
        }

        var position = args.GetPosition(root);
        var target = FindList(root, position);

        if (target is not { DataContext: LayoutTabRowViewModel tab })
        {
            return;
        }

        var request = new PanelDropRequest(state.Panel, tab, IndexAt(root, target, position));

        if (command.CanExecute(request))
        {
            command.Execute(request);
        }
    }

    /// <summary>
    /// Находит строку панели под указанной точкой.
    /// </summary>
    /// <param name="root">Предок столбцов-вкладок.</param>
    /// <param name="point">Точка в координатах предка.</param>
    /// <returns>Строка панели или <see langword="null"/>.</returns>
    private static ListBoxItem? FindPanelItem(Control root, Point point) =>
        root.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .FirstOrDefault(item => item.DataContext is LayoutPanelRowViewModel && Contains(root, item, point));

    /// <summary>
    /// Находит список панелей под указанной точкой.
    /// </summary>
    /// <param name="root">Предок столбцов-вкладок.</param>
    /// <param name="point">Точка в координатах предка.</param>
    /// <returns>Список панелей или <see langword="null"/>.</returns>
    private static ListBox? FindList(Control root, Point point) =>
        root.GetVisualDescendants()
            .OfType<ListBox>()
            .FirstOrDefault(list => list.DataContext is LayoutTabRowViewModel && Contains(root, list, point));

    /// <summary>
    /// Вычисляет место вставки по положению указателя.
    ///
    /// Панель, отпущенная ниже середины строки, встаёт после неё: так перенос
    /// попадает туда, куда его целили.
    /// </summary>
    /// <param name="root">Предок столбцов-вкладок.</param>
    /// <param name="list">Список назначения.</param>
    /// <param name="point">Точка в координатах предка.</param>
    /// <returns>Позиция вставки, начиная с нуля.</returns>
    private static int IndexAt(Control root, ListBox list, Point point)
    {
        var index = 0;

        foreach (var item in list.GetVisualDescendants().OfType<ListBoxItem>())
        {
            if (item.TranslatePoint(new Point(0, item.Bounds.Height / 2), root) is not { } middle)
            {
                continue;
            }

            if (point.Y < middle.Y)
            {
                return index;
            }

            index++;
        }

        return index;
    }

    /// <summary>
    /// Проверяет, накрывает ли элемент указанную точку.
    /// </summary>
    /// <param name="root">Предок, в координатах которого задана точка.</param>
    /// <param name="control">Проверяемый элемент.</param>
    /// <param name="point">Точка.</param>
    /// <returns><see langword="true"/>, если точка внутри элемента.</returns>
    private static bool Contains(Control root, Control control, Point point) =>
        control.TranslatePoint(default, root) is { } origin
        && new Rect(origin, control.Bounds.Size).Contains(point);

    /// <summary>
    /// Состояние переноса панели.
    /// </summary>
    /// <param name="Panel">Перетаскиваемая панель.</param>
    /// <param name="Item">Строка списка, за которую взялись.</param>
    /// <param name="Origin">Точка нажатия в координатах предка.</param>
    private sealed record DragState(LayoutPanelRowViewModel Panel, ListBoxItem Item, Point Origin)
    {
        /// <summary>Порог пройден и нажатие истолковано как перенос.</summary>
        public bool IsDragging { get; set; }
    }
}
