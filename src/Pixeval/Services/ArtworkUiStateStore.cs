// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Concurrent;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Controls;
using Pixeval.Native.Booru;
using Pixeval.Native.Mako;
using Pixeval.Native.SauceNao;
using Pixeval.Native.Storage;

namespace Pixeval.Services;

public sealed partial class ArtworkUiState : ObservableObject
{
    [ObservableProperty]
    private HeartButtonState _bookmarkState;

    [ObservableProperty]
    private bool _isInWatchLater;

    public ArtworkUiState(HeartButtonState bookmarkState, bool isInWatchLater)
    {
        _bookmarkState = bookmarkState;
        _isInWatchLater = isInWatchLater;
    }
}

public static class ArtworkUiStateStore
{
    private static readonly ConcurrentDictionary<string, ArtworkUiState> s_states = new(StringComparer.Ordinal);
    private static StorageEngine? s_storageEngine;
    private static bool s_observerHooked;

    public static void Initialize(StorageEngine storageEngine)
    {
        s_storageEngine = storageEngine;
        EnsureStorageObserver();
    }

    public static string GetKey(object entry)
    {
        var targetEntry = entry is WorkEntry we ? we.AsWorkEntry : entry;
        if (WatchLaterRecord.TryCreateWorkKey(targetEntry, out var key))
            return key;

        var (platform, id) = targetEntry switch
        {
            Illustration i => (i.Platform, i.Id.ToString()),
            Novel n => (n.Platform, n.Id.ToString()),
            BooruPost b => (b.PlatformName, b.Id),
            SauceNaoItem s => (s.Platform, s.RawId),
            _ => ("unknown", targetEntry?.ToString() ?? "")
        };
        return $"{platform}:{id}";
    }

    public static ArtworkUiState GetOrCreate(object entry)
    {
        EnsureStorageObserver();
        var key = GetKey(entry);
        return s_states.GetOrAdd(key, _ =>
        {
            var targetEntry = entry is WorkEntry we ? we.AsWorkEntry : entry;
            var bookmarkState = targetEntry switch
            {
                Illustration ill => ill.IsBookmarked ? HeartButtonState.Checked : HeartButtonState.Unchecked,
                Novel nov => nov.IsBookmarked ? HeartButtonState.Checked : HeartButtonState.Unchecked,
                _ => HeartButtonState.Unchecked
            };

            var inWatchLater = s_storageEngine?.WatchLaterRepository.ContainsWatchLater(targetEntry) is true;
            return new ArtworkUiState(bookmarkState, inWatchLater);
        });
    }

    public static bool TryGetState(object entry, out ArtworkUiState? state)
    {
        return s_states.TryGetValue(GetKey(entry), out state);
    }

    public static void SetBookmarkPending(object entry)
    {
        var state = GetOrCreate(entry);
        state.BookmarkState |= HeartButtonState.Pending;
    }

    public static void SetBookmarkState(object entry, bool isBookmarked)
    {
        var state = GetOrCreate(entry);
        state.BookmarkState = isBookmarked ? HeartButtonState.Checked : HeartButtonState.Unchecked;
    }

    public static void SetBookmarkState(object entry, HeartButtonState bookmarkState)
    {
        var state = GetOrCreate(entry);
        state.BookmarkState = bookmarkState;
    }

    public static void UpdateBookmark(object entry, HeartButtonState bookmarkState) => SetBookmarkState(entry, bookmarkState);

    public static void RevertBookmarkPending(object entry, bool? fallback = null)
    {
        var state = GetOrCreate(entry);
        if (fallback.HasValue)
        {
            state.BookmarkState = fallback.Value ? HeartButtonState.Checked : HeartButtonState.Unchecked;
        }
        else
        {
            state.BookmarkState &= ~HeartButtonState.Pending;
        }
    }

    public static void SetWatchLater(object entry, bool isInWatchLater)
    {
        var state = GetOrCreate(entry);
        state.IsInWatchLater = isInWatchLater;
    }

    public static void Clear()
    {
        s_states.Clear();
    }

    private static void EnsureStorageObserver()
    {
        var storageEngine = s_storageEngine;
        if (s_observerHooked || storageEngine is null)
            return;

        lock (s_states)
        {
            if (s_observerHooked)
                return;

            storageEngine.WatchLaterChanged += (_, _) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var currentEngine = s_storageEngine;
                    if (currentEngine?.WatchLaterRepository is not { } repo)
                        return;

                    foreach (var (key, state) in s_states)
                    {
                        var inWatchLater = repo.ContainsWatchLater(key);
                        if (state.IsInWatchLater != inWatchLater)
                            state.IsInWatchLater = inWatchLater;
                    }
                });
            };
            s_observerHooked = true;
        }
    }
}
