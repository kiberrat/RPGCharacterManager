using Avalonia;
using Avalonia.Controls;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.Behaviors;

/// <summary>
/// Отмечает классом оформления элемент списка, который сейчас исчезает.
///
/// Поведение относится целиком к слою представления. Модель представления лишь
/// сообщает, какой объект уходит, и выдерживает паузу перед его удалением, а как
/// именно элемент исчезает — сжимается, гаснет или и то и другое — решают стили.
///
/// Класс назначается контейнеру элемента, а не корню его шаблона: сжиматься должен
/// весь занимаемый элементом промежуток, включая поля и отступы, иначе на месте
/// исчезнувшей вкладки остался бы пустой обрубок.
/// </summary>
public static class DisappearingItem
{
    /// <summary>Класс оформления, которым отмечается исчезающий элемент.</summary>
    private const string DisappearingClass = "Исчезает";

    /// <summary>
    /// Объект, элемент которого сейчас исчезает,
    /// или <see langword="null"/>, если ни один элемент не исчезает.
    /// </summary>
    public static readonly AttachedProperty<object?> ItemProperty =
        AvaloniaProperty.RegisterAttached<ListBox, object?>("Item", typeof(DisappearingItem));

    static DisappearingItem() => ItemProperty.Changed.AddClassHandler<ListBox>(OnItemChanged);

    /// <summary>
    /// Возвращает объект, элемент которого сейчас исчезает.
    /// </summary>
    /// <param name="list">Список, к которому подключено поведение.</param>
    /// <returns>Исчезающий объект или <see langword="null"/>.</returns>
    public static object? GetItem(ListBox list) => Guard.NotNull(list).GetValue(ItemProperty);

    /// <summary>
    /// Задаёт объект, элемент которого должен исчезнуть.
    /// </summary>
    /// <param name="list">Список, к которому подключается поведение.</param>
    /// <param name="value">Исчезающий объект или <see langword="null"/>.</param>
    public static void SetItem(ListBox list, object? value) =>
        Guard.NotNull(list).SetValue(ItemProperty, value);

    private static void OnItemChanged(ListBox list, AvaloniaPropertyChangedEventArgs args)
    {
        SetClass(list, args.OldValue, false);
        SetClass(list, args.NewValue, true);
    }

    private static void SetClass(ListBox list, object? item, bool disappearing)
    {
        if (item is null || FindContainer(list, item) is not { } container)
        {
            return;
        }

        if (disappearing)
        {
            if (!container.Classes.Contains(DisappearingClass))
            {
                container.Classes.Add(DisappearingClass);
            }
        }
        else
        {
            container.Classes.Remove(DisappearingClass);
        }
    }

    private static Control? FindContainer(ListBox list, object item)
    {
        for (var index = 0; index < list.ItemCount; index++)
        {
            if (list.ContainerFromIndex(index) is { } container
                && ReferenceEquals(container.DataContext, item))
            {
                return container;
            }
        }

        return null;
    }
}
