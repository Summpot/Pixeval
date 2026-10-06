// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Misaki;
using Pixeval.Controls;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Storage;

namespace Pixeval.ViewModels;

public abstract partial class WorkEntryViewModel<T> : ThumbnailEntryViewModel<T>, IWorkViewModel where T : class, IArtworkInfo
{
    protected WorkEntryViewModel(T entry) : base(entry)
    {
        IsBookmarkedDisplay = IsFavorite ? HeartButtonState.Checked : HeartButtonState.Unchecked;
        IsInWatchLater = App.AppViewModel?.ContainsWatchLater(entry) is true;
    }

    public bool IsBlocked => BlockedContentHelper.IsBlockedPlaceholder(Entry);

    public bool IsBookmarkSupported => !IsBlocked && Entry.Platform is IPlatformInfo.Pixiv;

    public bool CanBlockAuthor => !IsBlocked && Entry is IWorkEntry;

    public Pixeval.Native.Mako.Series? Series => (Entry as IWorkEntry)?.Series;
