// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using Misaki;
using Pixeval.Services;
using Pixeval.ViewModels;
using Pixeval.Views.Markup;

namespace Pixeval.Controls;

public static class ArtworkStateBehavior
{
    public static readonly AttachedProperty<bool> TrackBookmarkProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "TrackBookmark",
            typeof(ArtworkStateBehavior));

    public static readonly AttachedProperty<bool> TrackWatchLaterProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "TrackWatchLater",
            typeof(ArtworkStateBehavior));

    private static readonly AttachedProperty<IDisposable?> BookmarkTrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, IDisposable?>(
            "BookmarkTracker",
            typeof(ArtworkStateBehavior));

    private static readonly AttachedProperty<IDisposable?> WatchLaterTrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, IDisposable?>(
            "WatchLaterTracker",
            typeof(ArtworkStateBehavior));

    static ArtworkStateBehavior()
    {
        TrackBookmarkProperty.Changed.AddClassHandler<Control>(OnTrackBookmarkChanged);
        TrackWatchLaterProperty.Changed.AddClassHandler<Control>(OnTrackWatchLaterChanged);
    }

    public static bool GetTrackBookmark(Control element) => element.GetValue(TrackBookmarkProperty);
    public static void SetTrackBookmark(Control element, bool value) => element.SetValue(TrackBookmarkProperty, value);

    public static bool GetTrackWatchLater(Control element) => element.GetValue(TrackWatchLaterProperty);
    public static void SetTrackWatchLater(Control element, bool value) => element.SetValue(TrackWatchLaterProperty, value);

    private static void OnTrackBookmarkChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            control.DataContextChanged += Control_OnBookmarkDataContextChanged;
            control.Unloaded += Control_OnBookmarkUnloaded;
            control.Loaded += Control_OnBookmarkLoaded;
            BindBookmark(control);
        }
        else
        {
            control.DataContextChanged -= Control_OnBookmarkDataContextChanged;
            control.Unloaded -= Control_OnBookmarkUnloaded;
            control.Loaded -= Control_OnBookmarkLoaded;
            UnbindBookmark(control);
        }
    }

    private static void Control_OnBookmarkDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is Control control)
            BindBookmark(control);
    }

    private static void Control_OnBookmarkUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
            UnbindBookmark(control);
    }

    private static void Control_OnBookmarkLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
            BindBookmark(control);
    }

    private static void BindBookmark(Control control)
    {
        UnbindBookmark(control);

        if (control.DataContext is not IArtworkInfo artwork)
            return;

        var state = ArtworkUiStateStore.GetOrCreate(artwork);

        if (control is HeartButton heartButton)
        {
            heartButton.Command = WorkCommands.BookmarkCommand;
            heartButton.CommandParameter = artwork;
            heartButton.State = state.BookmarkState;

            void OnStateChanged(object? _, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(ArtworkUiState.BookmarkState))
                    heartButton.State = state.BookmarkState;
            }

            state.PropertyChanged += OnStateChanged;
            control.SetValue(BookmarkTrackerProperty, new ActionDisposable(() => state.PropertyChanged -= OnStateChanged));
        }
        else if (control is MenuItem menuItem)
        {
            menuItem.Command = WorkCommands.BookmarkCommand;
            menuItem.CommandParameter = artwork;
            UpdateBookmarkIcon(menuItem, state.BookmarkState);

            void OnStateChanged(object? _, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(ArtworkUiState.BookmarkState))
                    UpdateBookmarkIcon(menuItem, state.BookmarkState);
            }

            state.PropertyChanged += OnStateChanged;
            control.SetValue(BookmarkTrackerProperty, new ActionDisposable(() => state.PropertyChanged -= OnStateChanged));
        }
    }

    private static void UnbindBookmark(Control control)
    {
        if (control.GetValue(BookmarkTrackerProperty) is { } disposable)
        {
            disposable.Dispose();
            control.SetValue(BookmarkTrackerProperty, null);
        }
    }

    private static void UpdateBookmarkIcon(MenuItem menuItem, HeartButtonState state)
    {
        if (menuItem.Icon is SymbolIcon icon)
        {
            icon.IconVariant = (state & HeartButtonState.Checked) is not 0
                ? IconVariant.Filled
                : IconVariant.Regular;
        }
    }

    private static void OnTrackWatchLaterChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            control.DataContextChanged += Control_OnWatchLaterDataContextChanged;
            control.Unloaded += Control_OnWatchLaterUnloaded;
            control.Loaded += Control_OnWatchLaterLoaded;
            BindWatchLater(control);
        }
        else
        {
            control.DataContextChanged -= Control_OnWatchLaterDataContextChanged;
            control.Unloaded -= Control_OnWatchLaterUnloaded;
            control.Loaded -= Control_OnWatchLaterLoaded;
            UnbindWatchLater(control);
        }
    }

    private static void Control_OnWatchLaterDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is Control control)
            BindWatchLater(control);
    }

    private static void Control_OnWatchLaterUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
            UnbindWatchLater(control);
    }

    private static void Control_OnWatchLaterLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
            BindWatchLater(control);
    }

    private static void BindWatchLater(Control control)
    {
        UnbindWatchLater(control);

        if (control.DataContext is not IArtworkInfo artwork)
            return;

        var state = ArtworkUiStateStore.GetOrCreate(artwork);

        if (control is MenuItem menuItem)
        {
            menuItem.Command = WorkCommands.AddToWatchLaterCommand;
            menuItem.CommandParameter = artwork;
            UpdateWatchLaterIcon(menuItem, state.IsInWatchLater);

            void OnStateChanged(object? _, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(ArtworkUiState.IsInWatchLater))
                    UpdateWatchLaterIcon(menuItem, state.IsInWatchLater);
            }

            state.PropertyChanged += OnStateChanged;
            control.SetValue(WatchLaterTrackerProperty, new ActionDisposable(() => state.PropertyChanged -= OnStateChanged));
        }
    }

    private static void UnbindWatchLater(Control control)
    {
        if (control.GetValue(WatchLaterTrackerProperty) is { } disposable)
        {
            disposable.Dispose();
            control.SetValue(WatchLaterTrackerProperty, null);
        }
    }

    private static void UpdateWatchLaterIcon(MenuItem menuItem, bool isInWatchLater)
    {
        if (menuItem.Icon is SymbolIcon icon)
        {
            icon.IconVariant = isInWatchLater
                ? IconVariant.Filled
                : IconVariant.Regular;
        }
    }

    private sealed class ActionDisposable(Action action) : IDisposable
    {
        private Action? _action = action;

        public void Dispose()
        {
            var a = System.Threading.Interlocked.Exchange(ref _action, null);
            a?.Invoke();
        }
    }
}
