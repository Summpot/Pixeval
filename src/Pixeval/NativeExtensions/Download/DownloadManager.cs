// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using Pixeval.Download;
using Pixeval.Models.Download.Tasks;
using Pixeval.Models.Options;
using Pixeval.Utilities;

namespace Pixeval.Native.Download;

public partial class DownloadManager
{
    private readonly ProgressCallbackAdapter _callbackAdapter;
    private readonly ConcurrentDictionary<DownloadTaskKey, (IDownloadTaskGroupBase Group, ImageDownloadTask? SubTask)> _subTaskMap = new();
    private readonly HashSet<ImageDownloadTask> _hookedTasks = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<DownloadTaskKey, IDownloadTaskGroupBase> _taskQuerySet = [];
    private int _concurrencyDegree;
    private bool _disposed;

    public void MarkDisposed() => _disposed = true;

    public ObservableCollection<IDownloadTaskGroupBase> QueuedTasks { get; } = [];

    public int ConcurrencyDegree
    {
        get => _concurrencyDegree;
        set
        {
            _concurrencyDegree = value;
            SetConcurrency((uint)Math.Max(1, value));
        }
    }

    public DownloadManager(HttpClient? httpClient, int concurrencyDegree)
        : this(new ProgressCallbackAdapter(), concurrencyDegree)
    {
    }

    private DownloadManager(ProgressCallbackAdapter adapter, int concurrencyDegree)
        : this((uint)Math.Max(1, concurrencyDegree), adapter, GetEffectiveNetworkOptions())
    {
        _concurrencyDegree = Math.Max(1, concurrencyDegree);
        _callbackAdapter = adapter;
        adapter.Parent = this;
    }

    public void UpdateNetworkOptions()
    {
        UpdateNetworkOptions(GetEffectiveNetworkOptions());
    }

    public static DownloadNetworkOptions GetEffectiveNetworkOptions()
    {
        var networkSettings = App.AppViewModel?.AppSettings?.NetworkSettings;
        var proxyUrl = MakoHelper.GetEffectiveProxyUrl(networkSettings);

        var staticDomainIps = new Dictionary<string, List<string>>();
        if (networkSettings?.PixivDomainFronting is { EnablePixivDomainFronting: true } df)
        {
            if (df.PixivImageNameResolver.Count > 0)
                staticDomainIps[MakoHelper.ImageHost] = [.. df.PixivImageNameResolver];
            if (df.PixivImageNameResolver2.Count > 0)
                staticDomainIps[MakoHelper.ImageHost2] = [.. df.PixivImageNameResolver2];
        }

        return new DownloadNetworkOptions(proxyUrl, staticDomainIps);
    }

    public void QueueTask(IDownloadTaskGroupBase taskGroup)
    {
        if (_taskQuerySet.TryGetValue(taskGroup.Key, out var v) && v == taskGroup)
            return;
        _taskQuerySet[taskGroup.Key] = taskGroup;

        if (v is not null && QueuedTasks.IndexOf(v) is var existingIndex and >= 0)
        {
            v.Cancel();
            CancelAndRemoveGroupFromNative(v);
            QueuedTasks[existingIndex] = taskGroup;
            if (existingIndex > 0)
                QueuedTasks.Move(existingIndex, 0);
        }
        else
        {
            QueuedTasks.Insert(0, taskGroup);
        }

        if (taskGroup.CurrentState is DownloadState.Queued || (int)taskGroup.CurrentState == 0)
            _ = EnqueueGroupToNativeAsync(taskGroup);
    }

    public bool TryRestoreTask(IDownloadTaskGroupBase taskGroup)
    {
        if (_disposed || !_taskQuerySet.TryAdd(taskGroup.Key, taskGroup))
            return false;

        QueuedTasks.Add(taskGroup);
        if (taskGroup.CurrentState is DownloadState.Queued || (int)taskGroup.CurrentState == 0)
            _ = EnqueueGroupToNativeAsync(taskGroup);
        return true;
    }

    public bool TryRemoveTask(IDownloadTaskGroupBase taskGroup)
    {
        var taskToRemove = _taskQuerySet.GetValueOrDefault(taskGroup.Key, taskGroup);
        taskToRemove.Cancel();
        CancelAndRemoveGroupFromNative(taskToRemove);
        if (!QueuedTasks.Remove(taskToRemove))
            return false;

        _ = _taskQuerySet.Remove(taskToRemove.Key);
        return true;
    }

    public void ClearTasks()
    {
        foreach (var task in QueuedTasks)
        {
            task.Cancel();
            CancelAndRemoveGroupFromNative(task);
            task.Dispose();
        }
        QueuedTasks.Clear();
        _taskQuerySet.Clear();
        _subTaskMap.Clear();
        ClearNativeTasks();
    }

