using CommunityToolkit.Mvvm.Input;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Core.Models.Settings;
using RPGCharacterManager.Core.Models.Shell;
using RPGCharacterManager.Shared;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Documents;

namespace RPGCharacterManager.UI.Shell;

/// <summary>
/// Элементы оболочки, предоставляемые ядром приложения.
///
/// Содержит только те разделы, которые реализованы на текущем этапе разработки.
/// Разделы «Кампании», «Инструменты» и «AI» добавляются собственными поставщиками
/// на соответствующих этапах ROADMAP — изменять этот класс или главное окно
/// для этого не требуется.
/// </summary>
public sealed class CoreShellContributor : IShellContributor
{
    /// <summary>Идентификатор документа обзора состояния приложения.</summary>
    public const string OverviewDocumentId = DocumentIds.Overview;

    /// <summary>Идентификатор документа настроек.</summary>
    public const string SettingsDocumentId = DocumentIds.Settings;

    /// <summary>Идентификатор документа обратной связи.</summary>
    public const string FeedbackDocumentId = DocumentIds.Feedback;

    /// <summary>Идентификатор документа встроенных мини-игр.</summary>
    public const string QuietTimeDocumentId = DocumentIds.QuietTime;

    /// <summary>Идентификатор документа управления резервными копиями.</summary>
    public const string BackupsDocumentId = DocumentIds.Backups;

    /// <summary>Идентификатор документа конструктора игровых правил.</summary>
    public const string RulesDocumentId = DocumentIds.Rules;

    /// <summary>Идентификатор документа менеджера контента.</summary>
    public const string ContentDocumentId = DocumentIds.Content;

    /// <summary>Идентификатор документа журнала событий.</summary>
    public const string JournalDocumentId = DocumentIds.Journal;

    private const double ScaleStep = 0.1;

    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly IAppPathService _paths;

    /// <summary>
    /// Создаёт поставщик элементов оболочки ядра.
    /// </summary>
    /// <param name="settings">Служба настроек.</param>
    /// <param name="dialogs">Служба диалоговых окон.</param>
    /// <param name="paths">Служба путей пользовательских данных.</param>
    public CoreShellContributor(
        ISettingsService settings,
        IDialogService dialogs,
        IAppPathService paths)
    {
        _settings = Guard.NotNull(settings);
        _dialogs = Guard.NotNull(dialogs);
        _paths = Guard.NotNull(paths);
    }

    /// <inheritdoc />
    public int Order => 0;

    /// <summary>
    /// Возвращает описания документов, предоставляемых ядром.
    /// </summary>
    /// <returns>Последовательность описаний документов.</returns>
    public static IEnumerable<IDocumentDescriptor> GetDocumentDescriptors()
    {
        yield return new DocumentDescriptor<ViewModels.Documents.OverviewViewModel>(
            OverviewDocumentId,
            "Обзор");

        yield return new DocumentDescriptor<ViewModels.Documents.SettingsViewModel>(
            SettingsDocumentId,
            "Настройки");

        yield return new DocumentDescriptor<ViewModels.Documents.FeedbackViewModel>(
            FeedbackDocumentId,
            "Обратная связь");

        yield return new DocumentDescriptor<ViewModels.Documents.QuietTimeViewModel>(
            QuietTimeDocumentId,
            "Тишину навели");

        yield return new DocumentDescriptor<ViewModels.Documents.BackupsViewModel>(
            BackupsDocumentId,
            "Резервные копии");

        yield return new DocumentDescriptor<ViewModels.Documents.RulesEditorViewModel>(
            RulesDocumentId,
            "Правила");

        yield return new DocumentDescriptor<ViewModels.Documents.ContentManagerViewModel>(
            ContentDocumentId,
            "Контент");

        yield return new DocumentDescriptor<ViewModels.Documents.JournalViewModel>(
            JournalDocumentId,
            "Журнал");
    }

    /// <inheritdoc />
    public IEnumerable<NavigationItemContribution> GetNavigationItems()
    {
        yield return new NavigationItemContribution("nav.overview", "Обзор", OverviewDocumentId)
        {
            Order = 0,
            IconKey = "ЗначокОбзор",
        };
        yield return new NavigationItemContribution("nav.content", "Контент", ContentDocumentId)
        {
            Order = 50,
            IconKey = "ЗначокКонтент",
        };
        yield return new NavigationItemContribution("nav.rules", "Правила", RulesDocumentId)
        {
            Order = 100,
            IconKey = "ЗначокПравила",
        };
        yield return new NavigationItemContribution("nav.journal", "Журнал", JournalDocumentId)
        {
            Order = 150,
            IconKey = "ЗначокЖурнал",
        };
        yield return new NavigationItemContribution("nav.backups", "Резервные копии", BackupsDocumentId)
        {
            Order = 890,
            IconKey = "ЗначокКопии",
        };
        yield return new NavigationItemContribution("nav.settings", "Настройки", SettingsDocumentId)
        {
            Order = 900,
            IconKey = "ЗначокНастройки",
        };
        yield return new NavigationItemContribution("nav.feedback", "Обратная связь", FeedbackDocumentId)
        {
            Order = 910,
            IconKey = "ЗначокОбратнаяСвязь",
        };
        yield return new NavigationItemContribution("nav.quietTime", "Тишину навели", QuietTimeDocumentId)
        {
            Order = 920,
            IconKey = "ЗначокМиниИгры",
        };
    }

    /// <inheritdoc />
    public IEnumerable<CommandContribution> GetCommands()
    {
        // Разделы здесь не перечисляются: их единственный вход — панель навигации.
        // Остаются действия, у которых своего раздела нет.
        yield return new CommandContribution(
            "command.scaleUp",
            "Увеличить масштаб интерфейса",
            new AsyncRelayCommand(() => ChangeScaleAsync(ScaleStep)))
        {
            Order = 0,
            GestureText = "Ctrl+OemPlus",
        };

        yield return new CommandContribution(
            "command.scaleDown",
            "Уменьшить масштаб интерфейса",
            new AsyncRelayCommand(() => ChangeScaleAsync(-ScaleStep)))
        {
            Order = 1,
            GestureText = "Ctrl+OemMinus",
        };

        yield return new CommandContribution(
            "command.scaleReset",
            "Сбросить масштаб интерфейса",
            new AsyncRelayCommand(ResetScaleAsync))
        {
            Order = 2,
            GestureText = "Ctrl+D0",
        };

        yield return new CommandContribution(
            "command.about",
            "О программе",
            new AsyncRelayCommand(ShowAboutAsync))
        {
            Order = 10,
            GestureText = "F1",
        };
    }

    private Task ChangeScaleAsync(double delta) =>
        _settings.UpdateAsync(settings => settings.InterfaceScale = Math.Clamp(
            Math.Round(settings.InterfaceScale + delta, 2),
            AppSettings.MinimumInterfaceScale,
            AppSettings.MaximumInterfaceScale));

    private Task ResetScaleAsync() =>
        _settings.UpdateAsync(settings => settings.InterfaceScale = 1.0);

    private Task ShowAboutAsync()
    {
        var version = typeof(CoreShellContributor).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        return _dialogs.ShowInformationAsync(
            $"О программе {ApplicationConstants.ApplicationName}",
            $"""
             Версия {version}

             Универсальный менеджер персонажей настольных ролевых игр.
             Все данные хранятся на этом компьютере. Сеть нужна только разделу «AI»,
             и только когда вы сами обращаетесь к помощнику.

             Каталог пользовательских данных:
             {_paths.DataDirectory}
             """);
    }

}
