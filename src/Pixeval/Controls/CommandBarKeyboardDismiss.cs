// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Pixeval.Controls;

/// <summary>Preserves Escape dismissal when focus returns from a disabled overflow action to the bar.</summary>
public static class CommandBarKeyboardDismiss
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<CommandBar, bool>("IsEnabled", typeof(CommandBarKeyboardDismiss));

    static CommandBarKeyboardDismiss()
    {
        IsEnabledProperty.Changed.AddClassHandler<CommandBar>(static (bar, args) =>
        {
            if (args.GetNewValue<bool>())
                bar.KeyDown += OnKeyDown;
            else
                bar.KeyDown -= OnKeyDown;
        });
    }

    public static bool GetIsEnabled(CommandBar bar) => bar.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(CommandBar bar, bool value) => bar.SetValue(IsEnabledProperty, value);

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // The upstream presenter handles keys inside its popup. This route also
        // covers its entry button when all overflow commands have become disabled.
        if (!e.Handled && e.Key == Key.Escape && sender is CommandBar { IsOpen: true } bar)
        {
            bar.SetCurrentValue(CommandBar.IsOpenProperty, false);
            e.Handled = true;
        }
    }
}
