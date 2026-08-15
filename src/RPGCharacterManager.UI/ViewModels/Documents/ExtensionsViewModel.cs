using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Extensions;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Документ «Расширения»: установка наборов игровых объектов и выгрузка своих.
///
/// Расширение — это набор объектов самого приложения, а не программа
/// (решение Р-102). Поэтому раздел не спрашивает разрешений и не предупреждает
/// об опасности: устанавливаемое не может сделать ничего, чего не может сделать
/// человек за тем же приложением.
/// </summary>
public sealed partial class ExtensionsViewModel : DocumentViewModelBase
{
    /// <summary>Версия, предлагаемая для нового расширения.</summary>
    public const string DefaultVersion = "1.0";

    private readonly IExtensionService _extensions;
    private readonly IFilePicker _files;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private ExtensionSource? _selectedSource;

    [ObservableProperty]
    private string _exportName = string.Empty;

    [ObservableProperty]
    private string _exportVersion = DefaultVersion;

    [ObservableProperty]
    private string _exportAuthor = string.Empty;

    [ObservableProperty]
    private string _exportDescription = string.Empty;

    [ObservableProperty]
    private string _exportLicense = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Создаёт модель представления расширений.
    /// </summary>
    /// <param name="extensions">Служба расширений.</param>
    /// <param name="files">Обзор файлов.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    public ExtensionsViewModel(
        IExtensionService extensions,
        IFilePicker files,
        IDialogService dialogs)
        : base(ExtensionShellContributor.ListDocumentId, "Расширения")
    {
        _extensions = Guard.NotNull(extensions);
        _files = Guard.NotNull(files);
        _dialogs = Guard.NotNull(dialogs);
    }

    /// <summary>Установленные расширения.</summary>
    public ObservableCollection<ExtensionItem> Items { get; } = [];

    /// <summary>То, что можно выгрузить в файл.</summary>
    public ObservableCollection<ExtensionSource> Sources { get; } = [];

    /// <summary>Установленных расширений нет.</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>Выгружать нечего: в приложении нет ни игровых систем, ни расширений.</summary>
    public bool HasSources => Sources.Count > 0;

    /// <summary>Краткая сводка над списком.</summary>
    public string Summary => Items.Count == 0
        ? "Расширений не установлено."
        : $"Установлено: {Items.Count} · объектов: {Format(Items.Sum(item => item.ObjectCount))}";

    /// <inheritdoc />
    public override Task InitializeAsync(CancellationToken cancellationToken = default) =>
        ReloadAsync(cancellationToken);

    /// <summary>
    /// Перечитывает список расширений и источников выгрузки.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после чтения.</returns>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            var installed = await _extensions.GetAllAsync(cancellationToken).ConfigureAwait(true);

            if (installed.IsFailure)
            {
                await _dialogs.ShowErrorAsync("Расширения", installed.Error!).ConfigureAwait(true);
                return;
            }

            Items.Clear();

            foreach (var item in installed.Value)
            {
                Items.Add(item);
            }

            var sources = await _extensions.GetSourcesAsync(cancellationToken).ConfigureAwait(true);

            if (sources.IsSuccess)
            {
                var selected = SelectedSource?.Id;

                Sources.Clear();

                foreach (var source in sources.Value)
                {
                    Sources.Add(source);
                }

                SelectedSource = Sources.FirstOrDefault(source => source.Id == selected)
                    ?? Sources.FirstOrDefault();
            }
        }
        finally
        {
            IsBusy = false;
            NotifyCounters();
        }
    }

    /// <summary>
    /// Перечитывает список по требованию пользователя.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после чтения.</returns>
    [RelayCommand]
    private Task RefreshAsync(CancellationToken cancellationToken) => ReloadAsync(cancellationToken);

    /// <summary>
    /// Устанавливает расширение из выбранного файла.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после установки.</returns>
    [RelayCommand]
    private async Task InstallAsync(CancellationToken cancellationToken)
    {
        var path = await _files
            .PickAsync("Установить расширение", ExtensionPackage.FormatTitle, [ExtensionPackage.FileExtension])
            .ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        IsBusy = true;

        try
        {
            var preview = await _extensions.InspectAsync(path, cancellationToken).ConfigureAwait(true);

            if (preview.IsFailure)
            {
                await _dialogs.ShowErrorAsync("Установка расширения", preview.Error!).ConfigureAwait(true);
                return;
            }

            if (!preview.Value.CanInstall)
            {
                await _dialogs
                    .ShowErrorAsync(
                        "Установка невозможна",
                        string.Join(Environment.NewLine, preview.Value.Problems))
                    .ConfigureAwait(true);

                return;
            }

            if (!await _dialogs
                .ShowConfirmationAsync("Установка расширения", Describe(preview.Value))
                .ConfigureAwait(true))
            {
                return;
            }

            var installed = await _extensions.InstallAsync(path, cancellationToken).ConfigureAwait(true);

            if (installed.IsFailure)
            {
                await _dialogs.ShowErrorAsync("Установка расширения", installed.Error!).ConfigureAwait(true);
                return;
            }
        }
        finally
        {
            IsBusy = false;
        }

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Включает или отключает расширение.
    /// </summary>
    /// <param name="item">Расширение.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    [RelayCommand]
    private async Task ToggleAsync(ExtensionItem? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        var result = await _extensions
            .SetEnabledAsync(item.Id, !item.IsEnabled, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Расширения", result.Error!).ConfigureAwait(true);
            return;
        }

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Удаляет расширение вместе с его объектами.
    /// </summary>
    /// <param name="item">Расширение.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    [RelayCommand]
    private async Task RemoveAsync(ExtensionItem? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        var confirmed = await _dialogs
            .ShowConfirmationAsync(
                "Удалить расширение",
                $"Удалить «{item.Manifest.Name}» и все его объекты ({Format(item.ObjectCount)})? "
                + "Персонажи, ссылающиеся на них, потеряют эти ссылки. Действие нельзя отменить.")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var result = await _extensions.RemoveAsync(item.Id, cancellationToken).ConfigureAwait(true);

        if (result.IsFailure)
        {
            await _dialogs.ShowErrorAsync("Расширения", result.Error!).ConfigureAwait(true);
            return;
        }

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Выгружает выбранный источник в файл расширения.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после выгрузки.</returns>
    [RelayCommand]
    private async Task ExportAsync(CancellationToken cancellationToken)
    {
        if (SelectedSource is not { } source)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(ExportName) ? source.Name : ExportName.Trim();

        var path = await _files
            .SaveAsync("Выгрузить расширение", ExtensionPackage.FormatTitle, ExtensionPackage.FileExtension, name)
            .ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        IsBusy = true;

        try
        {
            var manifest = new ExtensionManifest(
                name,
                string.IsNullOrWhiteSpace(ExportVersion) ? DefaultVersion : ExportVersion.Trim(),
                Trimmed(ExportAuthor),
                Trimmed(ExportDescription),
                Trimmed(ExportLicense),
                source.IsGameSystem ? source.Name : null);

            var request = new ExtensionExportRequest(
                path,
                manifest,
                source.IsGameSystem ? source.Id : null,
                source.IsGameSystem ? null : source.Id);

            var result = await _extensions.ExportAsync(request, cancellationToken).ConfigureAwait(true);

            if (result.IsFailure)
            {
                await _dialogs.ShowErrorAsync("Выгрузка расширения", result.Error!).ConfigureAwait(true);
                return;
            }

            await _dialogs
                .ShowInformationAsync(
                    "Расширение выгружено",
                    $"«{name}»: объектов {Format(result.Value.ObjectCount)}."
                    + Environment.NewLine + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        result.Value.Sections.Select(section => $"{section.Title}: {section.Count}"))
                    + Environment.NewLine + Environment.NewLine
                    + result.Value.Path)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Составляет описание устанавливаемого расширения для подтверждения.
    /// </summary>
    /// <param name="preview">Разбор файла.</param>
    /// <returns>Текст подтверждения.</returns>
    private static string Describe(ExtensionPreview preview)
    {
        var lines = new List<string>
        {
            preview.IsUpdate
                ? $"«{preview.Manifest.Name}» {preview.Manifest.Version} заменит установленную версию "
                  + $"{preview.ReplacesVersion}. Прежнее содержимое будет убрано."
                : $"Установить «{preview.Manifest.Name}» {preview.Manifest.Version}?",
            string.Empty,
        };

        lines.AddRange(preview.Sections.Select(section => $"{section.Title}: {section.Count}"));

        if (preview.Warnings.Count > 0)
        {
            lines.Add(string.Empty);
            lines.AddRange(preview.Warnings);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string? Trimmed(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Format(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private void NotifyCounters()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasSources));
        OnPropertyChanged(nameof(Summary));
    }

    partial void OnSelectedSourceChanged(ExtensionSource? value)
    {
        // Название заполняется по выбранному источнику, пока пользователь
        // не написал своё: чаще всего оно и нужно.
        if (value is not null && string.IsNullOrWhiteSpace(ExportName))
        {
            ExportName = value.Name;
        }
    }
}
