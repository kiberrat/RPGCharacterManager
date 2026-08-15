using System.ComponentModel;
using System.Runtime.CompilerServices;
using RPGCharacterManager.Core.Abstractions.Presentation;

namespace RPGCharacterManager.Infrastructure.Diagnostics;

/// <summary>
/// Сведения строки состояния главного окна.
/// Подсистемы записывают сюда свой контекст, а интерфейс лишь отображает значения.
/// </summary>
public sealed class ApplicationStatusService : IApplicationStatusService
{
    private string? _currentCharacterName;
    private string? _currentGameSystemName;
    private SaveState _saveState = SaveState.Saved;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public string? CurrentCharacterName
    {
        get => _currentCharacterName;
        set => SetField(ref _currentCharacterName, value);
    }

    /// <inheritdoc />
    public string? CurrentGameSystemName
    {
        get => _currentGameSystemName;
        set => SetField(ref _currentGameSystemName, value);
    }

    /// <inheritdoc />
    public SaveState SaveState
    {
        get => _saveState;
        set => SetField(ref _saveState, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
