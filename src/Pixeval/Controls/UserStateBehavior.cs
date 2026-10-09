// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.ViewModels;
using Pixeval.Views.Markup;

namespace Pixeval.Controls;

public static class UserStateBehavior
{
    public static readonly AttachedProperty<bool> TrackFollowProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "TrackFollow",
            typeof(UserStateBehavior));

    private static readonly AttachedProperty<IDisposable?> FollowTrackerProperty =
        AvaloniaProperty.RegisterAttached<Control, IDisposable?>(
            "FollowTracker",
            typeof(UserStateBehavior));

    static UserStateBehavior()
    {
        TrackFollowProperty.Changed.AddClassHandler<Control>(OnTrackFollowChanged);
    }

    public static bool GetTrackFollow(Control element) => element.GetValue(TrackFollowProperty);
    public static void SetTrackFollow(Control element, bool value) => element.SetValue(TrackFollowProperty, value);

    private static void OnTrackFollowChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            control.DataContextChanged += Control_OnFollowDataContextChanged;
            control.Unloaded += Control_OnFollowUnloaded;
            control.Loaded += Control_OnFollowLoaded;
            BindFollow(control);
        }
        else
        {
            control.DataContextChanged -= Control_OnFollowDataContextChanged;
            control.Unloaded -= Control_OnFollowUnloaded;
            control.Loaded -= Control_OnFollowLoaded;
            UnbindFollow(control);
        }
    }

    private static void Control_OnFollowDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is Control control)
            BindFollow(control);
    }

    private static void Control_OnFollowUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
            UnbindFollow(control);
    }

    private static void Control_OnFollowLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
            BindFollow(control);
    }

    private static void BindFollow(Control control)
    {
        UnbindFollow(control);

        if (control.DataContext is not User user)
            return;

        var state = UserUiStateStore.GetOrCreate(user);

        if (control is MenuItem menuItem)
        {
            menuItem.Command = WorkCommands.FollowUserCommand;
            menuItem.CommandParameter = user;
            UpdateFollowIcon(menuItem, state.FollowState);

            void OnStateChanged(object? _, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(UserUiState.FollowState))
                    UpdateFollowIcon(menuItem, state.FollowState);
            }

            state.PropertyChanged += OnStateChanged;
            control.SetValue(FollowTrackerProperty, new ActionDisposable(() => state.PropertyChanged -= OnStateChanged));
        }
        else if (control is PixevalBadge badge)
        {
            UpdateFollowBadge(badge, state.FollowState);

            void OnStateChanged(object? _, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(UserUiState.FollowState))
                    UpdateFollowBadge(badge, state.FollowState);
            }

            state.PropertyChanged += OnStateChanged;
            control.SetValue(FollowTrackerProperty, new ActionDisposable(() => state.PropertyChanged -= OnStateChanged));
        }
    }

    private static void UnbindFollow(Control control)
    {
        if (control.GetValue(FollowTrackerProperty) is { } disposable)
        {
            disposable.Dispose();
            control.SetValue(FollowTrackerProperty, null);
        }
    }

    private static void UpdateFollowBadge(PixevalBadge badge, HeartButtonState state)
    {
        badge.IsVisible = (state & HeartButtonState.Checked) is not 0;
    }

    private static void UpdateFollowIcon(MenuItem menuItem, HeartButtonState state)
    {
        if (menuItem.Icon is SymbolIcon icon)
        {
            icon.IconVariant = (state & HeartButtonState.Checked) is not 0
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
