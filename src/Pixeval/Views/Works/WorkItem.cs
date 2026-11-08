// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Linq;
using Avalonia.Input;
using Pixeval.Controls;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Pixeval.Views.Works;

public class WorkItem : EntryItem
{
    // Button.Click covers pointer, Enter and Space. Preserve Shift selection when
    // the list handles that activation, without interpreting child-button clicks.
    internal KeyModifiers ActivationModifiers { get; private set; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ActivationModifiers = e.KeyModifiers;
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ActivationModifiers = e.KeyModifiers;
        base.OnKeyDown(e);
    }

    protected override void OnClick()
    {
        try
        {
            base.OnClick();
        }
        finally
        {
            ActivationModifiers = KeyModifiers.None;
        }
    }

    protected void BookmarkButtonHost_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // A command disables its button while awaiting the service. Its surrounding
        // hit target must not turn that second click into a click on the artwork.
        if (sender is Border { Child: HeartButton { IsEffectivelyEnabled: false } })
            e.Handled = true;
    }

    protected void BookmarkButtonHost_OnTapped(object? sender, RoutedEventArgs e) => e.Handled = true;


    protected void BlockAuthor_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IWorkEntry { User: var user } work
            || BlockedContentHelper.IsBlockedPlaceholder(work))
            return;

        var blockedContent = this.GetRequiredUiService<IBlockedContentService>();
        var added = blockedContent.TryAddOrUpdateBlockedUser(user);
        if (this.GetVisualAncestors().OfType<WorkView>().FirstOrDefault()?.DataContext is IOperableViewViewModel model)
            model.ReplaceBlockedEntries(blockedContent);
        if (added)
            this.GetRequiredUiService<INotificationService>().ShowSuccess(Strings.BlockedContentAuthorBlockedFormatted(user.Name));
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (ContextMenu is { } contextMenu)
            contextMenu.PlacementTarget = this;
        foreach (var descendant in this.GetLogicalDescendants())
            if (descendant is Control { ContextMenu: { } menu } control)
                menu.PlacementTarget = control;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        Recycle();
        base.OnUnloaded(e);
    }

    internal virtual void Recycle()
    {
        ActivationModifiers = KeyModifiers.None;
        ContextMenu?.Close();
        foreach (var descendant in this.GetLogicalDescendants())
            if (descendant is Control { ContextMenu: { } menu })
                menu.Close();

        ClearValue(DataContextProperty);
    }
}
