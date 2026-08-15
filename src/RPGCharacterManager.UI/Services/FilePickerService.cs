using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using RPGCharacterManager.Core.Abstractions.Presentation;

namespace RPGCharacterManager.UI.Services;

/// <summary>
/// Обзор файлов средствами операционной системы.
/// </summary>
public sealed class FilePickerService : IFilePicker
{
    /// <inheritdoc />
    public Task<string?> PickAsync(string title, string description, IReadOnlyList<string> extensions) =>
        Dispatcher.UIThread.CheckAccess()
            ? PickCoreAsync(title, description, extensions)
            : Dispatcher.UIThread.InvokeAsync(() => PickCoreAsync(title, description, extensions));

    private static async Task<string?> PickCoreAsync(
        string title,
        string description,
        IReadOnlyList<string> extensions)
    {
        var window = ResolveOwnerWindow();

        if (window?.StorageProvider is not { CanOpen: true } storage)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(description)
                {
                    // Обзор ждёт образцы вида «*.pdf», а приложение хранит
                    // расширения с точкой: перечень один, вид записи разный.
                    Patterns = [.. extensions.Select(extension => "*" + extension)],
                },
            ],
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc />
    public Task<string?> SaveAsync(
        string title,
        string description,
        string extension,
        string? suggestedName = null) =>
        Dispatcher.UIThread.CheckAccess()
            ? SaveCoreAsync(title, description, extension, suggestedName)
            : Dispatcher.UIThread.InvokeAsync(() => SaveCoreAsync(title, description, extension, suggestedName));

    private static async Task<string?> SaveCoreAsync(
        string title,
        string description,
        string extension,
        string? suggestedName)
    {
        var window = ResolveOwnerWindow();

        if (window?.StorageProvider is not { CanSave: true } storage)
        {
            return null;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,

            // Расширение подставляется обзором, если пользователь его не набрал:
            // иначе файл сохранился бы без расширения и не открылся бы обратно.
            DefaultExtension = extension.TrimStart('.'),
            FileTypeChoices =
            [
                new FilePickerFileType(description)
                {
                    Patterns = ["*" + extension],
                },
            ],
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }

    private static Window? ResolveOwnerWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
