// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Config;

public enum DownloadListOption
{
    [LocalizedResource(nameof(Strings.EnumDownloadListOptionAllQueued))]
    AllQueued,

    [LocalizedResource(nameof(Strings.EnumDownloadListOptionRunning))]
    Running,

    [LocalizedResource(nameof(Strings.EnumDownloadListOptionCompleted))]
    Completed,

    [LocalizedResource(nameof(Strings.EnumDownloadListOptionCancelled))]
    Cancelled,

    [LocalizedResource(nameof(Strings.EnumDownloadListOptionError))]
    Error,

    [LocalizedResource(nameof(Strings.EnumDownloadListOptionCustomSearch))]
    CustomSearch
}
