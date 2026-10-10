// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Windows.Input;
using Avalonia;
using Avalonia.Interactivity;
using CommunityToolkit.Avalonia.Controls;

namespace Pixeval.Controls;

/// <summary>
/// Binds an <see cref="ICommand"/> to the <see cref="SettingsCard.ClickEvent"/> of a <see cref="SettingsCard"/>,
/// which (unlike <c>Button</c>) exposes no <c>Command</c> property of its own.
/// </summary>
public static class ClickCommandBehavior
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<SettingsCard, ICommand?>("Command", typeof(ClickCommandBehavior));

    public static readonly AttachedProperty<object?> CommandParameterProperty =
        AvaloniaProperty.RegisterAttached<SettingsCard, object?>("CommandParameter", typeof(ClickCommandBehavior));

    static ClickCommandBehavior()
    {
        CommandProperty.Changed.AddClassHandler<SettingsCard>(static (card, e) =>
        {
            card.RemoveHandler(SettingsCard.ClickEvent, OnClick);
            if (e.NewValue is not null)
                card.AddHandler(SettingsCard.ClickEvent, OnClick);
        });
    }

    public static ICommand? GetCommand(SettingsCard element) => element.GetValue(CommandProperty);

    public static void SetCommand(SettingsCard element, ICommand? value) => element.SetValue(CommandProperty, value);

    public static object? GetCommandParameter(SettingsCard element) => element.GetValue(CommandParameterProperty);

    public static void SetCommandParameter(SettingsCard element, object? value) => element.SetValue(CommandParameterProperty, value);

    private static void OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not SettingsCard card)
            return;
        var parameter = GetCommandParameter(card);
        if (GetCommand(card) is { } command && command.CanExecute(parameter))
            command.Execute(parameter);
    }
}
