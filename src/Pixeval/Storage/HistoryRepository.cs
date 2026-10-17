// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Pixeval.Pixiv;
using Pixeval.Mako;

namespace Pixeval.Storage;

public partial class HistoryRepository : IArtworkHistorySource
{
    public event EventHandler? Changed;

    internal void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public void Clear() => ClearBrowseHistory();

    public void AddBrowseHistory(object entry)
    {
        if (!ArtworkPayloads.TryWrap(entry, out var payload))
            return;
        AddBrowseHistory(payload);
    }

    public void AddSearchHistory(string text, string? translatedName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        UpsertSearchHistory(text, translatedName, DateTimeOffset.UtcNow.ToString("O"));
    }

    public async IAsyncEnumerable<object> StreamAsync(SimpleWorkKind workType, [EnumeratorCancellation] CancellationToken token = default)
    {
        long? cursorId = null;
        const int pageSize = 50;

        while (!token.IsCancellationRequested)
        {
            var batch = StreamBrowseHistoryCursor(cursorId, (uint)pageSize);
            if (batch.Count == 0)
                yield break;

            foreach (var record in batch)
            {
                token.ThrowIfCancellationRequested();
                if (record.Entry is { } entry && MatchesWorkType(workType, entry))
                {
                    yield return entry;
                }
            }

            cursorId = batch[^1].HistoryEntryId;
            if (batch.Count < pageSize)
                yield break;
        }
    }

    internal static bool MatchesWorkType(SimpleWorkKind workType, object entry)
    {
        var isNovel = ArtworkPayloads.Unwrap(entry) is Mako.Novel;
        return workType switch
        {
            SimpleWorkKind.Novel => isNovel,
            SimpleWorkKind.Illustration => !isNovel,
            _ => true
        };
    }
}
