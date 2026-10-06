// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Misaki;
using Pixeval.Download;
using Pixeval.Native.Storage;

namespace Pixeval.Models.Download.Tasks;

public interface IDownloadTaskGroup : IDownloadTaskGroupBase, IIdentityInfo
{
    string IPlatformInfo.Platform => Pixiv;

    IDownloadHistoryEntry DatabaseEntry { get; }

    DownloadTaskKey IDownloadTaskGroupBase.Key => DatabaseEntry.DownloadTaskKey;
}
