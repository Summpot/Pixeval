using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Download;
using Pixeval.Models.Download.Tasks;

namespace Pixeval.Tests;

[TestClass]
public sealed class ImageDownloadTaskLifecycleTests
{
    [TestMethod]
    public async Task RetryFromErrorCallbackIsQueuedUntilErrorCleanupFinishes()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var source = Path.Combine(directory, "initially-missing.txt");
            var destination = Path.Combine(directory, "output.txt");
            using var task = new TestImageTask(new Uri(source), destination);
            using var client = new HttpClient();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            task.DownloadErrorAsync += async _ =>
            {
                entered.SetResult();
                await released.Task;
            };
            var first = task.StartAsync(client);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                Assert.AreEqual(DownloadState.Error, task.CurrentState);
                await File.WriteAllTextAsync(source, "recovered");
                task.Reset();
                Assert.AreEqual(DownloadState.Queued, task.CurrentState);
                var second = task.StartAsync(client);
                Assert.IsFalse(second.IsCompleted);
                released.SetResult();
                await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
                Assert.AreEqual(DownloadState.Completed, task.CurrentState);
                Assert.IsNull(task.ErrorMessage);
                Assert.AreEqual("recovered", await File.ReadAllTextAsync(destination));
            }
            finally
            {
                released.TrySetResult();
                await first;
            }
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [TestMethod]
    public async Task PauseKeepsResumeDisabledUntilThePreviousDownloadStops()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var source = Path.Combine(directory, "source.txt");
            var destination = Path.Combine(directory, "output.txt");
            await File.WriteAllTextAsync(source, "content");
            using var task = new TestImageTask(new Uri(source), destination);
            using var client = new HttpClient();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            task.DownloadStartedAsync += async _ =>
            {
                entered.TrySetResult();
                await released.Task;
            };
            var first = task.StartAsync(client);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                task.Pause();
                Assert.AreEqual(DownloadState.Paused, task.CurrentState);
                Assert.IsTrue(task.IsProcessing);
                task.Resume();
                Assert.AreEqual(DownloadState.Paused, task.CurrentState, "A stale resume cannot replace the active attempt's cancellation token.");
                released.SetResult();
                await first.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.IsFalse(task.IsProcessing);
                Assert.AreEqual(DownloadState.Paused, task.CurrentState);
                Assert.IsFalse(File.Exists(destination));
                task.Resume();
                await task.StartAsync(client);
                Assert.AreEqual(DownloadState.Completed, task.CurrentState);
                Assert.AreEqual("content", await File.ReadAllTextAsync(destination));
            }
            finally
            {
                released.TrySetResult();
                await first;
            }
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RetryFromFinalChildCallbackWaitsForPreviousAttemptToUnwind(bool lateFailure)
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var source = Path.Combine(directory, "source.txt");
            var destination = Path.Combine(directory, "output.txt");
            await File.WriteAllTextAsync(source, "first");
            using var task = new TestImageTask(new Uri(source), destination);
            using var client = new HttpClient();
            var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbacks = 0;
            task.AfterDownloadAsync += async (_, _) =>
            {
                if (++callbacks != 1) return;
                callbackEntered.SetResult();
                await callbackReleased.Task;
                if (lateFailure) throw new InvalidOperationException("Old completion callback failed");
            };
            var first = task.StartAsync(client);
            await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                Assert.AreEqual(DownloadState.Completed, task.CurrentState);
                await File.WriteAllTextAsync(source, "second");
                task.Reset();
                Assert.AreEqual(DownloadState.Queued, task.CurrentState, "The group retry can reset its last completed child while its callback returns.");
                var second = task.StartAsync(client);
                Assert.IsFalse(second.IsCompleted, "The queued retry waits rather than being discarded or overlapping the old run.");
                Assert.AreEqual(1, callbacks);
                callbackReleased.SetResult();
                await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
                Assert.AreEqual(2, callbacks);
                Assert.AreEqual(DownloadState.Completed, task.CurrentState);
                Assert.AreEqual("second", await File.ReadAllTextAsync(destination));
                Assert.IsNull(task.ErrorMessage, "A late old callback failure cannot overwrite the queued attempt.");
            }
            finally
            {
                callbackReleased.TrySetResult();
                await first;
            }
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private sealed class TestImageTask(Uri uri, string destination) : ImageDownloadTask(uri, destination)
    {
        protected override bool OverwriteDownloadedFile => true;
    }
}
