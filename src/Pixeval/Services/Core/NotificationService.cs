// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Avalonia.Controls.Notifications;

namespace Pixeval.Services.Core;

public sealed class NotificationService : INotificationService
{
    private readonly IUiHost _host;
    private readonly Queue<PendingNotification> _pending = [];

    public NotificationService(IUiHost host)
    {
        _host = host;
        host.Attached += Flush;
    }

    internal int PendingCount => _pending.Count;

    public void ShowInformation(string title, string? content = null) =>
        Show(NotificationType.Information, title, content);

    public void ShowSuccess(string title, string? content = null) =>
        Show(NotificationType.Success, title, content);

    public void ShowWarning(string title, string? content = null) =>
        Show(NotificationType.Warning, title, content);

    public void ShowError(string title, string? content = null) =>
        Show(NotificationType.Error, title, content);

    private void Show(NotificationType type, string title, string? content)
    {
        if (_host.Container is { } container)
        {
            container.ShowNotification(type, title, content);
            return;
        }

        _pending.Enqueue(new PendingNotification(type, title, content));
    }

    private void Flush()
    {
        if (_host.Container is not { } container)
            return;

        while (_pending.TryDequeue(out var notification))
            container.ShowNotification(notification.Type, notification.Title, notification.Content);
    }

    private readonly record struct PendingNotification(NotificationType Type, string Title, string? Content);
}

public sealed class NullNotificationService : INotificationService
{
    public static NullNotificationService Instance { get; } = new();

    public void ShowInformation(string title, string? content = null)
    {
    }

    public void ShowSuccess(string title, string? content = null)
    {
    }

    public void ShowWarning(string title, string? content = null)
    {
    }

    public void ShowError(string title, string? content = null)
    {
    }
}
