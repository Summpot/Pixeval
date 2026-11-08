using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.VisualTree;
using Pixeval.Views.ViewContainers;

namespace Pixeval.Tests.Ui;

/// <summary>Composes the production shell. Observing navigation never changes its result.</summary>
internal sealed class PageTestHost
{
    internal TabViewContainer Shell { get; } = App.CreateViewContainer();
    private readonly object? _shellContent;
    private object? _content;
    private Page? _origin;
    private bool _mounting;

    internal PageTestHost()
    {
        _shellContent = Shell.Content;
        Shell.PageOpened += (_, page) =>
        {
            if (_mounting)
                return;
            Navigations++;
            LastPageType = page.GetType();
            NavigationObserver?.Invoke(page);
        };
    }

    // Fixture setup and explicit detach/remount only. Neither operation models closing.
    internal object? Content
    {
        get => _content;
        set
        {
            if (value is null)
            {
                Shell.Content = null;
                return;
            }
            Shell.Content = _shellContent;
            if (ReferenceEquals(value, _content))
                return;
            var previous = _origin;
            _content = value;
            _origin = value as Page ?? new ContentPage { Content = value };
            _mounting = true;
            try { Shell.NavigateTo(_origin); }
            finally { _mounting = false; }
            if (previous is not null)
                Shell.ClosePage(previous);
        }
    }

    internal int Navigations { get; private set; }
    internal Type? LastPageType { get; private set; }
    internal Action<Page>? NavigationObserver { get; init; }
    internal IEnumerable<INotification> Notifications => TopLevel.GetTopLevel(Shell)?.GetVisualDescendants()
        .OfType<NotificationCard>().Select(card => card.Content).OfType<INotification>() ?? [];

    // Called explicitly by a scenario after asserting the navigation result.
    internal void CloseDestinationAndReturn()
    {
        if (_origin is null || !Shell.Pages.Contains(_origin))
            throw new InvalidOperationException("The origin was replaced or closed; it cannot be restored.");
        var destination = Shell.SelectedPage;
        if (destination is null || ReferenceEquals(destination, _origin))
            throw new InvalidOperationException("No selected destination to close.");
        Shell.ClosePage(destination);
        Shell.SelectPage(_origin);
        Shell.UpdateLayout();
    }
}
