using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Characters;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Страница выбора игровой системы и источников контента.
///
/// Выбранная система определяет, из какого контента мастер берёт расы, классы,
/// навыки и остальные объекты: показываются объекты этой системы и объекты,
/// не привязанные ни к одной системе.
/// </summary>
public sealed partial class GameSystemStepViewModel : WizardStepViewModel
{
    [ObservableProperty]
    private GameSystemOption? _selectedGameSystem;

    [ObservableProperty]
    private bool _useAllSources = true;

    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>
    /// Создаёт страницу выбора игровой системы.
    /// </summary>
    /// <param name="definition">Описание шага.</param>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="builder">Мастер создания персонажа.</param>
    /// <param name="changed">Обратный вызов при изменении данных персонажа.</param>
    public GameSystemStepViewModel(
        CharacterStepDefinition definition,
        CharacterDraft draft,
        ICharacterBuilderService builder,
        Action changed)
        : base(definition, draft, builder, changed)
    {
    }

    /// <summary>Доступные игровые системы.</summary>
    public ObservableCollection<GameSystemOption> GameSystems { get; } = [];

    /// <summary>Источники контента выбранной игровой системы.</summary>
    public ObservableCollection<ContentSourceViewModel> Sources { get; } = [];

    /// <summary>Игровые системы отсутствуют.</summary>
    public bool HasNoGameSystems => GameSystems.Count == 0;

    /// <summary>Источники контента отсутствуют.</summary>
    public bool HasNoSources => Sources.Count == 0;

    /// <summary>Список источников доступен для изменения.</summary>
    public bool IsSourceSelectionEnabled => !UseAllSources;

    /// <inheritdoc />
    public override async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;

        try
        {
            var systems = await Builder.GetGameSystemsAsync(cancellationToken).ConfigureAwait(true);

            GameSystems.Clear();

            foreach (var system in systems)
            {
                GameSystems.Add(system);
            }

            SelectedGameSystem = GameSystems.FirstOrDefault(system => system.Id == Draft.GameSystemId);

            // Единственная установленная система выбирается сама: иначе
            // пользователь должен сначала догадаться, что этот шаг обязателен,
            // а до тех пор расы, классы и остальной контент нигде не появляются.
            if (SelectedGameSystem is null && GameSystems.Count == 1)
            {
                SelectedGameSystem = GameSystems[0];
            }

            CanLeave = SelectedGameSystem is not null;

            UseAllSources = Draft.UseAllSources;

            await ReloadSourcesAsync(cancellationToken).ConfigureAwait(true);

            OnPropertyChanged(nameof(HasNoGameSystems));
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedGameSystemChanged(GameSystemOption? value)
    {
        Draft.GameSystemId = value?.Id;
        CanLeave = value is not null;

        // Смена системы меняет состав источников, поэтому прежний выбор
        // источников теряет смысл и сбрасывается.
        Draft.EnabledSourceIds.Clear();

        NotifyChanged();

        _ = ReloadSourcesAsync(CancellationToken.None);
    }

    partial void OnUseAllSourcesChanged(bool value)
    {
        Draft.UseAllSources = value;

        OnPropertyChanged(nameof(IsSourceSelectionEnabled));
        UpdateSummary();
        NotifyChanged();
    }

    private async Task ReloadSourcesAsync(CancellationToken cancellationToken)
    {
        var sources = await Builder
            .GetSourcesAsync(Draft.GameSystemId, cancellationToken)
            .ConfigureAwait(true);

        Sources.Clear();

        foreach (var source in sources)
        {
            Sources.Add(new ContentSourceViewModel(
                source,
                Draft.EnabledSourceIds.Contains(source.Id),
                OnSourceToggled));
        }

        OnPropertyChanged(nameof(HasNoSources));
        UpdateSummary();
    }

    private void OnSourceToggled(ContentSourceViewModel source)
    {
        if (source.IsEnabled)
        {
            Draft.EnabledSourceIds.Add(source.Id);
        }
        else
        {
            Draft.EnabledSourceIds.Remove(source.Id);
        }

        UpdateSummary();
        NotifyChanged();
    }

    private void UpdateSummary() => Summary = UseAllSources
        ? "Используется весь доступный контент."
        : $"Разрешено источников: {Draft.EnabledSourceIds.Count}. "
          + "Объекты, не относящиеся ни к одному источнику, доступны всегда.";
}
