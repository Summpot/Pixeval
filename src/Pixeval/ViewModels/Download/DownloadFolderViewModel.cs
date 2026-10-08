// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Controls;
using Pixeval.Download;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.Models.Subscriptions;
using Pixeval.Native.Storage;

namespace Pixeval.ViewModels;

public sealed partial class DownloadFolderViewModel(WorkSubscriptionRecord subscription)
    : ViewModelBase, IDownloadListEntryViewModel, IDisposable
{
    private bool _isDisposed;

    public WorkSubscriptionRecord Subscription { get; private set; } = subscription;

    public ObservableCollection<DownloadItemViewModel> Items { get; } = [];

    public IReadOnlyList<DownloadItemViewModel> DownloadItems => Items;

    [ObservableProperty] public partial bool IsFetching { get; private set; }

    [ObservableProperty] public partial int FetchedCount { get; private set; }

    [ObservableProperty] public partial DateTimeOffset? RetryAt { get; private set; }

    public string Title => GetDisplayName(Subscription);

    public static string GetDisplayName(WorkSubscriptionRecord subscription) =>
        $"{subscription.DisplayName} · " +
        $"{SymbolComboBoxItem.GetResource(subscription.Type)} · " +
        $"{SymbolComboBoxItem.GetResource(subscription.Kind)}";

    public string Subtitle => IsFetching
        ? RetryAt is { } retryAt && retryAt > DateTimeOffset.UtcNow
            ? I18NManager.GetResource(DownloadPageResources.RateLimitedFolderSubtitleFormatted, FetchedCount, retryAt.ToLocalTime())
            : I18NManager.GetResource(DownloadPageResources.FetchingFolderSubtitleFormatted, FetchedCount)
        : I18NManager.GetResource(DownloadPageResources.FolderSubtitleFormatted, Items.Count);

    public bool HasItems => Items.Count is not 0;

    public DownloadState CurrentState
    {
        get
        {
            if (Items.Count is 0)
                return DownloadState.Completed;

            var hasError = false;
            var hasCancelled = false;
            var hasPaused = false;
            var hasRunning = false;
            var hasQueued = false;
            var hasPending = false;

            for (var i = 0; i < Items.Count; i++)
            {
                switch (Items[i].CurrentState)
                {
                    case DownloadState.Error: hasError = true; break;
                    case DownloadState.Cancelled: hasCancelled = true; break;
                    case DownloadState.Paused: hasPaused = true; break;
                    case DownloadState.Running: hasRunning = true; break;
                    case DownloadState.Queued: hasQueued = true; break;
                    case DownloadState.Pending: hasPending = true; break;
                }
            }

            if (hasError) return DownloadState.Error;
            if (hasCancelled) return DownloadState.Cancelled;
            if (hasPaused) return DownloadState.Paused;
            if (hasRunning) return DownloadState.Running;
            if (hasQueued) return DownloadState.Queued;
            if (hasPending) return DownloadState.Pending;
            return DownloadState.Completed;
        }
    }

    public int ActiveCount
    {
        get
        {
            var sum = 0;
            for (var i = 0; i < Items.Count; i++)
                sum += Items[i].DownloadTask.ActiveCount;
            return sum;
        }
    }

    public int CompletedCount
    {
        get
        {
            var sum = 0;
            for (var i = 0; i < Items.Count; i++)
                sum += Items[i].DownloadTask.CompletedCount;
            return sum;
        }
    }

    public int ErrorCount
    {
        get
        {
            var sum = 0;
            for (var i = 0; i < Items.Count; i++)
                sum += Items[i].DownloadTask.ErrorCount;
            return sum;
        }
    }

    public double ProgressPercentage
    {
        get
        {
            if (Items.Count is 0)
                return 100;

            var sum = 0.0;
            for (var i = 0; i < Items.Count; i++)
                sum += Items[i].DownloadTask.ProgressPercentage;
            return sum / Items.Count;
        }
    }

    public string StateBrushKey => CurrentState switch
    {
        DownloadState.Paused => "SystemFillColorCautionBrush",
        DownloadState.Cancelled => "SystemFillColorNeutralBrush",
        _ => "SystemFillColorAttentionBrush",
    };

    public bool MatchesSearch(string key) =>
        Title.Contains(key, StringComparison.OrdinalIgnoreCase)
        || Items.Any(t => t.MatchesSearch(key));

    public bool MatchesOption(DownloadListOption option, ISet<IDownloadListEntryViewModel>? customSearchResult) => option switch
    {
        DownloadListOption.AllQueued => true,
        DownloadListOption.CustomSearch => customSearchResult?.Contains(this) ?? true,
        _ => Items.Any(t => t.MatchesOption(option, null))
    };

    partial void OnIsFetchingChanged(bool value) => OnPropertyChanged(nameof(Subtitle));

    partial void OnFetchedCountChanged(int value) => OnPropertyChanged(nameof(Subtitle));

    partial void OnRetryAtChanged(DateTimeOffset? value) => OnPropertyChanged(nameof(Subtitle));

    internal void UpdateFetchState(SubscriptionFetchState? state)
    {
        if (state is not
            {
                IsFetching: true,
                WorkSubscriptionId: var workSubscriptionId
            } || workSubscriptionId != Subscription.HistoryEntryId)
        {
            IsFetching = false;
            FetchedCount = 0;
            RetryAt = null;
            return;
        }

        FetchedCount = state.FetchedCount;
        RetryAt = state.RetryAt;
        IsFetching = true;
    }

    internal void UpdateSubscription(WorkSubscriptionRecord subscription)
    {
        Subscription = subscription;
        OnPropertyChanged(nameof(Subscription));
        OnPropertyChanged(nameof(Title));
    }

    public void Add(DownloadItemViewModel item, bool insertAtFront)
    {
        if (insertAtFront)
            Items.Insert(0, item);
        else
            Items.Add(item);
        item.DownloadTask.PropertyChanged += DownloadTaskOnPropertyChanged;
        OnItemsChanged();
    }

    public bool Remove(DownloadItemViewModel item)
    {
        if (!Items.Remove(item))
            return false;

        item.DownloadTask.PropertyChanged -= DownloadTaskOnPropertyChanged;
        OnItemsChanged();
        return true;
    }

    private void DownloadTaskOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isDisposed)
            return;

        if (e.PropertyName is nameof(IDownloadTaskBase.CurrentState))
        {
            OnPropertyChanged(nameof(CurrentState));
            OnPropertyChanged(nameof(StateBrushKey));
            OnPropertyChanged(nameof(ActiveCount));
            OnPropertyChanged(nameof(CompletedCount));
            OnPropertyChanged(nameof(ErrorCount));
        }

        if (e.PropertyName is nameof(IDownloadTaskBase.CurrentState)
            or nameof(IDownloadTaskBase.ProgressPercentage))
            OnPropertyChanged(nameof(ProgressPercentage));
    }

    private void OnItemsChanged()
    {
        OnPropertyChanged(nameof(DownloadItems));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(CurrentState));
        OnPropertyChanged(nameof(StateBrushKey));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(ProgressPercentage));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        foreach (var item in Items)
            item.DownloadTask.PropertyChanged -= DownloadTaskOnPropertyChanged;
        Items.Clear();
    }
}
