// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Misaki;
using Pixeval.Models.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Entry;

public partial class SeriesItem : EntryItem
{
    public SeriesItem() => InitializeComponent();

    private void LatestContentButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: Series viewModel }
            || TopLevel.GetTopLevel(this)?.ViewContainer is not { } viewContainer)
            return;

        if (viewModel.LatestContentId is not { } latestContentId)
            return;

        if (viewModel.WorkType is SimpleWorkType.Novel)
            viewContainer.CreateNovelPage(latestContentId);
        else
            viewContainer.CreateIllustrationPage(
                latestContentId.ToString(CultureInfo.InvariantCulture),
                IPlatformInfo.Pixiv);
    }

    private void AuthorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: Series viewModel })
            return;
        if (TopLevel.GetTopLevel(this)?.ViewContainer is not { } viewContainer)
            return;

        if (viewModel.User is { } user)
            viewContainer.CreateUserPage(user.Id);
    }
}
