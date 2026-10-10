// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading;

namespace Pixeval.Utilities;

public sealed class ActionDisposable(Action action) : IDisposable
{
    private Action? _action = action;

    public void Dispose()
    {
        var a = Interlocked.Exchange(ref _action, null);
        a?.Invoke();
    }
}
