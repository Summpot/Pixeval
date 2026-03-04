using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Pixeval.AppManagement;

public static class ProtocolActivationHub
{
    private static readonly ConcurrentQueue<Uri> PendingUris = [];

    public static event EventHandler<Uri>? UriActivated;

    public static void Publish(Uri uri)
    {
        PendingUris.Enqueue(uri);
        UriActivated?.Invoke(null, uri);
    }

    public static void Publish(string? input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
            return;

        Publish(uri);
    }

    public static IEnumerable<Uri> DrainPendingUris()
    {
        while (PendingUris.TryDequeue(out var uri))
            yield return uri;
    }
}
