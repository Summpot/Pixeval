// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Misaki;
using Pixeval.Controls;
using Pixeval.Models.Blocking;
using Pixeval.Models.Database;
using Pixeval.Models.Database.Managers;
using Pixeval.Models.Pixiv;

namespace Pixeval.ViewModels;

public abstract partial class WorkEntryViewModel<T> : ThumbnailEntryViewModel<T>, IWorkViewModel where T : class, IArtworkInfo
{
    protected WorkEntryViewModel(T entry) : base(entry)
    {
        IsBookmarkedDisplay = IsFavorite ? HeartButtonState.Checked : HeartButtonState.Unchecked;
        IsInWatchLater = GetHistoryPersistHelper()?.ContainsWatchLater(entry) is true;
    }

    public bool IsBlocked => BlockedContentHelper.IsBlockedPlaceholder(Entry);

    public bool IsBookmarkSupported => !IsBlocked && Entry.Platform is IPlatformInfo.Pixiv;

    public Pixeval.Native.Mako.Series? Series => (Entry as IWorkEntry)?.Series;

    public bool HasSeries => Series is not null;

    IArtworkInfo IWorkViewModel.Entry => Entry;

    public bool IsFavorite => Entry.IsFavorite;

    [ObservableProperty] public partial HeartButtonState IsBookmarkedDisplay { get; set; }

    [ObservableProperty] public partial bool IsInWatchLater { get; set; }

    public DateTimeOffset CreateDate => Entry.CreateDate;

    public override string? ThumbnailUrl => Entry.Thumbnails.PickClosestHeight(300)?.ImageUri.OriginalString;

    protected bool CanManageWatchLater => !IsBlocked
                                          && GetHistoryPersistHelper() is not null
                                          && WatchLaterEntry.TryCreateWorkKey(Entry, out _);

    private static HistoryPersistHelper? GetHistoryPersistHelper() => App.AppViewModel?.AppServiceProvider?.GetService<HistoryPersistHelper>();
}
