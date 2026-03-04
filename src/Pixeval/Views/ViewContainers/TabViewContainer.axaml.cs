using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Navigation;
using Pixeval.AppManagement;
using Pixeval.Utilities;
using Tabalonia.Controls;
using Tabalonia.InterTab;

namespace Pixeval.Views.ViewContainers;

public partial class TabViewContainer : ViewContainerBase
{
    public TabViewContainer()
    {
        InitializeComponent();

        TabsControl.InterTabController = new InterTabController
        {
            InterTabClient = new PixevalInterTabClient()
        };
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Manager = new WindowNotificationManager(TopLevel.GetTopLevel(this))
        {
            MaxItems = 3,
            Position = NotificationPosition.BottomRight
        };
    }

    /// <inheritdoc />
    public override void NavigateTo<TParameter>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type pageType,
        object? icon,
        string header,
        TParameter parameter,
        bool removeCurrent = false)
    {
        var page = (Control) Activator.CreateInstance(pageType)!;

        var navEa = new NavigationEventArgs(
            page,
            NavigationMode.New,
            null,
            parameter,
            pageType)
        {
            RoutedEvent = Frame.NavigatedToEvent
        };

        page.RaiseEvent(navEa);

        var dragTabItem = new DragTabItem
        {
            Icon = icon,
            Header = header,
            Content = page
        };

        TabsControl.Items.Add(dragTabItem);

        var selected = TabsControl.SelectedItem;
        TabsControl.SelectedItem = dragTabItem;

        if (removeCurrent)
            if (selected is not null)
                TabsControl.Items.Remove(selected);
    }

    /// <summary>
    /// Custom <see cref="IInterTabClient"/> that creates new <see cref="Window"/>
    /// instances with a fresh <see cref="TabViewContainer"/> for tab tear-off.
    /// </summary>
    private sealed class PixevalInterTabClient : IInterTabClient
    {
        public TabHost GetNewHost(InterTabController controller, TabHost host)
        {
            var container = new TabViewContainer();
            var window = new Window
            {
                Content = container
            }.Fork(host.Window);

            return new TabHost(window, container.TabsControl);
        }
    }

    private void NavigationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: Type type })
            return;

        // 默认参数是自己的Uid，无需指定
#pragma warning disable IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
        this.NavigateTo(type);
#pragma warning restore IL2072
    }

}
