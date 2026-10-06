// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Native.Download;

namespace Pixeval.Tests;

[TestClass]
public sealed class DownloadEngineTest
{
    private sealed class TestProgressCallback : IDownloadProgressCallback
    {
        public TaskCompletionSource<DownloadState>? StateChangedSource { get; set; }
        public Action<DownloadState, string?>? StateChangedCallback { get; set; }
        public DownloadTaskKey ExpectedKey { get; set; } = null!;

        public void OnProgress(DownloadTaskKey key, double progressPercentage, ulong downloadedBytes, ulong totalBytes)
        {
        }

        public void OnStateChanged(DownloadTaskKey key, DownloadState state, string? errorMessage)
        {
            if (key.Equals(ExpectedKey))
            {
                StateChangedCallback?.Invoke(state, errorMessage);
                if (state == DownloadState.Cancelled)
                {
                    StateChangedSource?.TrySetResult(state);
                }
            }
        }

        public void OnCompleted(DownloadTaskKey key, string destination)
        {
            if (key.Equals(ExpectedKey))
            {
                StateChangedCallback?.Invoke(DownloadState.Completed, null);
            }
        }
    }

    [TestMethod]
    public void DownloadManagerLifecycleShouldWork()
    {
        using var manager = new DownloadManager(concurrencyDegree: 2, callback: null, networkOptions: null);
        var key = new DownloadTaskKey("test_destination.png", 0, "12345");

        Assert.IsFalse(manager.HasTask(key));
        Assert.IsNull(manager.GetTaskInfo(key));

        manager.EnqueueTask(key, "https://example.com/test.png", "test_destination.png", true);

        Assert.IsTrue(manager.HasTask(key));
        var info = manager.GetTaskInfo(key);
        Assert.IsNotNull(info);
        Assert.AreEqual(key, info.Key);

        Assert.IsTrue(manager.CancelTask(key));
        var cancelledInfo = manager.GetTaskInfo(key);
        Assert.IsNotNull(cancelledInfo);
        Assert.AreEqual(DownloadState.Cancelled, cancelledInfo.State);

        Assert.IsTrue(manager.RemoveTask(key));
        Assert.IsFalse(manager.HasTask(key));
    }

    [TestMethod]
    public async Task DownloadManagerCallbacksShouldBeInvoked()
    {
        var callback = new TestProgressCallback();
        using var manager = new DownloadManager(concurrencyDegree: 1, callback: callback, networkOptions: null);
        var key = new DownloadTaskKey("callback_test.png", 0, "99999");
        callback.ExpectedKey = key;

        var stateChangedEvent = new TaskCompletionSource<DownloadState>();
        callback.StateChangedSource = stateChangedEvent;

        manager.EnqueueTask(key, "https://example.com/image.png", "callback_test.png", true);
        manager.CancelTask(key);

        using var cts = new CancellationTokenSource(5000);
        cts.Token.Register(() => stateChangedEvent.TrySetCanceled());

        var resultState = await stateChangedEvent.Task;
        Assert.AreEqual(DownloadState.Cancelled, resultState);
    }

    [TestMethod]
    public void DownloadManagerNetworkOptionsShouldWork()
    {
        var options = new DownloadNetworkOptions("http://127.0.0.1:7890", new Dictionary<string, List<string>>
        {
            ["i.pximg.net"] = ["210.140.139.134"]
        });
        using var manager = new DownloadManager(concurrencyDegree: 2, callback: null, networkOptions: options);
        manager.UpdateNetworkOptions(null);
    }

    [TestMethod]
    public async Task DownloadManagerActualDownloadTest()
    {
        using var listener = new System.Net.HttpListener();
        var port = Random.Shared.Next(20000, 30000);
        var prefix = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(prefix);
        listener.Start();

        var listenerTask = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            var response = context.Response;
            var data = new byte[1024];
            Random.Shared.NextBytes(data);
            response.ContentLength64 = data.Length;
            await response.OutputStream.WriteAsync(data);
            response.Close();
        });

        var callback = new TestProgressCallback();
        using var manager = new DownloadManager(concurrencyDegree: 1, callback: callback, networkOptions: null);
        var tempFile = System.IO.Path.GetTempFileName();
        var key = new DownloadTaskKey(tempFile, 0, "136725422");
        callback.ExpectedKey = key;
        var stateTcs = new TaskCompletionSource<(DownloadState State, string? Error)>();
        callback.StateChangedCallback = (state, err) =>
        {
            if (state is DownloadState.Completed or DownloadState.Error)
                stateTcs.TrySetResult((state, err));
        };

        manager.EnqueueTask(key, $"{prefix}test.jpg", tempFile, true);

        using var cts = new CancellationTokenSource(15000);
        cts.Token.Register(() => stateTcs.TrySetCanceled());

        var (resultState, error) = await stateTcs.Task;
        await listenerTask;
        Assert.AreEqual(DownloadState.Completed, resultState, error);
        Assert.AreEqual(1024, new System.IO.FileInfo(tempFile).Length);
        System.IO.File.Delete(tempFile);
    }
}
