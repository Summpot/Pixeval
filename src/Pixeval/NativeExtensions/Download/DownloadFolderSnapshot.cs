// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Options;

namespace Pixeval.Native.Download;

public partial record DownloadFolderSnapshot
{
    public string Title =>
        $"{DisplayName} · {SymbolComboBoxItem.GetResource((WorkSubscriptionType)SubscriptionType)} · {SymbolComboBoxItem.GetResource((WorkSubscriptionWorkKind)WorkKind)}";

    public DateTimeOffset? RetryAt =>
        RetryAtTimestamp is { } timestamp ? DateTimeOffset.FromUnixTimeSeconds(timestamp) : null;

    public string Subtitle => IsFetching
        ? RetryAt is { } retryAt && retryAt > DateTimeOffset.UtcNow
            ? I18NManager.GetResource(DownloadPageResources.RateLimitedFolderSubtitleFormatted, (int)FetchedCount, retryAt.ToLocalTime())
            : I18NManager.GetResource(DownloadPageResources.FetchingFolderSubtitleFormatted, (int)FetchedCount)
        : I18NManager.GetResource(DownloadPageResources.FolderSubtitleFormatted, (int)TotalCount);

    public string StateBrushKey => CurrentState switch
    {
        DownloadState.Paused => "SystemFillColorCautionBrush",
        DownloadState.Cancelled => "SystemFillColorNeutralBrush",
        _ => "SystemFillColorAttentionBrush"
    };
}
