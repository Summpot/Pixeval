// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Views.Viewers;

public partial class WorkSeriesInfoPane : UserControl
{
    private WorkSeriesInfoViewModel? ViewModel => DataContext as WorkSeriesInfoViewModel;

    public WorkSeriesInfoPane()
    {
        InitializeComponent();
        IsVisible = false;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        IsVisible = ViewModel is not null;
    }

    private void OpenSeriesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel
            || this.AppNavigation is not { } navigator)
            return;

        navigator.CreateSeriesPage(viewModel.WorkType, viewModel.Id);
    }

    private void NavigationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel
            || sender is not Control { Tag: WorkSeriesNavigationViewModel navigation }
            || this.AppNavigation is not { } navigator)
            return;

        if (viewModel.WorkType is SimpleWorkType.Novel)
            navigator.CreateNovelPage(navigation.Id);
        else if (navigation.Illustration is { } illustration)
            navigator.CreateIllustrationPage(illustration);
        else
            navigator.CreateIllustrationPage(
                navigation.Id.ToString(),
                PlatformConstants.Pixiv);
    }
}
