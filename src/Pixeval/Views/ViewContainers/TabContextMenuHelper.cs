// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pixeval.I18N;
using Pixeval.Utilities;
using TabView.Avalonia;

namespace Pixeval.Views.ViewContainers;

internal static class TabContextMenuHelper
{
    public static void AttachToTabsViewItem(TabsViewItem tabItem, ContextRequestedEventArgs e)
    {
        if ((tabItem.DataContext as Page ?? tabItem.Content as Page) is not { } contextPage
            || TopLevel.GetTopLevel(tabItem)?.ViewContainer is not TabViewContainer container
            || container.TabsControl.Pages is not IList<Page> pages
            || !pages.Contains(contextPage))
            return;

        var snapshot = pages.ToArray();
        var menu = new ContextMenu
        {
            ItemsSource = new[]
            {
                CreateTabCloseMenuItem(container, contextPage, snapshot, TabCloseScope.Others),
                CreateTabCloseMenuItem(container, contextPage, snapshot, TabCloseScope.Left),
                CreateTabCloseMenuItem(container, contextPage, snapshot, TabCloseScope.Right)
            }
        };
        tabItem.ContextMenu = menu;
        menu.Open(tabItem);
        e.Handled = true;
    }

    private static MenuItem CreateTabCloseMenuItem(
        TabViewContainer container,
        Page contextPage,
        IReadOnlyList<Page> pages,
        TabCloseScope scope)
    {
        var item = new MenuItem
        {
            Header = I18NManager.GetResource(scope switch
            {
                TabCloseScope.Others => MainPageResources.TabContextMenu.CloseOtherTabs,
                TabCloseScope.Left => MainPageResources.TabContextMenu.CloseTabsToLeft,
                TabCloseScope.Right => MainPageResources.TabContextMenu.CloseTabsToRight,
                _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
            }),
            IsEnabled = TabClosePlanner.GetTargets(pages, contextPage, scope).Count is not 0
        };
        item.Click += (_, _) => CloseTabs(container, contextPage, scope);
        return item;
    }

    public static void CloseTabs(TabViewContainer container, Page contextPage, TabCloseScope scope)
    {
        if (container.TabsControl.Pages is not IList<Page> pages || !pages.Contains(contextPage))
            return;

        container.TabsControl.SelectedIndex = pages.IndexOf(contextPage);
        var targets = TabClosePlanner.GetTargets([.. pages], contextPage, scope);
        foreach (var target in targets)
        {
            var args = new RoutedEventArgs(ViewModelDisposal.RequestDisposeEvent, target);
            target.RaiseEvent(args);
            if (!args.Handled)
                ViewModelDisposal.Dispose(target);
            _ = pages.Remove(target);
        }
    }
}
