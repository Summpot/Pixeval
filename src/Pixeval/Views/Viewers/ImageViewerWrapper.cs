// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Pixeval.Config;
using Pixeval.Controls;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Views.Viewers;

public class ImageViewerWrapper : ContentControl
{
    public static readonly DirectProperty<ImageViewerWrapper, int> CurrentWorkIndexProperty =
        AvaloniaProperty.RegisterDirect<ImageViewerWrapper, int>(
            nameof(CurrentWorkIndex),
            o => o.CurrentWorkIndex,
            (o, v) => o.CurrentWorkIndex = v);

    public int CurrentWorkIndex
    {
        get;
        set
        {
            if (field == value)
                return;

            var old = field;
            SetAndRaise(CurrentWorkIndexProperty, ref field, value);
            SwipeImageViewer?.SetWorkTransitionDirection(old, value);
        }
    } = -1;

    public ImageViewerBase ImageViewer => (ImageViewerBase) Content!;

    public bool IsSwipeMode { get; private set; } = true;

    public SwipeImageViewer? SwipeImageViewer => Content as SwipeImageViewer;

    public ImageViewerWrapper()
    {
        DataContext = null;
        var imageViewer = new SwipeImageViewer
        {
            BrowseDirection = Orientation.Horizontal
        };
        Content = imageViewer;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplySettings(this.GetUiService<AppSettings>());
    }

    private void ApplySettings(AppSettings? settings)
    {
        if (settings is null)
            return;

        var swipe = settings.BrowsingExperienceSettings.BrowseMode is BrowseMode.Swipe;
        var direction = settings.BrowsingExperienceSettings.BrowseDirection;
        if (swipe != IsSwipeMode)
        {
            IsSwipeMode = swipe;
            ImageViewerBase imageViewer = swipe ? new SwipeImageViewer() : new ContinuousImageViewer();
            imageViewer.BrowseDirection = direction;
            Content = imageViewer;
            return;
        }

        if (Content is ImageViewerBase viewer)
            viewer.BrowseDirection = direction;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (KeyboardShortcut.TryExecuteCopy(e, ImageViewer.CopyCommand)
            || KeyboardShortcut.TryExecutePlatformCommand(e, Key.S, ImageViewer.SaveCommand))
            return;

        if (ImageViewer.DataContext is not ImageViewerViewModel { CurrentPage: { } currentPage })
            return;

        if (KeyboardShortcut.TryExecutePlatformCommand(
                e,
                Key.S,
                currentPage.SaveAsCommand,
                this,
                KeyModifiers.Shift))
            return;

        if (KeyboardShortcut.Matches(e, Key.Space)
            && currentPage.PlayPauseCommand.CanExecute(null))
        {
            currentPage.IsPlaying = !currentPage.IsPlaying;
            e.Handled = true;
        }
    }

    public event EventHandler<Control, ImageViewerSelectionChangedEventArgs>? SelectionChanged
    {
        add => ImageViewer.SelectionChanged += value;
        remove => ImageViewer.SelectionChanged -= value;
    }
}
