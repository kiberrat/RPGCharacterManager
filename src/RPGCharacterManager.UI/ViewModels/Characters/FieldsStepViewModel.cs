using System.Collections.ObjectModel;
using RPGCharacterManager.Core.Abstractions.Characters;
using RPGCharacterManager.Core.Models.Characters;
using RPGCharacterManager.UI.ViewModels.Content;

namespace RPGCharacterManager.UI.ViewModels.Characters;

/// <summary>
/// Страница мастера, отображающая форму полей персонажа.
///
/// Форма строится по описанию шага теми же средствами, что и редактор контента,
/// поэтому игровая система может добавить собственные поля, не изменяя мастер.
/// </summary>
public sealed class FieldsStepViewModel : WizardStepViewModel
{
    /// <summary>
    /// Создаёт страницу формы полей.
    /// </summary>
    /// <param name="definition">Описание шага.</param>
    /// <param name="draft">Создаваемый персонаж.</param>
    /// <param name="builder">Мастер создания персонажа.</param>
    /// <param name="changed">Обратный вызов при изменении данных персонажа.</param>
    public FieldsStepViewModel(
        CharacterStepDefinition definition,
        CharacterDraft draft,
        ICharacterBuilderService builder,
        Action changed)
        : base(definition, draft, builder, changed)
    {
        foreach (var group in definition.Fields.GroupBy(field => field.Group))
        {
            var fields = group.Select(field => new ContentFieldViewModel(
                field,
                draft.Character,
                [],
                ApplyFields));

            FieldGroups.Add(new ContentFieldGroupViewModel(group.Key, fields));
        }
    }

    /// <summary>Разделы формы.</summary>
    public ObservableCollection<ContentFieldGroupViewModel> FieldGroups { get; } = [];

    /// <summary>
    /// Записывает введённые значения в персонажа.
    ///
    /// Значения переносятся при каждом изменении, а не при переходе на следующий шаг:
    /// уровень персонажа участвует в формулах и требованиях, поэтому предварительный
    /// просмотр должен отражать введённое значение немедленно.
    /// </summary>
    private void ApplyFields()
    {
        foreach (var field in FieldGroups.SelectMany(group => group.Fields))
        {
            field.TryApply(out _);
        }

        NotifyChanged();
    }
}
