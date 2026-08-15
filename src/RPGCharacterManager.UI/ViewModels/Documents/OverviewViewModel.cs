using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Data;
using RPGCharacterManager.Core.Abstractions.Engine;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Shell;

namespace RPGCharacterManager.UI.ViewModels.Documents;

/// <summary>
/// Документ «Обзор»: сводка о состоянии приложения, хранилища данных и журналов.
/// </summary>
public sealed partial class OverviewViewModel : DocumentViewModelBase
{
    private const double BytesInKilobyte = 1024.0;
    private const double BytesInMegabyte = BytesInKilobyte * BytesInKilobyte;

    private readonly IAppPathService _paths;
    private readonly IDatabaseService _database;
    private readonly IBackgroundTaskService _backgroundTasks;
    private readonly INotificationService _notifications;
    private readonly IFormulaEngine _formulaEngine;

    [ObservableProperty]
    private string _databaseStateText = "Состояние не проверялось";

    [ObservableProperty]
    private bool _isDatabaseAvailable;

    [ObservableProperty]
    private string _databaseSizeText = "—";

    [ObservableProperty]
    private string _logFilesText = "—";

    [ObservableProperty]
    private string _formulaEngineText = "—";

    [ObservableProperty]
    private string _formulaFunctionsText = "—";

    /// <summary>
    /// Создаёт модель представления документа обзора.
    /// </summary>
    /// <param name="paths">Служба путей пользовательских данных.</param>
    /// <param name="database">Служба обслуживания базы данных.</param>
    /// <param name="backgroundTasks">Служба фоновых задач.</param>
    /// <param name="notifications">Служба всплывающих уведомлений.</param>
    /// <param name="formulaEngine">Единый движок вычислений.</param>
    /// <param name="contributors">Подсистемы, подключённые к оболочке приложения.</param>
    public OverviewViewModel(
        IAppPathService paths,
        IDatabaseService database,
        IBackgroundTaskService backgroundTasks,
        INotificationService notifications,
        IFormulaEngine formulaEngine,
        IEnumerable<IShellContributor> contributors)
        : base(CoreShellContributor.OverviewDocumentId, "Обзор")
    {
        _paths = Guard.NotNull(paths);
        _database = Guard.NotNull(database);
        _backgroundTasks = Guard.NotNull(backgroundTasks);
        _notifications = Guard.NotNull(notifications);
        _formulaEngine = Guard.NotNull(formulaEngine);

        // Перечень разделов берётся у самих подсистем, а не переписывается здесь
        // руками: список, набранный текстом, устаревает на первом же этапе,
        // и обзор начинает обещать то, что уже сделано, — или молчать о сделанном.
        SectionsText = string.Join(
            ", ",
            Guard.NotNull(contributors)
                .OrderBy(contributor => contributor.Order)
                .SelectMany(contributor => contributor.GetNavigationItems())
                .OrderBy(item => item.Order)
                .Select(item => item.Title));
    }

    /// <summary>Разделы, подключённые к приложению, в порядке панели навигации.</summary>
    public string SectionsText { get; }

    /// <summary>Название приложения.</summary>
    public string ApplicationName { get; } = ApplicationConstants.ApplicationName;

    /// <summary>Версия приложения.</summary>
    public string VersionText { get; } = ApplicationConstants.Version;

    /// <summary>Версия среды выполнения .NET.</summary>
    public string RuntimeText { get; } = Environment.Version.ToString();

    /// <summary>Каталог пользовательских данных.</summary>
    public string DataDirectory => _paths.DataDirectory;

    /// <summary>Путь к файлу базы данных.</summary>
    public string DatabaseFilePath => _paths.DatabaseFilePath;

    /// <summary>Каталог журналов.</summary>
    public string LogsDirectory => _paths.LogsDirectory;

    /// <summary>Каталог резервных копий.</summary>
    public string BackupsDirectory => _paths.BackupsDirectory;

