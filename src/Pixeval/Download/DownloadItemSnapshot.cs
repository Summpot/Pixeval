// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using FluentIcons.Common;
using Pixeval.Localization;
using Pixeval.Config;

namespace Pixeval.Download;

public partial record struct DownloadItemSnapshot
{
    public bool ShowGroupStats => ActiveCount + CompletedCount + ErrorCount > 1;

    public bool IsPending => State is DownloadState.Pending;

    public bool IsError => State is DownloadState.Error;

    public string ProgressMessage => State switch
    {
        DownloadState.Queued => Strings.DownloadItemDownloadQueued(),
        DownloadState.Running => Strings.DownloadItemDownloadRunningFormatted(progress: (int)ProgressPercentage),
        DownloadState.Error => Strings.DownloadItemDownloadErrorMessageFormatted(error: ErrorMessage),
        DownloadState.Completed => Strings.DownloadItemDownloadCompleted(),
        DownloadState.Cancelled => Strings.DownloadItemDownloadCancelled(),
        DownloadState.Pending => Strings.DownloadItemDownloadPending(),
        DownloadState.Paused => Strings.DownloadItemDownloadPaused(),
        _ => ""
    };

    public Symbol ActionButtonSymbol => State switch
    {
        DownloadState.Pending => Symbol.Dismiss,
        DownloadState.Queued or DownloadState.Running => Symbol.Pause,
        DownloadState.Cancelled or DownloadState.Error => Symbol.ArrowRepeatAll,
        DownloadState.Completed => Symbol.Open,
        DownloadState.Paused => Symbol.Play,
        _ => Symbol.Question
    };

    public string ActionButtonContent => ActionButtonSymbol switch
    {
        Symbol.Dismiss => Strings.DownloadItemActionDownloadCancelled(),
        Symbol.Pause => Strings.DownloadItemActionButtonContentPause(),
        Symbol.ArrowRepeatAll => Strings.DownloadItemActionButtonContentRetry(),
        Symbol.Open => Strings.DownloadItemActionButtonContentOpen(),
        Symbol.Play => Strings.DownloadItemActionButtonContentResume(),
        _ => ""
    };

    public bool IsItemEnabled => !IsProcessing || State is DownloadState.Completed;

    public bool IsResetItemEnabled => !IsProcessing && State is DownloadState.Completed or DownloadState.Error;

    public bool IsCancelItemEnabled =>
        !IsProcessing && State is DownloadState.Running or DownloadState.Queued or DownloadState.Paused;

    public string StateBrushKey => State switch
    {
        DownloadState.Paused => "SystemFillColorCautionBrush",
        DownloadState.Cancelled => "SystemFillColorNeutralBrush",
        _ => "SystemFillColorAttentionBrush"
    };

    public bool MatchesSearch(string key) =>
        Title.Contains(key, StringComparison.OrdinalIgnoreCase)
        || ArtworkId.Contains(key, StringComparison.OrdinalIgnoreCase);

    public bool MatchesOption(DownloadListOption option, ISet<DownloadTaskKey>? customSearchResult) => option switch
    {
        DownloadListOption.AllQueued => true,
        DownloadListOption.Running => State is DownloadState.Running,
        DownloadListOption.Completed => State is DownloadState.Completed,
        DownloadListOption.Cancelled => State is DownloadState.Cancelled,
        DownloadListOption.Error => State is DownloadState.Error,
        DownloadListOption.CustomSearch => customSearchResult?.Contains(Key) ?? true,
        _ => true
    };
}
