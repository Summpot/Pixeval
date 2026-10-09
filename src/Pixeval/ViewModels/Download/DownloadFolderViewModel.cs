// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Pixeval.Controls;
using Pixeval.Download;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.Models.Subscriptions;
using Pixeval.Native.Storage;
using Pixeval.Native.Subscription;

namespace Pixeval.ViewModels;

public sealed partial class DownloadFolderViewModel : ViewModelBase, IDisposable
{
    private bool _isDisposed;

    public WorkSubscriptionRecord Subscription { get; private set; }
    public ObservableCollection<DownloadItemViewModel> Items { get; } = [];
    public IReadOnlyList<DownloadItemViewModel> DownloadItems => Items;
    public SubscriptionFolderSnapshot Snapshot { get; private set; }

    public DownloadFolderViewModel(WorkSubscriptionRecord subscription)
    {
        Subscription = subscription;
        Snapshot = PixevalSubscriptionMethods.ComputeSubscriptionFolderSnapshot(subscription.HistoryEntryId, [], false, 0, null);
    }

    public string Title => GetDisplayName(Subscription);

    public static string GetDisplayName(WorkSubscriptionRecord subscription) =>
        $"{subscription.DisplayName} · " +
        $"{SymbolComboBoxItem.GetResource(subscription.Type)} · " +
        $"{SymbolComboBoxItem.GetResource(subscription.Kind)}";

    public string Subtitle => Snapshot.IsFetching
        ? RetryAt is { } retryAt && retryAt > DateTimeOffset.UtcNow
            ? I18NManager.GetResource(DownloadPageResources.RateLimitedFolderSubtitleFormatted, (int)Snapshot.FetchedCount, retryAt.ToLocalTime())
            : I18NManager.GetResource(DownloadPageResources.FetchingFolderSubtitleFormatted, (int)Snapshot.FetchedCount)
        : I18NManager.GetResource(DownloadPageResources.FolderSubtitleFormatted, Items.Count);

    public bool HasItems => Items.Count is not 0;
    public DownloadState CurrentState => (DownloadState)Snapshot.CurrentState;
    public int ActiveCount => (int)Snapshot.ActiveCount;
    public int CompletedCount => (int)Snapshot.CompletedCount;
    public int ErrorCount => (int)Snapshot.ErrorCount;
    public double ProgressPercentage => Snapshot.ProgressPercentage;
    public bool IsFetching => Snapshot.IsFetching;
    public int FetchedCount => (int)Snapshot.FetchedCount;
    public DateTimeOffset? RetryAt => Snapshot.RetryAtTimestamp is { } ts ? DateTimeOffset.FromUnixTimeSeconds(ts) : null;

    public string StateBrushKey => CurrentState switch
    {
        DownloadState.Paused => "SystemFillColorCautionBrush",
        DownloadState.Cancelled => "SystemFillColorNeutralBrush",
        _ => "SystemFillColorAttentionBrush",
    };

    public bool MatchesSearch(string key) =>
        Title.Contains(key, StringComparison.OrdinalIgnoreCase)
        || Items.Any(t => t.MatchesSearch(key));

    public bool MatchesOption(DownloadListOption option, ISet<DownloadItemViewModel>? customSearchResult) => option switch
    {
        DownloadListOption.AllQueued => true,
        DownloadListOption.CustomSearch => customSearchResult?.Any(t => Items.Contains(t)) ?? true,
        _ => Items.Any(t => t.MatchesOption(option, null))
    };

    private void RecomputeSnapshot(bool? isFetching = null, uint? fetchedCount = null, long? retryAtTimestamp = null)
    {
        var fetch = isFetching ?? Snapshot.IsFetching;
        var count = fetchedCount ?? Snapshot.FetchedCount;
        var retry = retryAtTimestamp ?? Snapshot.RetryAtTimestamp;

        var dtos = Items.Select(i => new FolderTaskItemState(
            (uint)i.CurrentState,
            i.DownloadTask.ProgressPercentage,
            (uint)i.DownloadTask.ActiveCount,
            (uint)i.DownloadTask.CompletedCount,
            (uint)i.DownloadTask.ErrorCount)).ToList();

        Snapshot = PixevalSubscriptionMethods.ComputeSubscriptionFolderSnapshot(Subscription.HistoryEntryId, dtos, fetch, count, retry);
        OnPropertyChanged(string.Empty);
    }

    internal void UpdateFetchState(SubscriptionFetchState? state)
    {
        if (state is { IsFetching: true, WorkSubscriptionId: var id } && id == Subscription.HistoryEntryId)
            RecomputeSnapshot(true, (uint)state.FetchedCount, state.RetryAt?.ToUnixTimeSeconds());
        else
            RecomputeSnapshot(false, 0, null);
    }

    internal void UpdateSubscription(WorkSubscriptionRecord subscription)
    {
        Subscription = subscription;
        OnPropertyChanged(nameof(Subscription));
        OnPropertyChanged(nameof(Title));
    }

    public void Add(DownloadItemViewModel item, bool insertAtFront)
    {
        if (insertAtFront) Items.Insert(0, item); else Items.Add(item);
        item.DownloadTask.PropertyChanged += DownloadTaskOnPropertyChanged;
        RecomputeSnapshot();
    }

    public bool Remove(DownloadItemViewModel item)
    {
        if (!Items.Remove(item)) return false;
        item.DownloadTask.PropertyChanged -= DownloadTaskOnPropertyChanged;
        RecomputeSnapshot();
        return true;
    }

    private void DownloadTaskOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isDisposed && e.PropertyName is nameof(IDownloadTaskBase.CurrentState) or nameof(IDownloadTaskBase.ProgressPercentage))
            RecomputeSnapshot();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        foreach (var item in Items) item.DownloadTask.PropertyChanged -= DownloadTaskOnPropertyChanged;
        Items.Clear();
    }
}
