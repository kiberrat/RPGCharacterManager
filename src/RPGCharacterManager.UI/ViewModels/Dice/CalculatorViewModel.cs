using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Shared.Guards;

namespace RPGCharacterManager.UI.ViewModels.Dice;

/// <summary>Строка истории калькулятора.</summary>
public sealed record CalculatorHistoryRowViewModel(string Expression, string Result);

/// <summary>Удобный арифметический калькулятор в панели кубиков.</summary>
public sealed partial class CalculatorViewModel : ViewModelBase
{
    private const int MaximumHistory = 12;
    private readonly IFormulaEngine _formulas;

    [ObservableProperty]
    private string _expression = string.Empty;

    [ObservableProperty]
    private string _result = string.Empty;

    [ObservableProperty]
    private string? _error;

    /// <summary>Создаёт калькулятор.</summary>
    /// <param name="formulas">Движок арифметических выражений приложения.</param>
    public CalculatorViewModel(IFormulaEngine formulas) => _formulas = Guard.NotNull(formulas);

    /// <summary>Последние вычисления; новые находятся сверху.</summary>
    public ObservableCollection<CalculatorHistoryRowViewModel> History { get; } = [];

    /// <summary>Есть готовый результат.</summary>
    public bool HasResult => !string.IsNullOrWhiteSpace(Result);

    /// <summary>Есть сообщение об ошибке.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    /// <summary>Есть история вычислений.</summary>
    public bool HasHistory => History.Count > 0;

    [RelayCommand]
    private void Input(string? token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            Expression += token;
            Error = null;
        }
    }

    [RelayCommand]
    private void Backspace()
    {
        if (Expression.Length > 0)
        {
            Expression = Expression[..^1];
        }
        Error = null;
    }

    [RelayCommand]
    private void ToggleSign()
    {
        Expression = string.IsNullOrWhiteSpace(Expression) ? "-" : $"-({Expression.Trim()})";
        Error = null;
    }

    [RelayCommand]
    private void Clear()
    {
        Expression = string.Empty;
        Result = string.Empty;
        Error = null;
    }

    [RelayCommand]
    private void ClearHistory()
    {
        History.Clear();
        OnPropertyChanged(nameof(HasHistory));
    }

    [RelayCommand]
    private void UseHistory(CalculatorHistoryRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        Expression = row.Result;
        Result = row.Result;
        Error = null;
    }

    [RelayCommand]
    private void Calculate()
    {
        var source = Expression.Trim();
        if (source.Length == 0)
        {
            Error = "Введите выражение.";
            Result = string.Empty;
            return;
        }

        var normalized = source.Replace(',', '.').Replace('×', '*').Replace('÷', '/').Replace('−', '-');
        var range = _formulas.EvaluateRange(normalized);
        if (range.IsFailure)
        {
            Error = range.Error ?? "Не удалось вычислить выражение.";
            Result = string.Empty;
            return;
        }

        if (!range.Value.IsExact)
        {
            Error = "Для случайных бросков используйте модуль кубиков выше.";
            Result = string.Empty;
            return;
        }

        var evaluated = _formulas.Evaluate(normalized);
        if (evaluated.IsFailure)
        {
            Error = evaluated.Error ?? "Не удалось вычислить выражение.";
            Result = string.Empty;
            return;
        }

        var number = evaluated.Value.AsNumber();
        Result = number.ToString("0.############", CultureInfo.CurrentCulture);
        Error = null;
        History.Insert(0, new CalculatorHistoryRowViewModel(source, Result));
        while (History.Count > MaximumHistory)
        {
            History.RemoveAt(History.Count - 1);
        }
        OnPropertyChanged(nameof(HasHistory));
    }

    partial void OnResultChanged(string value) => OnPropertyChanged(nameof(HasResult));
    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));
}
