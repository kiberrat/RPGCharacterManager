using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RPGCharacterManager.UI.ViewModels;

namespace RPGCharacterManager.UI;

/// <summary>
/// Сопоставление модели представления с её представлением по соглашению об именовании.
///
/// Модель <c>…ViewModels.Documents.SettingsViewModel</c> отображается представлением
/// <c>…Views.Documents.SettingsView</c>. Благодаря этому подключение нового документа
/// не требует регистрации шаблонов данных вручную.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private const string ViewModelNamespaceSegment = ".ViewModels.";
    private const string ViewNamespaceSegment = ".Views.";
    private const string ViewModelSuffix = "ViewModel";
    private const string ViewSuffix = "View";

    /// <inheritdoc />
    public Control Build(object? param)
    {
        if (param is null)
        {
            return CreateMissingViewPlaceholder("Модель представления не задана.");
        }

        var viewModelTypeName = param.GetType().FullName!;
        var viewTypeName = viewModelTypeName
            .Replace(ViewModelNamespaceSegment, ViewNamespaceSegment, StringComparison.Ordinal);

        if (viewTypeName.EndsWith(ViewModelSuffix, StringComparison.Ordinal))
        {
            viewTypeName = string.Concat(
                viewTypeName.AsSpan(0, viewTypeName.Length - ViewModelSuffix.Length),
                ViewSuffix);
        }

        var viewType = param.GetType().Assembly.GetType(viewTypeName);

        return viewType is null
            ? CreateMissingViewPlaceholder($"Представление «{viewTypeName}» не найдено.")
            : (Control)Activator.CreateInstance(viewType)!;
    }

    /// <inheritdoc />
    public bool Match(object? data) => data is ViewModelBase;

    private static TextBlock CreateMissingViewPlaceholder(string message) => new()
    {
        Text = message,
        Margin = new Avalonia.Thickness(20),
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
    };
}
