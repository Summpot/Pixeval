// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pixeval.Download;
using Pixeval.Native.Storage;
using Pixeval.Utilities.IO;

namespace Pixeval.Models.Download.Tasks;

public abstract class SingleImageDownloadTaskGroupBase : ImageDownloadTask, IDownloadTaskGroup
{
    public IDownloadHistoryEntry DatabaseEntry { get; }

    public ValueTask InitializeTaskGroupAsync()
    {
        SetNotCreateFromEntry();
        return ValueTask.CompletedTask;
    }

    public string Id => DatabaseEntry.Entry switch
    {
        Native.Mako.Illustration i => i.Id.ToString(),
        Native.Mako.Novel n => n.Id.ToString(),
        Native.Booru.BooruPost b => b.Id,
        Native.SauceNao.SauceNaoItem s => s.RawId,
        _ => ""
    };

    protected SingleImageDownloadTaskGroupBase(
        object entry,
        string destination,
        int? workSubscriptionId = null) : this(IDownloadHistoryEntry.Create(destination, entry, workSubscriptionId))
    {
        CurrentState = DownloadState.Queued;
        ProgressPercentage = 0;
    }

    protected SingleImageDownloadTaskGroupBase(IDownloadHistoryEntry entry) : base(GetImageUri(entry.Entry!),
        IoHelper.ReplaceTokenExtensionFromUrl(entry.Destination, GetImageUri(entry.Entry!), entry.Entry!.TryGetSetIndex()))
    {
        DatabaseEntry = entry;
        ErrorMessage = entry.ErrorMessage;
        CurrentState = entry.State;
        if (entry.State is DownloadState.Completed or DownloadState.Cancelled or DownloadState.Error)
            ProgressPercentage = 100;
        SetNotCreateFromEntry();
    }

    private static Uri GetImageUri(object info) =>
        info switch
        {
            Native.Mako.Illustration illust => new Uri(illust.OriginalSingleUrl ?? illust.ImageUrls?.Original ?? illust.ImageUrls?.Large ?? "about:blank"),
            Native.Booru.BooruPost booru => new Uri(booru.OriginalUrl ?? booru.SampleUrl ?? booru.PreviewUrl ?? "about:blank"),
            Native.SauceNao.SauceNaoItem sauce => new Uri(string.IsNullOrWhiteSpace(sauce.ThumbnailUrl) ? "about:blank" : sauce.ThumbnailUrl),
            _ => throw new NotSupportedException(info?.ToString())
        };

    private void SetNotCreateFromEntry()
    {
        if (!IsCreateFromEntry)
            return;
        IsCreateFromEntry = false;
        PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName is not nameof(CurrentState))
                return;
            if (sender is SingleImageDownloadTaskGroupBase g and not { CurrentState: DownloadState.Running or DownloadState.Pending })
            {
                g.DatabaseEntry.State = g.CurrentState;
                g.DatabaseEntry.ErrorMessage = g.CurrentState is DownloadState.Error
                    ? g.ErrorMessage
                    : null;
                App.AppViewModel.UpdateDownloadHistory(g.DatabaseEntry);
            }
        };
    }

    private bool IsCreateFromEntry { get; set; } = true;

    public int ActiveCount => CurrentState is DownloadState.Queued or DownloadState.Running or DownloadState.Pending or DownloadState.Paused or DownloadState.Cancelled ? 1 : 0;

    public int CompletedCount => CurrentState is DownloadState.Completed ? 1 : 0;

    public int ErrorCount => CurrentState is DownloadState.Error ? 1 : 0;

    public int Count => 1;

    public IEnumerator<ISingleDownloadTaskBase> GetEnumerator() => ((IReadOnlyList<ISingleDownloadTaskBase>) [this]).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
