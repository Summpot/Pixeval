// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Misaki;
using Pixeval.Native.Download;

namespace Pixeval.Native.Storage;

public partial record SubscriptionDownloadHistoryRecord : IDownloadHistoryEntry
{
    private IArtworkInfo? _entry;
    private DownloadState? _currentState;
    private string? _formatTokenOverride;
    private string? _errorMessageOverride;

    internal IArtworkInfo? EntryOverride { get; init; }

    public IArtworkInfo? Entry => EntryOverride ?? (_entry ??= ArtworkPayloadHydrator.Hydrate(SerializeKey, PayloadJson));

    public DownloadState DownloadState => _currentState ?? (DownloadState)State;

    DownloadState IDownloadHistoryEntry.State
    {
        get => DownloadState;
        set => _currentState = value;
    }

    string? IDownloadHistoryEntry.FormatToken
    {
        get => _formatTokenOverride ?? FormatToken;
        set => _formatTokenOverride = value;
    }

    string? IDownloadHistoryEntry.ErrorMessage
    {
        get => _errorMessageOverride ?? ErrorMessage;
        set => _errorMessageOverride = value;
    }

    public DownloadTaskKey DownloadTaskKey => new(Destination, (int)WorkSubscriptionId, ArtworkId);
}