    /// <summary>Каталог пользовательского контента.</summary>
    public string ContentDirectory => _paths.ContentDirectory;

    /// <inheritdoc />
    public override Task InitializeAsync(CancellationToken cancellationToken = default) =>
        RefreshAsync(cancellationToken);

    /// <summary>
    /// Обновляет сведения о состоянии хранилища данных.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после обновления сведений.</returns>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        // Проверка выполняется как фоновая задача: обращение к файловой системе
        // и базе данных не должно задерживать поток интерфейса.
        var isAvailable = await _backgroundTasks
            .RunAsync(
                "Проверка состояния хранилища",
                token => _database.CanConnectAsync(token),
                cancellationToken)
            .ConfigureAwait(true);

        IsDatabaseAvailable = isAvailable;
        DatabaseStateText = isAvailable
            ? "Соединение установлено"
            : "База данных недоступна";

        DatabaseSizeText = FormatFileSize(_paths.DatabaseFilePath);
        LogFilesText = FormatLogSummary(_paths.LogsDirectory);

        RefreshFormulaEngineState();
    }

    /// <summary>
    /// Выполняет проверочное вычисление и показывает состав доступных функций.
    /// Проверка подтверждает, что движок формул подключён и работает.
    /// </summary>
    private void RefreshFormulaEngineState()
    {
        const string SelfCheckExpression = "ОкруглитьВниз((Сила - 10) / 2) + Максимум(1; Уровень)";

        var context = new SelfCheckContext();
        var result = _formulaEngine.Evaluate(SelfCheckExpression, context);

        FormulaEngineText = result.IsSuccess
            ? $"{SelfCheckExpression} = {result.Value.AsText()}"
            : $"Ошибка: {result.Error}";

        var names = _formulaEngine.Functions
            .Select(function => function.Name)
            .OrderBy(name => name, StringComparer.CurrentCulture);

        FormulaFunctionsText = string.Create(
            CultureInfo.CurrentCulture,
            $"Доступно функций: {_formulaEngine.Functions.Count} — {string.Join(", ", names)}");
    }

    /// <summary>
    /// Значения переменных для проверочного вычисления.
    /// </summary>
    private sealed class SelfCheckContext : IFormulaContext
    {
        private const double Strength = 18;
        private const double Level = 5;

        /// <inheritdoc />
        public bool TryGetVariable(string name, out FormulaValue value)
        {
            switch (name)
            {
                case "Сила":
                    value = FormulaValue.FromNumber(Strength);
                    return true;

                case "Уровень":
                    value = FormulaValue.FromNumber(Level);
                    return true;

                default:
                    value = default;
                    return false;
            }
        }
    }

    /// <summary>
    /// Копирует путь к каталогу пользовательских данных в буфер обмена.
    /// </summary>
    [RelayCommand]
    private void ShowDataDirectory() =>
        _notifications.Show($"Данные приложения хранятся в каталоге: {_paths.DataDirectory}");

    private static string FormatFileSize(string path)
    {
        if (!File.Exists(path))
        {
            return "Файл ещё не создан";
        }

        var bytes = new FileInfo(path).Length;

        return bytes >= BytesInMegabyte
            ? string.Create(CultureInfo.CurrentCulture, $"{bytes / BytesInMegabyte:F2} МБ")
            : string.Create(CultureInfo.CurrentCulture, $"{bytes / BytesInKilobyte:F1} КБ");
    }

    private static string FormatLogSummary(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return "Каталог журналов ещё не создан";
        }

        var files = Directory.GetFiles(directory, "*.log");
        if (files.Length == 0)
        {
            return "Файлов журнала нет";
        }

        var totalBytes = files.Sum(file => new FileInfo(file).Length);

        return string.Create(
            CultureInfo.CurrentCulture,
            $"Файлов: {files.Length}, общий объём: {totalBytes / BytesInKilobyte:F1} КБ");
    }
}
