// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Misaki;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Storage;
using Pixeval.Utilities;

namespace Pixeval.Models.Database.Managers;

public abstract class ArtworkHistoryPersistentManager<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TEntry> : SqlitePersistentManager, IArtworkHistorySource
    where TEntry : ArtworkHistoryEntry, new()
{
    private readonly FileLogger _logger;

    protected ArtworkHistoryPersistentManager(StorageEngine storage, FileLogger logger) : base(storage)
    {
        _logger = logger;
    }

    public abstract int Count { get; }

    public event EventHandler? Changed;

    public IAsyncEnumerable<IArtworkInfo> StreamAsync(SimpleWorkType workType, CancellationToken token = default)
    {
        var novelSerializeKey = Pixeval.Native.Mako.Novel.LegacyNovelToken;
        var modernNovelKey = typeof(Pixeval.Native.Mako.Novel).FullName!;
        const string transitionalNovelKey = "Pixeval.Models.Pixiv.PixivNovel";
        return workType switch
        {
            SimpleWorkType.Novel => StreamArtworksAsync<Pixeval.Native.Mako.Novel>(entry => entry.SerializeKey == novelSerializeKey || entry.SerializeKey == modernNovelKey || entry.SerializeKey == transitionalNovelKey, token),
            SimpleWorkType.Illustration => StreamArtworksAsync<IArtworkInfo>(entry => entry.SerializeKey != novelSerializeKey && entry.SerializeKey != modernNovelKey && entry.SerializeKey != transitionalNovelKey, token),
            _ => throw new ArgumentOutOfRangeException(nameof(workType), workType, null)
        };
    }

    public abstract IAsyncEnumerable<TEntry> StreamEntriesAsync(
        int skip = 0,
        CancellationToken token = default);

    public abstract void Clear();

    protected void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    protected virtual void OnEntryInserted(TEntry entry) { }
    protected virtual void OnEntriesDeleted(IReadOnlyCollection<TEntry> entries) { }
    protected virtual void OnEntriesCleared() { }

    private async IAsyncEnumerable<TArtwork> StreamArtworksAsync<TArtwork>(
        Func<TEntry, bool> predicate,
        [EnumeratorCancellation] CancellationToken token = default)
        where TArtwork : class, IArtworkInfo
    {
        await foreach (var entry in StreamEntriesAsync(0, token))
        {
            token.ThrowIfCancellationRequested();
            if (predicate(entry) && entry.Entry is TArtwork artwork)
            {
                yield return artwork;
            }
        }
    }
}
