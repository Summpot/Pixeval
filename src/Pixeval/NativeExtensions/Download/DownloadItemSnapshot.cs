// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using FluentIcons.Common;
using Pixeval.I18N;
using Pixeval.Models.Options;

namespace Pixeval.Native.Download;

public partial record DownloadItemSnapshot
{
    public string AuthorsText => Author;

    public bool ShowGroupStats => ActiveCount + CompletedCount + ErrorCount > 1;

    public bool IsPending => State is DownloadState.Pending;

    public bool IsError => State is DownloadState.Error;

    public string ProgressMessage => State switch
    {
        DownloadState.Queued => I18NManager.GetResource(DownloadItemResources.DownloadQueued),
        DownloadState.Running => I18NManager.GetResource(DownloadItemResources.DownloadRunningFormatted, (int)ProgressPercentage),
        DownloadState.Error => I18NManager.GetResource(DownloadItemResources.DownloadErrorMessageFormatted, ErrorMessage),
        DownloadState.Completed => I18NManager.GetResource(DownloadItemResources.DownloadCompleted),
        DownloadState.Cancelled => I18NManager.GetResource(DownloadItemResources.DownloadCancelled),
        DownloadState.Pending => I18NManager.GetResource(DownloadItemResources.DownloadPending),
        DownloadState.Paused => I18NManager.GetResource(DownloadItemResources.DownloadPaused),
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
        Symbol.Dismiss => I18NManager.GetResource(DownloadItemResources.ActionDownloadCancelled),
        Symbol.Pause => I18NManager.GetResource(DownloadItemResources.ActionButtonContentPause),
        Symbol.ArrowRepeatAll => I18NManager.GetResource(DownloadItemResources.ActionButtonContentRetry),
        Symbol.Open => I18NManager.GetResource(DownloadItemResources.ActionButtonContentOpen),
        Symbol.Play => I18NManager.GetResource(DownloadItemResources.ActionButtonContentResume),
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
