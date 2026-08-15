namespace RPGCharacterManager.UI.Behaviors;

/// <summary>
/// Запрос на перемещение элемента списка в новую позицию.
///
/// Список отображает коллекцию, доступную только для чтения, поэтому изменить
/// порядок элементов может лишь модель представления. Поведение
/// <see cref="DragReorder"/> сообщает ей о перемещении этим запросом.
/// </summary>
/// <param name="Item">Перемещаемый элемент — объект, который отображает вкладка.</param>
/// <param name="TargetIndex">Позиция, в которую следует поместить элемент.</param>
public sealed record ReorderRequest(object Item, int TargetIndex);
