using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RPGCharacterManager.Core.Abstractions.Infrastructure;
using RPGCharacterManager.Core.Abstractions.Layouts;
using RPGCharacterManager.Core.Abstractions.Presentation;
using RPGCharacterManager.Shared.Guards;
using RPGCharacterManager.UI.Services;
using RPGCharacterManager.UI.Shell;
using RPGCharacterManager.UI.ViewModels;
using RPGCharacterManager.UI.ViewModels.Dice;
using RPGCharacterManager.UI.ViewModels.Shell;

namespace RPGCharacterManager.UI;

/// <summary>
/// Регистрация служб и моделей представления слоя интерфейса.
/// </summary>
public static class UiServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует службы представления, поставщики элементов оболочки,
    /// описания документов и модели представления главного окна.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Та же коллекция служб для построения цепочки вызовов.</returns>
    public static IServiceCollection AddUserInterface(this IServiceCollection services)
    {
        Guard.NotNull(services);

        // --- Инфраструктурные службы, реализованные средствами Avalonia ---
        services.TryAddSingleton<IUiDispatcher, AvaloniaDispatcher>();
        services.TryAddSingleton<IThemeService, ThemeService>();
        services.TryAddSingleton<IDialogService, DialogService>();
        services.TryAddSingleton<IFilePicker, FilePickerService>();
        services.TryAddSingleton<INotificationService, NotificationService>();
        services.TryAddSingleton<INavigationService, NavigationService>();

        // Панели листа персонажа объявляет слой интерфейса: подсистема макетов
        // работает с их ключами и о разметке не знает.
        services.TryAddSingleton<ISheetPanelCatalog, SheetPanelCatalog>();

        // --- Элементы оболочки, предоставляемые ядром и подсистемами ---
        services.AddSingleton<IShellContributor, CoreShellContributor>();
        services.AddSingleton<IShellContributor, CharacterShellContributor>();
        services.AddSingleton<IShellContributor, CampaignShellContributor>();
        services.AddSingleton<IShellContributor, MasterShellContributor>();
        services.AddSingleton<IShellContributor, LayoutShellContributor>();
        services.AddSingleton<IShellContributor, SearchShellContributor>();
        services.AddSingleton<IShellContributor, MacroShellContributor>();
        services.AddSingleton<IShellContributor, StatisticsShellContributor>();
        services.AddSingleton<IShellContributor, ExtensionShellContributor>();
        services.AddSingleton<IShellContributor, AiShellContributor>();

        foreach (var descriptor in CoreShellContributor.GetDocumentDescriptors()
                     .Concat(CharacterShellContributor.GetDocumentDescriptors())
                     .Concat(CampaignShellContributor.GetDocumentDescriptors())
                     .Concat(MasterShellContributor.GetDocumentDescriptors())
                     .Concat(LayoutShellContributor.GetDocumentDescriptors())
                     .Concat(SearchShellContributor.GetDocumentDescriptors())
                     .Concat(MacroShellContributor.GetDocumentDescriptors())
                     .Concat(StatisticsShellContributor.GetDocumentDescriptors())
                     .Concat(ExtensionShellContributor.GetDocumentDescriptors())
                     .Concat(AiShellContributor.GetDocumentDescriptors()))
        {
            services.AddSingleton(descriptor);
        }

        // --- Модели представления ---
        // Документы создаются навигацией через ActivatorUtilities, поэтому
        // регистрировать каждый из них отдельно не требуется.
        services.TryAddSingleton<StatusBarViewModel>();
        services.TryAddSingleton<DicePanelViewModel>();
        services.TryAddSingleton<MainWindowViewModel>();

        return services;
    }
}
