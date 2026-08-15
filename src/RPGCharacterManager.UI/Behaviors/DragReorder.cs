using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.Behaviors;

/// <summary>
/// Изменение порядка элементов горизонтального списка перетаскиванием.
///
/// Поведение подключается к списку присоединённым свойством
/// <see cref="CommandProperty"/> и относится целиком к слою представления:
/// оно работает с указателем и разметкой, а сам порядок элементов меняет
/// модель представления, выполняя переданную команду с <see cref="ReorderRequest"/>.
///
/// Перетаскиваемый элемент следует за указателем, а соседи занимают освобождённые
/// места сразу, как только указатель входит в их границы. Обратного колебания при
/// этом не возникает: после перестановки указатель оказывается над самим
/// перетаскиваемым элементом, а он в расчёте позиции не участвует.
/// </summary>
public static class DragReorder
{
    /// <summary>
    /// Расстояние в единицах разметки, которое указатель должен пройти,
    /// прежде чем нажатие будет истолковано как перетаскивание.
    /// Без порога любой щелчок по вкладке сдвигал бы её на несколько пикселей.
    /// </summary>
    private const double DragThreshold = 5.0;

    /// <summary>
    /// Класс оформления, которым отмечается перетаскиваемый элемент.
    /// Оформление приподнятой вкладки задано стилями, а не кодом.
    /// </summary>
    private const string DraggingClass = "Перетаскивается";

    /// <summary>
    /// Команда, выполняющая перемещение элемента.
    /// Получает <see cref="ReorderRequest"/> с перемещаемым элементом и новой позицией.
    /// </summary>
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<ListBox, ICommand?>("Command", typeof(DragReorder));

    /// <summary>Состояние текущего перетаскивания. Хранится у списка, а не глобально.</summary>
    private static readonly AttachedProperty<DragState?> StateProperty =
        AvaloniaProperty.RegisterAttached<ListBox, DragState?>("State", typeof(DragReorder));

    static DragReorder() => CommandProperty.Changed.AddClassHandler<ListBox>(OnCommandChanged);

    /// <summary>
    /// Возвращает команду перемещения элемента.
    /// </summary>
    /// <param name="list">Список, к которому подключено поведение.</param>
    /// <returns>Команда перемещения или <see langword="null"/>.</returns>
    public static ICommand? GetCommand(ListBox list) => Guard.NotNull(list).GetValue(CommandProperty);

    /// <summary>
    /// Задаёт команду перемещения элемента.
    /// </summary>
    /// <param name="list">Список, к которому подключается поведение.</param>
    /// <param name="value">Команда перемещения.</param>
    public static void SetCommand(ListBox list, ICommand? value) =>
        Guard.NotNull(list).SetValue(CommandProperty, value);

    private static void OnCommandChanged(ListBox list, AvaloniaPropertyChangedEventArgs args)
    {
        list.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        list.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
        list.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        list.RemoveHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost);

        if (args.NewValue is not ICommand)
        {
            return;
        }

