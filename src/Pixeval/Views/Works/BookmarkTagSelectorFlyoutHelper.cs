// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Pixeval.Pixiv;

namespace Pixeval.Views.Works;

public static class BookmarkTagSelectorFlyoutHelper
{
    public static async Task ShowAsync(
        Control placementTarget,
        SimpleWorkType workType,
        long workId,
        Func<(bool IsPrivate, IReadOnlyList<string>? Tags), Task> onTagsSelected,
        PlacementMode placement = PlacementMode.Bottom)
    {
        var flyout = new Flyout
        {
            Placement = placement
        };
        var tagSelector = new TagSelector
        {
            WorkType = workType,
            WorkId = workId
        };
        var content = new Border
        {
            Width = 340,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6),
            Child = tagSelector
        };
        flyout.Content = content;
        var topLevel = TopLevel.GetTopLevel(placementTarget);
        if (topLevel is not null)
        {
            content.MaxWidth = Math.Max(0, topLevel.ClientSize.Width - 48);
            topLevel.SizeChanged += OnSizeChanged;
        }
        var isSubmitting = false;
        tagSelector.TagsSelected += OnTagsSelected;
        flyout.Opened += (_, _) =>
        {
            // The invoking context-menu item has already detached. Give the new
            // popup its own keyboard route immediately, including while loading.
            if (tagSelector.FindAncestorOfType<FlyoutPresenter>() is { } presenter)
            {
                presenter.Focusable = true;
                presenter.Focus(NavigationMethod.Tab);
            }
        };
        flyout.Closed += (_, _) =>
        {
            tagSelector.TagsSelected -= OnTagsSelected;
            if (topLevel is not null)
                topLevel.SizeChanged -= OnSizeChanged;
        };
        flyout.ShowAt(placementTarget);
        await tagSelector.ResetSourceAsync();
        return;

        void OnTagsSelected(TagSelector sender, (bool IsPrivate, IReadOnlyList<string>? Tags) e) =>
            placementTarget.RunInteraction(async () =>
            {
                await onTagsSelected(e);
                flyout.Hide();
            });
    }
}
