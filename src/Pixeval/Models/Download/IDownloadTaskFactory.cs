// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using Pixeval.Download;

namespace Pixeval.Models.Download;

public interface IDownloadTaskFactory<in TContext, out TDownloadTask, in TParameter> where TDownloadTask : IDownloadTaskGroupBase
{
    TDownloadTask Create(TContext context, string rawPath, TParameter? parameter);
}
