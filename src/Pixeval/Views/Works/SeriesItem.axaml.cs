// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Works;

public partial class SeriesItem : EntryItem
{
    public SeriesItem() => InitializeComponent();

    private void LatestContentButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: Series viewModel }
            || this.AppNavigation is not { } navigator)
            return;

        if (viewModel.LatestContentId is not { } latestContentId)
            return;

        if (viewModel.WorkKind is SimpleWorkKind.Novel)
            navigator.CreateNovelPage(latestContentId);
        else
            navigator.CreateIllustrationPage(
                latestContentId.ToString(CultureInfo.InvariantCulture),
                PlatformConstants.Pixiv);
    }
}
