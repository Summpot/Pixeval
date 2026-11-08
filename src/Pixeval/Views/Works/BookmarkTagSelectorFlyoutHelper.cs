// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Pixeval.Pixiv;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Pixeval.Views.Works;

public static class BookmarkTagSelectorFlyoutHelper
{
    public static async Task ShowAsync(
        Control placementTarget,
        SimpleWorkKind workType,
        long workId,
        Func<(bool IsPrivate, IReadOnlyList<string>? Tags), Task> onTagsSelected,
        PlacementMode placement = PlacementMode.Bottom)
    {
        var provider = UiServices.GetProvider(placementTarget);
        if (placementTarget.FindAncestorOfType<ContextMenu>() is { } menu)
        {
            placementTarget = menu.PlacementTarget ?? menu.Parent as Control ?? placementTarget;
            menu.Close();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
        }
        var flyout = new Flyout
        {
            Placement = placement
        };
        var tagSelector = new TagSelector
        {
            WorkKind = workType,
            WorkId = workId
        };
        UiServices.SetProvider(tagSelector, provider);
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

        void OnSizeChanged(object? sender, SizeChangedEventArgs e) => content.MaxWidth = Math.Max(0, e.NewSize.Width - 48);

        async void OnTagsSelected(TagSelector sender, (bool IsPrivate, IReadOnlyList<string>? Tags) e)
        {
            if (isSubmitting)
                return;
            isSubmitting = true;
            var submittedFrom = TopLevel.GetTopLevel(tagSelector)?.FocusManager?.GetFocusedElement() as Control;
            // Disabling the focused confirm button otherwise drops the popup's
            // keyboard route, including Escape while the request is pending.
            if (tagSelector.FindAncestorOfType<FlyoutPresenter>() is { } presenter)
            {
                presenter.Focusable = true;
                presenter.Focus();
            }
            tagSelector.IsEnabled = false;
            try
            {
                await onTagsSelected(e);
                flyout.Hide();
            }
            finally
            {
                isSubmitting = false;
                tagSelector.IsEnabled = true;
                if (flyout.IsOpen && submittedFrom is { IsEffectivelyEnabled: true, IsEffectivelyVisible: true })
                    submittedFrom.Focus();
            }
        }
    }
}
