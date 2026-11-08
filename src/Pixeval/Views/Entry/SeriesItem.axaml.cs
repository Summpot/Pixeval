// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Mako.Global.Enum;
using Material3Expressive.Components;
using Misaki;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Entry;

public partial class SeriesItem : Card
{
    public SeriesItem()
    {
        InitializeComponent();
        SizeChanged += (_, e) => SeriesLayout.ColumnDefinitions[0].Width = new GridLength(e.NewSize.Width < 320 ? 64 : 96);
    }

    private void LatestContentButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: SeriesItemViewModel viewModel }
            || TopLevel.GetTopLevel(this)?.ViewContainer is not { } viewContainer)
            return;

        e.Handled = true;
        if (viewModel.WorkType is SimpleWorkType.Novel)
            viewContainer.CreateNovelPage(viewModel.Entry.LatestContentId);
        else
            viewContainer.CreateIllustrationPage(
                viewModel.Entry.LatestContentId.ToString(CultureInfo.InvariantCulture),
                IPlatformInfo.Pixiv);
    }

    private void AuthorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: SeriesItemViewModel viewModel })
            return;
        if (TopLevel.GetTopLevel(this)?.ViewContainer is not { } viewContainer)
            return;

        e.Handled = true;
        viewContainer.CreateUserPage(viewModel.Entry.User.Id);
    }
}
