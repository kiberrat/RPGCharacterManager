using System.Windows.Input;
using Avalonia.Input;

namespace RPGCharacterManager.UI.ViewModels.Shell;

/// <summary>
/// Описание горячей клавиши главного окна.
///
/// Модель представления объявляет сочетание клавиш и команду, а представление
/// преобразует описание в <see cref="Avalonia.Input.KeyBinding"/>. Логика при этом
/// остаётся в модели представления.
/// </summary>
/// <param name="Gesture">Сочетание клавиш.</param>
/// <param name="Command">Выполняемая команда.</param>
/// <param name="IsMacro">
/// Сочетание принадлежит макросу. Такие сочетания перечитываются при изменении
/// макросов, а встроенные команды остаются на месте.
/// </param>
public sealed record ShortcutDefinition(KeyGesture Gesture, ICommand Command, bool IsMacro = false);
