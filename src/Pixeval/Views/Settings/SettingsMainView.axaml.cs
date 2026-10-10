// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using AutoSettingsPage.Models;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace Pixeval.Views.Settings;

/// <summary>
/// Settings landing page. Business interactions live in <see cref="ViewModels.Settings.SettingsPageViewModel"/>;
/// this view only pushes sub pages onto its own navigation host, which is a purely visual concern.
/// </summary>
public partial class SettingsMainView : ContentPage
{
    public SettingsMainView() => InitializeComponent();

    private Task PushAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(object? parameter = null) where TPage : Page =>
        this.AppNavigation?.PushAsync<TPage>(this, parameter) ?? Task.CompletedTask;

    [RelayCommand]
    private Task OpenGroupAsync(ISettingsGroup? group) =>
        group is null ? Task.CompletedTask : PushAsync<SettingsSubView>(group);

    [RelayCommand]
    private Task OpenAboutAsync() => PushAsync<AboutPage>();

    [RelayCommand]
    private Task OpenHelpAsync() => PushAsync<HelpPage>();

    [RelayCommand]
    private Task OpenNavigationSettingsAsync() => PushAsync<NavigationSettingsPage>();
}
