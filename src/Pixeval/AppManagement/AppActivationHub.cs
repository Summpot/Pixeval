// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;

namespace Pixeval.AppManagement;

public static class AppActivationHub
{
    private static readonly object _Sync = new();

    private static int _pendingActivations;

    public static event Action? Activated;

    public static void Submit()
    {
        Action? handler;
        lock (_Sync)
        {
            handler = Activated;
            if (handler is null)
                ++_pendingActivations;
        }

        handler?.Invoke();
    }

    public static int DrainPendingActivations()
    {
        lock (_Sync)
        {
            var activations = _pendingActivations;
            _pendingActivations = 0;
            return activations;
        }
    }
}
