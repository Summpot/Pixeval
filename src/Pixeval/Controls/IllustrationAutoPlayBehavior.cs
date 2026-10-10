// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Controls;

public static class IllustrationAutoPlayBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(IllustrationAutoPlayBehavior));

    private static readonly AttachedProperty<DispatcherTimer?> TimerProperty =
        AvaloniaProperty.RegisterAttached<Control, DispatcherTimer?>("Timer", typeof(IllustrationAutoPlayBehavior));

    private static readonly AttachedProperty<IIllustrationAutoPlayTarget?> TargetProperty =
        AvaloniaProperty.RegisterAttached<Control, IIllustrationAutoPlayTarget?>("Target", typeof(IllustrationAutoPlayBehavior));

    private static readonly AttachedProperty<PropertyChangedEventHandler?> HandlerProperty =
        AvaloniaProperty.RegisterAttached<Control, PropertyChangedEventHandler?>("Handler", typeof(IllustrationAutoPlayBehavior));

    static IllustrationAutoPlayBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

    internal static void Tick(object? dataContext)
    {
        if (dataContext is IIllustrationAutoPlayTarget { IsAutoPlaying: true } target)
            target.MoveAutoPlayNext();
    }

    internal static bool IsTimerRunning(Control control) =>
        control.GetValue(TimerProperty)?.IsEnabled is true;

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            control.DataContextChanged += OnDataContextChanged;
            control.Unloaded += OnUnloaded;
            Attach(control);
        }
        else
        {
            Detach(control);
        }
    }

    private static void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is Control control)
            Attach(control);
    }

    private static void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
            Stop(control);
    }

    private static void Attach(Control control)
    {
        Unsubscribe(control);
        if (control.DataContext is not IIllustrationAutoPlayTarget target)
        {
            control.SetValue(TargetProperty, null);
            Stop(control);
            return;
        }

        control.SetValue(TargetProperty, target);
        if (target is INotifyPropertyChanged source)
        {
            PropertyChangedEventHandler handler = (_, eventArgs) =>
            {
                if (eventArgs.PropertyName is nameof(IIllustrationAutoPlayTarget.IsAutoPlaying)
                    or nameof(IIllustrationAutoPlayTarget.AutoPlayInterval))
                    UpdateTimer(control);
            };
            source.PropertyChanged += handler;
            control.SetValue(HandlerProperty, handler);
        }

        UpdateTimer(control);
    }

    private static void UpdateTimer(Control control)
    {
        if (control.GetValue(TargetProperty) is not { IsAutoPlaying: true } target)
        {
            Stop(control);
            return;
        }

        var timer = control.GetValue(TimerProperty);
        if (timer is null)
        {
            timer = new DispatcherTimer();
            timer.Tick += (_, _) => Tick(control.DataContext);
            control.SetValue(TimerProperty, timer);
        }

        timer.Interval = TimeSpan.FromSeconds(int.Clamp(target.AutoPlayInterval, 1, 60));
        if (!timer.IsEnabled)
            timer.Start();
    }

    private static void Stop(Control control) => control.GetValue(TimerProperty)?.Stop();

    private static void Unsubscribe(Control control)
    {
        if (control.GetValue(TargetProperty) is INotifyPropertyChanged source
            && control.GetValue(HandlerProperty) is { } handler)
            source.PropertyChanged -= handler;

        control.SetValue(HandlerProperty, null);
    }

    private static void Detach(Control control)
    {
        control.DataContextChanged -= OnDataContextChanged;
        control.Unloaded -= OnUnloaded;
        Unsubscribe(control);
        Stop(control);
        control.SetValue(TargetProperty, null);
    }
}
