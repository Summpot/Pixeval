// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using MakoNovel = Pixeval.Native.Mako.Novel;

namespace Pixeval.Native.Storage;

public partial class HistoryRepository : IArtworkHistorySource
{
    public event EventHandler? Changed;

    internal void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public void Clear() => ClearBrowseHistory();

    public void AddBrowseHistory(object entry)
    {
        if (!BrowseHistoryRecord.TryCreateWorkKey(entry, out var workKey))
            return;
        var serializable = entry as IArtworkSerializable;
        var serializeKey = serializable?.SerializeKey;
        var payloadJson = serializable?.Serialize();
        var id = entry switch
        {
            Illustration i => i.Id.ToString(),
            MakoNovel n => n.Id.ToString(),
            Booru.BooruPost b => b.Id,
            SauceNao.SauceNaoItem s => s.RawId,
            _ => ""
        };
        AddOrReplaceBrowseHistory(id, serializeKey, workKey, payloadJson ?? "");
    }

    public void AddSearchHistory(string text, string? translatedName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        UpsertSearchHistory(text, translatedName, DateTimeOffset.UtcNow.ToString("O"));
    }

    public async IAsyncEnumerable<object> StreamAsync(SimpleWorkType workType, [EnumeratorCancellation] CancellationToken token = default)
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
                if (record.Entry is { } entry && MatchesWorkType(workType, record.SerializeKey, entry))
                {
                    yield return entry;
                }
            }

            cursorId = batch[^1].HistoryEntryId;
            if (batch.Count < pageSize)
                yield break;
        }
    }

    internal static bool MatchesWorkType(SimpleWorkType workType, string? serializeKey, object entry)
    {
        var isNovel = entry is MakoNovel
            || string.Equals(serializeKey, MakoNovel.LegacyNovelToken, StringComparison.OrdinalIgnoreCase)
            || string.Equals(serializeKey, typeof(MakoNovel).FullName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(serializeKey, "Pixeval.Models.Pixiv.PixivNovel", StringComparison.OrdinalIgnoreCase)
            || serializeKey?.StartsWith("Novel", StringComparison.OrdinalIgnoreCase) == true;

        return workType switch
        {
            SimpleWorkType.Novel => isNovel,
            SimpleWorkType.Illustration => !isNovel,
            _ => true
        };
    }
}