        // Список сам обрабатывает нажатие, выбирая элемент, поэтому обработчики
        // подключаются с учётом уже обработанных событий.
        list.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble, true);
        list.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble, true);
        list.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble, true);
        list.AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Bubble, true);
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is not ListBox list)
        {
            return;
        }

        if (!args.GetCurrentPoint(list).Properties.IsLeftButtonPressed || args.Source is not Visual source)
        {
            return;
        }

        // Кнопка внутри элемента — закрытие вкладки, а не начало перетаскивания.
        if (source.FindAncestorOfType<Button>(true) is not null)
        {
            return;
        }

        if (source.FindAncestorOfType<ListBoxItem>(true) is not { DataContext: { } item } container
            || container.GetVisualParent() is not Panel panel)
        {
            return;
        }

        var position = args.GetPosition(panel);
        list.SetValue(
            StateProperty,
            new DragState(item, container, panel, position.X - container.Bounds.X, position.X));
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (sender is not ListBox list || list.GetValue(StateProperty) is not { } state)
        {
            return;
        }

        // Кнопку могли отпустить за пределами списка: тогда события отпускания
        // сюда не приходит, и состояние нужно сбросить при следующем движении.
        if (!args.GetCurrentPoint(list).Properties.IsLeftButtonPressed)
        {
            EndDrag(list);
            return;
        }

        var position = args.GetPosition(state.Panel);

        if (!state.IsDragging)
        {
            if (Math.Abs(position.X - state.OriginX) < DragThreshold)
            {
                return;
            }

            state.IsDragging = true;
            args.Pointer.Capture(list);
        }

        var targetIndex = FindTargetIndex(list, state.Container, position.X);
        if (targetIndex >= 0 && GetCommand(list) is { } command)
        {
            var request = new ReorderRequest(state.Item, targetIndex);
            if (command.CanExecute(request))
            {
                command.Execute(request);

                // Перемещение меняет разметку списка. Без немедленного пересчёта смещение
                // элемента вычислялось бы по его прежнему положению, и вкладка прыгала бы.
                list.UpdateLayout();
                state.Attach(list.ContainerFromIndex(targetIndex));
            }
        }

        state.Follow(position.X);
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs args) =>
        EndDrag(sender as ListBox);

    private static void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs args) =>
        EndDrag(sender as ListBox);

    private static void EndDrag(ListBox? list)
    {
        if (list?.GetValue(StateProperty) is not { } state)
        {
            return;
        }

        state.Release();
        list.SetValue(StateProperty, null);
    }

    /// <summary>
    /// Ищет элемент, в границах которого находится указатель.
    /// Перетаскиваемый элемент пропускается: он смещён и своё место уже покинул.
    /// </summary>
    /// <param name="list">Список элементов.</param>
    /// <param name="dragged">Перетаскиваемый элемент.</param>
    /// <param name="pointerX">Положение указателя вдоль полосы.</param>
    /// <returns>Позиция элемента под указателем или -1, если указатель вне элементов.</returns>
    private static int FindTargetIndex(ListBox list, Control? dragged, double pointerX)
    {
        for (var index = 0; index < list.ItemCount; index++)
        {
            if (list.ContainerFromIndex(index) is not { } container || ReferenceEquals(container, dragged))
            {
                continue;
            }

            var bounds = container.Bounds;
            if (pointerX >= bounds.X && pointerX <= bounds.Right)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Состояние одного перетаскивания.
    /// </summary>
    private sealed class DragState
    {
        private readonly TranslateTransform _transform = new();

        /// <summary>
        /// Создаёт состояние перетаскивания.
        /// </summary>
        /// <param name="item">Перемещаемый элемент коллекции.</param>
        /// <param name="container">Отображающий его элемент разметки.</param>
        /// <param name="panel">Панель, размещающая элементы списка.</param>
        /// <param name="grabOffset">Расстояние от левого края элемента до указателя.</param>
        /// <param name="originX">Положение указателя в момент нажатия.</param>
        public DragState(object item, Control container, Panel panel, double grabOffset, double originX)
        {
            Item = item;
            Container = container;
            Panel = panel;
            GrabOffset = grabOffset;
            OriginX = originX;
        }

        /// <summary>Перемещаемый элемент коллекции.</summary>
        public object Item { get; }

        /// <summary>Элемент разметки, отображающий <see cref="Item"/>.</summary>
        public Control Container { get; private set; }

        /// <summary>Панель, размещающая элементы списка.</summary>
        public Panel Panel { get; }

        /// <summary>Расстояние от левого края элемента до указателя в момент нажатия.</summary>
        public double GrabOffset { get; }

        /// <summary>Положение указателя в момент нажатия.</summary>
        public double OriginX { get; }

        /// <summary>Признак того, что порог перетаскивания уже пройден.</summary>
        public bool IsDragging { get; set; }

        /// <summary>
        /// Переключает состояние на другой элемент разметки.
        /// Список вправе пересоздать элемент после перестановки, поэтому ссылка
        /// на него обновляется после каждого перемещения.
        /// </summary>
        /// <param name="container">Новый элемент разметки или <see langword="null"/>.</param>
        public void Attach(Control? container)
        {
            if (container is null || ReferenceEquals(container, Container))
            {
                return;
            }

            Restore(Container);
            Container = container;
        }

        /// <summary>
        /// Сдвигает перетаскиваемый элемент так, чтобы он оставался под указателем.
        /// </summary>
        /// <param name="pointerX">Текущее положение указателя вдоль полосы.</param>
        public void Follow(double pointerX)
        {
            _transform.X = pointerX - GrabOffset - Container.Bounds.X;

            // Перетаскиваемый элемент проходит поверх соседей, а не под ними.
            Container.ZIndex = 1;
            Container.RenderTransform = _transform;

            if (!Container.Classes.Contains(DraggingClass))
            {
                Container.Classes.Add(DraggingClass);
            }
        }

        /// <summary>
        /// Возвращает элемент на своё место в разметке.
        /// </summary>
        public void Release() => Restore(Container);

        private static void Restore(Control container)
        {
            container.RenderTransform = null;
            container.ZIndex = 0;
            container.Classes.Remove(DraggingClass);
        }
    }
}
