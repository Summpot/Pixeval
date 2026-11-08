// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Concurrent;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Controls;
using Pixeval.Booru;
using Pixeval.Mako;
using Pixeval.SauceNao;
using Pixeval.Storage;

namespace Pixeval.Services.Core;

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
        if (s_storageEngine is { } previous)
            previous.WatchLaterChanged -= OnWatchLaterChanged;
        s_observerHooked = false;
        s_states.Clear();
        s_storageEngine = storageEngine;
        EnsureStorageObserver();
    }

    public static void Shutdown(StorageEngine storageEngine)
    {
        if (!ReferenceEquals(s_storageEngine, storageEngine)) return;
        storageEngine.WatchLaterChanged -= OnWatchLaterChanged;
        s_storageEngine = null;
        s_observerHooked = false;
        s_states.Clear();
    }

    private static object UnwrapEntry(object entry) => ArtworkPayloads.Unwrap(entry);

    public static string GetKey(object entry)
    {
        var targetEntry = UnwrapEntry(entry);
        if (WatchLaterRecord.TryCreateWorkKey(targetEntry, out var key))
            return key;

        var (platform, id) = targetEntry switch
        {
            Illustration i => (i.Platform, i.Id.ToString()),
            Mako.Novel n => (n.Platform, n.Id.ToString()),
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
            var targetEntry = UnwrapEntry(entry);
            var bookmarkState = targetEntry switch
            {
                Illustration ill => ill.IsBookmarked ? HeartButtonState.Checked : HeartButtonState.Unchecked,
                Mako.Novel nov => nov.IsBookmarked ? HeartButtonState.Checked : HeartButtonState.Unchecked,
                _ => HeartButtonState.Unchecked
            };

            var inWatchLater = s_storageEngine?.WatchLaterRepository.ContainsWatchLater(targetEntry) is true;
            return new ArtworkUiState(bookmarkState, inWatchLater);
        });
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

            storageEngine.WatchLaterChanged += OnWatchLaterChanged;
            s_observerHooked = true;
        }
    }

    private static void OnWatchLaterChanged(object? sender, EventArgs args) => Dispatcher.UIThread.Post(() =>
    {
        if (sender is not StorageEngine current || !ReferenceEquals(current, s_storageEngine)) return;
        var repo = current.WatchLaterRepository;
        foreach (var (key, state) in s_states)
            state.IsInWatchLater = repo.ContainsWatchLater(key);
    });
}
