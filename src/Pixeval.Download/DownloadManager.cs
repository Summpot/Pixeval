// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Pixeval.Utilities.Threading;

namespace Pixeval.Download;

public sealed class DownloadManager : IDisposable
{
    /// <summary>
    /// 正在排队的任务队列
    /// </summary>
    private readonly Channel<DownloadToken> _downloadTaskChannel = Channel.CreateUnbounded<DownloadToken>();

    /// <summary>
    /// 使用图片下载接口的<see cref="HttpClient"/>
    /// </summary>
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 指示是否可以进行新的下载任务，要求<see cref="_workingTasks"/>小于<see cref="ConcurrencyDegree"/>
    /// </summary>
    /// <remarks>
    /// the generic parameter is used to simplify the code, the expression can be embedded directly into the condition of the while loop
    /// </remarks>
    private readonly ReenterableAwaiter<bool> _throttle = new(true, true);

    /// <summary>
    /// 正在下载中的数量
    /// </summary>
    private int _workingTasks;

    /// <summary>
    /// 与<see cref="QueuedTasks"/>内容一样，但是用于快速查询
    /// </summary>
    private readonly ConcurrentDictionary<DownloadTaskKey, IDownloadTaskGroupBase> _taskQuerySet = [];

    private readonly ConditionalWeakTable<IDownloadTaskGroupBase, Task> _replacementWaits = new();

    private readonly ConcurrentDictionary<DownloadTaskKey, Task> _removedTaskWaits = [];

    private readonly CancellationTokenSource _replacementCancellationTokenSource = new();

    private bool _disposed;

    /// <summary>
    /// 所有的下载任务
    /// </summary>
    public ObservableCollection<IDownloadTaskGroupBase> QueuedTasks { get; } = [];

    /// <summary>
    /// 同时下载的任务数量
    /// </summary>
    public int ConcurrencyDegree { get; set; }

    public DownloadManager(HttpClient httpClient, int concurrencyDegree)
    {
        _httpClient = httpClient;
        ConcurrencyDegree = concurrencyDegree;

        PollTask();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _replacementCancellationTokenSource.Cancel();
        _replacementCancellationTokenSource.Dispose();
        _removedTaskWaits.Clear();
        _ = _downloadTaskChannel.Writer.TryComplete();
        _throttle.Dispose();
        foreach (var task in QueuedTasks)
            task.Dispose();
    }

    /// <summary>
    /// 保证不重复的前提下，将任务加入下载队列前端
    /// </summary>
    /// <remarks>
    /// intrinsic download task are not counted
    /// </remarks>
    public void QueueTask(IDownloadTaskGroupBase taskGroup)
    {
        if (_disposed)
            return;
        if (_taskQuerySet.TryGetValue(taskGroup.Key, out var v) && v == taskGroup)
            return;
        _taskQuerySet[taskGroup.Key] = taskGroup;

        if (v is not null && QueuedTasks.IndexOf(v) is var existingIndex and >= 0)
        {
            v.Cancel();
            _replacementWaits.AddOrUpdate(taskGroup, GetStopWait(v));
            InheritRemovedTaskWait(taskGroup);
            QueuedTasks[existingIndex] = taskGroup;
            if (existingIndex > 0)
                QueuedTasks.Move(existingIndex, 0);
        }
        else
        {
            InheritRemovedTaskWait(taskGroup);
            QueuedTasks.Insert(0, taskGroup);
        }

        taskGroup.SubscribeProgress(_downloadTaskChannel.Writer);
        if (taskGroup.CurrentState is DownloadState.Queued)
            _ = _downloadTaskChannel.Writer.TryWrite(taskGroup.GetToken());
    }

    /// <summary>
    /// 恢复持久化任务，但不替换应用启动后新加入的同路径任务。
    /// </summary>
    public bool TryRestoreTask(IDownloadTaskGroupBase taskGroup)
    {
        if (_disposed || !_taskQuerySet.TryAdd(taskGroup.Key, taskGroup))
            return false;

        InheritRemovedTaskWait(taskGroup);
        QueuedTasks.Add(taskGroup);
        taskGroup.SubscribeProgress(_downloadTaskChannel.Writer);
        if (taskGroup.CurrentState is DownloadState.Queued)
            _ = _downloadTaskChannel.Writer.TryWrite(taskGroup.GetToken());
        return true;
    }

    /// <summary>
    /// 当队列有排队且未到达最大同时下载数时，开始下载任务
    /// </summary>
    private async void PollTask()
    {
        try
        {
            while (await _downloadTaskChannel.Reader.WaitToReadAsync())
                while (await _throttle && _downloadTaskChannel.Reader.TryRead(out var taskToken) && taskToken is { Token.IsCancellationRequested: false, Task: var taskGroup })
                {
                    if (!IsCurrentTask(taskToken))
                        continue;
                    if (_replacementWaits.TryGetValue(taskGroup, out var stopped) && !stopped.IsCompleted)
                    {
                        // Do not block other destinations behind a provider that
                        // has no cancellation ABI and has not acknowledged yet.
                        _ = RequeueAfterStoppedAsync(taskToken, stopped);
                        continue;
                    }
                    await taskGroup.InitializeTaskGroupAsync();

                    if (!IsCurrentTask(taskToken))
                        continue;

                    foreach (var subTask in taskGroup)
                    {
                        if (!IsCurrentTask(taskToken))
                            break;
                        // 需要判断是否处于Queued状态才能运行
                        if (subTask.CurrentState is DownloadState.Queued)
                        {
                            await DownloadAsync(subTask);
                            _ = await _throttle;
                        }
                    }
                }
        }
        catch (ObjectDisposedException) when (_disposed)
        {
        }
    }

