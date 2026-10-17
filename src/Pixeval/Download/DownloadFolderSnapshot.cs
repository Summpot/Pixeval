// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.Config;

namespace Pixeval.Download;

public partial record struct DownloadFolderSnapshot
{
    public string Title =>
        $"{DisplayName} · {SymbolComboBoxItem.GetResource((WorkSubscriptionType)SubscriptionType)} · {SymbolComboBoxItem.GetResource((WorkSubscriptionWorkKind)WorkKind)}";

    public DateTimeOffset? RetryAt =>
        RetryAtTimestamp is { } timestamp ? DateTimeOffset.FromUnixTimeSeconds(timestamp) : null;

    public string Subtitle => IsFetching
        ? RetryAt is { } retryAt && retryAt > DateTimeOffset.UtcNow
            ? Strings.DownloadPageRateLimitedFolderSubtitleFormatted(count: (int)FetchedCount, retryAt: (retryAt.ToLocalTime()).ToString("yyyy-MM-dd HH:mm:ss", AppLocalization.Instance.CurrentCulture))
            : Strings.DownloadPageFetchingFolderSubtitleFormatted(count: (int)FetchedCount)
        : Strings.DownloadPageFolderSubtitleFormatted(count: (int)TotalCount);

    public string StateBrushKey => CurrentState switch
    {
        DownloadState.Paused => "SystemFillColorCautionBrush",
        DownloadState.Cancelled => "SystemFillColorNeutralBrush",
        _ => "SystemFillColorAttentionBrush"
    };
}
