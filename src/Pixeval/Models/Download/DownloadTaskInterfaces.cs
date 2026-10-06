// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Pixeval.Download;

public interface IDownloadTaskBase
{
    /// <summary>
    /// 只有<see cref="CurrentState"/>是<see cref="DownloadState.Running"/>或<see cref="DownloadState.Paused"/>值有效
    /// </summary>
    double ProgressPercentage { get; }

    /// <summary>
    /// 当前下载任务的状态
    /// </summary>
    DownloadState CurrentState { get; }

    /// <summary>
    /// 下载目标路径
    /// </summary>
    string Destination { get; }

    /// <summary>
    /// 当<see cref="CurrentState"/>是<see cref="DownloadState.Error"/>时表示可持久化的失败原因，其他状态值为null
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// 打开本地文件位置
    /// </summary>
    string OpenLocalDestination { get; }

    /// <summary>
    /// 是否正在处理
    /// </summary>
    bool IsProcessing { get; }

    void Reset();

    void Pause();

    void Resume();

    void Cancel();

    void Delete();
}

public interface ISingleDownloadTaskBase : IDownloadTaskBase;

public interface IDownloadTaskGroupBase : IDownloadTaskBase, INotifyPropertyChanged, INotifyPropertyChanging, IReadOnlyCollection<ISingleDownloadTaskBase>, IDisposable
{
    DownloadTaskKey Key { get; }

    ValueTask InitializeTaskGroupAsync();

    int ActiveCount { get; }

    int CompletedCount { get; }

    int ErrorCount { get; }
}