    public bool TryExecuteTaskGroupInline(IDownloadTaskGroupBase taskGroup)
    {
        if (QueuedTasks.Contains(taskGroup) && (taskGroup.CurrentState is DownloadState.Queued || (int)taskGroup.CurrentState == 0))
        {
            _ = EnqueueGroupToNativeAsync(taskGroup);
            return true;
        }

        return false;
    }

    private async Task EnqueueGroupToNativeAsync(IDownloadTaskGroupBase taskGroup)
    {
        if (_disposed)
            return;

        try
        {
            await taskGroup.InitializeTaskGroupAsync();
        }
        catch
        {
            return;
        }

        if (_disposed)
            return;

        foreach (var subTask in taskGroup)
        {
            if (subTask is not ImageDownloadTask imgTask)
                continue;

            if (imgTask.CurrentState is not DownloadState.Queued && (int)imgTask.CurrentState != 0)
                continue;

            var subKey = new DownloadTaskKey(
                imgTask.Destination,
                taskGroup.Key.WorkSubscriptionId,
                taskGroup.Key.ArtworkId);

            _subTaskMap[subKey] = (taskGroup, imgTask);
            if (!subKey.Equals(taskGroup.Key))
            {
                _subTaskMap.TryAdd(taskGroup.Key, (taskGroup, imgTask));
            }

            HookSubTaskLifecycle(subKey, imgTask);

            var overwrite = App.AppViewModel?.AppSettings?.DownloadSettings?.OverwriteDownloadedFile ?? true;
            EnqueueTask(
                subKey,
                imgTask.Uri.OriginalString,
                imgTask.Destination,
                overwrite);
        }
    }

    private void HookSubTaskLifecycle(DownloadTaskKey key, ImageDownloadTask imgTask)
    {
        lock (_hookedTasks)
        {
            if (!_hookedTasks.Add(imgTask))
                return;
        }

        imgTask.DownloadPaused += OnTaskPaused;
        imgTask.DownloadCancelled += OnTaskCancelled;
        imgTask.DownloadTryResume += OnTaskResume;
        imgTask.DownloadTryReset += OnTaskReset;

        void OnTaskPaused(ImageDownloadTask _)
        {
            PauseTask(key);
        }

        void OnTaskCancelled(ImageDownloadTask _)
        {
            CancelTask(key);
        }

        void OnTaskResume(ImageDownloadTask _)
        {
            var overwrite = App.AppViewModel?.AppSettings?.DownloadSettings?.OverwriteDownloadedFile ?? true;
            EnqueueTask(key, imgTask.Uri.OriginalString, imgTask.Destination, overwrite);
        }

        void OnTaskReset(ImageDownloadTask _)
        {
            var overwrite = App.AppViewModel?.AppSettings?.DownloadSettings?.OverwriteDownloadedFile ?? true;
            EnqueueTask(key, imgTask.Uri.OriginalString, imgTask.Destination, overwrite);
        }
    }

    private void CancelAndRemoveGroupFromNative(IDownloadTaskGroupBase taskGroup)
    {
        RemoveTask(taskGroup.Key);
        _subTaskMap.TryRemove(taskGroup.Key, out _);

        foreach (var subTask in taskGroup)
        {
            if (subTask is ImageDownloadTask imgTask)
            {
                var subKey = new DownloadTaskKey(
                    imgTask.Destination,
                    taskGroup.Key.WorkSubscriptionId,
                    taskGroup.Key.ArtworkId);
                RemoveTask(subKey);
                _subTaskMap.TryRemove(subKey, out _);
            }
        }
    }

    private static void SafePost(Action action)
    {
        if (Application.Current is null || Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    private sealed class ProgressCallbackAdapter : IDownloadProgressCallback
    {
        internal DownloadManager? Parent { get; set; }

        public void OnProgress(DownloadTaskKey key, double progressPercentage, ulong downloadedBytes, ulong totalBytes)
        {
            if (Parent?._subTaskMap.TryGetValue(key, out var mapping) == true && mapping.SubTask is { } subTask)
            {
                SafePost(() => subTask.UpdateProgress(progressPercentage, downloadedBytes, totalBytes));
            }
        }

        public void OnStateChanged(DownloadTaskKey key, DownloadState state, string? errorMessage)
        {
            if (Parent?._subTaskMap.TryGetValue(key, out var mapping) == true && mapping.SubTask is { } subTask)
            {
                SafePost(() =>
                {
                    if (state is DownloadState.Error)
                        subTask.SetNativeError(errorMessage ?? "Unknown download error");
                    else
                        subTask.SetNativeState(state);
                });
            }
        }

        public void OnCompleted(DownloadTaskKey key, string destination)
        {
            if (Parent?._subTaskMap.TryGetValue(key, out var mapping) == true && mapping.SubTask is { } subTask)
            {
                SafePost(() => _ = subTask.OnNativeCompletedAsync(destination));
            }
        }
    }
}
