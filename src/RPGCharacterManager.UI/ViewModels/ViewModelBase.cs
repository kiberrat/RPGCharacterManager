using CommunityToolkit.Mvvm.ComponentModel;

namespace RPGCharacterManager.UI.ViewModels;

/// <summary>
/// Базовый класс моделей представления.
///
/// Реализация <see cref="System.ComponentModel.INotifyPropertyChanged"/> предоставляется
/// пакетом CommunityToolkit.Mvvm через исходные генераторы, поэтому уведомления об
/// изменении свойств не пишутся вручную.
/// </summary>
public abstract class ViewModelBase : ObservableObject;
