using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Download;
using Pixeval.Tests.Ui;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class DownloadManagerReplacementTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task InitializationReturningAfterReplacementOrDisposalCannotStartOldChildren(bool dispose) =>
        UiTestSession.RunAsync(TestContext, async _ =>
        {
            using var client = new HttpClient();
            using var manager = new DownloadManager(client, 2);
            using var original = new ControlledGroup("same", blockInitialization: true);
            using var replacement = new ControlledGroup("same");
            manager.QueueTask(original);
            await original.InitializationEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                if (dispose)
                    manager.Dispose();
                else
                    manager.QueueTask(replacement);

                original.ReleaseInitialization.TrySetResult();
                await original.InitializationReturned.Task.WaitAsync(TimeSpan.FromSeconds(10));
                // The real manager resumes on this dispatcher. Drain its normal
                // continuation before checking that no old child was scheduled.
                await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
                Assert.AreEqual(0, original.Child.StartCount);
                if (!dispose)
                {
                    await replacement.Child.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.AreSame(replacement, manager.QueuedTasks[0]);
                    Assert.AreEqual(1, replacement.Child.StartCount);
                }
            }
            finally
            {
                original.ReleaseInitialization.TrySetResult();
            }
        });

    [TestMethod]
    public Task DisposingManagerDetachesPredecessorWaitAndCannotReviveReplacement() =>
        UiTestSession.RunAsync(TestContext, async _ =>
        {
            using var client = new HttpClient();
            using var manager = new DownloadManager(client, 2);
            // This fixture represents a cancelled provider that is still using
            // its files. No artificial downloader is run during manager teardown.
            using var original = new ControlledGroup("same", alreadyStopping: true);
            using var replacement = new ControlledGroup("same");
            using var independent = new ControlledGroup("independent");
            manager.QueueTask(original);
            manager.QueueTask(replacement);
            manager.QueueTask(independent);
            await independent.Child.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(1, original.ListenerCount);
            Assert.AreEqual(0, replacement.InitializationCount,
                "The independent task passes the waiting replacement in the actual queue.");

            manager.Dispose();
            await original.ListenerRemoved.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(0, original.ListenerCount, "Manager shutdown releases its predecessor subscription.");
            original.ReleaseStop();
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
            Assert.AreEqual(0, replacement.InitializationCount);
            Assert.AreEqual(0, replacement.Child.StartCount);
            Assert.IsTrue(replacement.WasDisposed);
        });

    private sealed class ControlledGroup : IDownloadTaskGroupBase
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly bool _blockInitialization;
        private PropertyChangedEventHandler? _propertyChanged;
        private int _listenerCount;
        private bool _initialized;

        internal ControlledGroup(string destination, bool blockInitialization = false, bool alreadyStopping = false)
        {
            Destination = destination;
            _blockInitialization = blockInitialization;
            CurrentState = alreadyStopping ? DownloadState.Cancelled : DownloadState.Queued;
            IsProcessing = alreadyStopping;
        }

        internal TaskCompletionSource InitializationEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseInitialization { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource InitializationReturned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ListenerRemoved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal RecordingChild Child { get; } = new();
        internal int InitializationCount { get; private set; }
        internal int ListenerCount => Volatile.Read(ref _listenerCount);
        internal bool WasDisposed { get; private set; }
        public DownloadTaskKey Key => DownloadTaskKey.CreateOrdinary(Destination);
        public string Destination { get; }
        public string OpenLocalDestination => Destination;
        public string? ErrorMessage => null;
        public DownloadState CurrentState { get; private set; }
        public bool IsProcessing { get; private set; }
        public double ProgressPercentage => 0;
        public int ActiveCount => 1;
        public int CompletedCount => 0;
        public int ErrorCount => 0;
        public int Count => _initialized ? 1 : 0;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { _propertyChanged += value; Interlocked.Increment(ref _listenerCount); }
            remove
            {
                _propertyChanged -= value;
                if (Interlocked.Decrement(ref _listenerCount) == 0)
                    ListenerRemoved.TrySetResult();
            }
        }

        public event PropertyChangingEventHandler? PropertyChanging { add { } remove { } }

        public async ValueTask InitializeTaskGroupAsync()
        {
            InitializationCount++;
            InitializationEntered.TrySetResult();
            if (_blockInitialization)
                await ReleaseInitialization.Task;
            _initialized = true;
            InitializationReturned.TrySetResult();
        }

        internal void ReleaseStop()
        {
            IsProcessing = false;
            _propertyChanged?.Invoke(this, new(nameof(IsProcessing)));
        }

        public void SubscribeProgress(ChannelWriter<DownloadToken> writer) { }
        public DownloadToken GetToken() => new(this, _cancellation.Token);
        public void Cancel() { _cancellation.Cancel(); CurrentState = DownloadState.Cancelled; }
        public void Reset() { }
        public void Pause() { }
        public void Resume() { }
        public void Delete() { }
        public void Dispose()
        {
            if (WasDisposed) return;
            WasDisposed = true;
            _cancellation.Cancel();
            _cancellation.Dispose();
        }

        // A late initialization can expose newly queued children even after the
        // group was cancelled/disposed. The manager must validate the old token.
        public IEnumerator<ISingleDownloadTaskBase> GetEnumerator() =>
            ((IEnumerable<ISingleDownloadTaskBase>) (_initialized ? [Child] : [])).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class RecordingChild : ISingleDownloadTaskBase
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int StartCount { get; private set; }
        public Task StartAsync(HttpClient httpClient)
        {
            StartCount++;
            CurrentState = DownloadState.Completed;
            Started.TrySetResult();
            return Task.CompletedTask;
        }

        public DownloadState CurrentState { get; private set; } = DownloadState.Queued;
        public string Destination => "unused";
        public string OpenLocalDestination => Destination;
        public string? ErrorMessage => null;
        public bool IsProcessing => false;
        public double ProgressPercentage => 0;
        public void Reset() { }
        public void Pause() { }
        public void Resume() { }
        public void Cancel() { }
        public void Delete() { }
    }
}
