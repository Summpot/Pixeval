// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pixeval.Mako;
using Pixeval.Pixiv;
using Pixeval.Services.Media;
using Pixeval.ViewModels.Works;
using Pixeval.Views.Works;

namespace Pixeval.Controls;

public static class BookmarkTagSelectorBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "IsEnabled",
            typeof(BookmarkTagSelectorBehavior));

    static BookmarkTagSelectorBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Control element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            control.AddHandler(Control.ContextRequestedEvent, OnContextRequested, RoutingStrategies.Bubble);
        }
        else
        {
            control.RemoveHandler(Control.ContextRequestedEvent, OnContextRequested);
        }
    }

    private static void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control control)
            return;

        var dataContext = control.DataContext;
        if (dataContext is not IWorkEntry work)
            return;

        e.Handled = true;

        var workType = (work is Mako.Novel || (work is WorkEntry we && we.AsWorkEntry is Mako.Novel))
            ? SimpleWorkType.Novel
            : SimpleWorkType.Illustration;
        _ = BookmarkTagSelectorFlyoutHelper.ShowAsync(
            control,
            workType,
            work.Id,
            async result =>
            {
                await control.GetRequiredUiService<WorkCommands>().AddToBookmarkCommand.ExecuteAsync(new BookmarkRequest(work, result.IsPrivate, result.Tags));
            },
            PlacementMode.Bottom);
    }
}
