// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Pixeval.Views.Shell;

namespace Pixeval.Services.Core;

public sealed class UiHost : IUiHost
{
    private readonly List<Registration> _entries = [];

    public ViewContainerBase? Container { get; private set; }

    public TopLevel? TopLevel { get; private set; }

    public bool IsAttached => Container is not null && TopLevel is not null;

    public event Action? Attached;

    public void Attach(ViewContainerBase container, TopLevel topLevel)
    {
        Detach(container);
        EventHandler? activated = null;
        if (topLevel is Window window)
        {
            activated = (_, _) => Activate(container, topLevel);
            window.Activated += activated;
        }

        _entries.Add(new Registration(container, topLevel, activated));
        Activate(container, topLevel);
        Attached?.Invoke();
    }

    public void Detach(ViewContainerBase container)
    {
        for (var index = _entries.Count - 1; index >= 0; --index)
        {
            var entry = _entries[index];
            if (!ReferenceEquals(entry.Container, container))
                continue;

            if (entry.TopLevel is Window window && entry.Activated is not null)
                window.Activated -= entry.Activated;
            _entries.RemoveAt(index);
        }

        if (!ReferenceEquals(Container, container))
            return;

        if (_entries.Count is 0)
        {
            Container = null;
            TopLevel = null;
            return;
        }

        var fallback = _entries[^1];
        Activate(fallback.Container, fallback.TopLevel);
    }

    private void Activate(ViewContainerBase container, TopLevel topLevel)
    {
        Container = container;
        TopLevel = topLevel;
    }

    private sealed record Registration(ViewContainerBase Container, TopLevel TopLevel, EventHandler? Activated);
}
