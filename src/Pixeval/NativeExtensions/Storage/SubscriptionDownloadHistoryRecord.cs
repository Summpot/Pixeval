// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Native.Download;

namespace Pixeval.Native.Storage;

public partial record SubscriptionDownloadHistoryRecord : IDownloadHistoryEntry
{
    private object? _entry;
    private DownloadState? _currentState;
    private string? _formatTokenOverride;
    private string? _errorMessageOverride;

    internal object? EntryOverride { get; init; }

    public object? Entry => EntryOverride ?? (_entry ??= ArtworkPayloadHydrator.Hydrate(SerializeKey, PayloadJson));

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
