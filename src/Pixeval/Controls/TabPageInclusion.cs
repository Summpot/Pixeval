// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Pixeval.Controls;

/// <summary>
/// Shows or hides a page and its tab header without reconstructing the page.
/// </summary>
public static class TabPageInclusion
{
    public static readonly AttachedProperty<bool> IsIncludedProperty =
        AvaloniaProperty.RegisterAttached<Page, bool>("IsIncluded", typeof(TabPageInclusion), true);

    private static readonly AttachedProperty<bool> HookedProperty =
        AvaloniaProperty.RegisterAttached<Page, bool>("Hooked", typeof(TabPageInclusion));

    static TabPageInclusion()
    {
        IsIncludedProperty.Changed.AddClassHandler<Page>(OnIsIncludedChanged);
    }

    public static bool GetIsIncluded(Page page) => page.GetValue(IsIncludedProperty);

    public static void SetIsIncluded(Page page, bool value) => page.SetValue(IsIncludedProperty, value);

    private static void OnIsIncludedChanged(Page page, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is bool included)
            Apply(page, included);
    }

    private static void Apply(Page page, bool included)
    {
        page.IsVisible = included;
        TabbedPage.SetIsTabEnabled(page, included);
        if (page.FindAncestorOfType<TabItem>() is { } tabItem)
        {
            tabItem.IsVisible = included;
            return;
        }

        if (page.GetValue(HookedProperty))
            return;

        page.SetValue(HookedProperty, true);
        page.AttachedToVisualTree += Page_OnAttachedToVisualTree;
    }

    private static void Page_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Page page)
            return;

        page.AttachedToVisualTree -= Page_OnAttachedToVisualTree;
        page.SetValue(HookedProperty, false);
        Apply(page, GetIsIncluded(page));
    }
}
