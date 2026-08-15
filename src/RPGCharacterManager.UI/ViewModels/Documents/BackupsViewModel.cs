using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Entities;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Строка списка резервных копий.
/// </summary>
/// <param name="Record">Сведения о копии.</param>
/// <param name="CreatedAtText">Дата и время создания в виде текста.</param>
/// <param name="SizeText">Размер файла в виде текста.</param>
/// <param name="KindText">Способ создания копии.</param>
public sealed record BackupListItem(
    BackupRecord Record,
    string CreatedAtText,
    string SizeText,
    string KindText);

/// <summary>
/// Документ «Резервные копии»: создание, восстановление и очистка копий базы данных.
/// </summary>
public sealed partial class BackupsViewModel : DocumentViewModelBase
{
    private const double BytesInMegabyte = 1024.0 * 1024.0;
    private const string DateTimeFormat = "dd.MM.yyyy HH:mm:ss";

    private readonly IBackupService _backups;
    private readonly IBackgroundTaskService _backgroundTasks;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly ISettingsService _settings;
    private readonly IAppPathService _paths;

    [ObservableProperty]
    private BackupListItem? _selectedBackup;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Создаёт модель представления документа резервных копий.
    /// </summary>
    /// <param name="backups">Служба резервного копирования.</param>
    /// <param name="backgroundTasks">Служба фоновых задач.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    /// <param name="settings">Служба настроек.</param>
    /// <param name="paths">Служба путей пользовательских данных.</param>
    public BackupsViewModel(
        IBackupService backups,
        IBackgroundTaskService backgroundTasks,
        IDialogService dialogs,
        INotificationService notifications,
        ISettingsService settings,
        IAppPathService paths)
        : base(CoreShellContributor.BackupsDocumentId, "Резервные копии")
    {
        _backups = Guard.NotNull(backups);
        _backgroundTasks = Guard.NotNull(backgroundTasks);
        _dialogs = Guard.NotNull(dialogs);
        _notifications = Guard.NotNull(notifications);
        _settings = Guard.NotNull(settings);
        _paths = Guard.NotNull(paths);
    }

    /// <summary>Доступные резервные копии, начиная с самой новой.</summary>
    public ObservableCollection<BackupListItem> Backups { get; } = [];

    /// <summary>Каталог хранения резервных копий.</summary>
    public string BackupsDirectory => _paths.BackupsDirectory;

    /// <summary>Список копий пуст.</summary>
    public bool IsEmpty => Backups.Count == 0;

    /// <inheritdoc />
    public override Task InitializeAsync(CancellationToken cancellationToken = default) =>
        RefreshAsync(cancellationToken);

    /// <summary>
    /// Обновляет список доступных резервных копий.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после обновления списка.</returns>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var records = await _backgroundTasks
            .RunAsync("Чтение списка резервных копий", _backups.ListBackupsAsync, cancellationToken)
            .ConfigureAwait(true);

        Backups.Clear();

        foreach (var record in records)
        {
            Backups.Add(CreateListItem(record));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Создаёт резервную копию базы данных.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после создания копии.</returns>
    [RelayCommand]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            var result = await _backgroundTasks
                .RunAsync(
                    "Создание резервной копии",
                    token => _backups.CreateBackupAsync("Создана пользователем", false, token),
                    cancellationToken)
                .ConfigureAwait(true);

            if (result.IsFailure)
            {
                await _dialogs
                    .ShowErrorAsync("Резервное копирование", result.Error ?? "Неизвестная ошибка.")
                    .ConfigureAwait(true);
                return;
            }

            _notifications.Show("Резервная копия создана", NotificationKind.Success);
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Восстанавливает базу данных из выбранной копии.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после восстановления.</returns>
    [RelayCommand]
    private async Task RestoreAsync(CancellationToken cancellationToken)
    {
        if (SelectedBackup is null)
        {
            return;
        }

        var confirmed = await _dialogs.ShowConfirmationAsync(
                "Восстановление базы данных",
                $"""
                 Текущее состояние базы данных будет заменено копией от {SelectedBackup.CreatedAtText}.

                 Перед заменой приложение автоматически сохранит текущее состояние
                 в новую резервную копию, поэтому действие обратимо.

                 Продолжить?
                 """)
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var filePath = SelectedBackup.Record.FilePath;

            var result = await _backgroundTasks
                .RunAsync(
                    "Восстановление базы данных",
                    token => _backups.RestoreAsync(filePath, token),
                    cancellationToken)
                .ConfigureAwait(true);

            if (result.IsFailure)
            {
                await _dialogs
                    .ShowErrorAsync("Восстановление", result.Error ?? "Неизвестная ошибка.")
                    .ConfigureAwait(true);
                return;
            }

            await _dialogs.ShowInformationAsync(
                    "Восстановление завершено",
                    "База данных восстановлена. Перезапустите приложение, чтобы все разделы прочитали обновлённые данные.")
                .ConfigureAwait(true);

            await RefreshAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Удаляет копии, срок хранения которых истёк.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после очистки.</returns>
    [RelayCommand]
    private async Task RemoveObsoleteAsync(CancellationToken cancellationToken)
    {
        var retention = TimeSpan.FromDays(_settings.Current.BackupRetentionDays);

        var removed = await _backgroundTasks
            .RunAsync(
                "Очистка устаревших копий",
                token => _backups.RemoveObsoleteBackupsAsync(retention, token),
                cancellationToken)
            .ConfigureAwait(true);

        _notifications.Show(
            removed == 0 ? "Устаревших копий не найдено" : $"Удалено копий: {removed}",
            NotificationKind.Success);

        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    private static BackupListItem CreateListItem(BackupRecord record) => new(
        record,
        record.CreatedAt.ToLocalTime().ToString(DateTimeFormat, CultureInfo.CurrentCulture),
        string.Create(CultureInfo.CurrentCulture, $"{record.SizeInBytes / BytesInMegabyte:F2} МБ"),
        record.IsAutomatic ? "Автоматически" : "Вручную");
}
