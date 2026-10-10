// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Models.Navigation;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using TabView.Avalonia;

namespace Pixeval.Views.ViewContainers;

public partial class TabViewContainer : ViewContainerBase
{
    private readonly IAppUpdateNotificationCoordinator _updateCoordinator;

    public static ICommand OpenNavigationItemCommand { get; } = new RelayCommand<Control>(static control =>
    {
        if (control is not null && TopLevel.GetTopLevel(control)?.ViewContainer is TabViewContainer { DataContext: TabViewContainerViewModel vm })
            vm.OpenNavigationItem(control);
        else if (control?.DataContext is NavigationPageItem { PageType: { } type })
        {
            var nav = App.Services?.GetService<INavigationService>() ?? new NavigationService();
            if (!nav.TrySelectExisting(type, control))
                nav.NavigateTo(type, null, false, control);
        }
    });

    static TabViewContainer()
    {
        RegisterNavigationItemInputHandlers<Button>();
        RegisterNavigationItemInputHandlers<MenuItem>();
        ContextRequestedEvent.AddClassHandler<TabsViewItem>(
            TabContextMenuHelper.AttachToTabsViewItem,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    public TabViewContainer() : this(
        App.Services?.GetService<IAppUpdateNotificationCoordinator>() ?? new AppUpdateNotificationCoordinator(App.Services?.GetService<FileLogger>()))
    {
    }

    public TabViewContainer(IAppUpdateNotificationCoordinator updateCoordinator)
    {
        _updateCoordinator = updateCoordinator;
        InitializeComponent();

        AddHandler(ViewModelDisposal.ViewModelDisposalEvent, OnViewModelDisposal, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(ViewModelDisposal.RequestDisposeEvent, OnRequestDispose, RoutingStrategies.Bubble, handledEventsToo: true);

        Task.Delay(5000).ContinueWith(_ => LoggingInDescriptionTextBlock.IsVisible = true, TaskScheduler.FromCurrentSynchronizationContext());
    }

    public void SetInterTabController(bool set)
    {
        TabsControl.InterTabController = set ? new InterTabController { InterTabClient = new PixevalInterTabClient() } : null;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Manager = new WindowNotificationManager(TopLevel.GetTopLevel(this))
        {
            MaxItems = 3,
            Position = NotificationPosition.BottomRight
        };
        FlushPendingNotifications();
        RegisterContentDialogHost(TopLevel.GetTopLevel(this));

        _ = _updateCoordinator.CheckAndNotifyUpdatesAsync(this);
    }

    public override void NavigateTo(Page page, bool removeCurrentPage = false)
    {
        if (TabsControl.Pages is not IList<Page> pages)
            throw new InvalidOperationException($"{nameof(TabsControl)} must use a mutable {nameof(TabsControl.Pages)} collection.");

        pages.Add(page);

        var selected = TabsControl.SelectedPage;
        TabsControl.SelectedIndex = pages.Count - 1;

        if (removeCurrentPage && selected is not null)
        {
            ViewModelDisposal.Dispose(selected);
            _ = pages.Remove(selected);
        }
    }

    public void ReloadNavigation() => (DataContext as TabViewContainerViewModel)?.RebuildNavigation();

    public bool TrySelectPage(Type pageType)
    {
        if (TabsControl.Pages is not IList<Page> pages)
            return false;

        if (TabsControl.SelectedPage?.GetType() == pageType)
            return true;

        for (var index = pages.Count - 1; index >= 0; --index)
        {
            if (pages[index].GetType() != pageType)
                continue;

            TabsControl.SelectedIndex = index;
            return true;
        }

        return false;
    }

    private static void RegisterNavigationItemInputHandlers<T>() where T : Control
    {
        PointerReleasedEvent.AddClassHandler<T>(NavigationItem_OnPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        ContextRequestedEvent.AddClassHandler<T>(NavigationItem_OnContextRequested, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private static void NavigationItem_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton is MouseButton.Middle
            && sender is Control control
            && IsNavigationItemControl(control)
            && TryOpenNewTab(control))
            e.Handled = true;
    }

    private static void NavigationItem_OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is Control control
            && IsNavigationItemControl(control)
            && TryOpenNewTab(control))
            e.Handled = true;
    }

    private static bool TryOpenNewTab(Control control)
    {
        if (TopLevel.GetTopLevel(control)?.ViewContainer is TabViewContainer { DataContext: TabViewContainerViewModel vm })
            return vm.TryOpenNavigationItem(control, openNew: true);

        if (control.DataContext is NavigationPageItem { PageType: { } type })
        {
            var nav = App.Services?.GetService<INavigationService>() ?? new NavigationService();
            nav.NavigateTo(type, null, false, control);
            return true;
        }

        return false;
    }

    private static bool IsNavigationItemControl(Control control) => control switch
    {
        Button { Command: { } command } => ReferenceEquals(command, OpenNavigationItemCommand),
        MenuItem { Command: { } command } => ReferenceEquals(command, OpenNavigationItemCommand),
        _ => false
    };

    private void TabsView_OnAddTabButtonClick(TabsView sender, EventArgs e) =>
        (DataContext as TabViewContainerViewModel)?.CreateNewTabCommand.Execute(null);

    private void TabsView_OnTabClosing(TabsView sender, TabClosingEventArgs e)
    {
        if (e.Item is not Control control || sender.Pages is not IReadOnlyCollection<Page> pages)
            return;

        if (sender.SelectedIndex < pages.Count - 1)
            sender.SelectedIndex++;
        else if (sender.SelectedIndex > 0)
            sender.SelectedIndex--;

        var args = new RoutedEventArgs(ViewModelDisposal.RequestDisposeEvent, control);
        control.RaiseEvent(args);
        if (!args.Handled)
            ViewModelDisposal.Dispose(control);
    }

    private void OnViewModelDisposal(object? sender, ViewModelDisposalEventArgs e)
    {
        if (e.Source is not Control source || TabsControl.Pages is not IReadOnlyCollection<Page> pages)
            return;

        var page = source is Page sourcePage && pages.Contains(sourcePage)
            ? sourcePage
            : source.GetLogicalAncestors().OfType<Page>().FirstOrDefault(pages.Contains);
        if (page is null)
            return;

        ViewModelDisposal.Register(page, e.Disposable);
        e.Handled = true;
    }

    private static void OnRequestDispose(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Control control)
            return;
        ViewModelDisposal.Dispose(control);
        e.Handled = true;
    }

    private sealed class PixevalInterTabClient : IInterTabClient
    {
        public TabsHost GetNewHost(InterTabController controller, TabsHost host)
        {
            var container = new TabViewContainer();
            var window = new Window { Content = container }.Fork(host.Window);
            container.SetInterTabController(true);
            window.Closed += static (sender, _) =>
            {
                if (sender is TopLevel { ViewContainer: TabViewContainer c })
                {
                    foreach (var page in c.TabsControl.Pages ?? [])
                        ViewModelDisposal.Dispose(page);
                    if (c.DataContext is IDisposable disposable)
                        disposable.Dispose();
                    c.DataContext = null;
                }
            };

            return new TabsHost(window, container.TabsControl);
        }
    }
}
