using System;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Pixeval.ViewModels;
public abstract class ViewModelBase : ObservableObject
{
    private readonly IDisposable _languageSubscription;
    protected ViewModelBase() => _languageSubscription = AppLocalization.Instance.Subscribe(OnLanguageChanged);
    protected virtual void OnLanguageChanged() => OnPropertyChanged(string.Empty);
}