    private bool IsCurrentTask(DownloadToken token) =>
        !_disposed && !token.Token.IsCancellationRequested
        && _taskQuerySet.TryGetValue(token.Task.Key, out var current)
        && ReferenceEquals(current, token.Task);

    private async Task RequeueAfterStoppedAsync(DownloadToken token, Task stopped)
    {
        try
        {
            await stopped.ConfigureAwait(false);
            if (IsCurrentTask(token))
                _ = _downloadTaskChannel.Writer.TryWrite(token);
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
    }

    private static async Task WaitForStoppedAsync(IDownloadTaskGroupBase task, CancellationToken token)
    {
        if (!task.IsProcessing)
            return;
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName is nameof(IDownloadTaskBase.IsProcessing) && !task.IsProcessing)
                stopped.TrySetResult();
        }
        task.PropertyChanged += OnPropertyChanged;
        try
        {
            using var registration = token.Register(() => stopped.TrySetCanceled(token));
            // Completion can race the subscription; check again after attaching.
            if (!task.IsProcessing)
                stopped.TrySetResult();
            await stopped.Task.ConfigureAwait(false);
        }
        finally
        {
            task.PropertyChanged -= OnPropertyChanged;
        }
    }

    private Task GetStopWait(IDownloadTaskGroupBase task)
    {
        if (_disposed)
            return Task.CompletedTask;
        var stopped = WaitForStoppedAsync(task, _replacementCancellationTokenSource.Token);
        // A queued replacement may itself be replaced, paused, or removed.
        // Its predecessor still owns the files until the entire chain stops.
        return _replacementWaits.TryGetValue(task, out var inherited)
            ? Task.WhenAll(inherited, stopped)
            : stopped;
    }

    private void RememberRemovedTaskWait(IDownloadTaskGroupBase task)
    {
        var stopped = GetStopWait(task);
        if (_removedTaskWaits.TryGetValue(task.Key, out var inherited))
            stopped = Task.WhenAll(inherited, stopped);
        if (stopped.IsCompleted)
            return;

        // History disappears immediately, but removing a row does not release
        // an in-flight provider's input streams and temporary output paths.
        _removedTaskWaits[task.Key] = stopped;
        _ = ForgetRemovedTaskWaitAsync(task.Key, stopped);
    }

    private void InheritRemovedTaskWait(IDownloadTaskGroupBase task)
    {
        if (!_removedTaskWaits.TryRemove(task.Key, out var stopped))
            return;
        if (_replacementWaits.TryGetValue(task, out var inherited))
            stopped = Task.WhenAll(inherited, stopped);
        _replacementWaits.AddOrUpdate(task, stopped);
    }

    private async Task ForgetRemovedTaskWaitAsync(DownloadTaskKey key, Task stopped)
    {
        try
        {
            await stopped.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
        finally
        {
            // A later removal can install another wait under the same identity.
            _ = _removedTaskWaits.TryRemove(new KeyValuePair<DownloadTaskKey, Task>(key, stopped));
        }
    }

    /// <summary>
    /// 清除指定任务
    /// </summary>
    /// <param name="taskGroup"></param>
    public bool TryRemoveTask(IDownloadTaskGroupBase taskGroup)
    {
        var taskToRemove = _taskQuerySet.GetValueOrDefault(taskGroup.Key, taskGroup);
        taskToRemove.Cancel();
        if (!QueuedTasks.Contains(taskToRemove))
            return false;

        RememberRemovedTaskWait(taskToRemove);
        _ = _taskQuerySet.TryRemove(taskToRemove.Key, out _);
        return QueuedTasks.Remove(taskToRemove);
    }

    /// <summary>
    /// 清除所有任务
    /// </summary>
    public void ClearTasks()
    {
        foreach (var task in QueuedTasks)
        {
            task.Cancel();
            RememberRemovedTaskWait(task);
        }
        _taskQuerySet.Clear();
        QueuedTasks.Clear();
    }

    /// <summary>
    /// Attempts to re-download a task only if its already queued and not running
    /// </summary>
    /// <remarks>
    /// Execute the task only if it's already queued
    /// </remarks>
    public bool TryExecuteTaskGroupInline(IDownloadTaskGroupBase taskGroup)
    {
        if (QueuedTasks.Contains(taskGroup) && taskGroup.CurrentState is DownloadState.Queued)
        {
            taskGroup.SubscribeProgress(_downloadTaskChannel.Writer);
            _ = _downloadTaskChannel.Writer.TryWrite(taskGroup.GetToken());
            return true;
        }

        return false;
    }

    private async Task DownloadAsync(ISingleDownloadTaskBase task)
    {
        await IncrementCounterAsync();
        _ = Download();

        return;

        async Task Download()
        {
            try
            {
                await task.StartAsync(_httpClient);
            }
            catch
            {
                // ignored
            }

            await DecrementCounterAsync();
        }
    }

    private async Task IncrementCounterAsync()
    {
        if (Interlocked.Increment(ref _workingTasks) == ConcurrencyDegree)
            await _throttle.ResetAsync();
    }

    private async Task DecrementCounterAsync()
    {
        _ = Interlocked.Decrement(ref _workingTasks);
        await _throttle.SetResultAsync(true);
    }
}
