// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Concurrent;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Misaki;
using Pixeval.Controls;
using Pixeval.Native.Mako;
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
    private static bool s_observerHooked;

    public static string GetKey(IArtworkInfo entry)
    {
        if (WatchLaterRecord.TryCreateWorkKey(entry, out var key))
            return key;
        return $"{entry.Platform}:{entry.Id}";
    }

    public static ArtworkUiState GetOrCreate(IArtworkInfo entry)
    {
        EnsureStorageObserver();
        var key = GetKey(entry);
        return s_states.GetOrAdd(key, _ =>
        {
            var bookmarkState = entry switch
            {
                Illustration ill => ill.IsBookmarked ? HeartButtonState.Checked : HeartButtonState.Unchecked,
                Novel nov => nov.IsBookmarked ? HeartButtonState.Checked : HeartButtonState.Unchecked,
                _ => HeartButtonState.Unchecked
            };

            var inWatchLater = App.AppViewModel?.ContainsWatchLater(entry) is true;
            return new ArtworkUiState(bookmarkState, inWatchLater);
        });
    }

    public static bool TryGetState(IArtworkInfo entry, out ArtworkUiState? state)
    {
        return s_states.TryGetValue(GetKey(entry), out state);
    }

    public static void SetBookmarkPending(IArtworkInfo entry)
    {
        var state = GetOrCreate(entry);
        state.BookmarkState |= HeartButtonState.Pending;
    }

    public static void SetBookmarkState(IArtworkInfo entry, bool isBookmarked)
    {
        var state = GetOrCreate(entry);
        state.BookmarkState = isBookmarked ? HeartButtonState.Checked : HeartButtonState.Unchecked;
    }

    public static void SetBookmarkState(IArtworkInfo entry, HeartButtonState bookmarkState)
    {
        var state = GetOrCreate(entry);
        state.BookmarkState = bookmarkState;
    }

    public static void UpdateBookmark(IArtworkInfo entry, HeartButtonState bookmarkState) => SetBookmarkState(entry, bookmarkState);

    public static void RevertBookmarkPending(IArtworkInfo entry, bool? fallback = null)
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

    public static void SetWatchLater(IArtworkInfo entry, bool isInWatchLater)
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
        if (s_observerHooked || App.AppViewModel?.StorageEngine is not { } storageEngine)
            return;

        lock (s_states)
        {
            if (s_observerHooked)
                return;

            storageEngine.WatchLaterChanged += (_, _) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (App.AppViewModel?.StorageEngine?.WatchLaterRepository is not { } repo)
                        return;

                    foreach (var (key, state) in s_states)
                    {
                        var inWatchLater = repo.Contains(key);
                        if (state.IsInWatchLater != inWatchLater)
                            state.IsInWatchLater = inWatchLater;
                    }
                });
            };
            s_observerHooked = true;
        }
    }
}
