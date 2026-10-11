// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Pixeval.Views.Shell;

namespace Pixeval.Services.Core;

public interface IUiHost
{
    ViewContainerBase? Container { get; }

    TopLevel? TopLevel { get; }

    bool IsAttached { get; }

    event Action? Attached;

    void Attach(ViewContainerBase container, TopLevel topLevel);

    void Detach(ViewContainerBase container);
}
