// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using AutoSettingsPage;
using AutoSettingsPage.Avalonia;
using Avalonia.Interactivity;
using Pixeval.Models.Settings;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Settings;

namespace Pixeval.Views.Viewers;

public sealed class NovelViewerSettingsPage : SettingsSubView
{
    private bool _applied;

    public NovelViewerSettingsPage()
    {
        DataContextChanged += (_, _) => Apply();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Apply();
    }

    private void Apply()
    {
        if (_applied || DataContext is not NovelViewerPageViewModel viewModel)
            return;

        _applied = true;
        LocalSettingsEntryHelper.Initialize();
        var group = SettingsBuilder.CreateGroupList(App.AppViewModel.AppSettings)
            .NewGroup(t => t.NovelSettings, entries => entries
                .Color(t => t.NovelBackground, entry => entry.PropertyChanged += (_, _) => viewModel.NotifyNovelBackgroundChanged())
                .Color(t => t.NovelFontColor, entry => entry.PropertyChanged += (_, _) => viewModel.NotifyNovelFontColorChanged())
                .Font(t => t.NovelFontFamily, entry => entry.PropertyChanged += (_, _) => viewModel.NotifyNovelFontFamilyChanged())
                .Enum(t => t.NovelFontWeight, entry => entry.PropertyChanged += (_, _) => viewModel.NotifyNovelFontWeightChanged())
                .Int(t => t.NovelFontSize, 5, 100, 1, entry => entry.PropertyChanged += (_, _) => viewModel.NotifyNovelFontSizeChanged())
                .Int(t => t.NovelLineHeight, 0, 150, 1, entry => entry.PropertyChanged += (_, _) => viewModel.NotifyNovelLineHeightChanged())
                .Int(t => t.NovelMaxWidth, 50, 10000, 50, entry => entry.PropertyChanged += (_, _) => viewModel.NotifyNovelMaxWidthChanged()))
            .Build()[0];
        SetGroup(group);
    }
}
