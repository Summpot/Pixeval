using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Pixeval.AppManagement;

public static class ProtocolActivationHub
{
    private static readonly ConcurrentQueue<Uri> PendingUris = [];

    private static readonly Dictionary<string, DateTimeOffset> RecentlyPublishedUris = [];

    private static readonly object PublicationGate = new();

    private static readonly TimeSpan DuplicateSuppressionWindow = TimeSpan.FromSeconds(1);

    public static event EventHandler<Uri>? UriActivated;

    public static void Publish(Uri uri)
    {
        if (!TryRegisterPublication(uri))
            return;

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

    private static bool TryRegisterPublication(Uri uri)
    {
        var now = DateTimeOffset.UtcNow;
        var key = uri.AbsoluteUri;

        lock (PublicationGate)
        {
            var expiredBefore = now - DuplicateSuppressionWindow;
            foreach (var staleKey in RecentlyPublishedUris
                         .Where(pair => pair.Value < expiredBefore)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                RecentlyPublishedUris.Remove(staleKey);
            }

            if (RecentlyPublishedUris.TryGetValue(key, out var previousPublication)
                && now - previousPublication <= DuplicateSuppressionWindow)
                return false;

            RecentlyPublishedUris[key] = now;
            return true;
        }
    }
}
