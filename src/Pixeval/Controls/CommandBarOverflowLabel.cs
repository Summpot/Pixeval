// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace Pixeval.Controls;

/// <summary>Localizes the template-owned overflow button without replacing the command bar template.</summary>
public static class CommandBarOverflowLabel
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<CommandBarButton, string?>("Text", typeof(CommandBarOverflowLabel));

    static CommandBarOverflowLabel()
    {
        TextProperty.Changed.AddClassHandler<CommandBarButton>(static (button, args) =>
        {
            // The upstream template assigns local English values, which ordinary style setters
            // cannot override. Only its named overflow button opts into this localized label.
            var text = args.GetNewValue<string?>();
            button.SetCurrentValue(CommandBarButton.LabelProperty, text);
            button.SetCurrentValue(ToolTip.TipProperty, text);
            button.SetCurrentValue(AutomationProperties.NameProperty, text);
        });
    }

    public static string? GetText(CommandBarButton button) => button.GetValue(TextProperty);

    public static void SetText(CommandBarButton button, string? value) => button.SetValue(TextProperty, value);
}
