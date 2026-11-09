using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Download;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.Downloaders;
using Pixeval.Models.Database;
using Pixeval.Models.Download.Tasks;
using Pixeval.Models.Extensions;
using Pixeval.Utilities;
using Pixeval.Utilities.IO;
using Pixeval.ViewModels;
using Pixeval.Views.Download;
using SQLite;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class DownloaderExtensionLifecycleTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public Task DownloaderPauseOrCancelWaitsForAcknowledgement(bool cancel, bool lateFailure) =>
        UiTestSession.RunAsync(TestContext, directory => RunInterruptedAsync(directory, cancel, lateFailure));

    [TestMethod]
    [DataRow("success")]
    [DataRow("failure")]
    [DataRow("abort-null")]
    [DataRow("bad-error")]
    [DataRow("duplicate-bad-error")]
    [DataRow("throw")]
    public Task DownloaderTerminalResultAndRetry(string result) =>
        UiTestSession.RunAsync(TestContext, directory => RunTerminalAsync(directory, result));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task RepeatedSaveWaitsForThePreviousDownloader(bool lateFailure) =>
        UiTestSession.RunAsync(TestContext, directory => RunReplacementAsync(directory, lateFailure));

    [TestMethod]
    public Task DownloaderAbortOwnsTheResultWhileReadingExternalErrorMetadata() =>
        UiTestSession.RunAsync(TestContext, async directory =>
        {
            await using var fixture = new Fixture(directory);
            var task = await fixture.SaveAsync(997605);
            var request = await fixture.NextRequestAsync(1);
            using var error = new DelayedException();
            var abort = Task.Run(() => request.FinishWithException(error));
            try
            {
                await error.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                request.Notifier.Completed();
                request.Notifier.ProgressChanged(99);
                await RenderAsync(fixture.Window);
                Require(task.CurrentState == DownloadState.Running && !File.Exists(task.Destination),
                    "A later success cannot overtake an abort still reading the external error metadata");
            }
            finally
            {
                error.Release.Set();
                await abort.WaitAsync(TimeSpan.FromSeconds(10));
            }
            await UntilAsync(() => task.CurrentState == DownloadState.Error && !task.IsProcessing);
            Require(task.ErrorMessage?.Contains("Controlled delayed downloader error", StringComparison.Ordinal) == true
                && !File.Exists(task.Destination) && !File.Exists(request.Destination),
                "The original abort reports its own error and releases partial output");
            RequirePersisted(task, DownloadState.Error);
            fixture.Report.Add("PASS competing callbacks during external exception metadata: first abort owns the result");
        });

    private static async Task RunInterruptedAsync(string directory, bool cancel, bool lateFailure)
    {
        await using var fixture = new Fixture(directory);
        var task = await fixture.SaveAsync(997601);
        var request = await fixture.NextRequestAsync(1);
        await RenderAsync(fixture.Window);
        var row = fixture.Row(task);
        var primary = row.FindControl<CommandBarButton>("PrimaryActionButton")!;
        request.Notifier.ProgressChanged(23);
        if (cancel)
        {
            var bar = row.FindControl<CommandBar>("ItemCommands")!;
            await ActivateAsync(fixture.Window, (CommandBarButton) bar.SecondaryCommands[1]);
        }
        else
            await ActivateAsync(fixture.Window, primary);

        var interruptedState = cancel ? DownloadState.Cancelled : DownloadState.Paused;
        var token = task.GetToken().Token;
        fixture.Report.Add($"Before acknowledgement: state={task.CurrentState}, processing={task.IsProcessing}, primaryEnabled={primary.IsEffectivelyEnabled}");
        Require(task.CurrentState == interruptedState && token.IsCancellationRequested && task.IsProcessing
            && !primary.IsEffectivelyEnabled, "The real download row must wait for the external writer before enabling restart");
        task.Reset();
        task.Resume();
        Require(task.CurrentState == interruptedState && task.GetToken().Token == token && fixture.Downloader.Requests.Count == 1,
            "Stale restart calls cannot replace the token or reuse an unacknowledged downloader's file");
        request.Notifier.ProgressChanged(87);
        Require(task.ProgressPercentage == 23, "Progress from a cancelled or paused downloader cannot update the interrupted attempt");
        Require(request.Stream.CanWrite && File.Exists(request.Destination), "The external writer keeps its temporary file until acknowledgement");
        await Task.Run(() => request.Finish(lateFailure));
        await UntilAsync(() => !task.IsProcessing);
        Require(task.CurrentState == interruptedState && task.ErrorMessage is null && !File.Exists(task.Destination),
            "Late success/failure cannot publish an interrupted downloader's output or change its state");
        Require(!File.Exists(request.Destination), "Acknowledged interrupted downloads release their temporary files");
        RequirePersisted(task, interruptedState);
        await ActivateAsync(fixture.Window, primary);
        var retry = await fixture.NextRequestAsync(2);
        retry.Notifier.ProgressChanged(31);
        request.Notifier.ProgressChanged(95);
        request.Notifier.Completed();
        request.Notifier.Aborted(new InvalidOperationException("Duplicate old notification"));
        Require(task.ProgressPercentage == 31 && task.CurrentState == DownloadState.Running,
            "Old notifier callbacks cannot mutate the resumed or retried attempt");
        await Task.Run(() => retry.Finish(false));
        await UntilAsync(() => task.CurrentState == DownloadState.Completed && !task.IsProcessing);
        Require(File.ReadAllText(task.Destination) == retry.Output, "The real retry commits only its own complete output");
        RequirePersisted(task, DownloadState.Completed);
        fixture.Report.Add($"PASS {interruptedState}/lateFailure={lateFailure}: writer acknowledgement, disabled restart, stale callback isolation, temporary cleanup, retry and SQLite state");
        Save(fixture.Window, directory, "downloader-retried");
    }

    private static async Task RunTerminalAsync(string directory, string result)
    {
        await using var fixture = new Fixture(directory);
        fixture.Downloader.ThrowOnDownload = result == "throw";
        var task = await fixture.SaveAsync(997602);
        if (result == "throw")
            await UntilAsync(() => task.CurrentState == DownloadState.Error && !task.IsProcessing);
        else
        {
            var request = await fixture.NextRequestAsync(1);
            // The first terminal notification owns the result, even if a broken
            // provider immediately follows it with the opposite notification.
            await Task.Run(() =>
            {
                request.Finish(result is not ("success" or "duplicate-bad-error"), result == "abort-null", result == "bad-error");
                if (result == "duplicate-bad-error") request.Notifier.Aborted(new BrokenException());
                else if (result == "success") request.Notifier.Aborted(new InvalidOperationException("Duplicate abort"));
                else request.Notifier.Completed();
                request.Notifier.ProgressChanged(9);
            });
            await UntilAsync(() => (task.CurrentState is DownloadState.Completed or DownloadState.Error) && !task.IsProcessing);
        }
        var expected = result is "success" or "duplicate-bad-error" ? DownloadState.Completed : DownloadState.Error;
        Require(task.CurrentState == expected, "The first terminal notification must decide success/error: " + result + "/" + task.CurrentState);
        RequirePersisted(task, expected);
        Require(!File.Exists(task.Destination + IoHelper.PixevalTempExtension), "Terminal acknowledgement releases the temporary file");
        if (expected == DownloadState.Error)
        {
            Require(!File.Exists(task.Destination) && task.ErrorMessage is not null, "A failed downloader does not commit partial bytes");
            fixture.Downloader.ThrowOnDownload = false;
            await RenderAsync(fixture.Window);
            await ActivateAsync(fixture.Window, fixture.Row(task).FindControl<CommandBarButton>("PrimaryActionButton")!);
            var retry = await fixture.NextRequestAsync(result == "throw" ? 1 : 2);
            await Task.Run(() => retry.Finish(false));
            await UntilAsync(() => task.CurrentState == DownloadState.Completed && !task.IsProcessing);
            Require(task.ErrorMessage is null && File.ReadAllText(task.Destination) == retry.Output, "The real error-row retry clears the failure and commits new bytes");
            RequirePersisted(task, DownloadState.Completed);
        }
        fixture.Report.Add($"PASS {result}: first terminal callback, duplicate isolation, partial cleanup, error feedback and real retry");
    }

    private static async Task RunReplacementAsync(string directory, bool lateFailure)
    {
        await using var fixture = new Fixture(directory);
        var old = await fixture.SaveAsync(997603);
        var oldRequest = await fixture.NextRequestAsync(1);
        var replacement = await fixture.SaveAsync(997603);
        var independent = await fixture.SaveAsync(997604);
        await UntilAsync(() => fixture.Downloader.Requests.Any(request => request.Destination.Contains("997604", StringComparison.Ordinal))
            || replacement.CurrentState == DownloadState.Error);
        fixture.Report.Add($"Replacement while writer pending: old={old.CurrentState}/{old.IsProcessing}, replacement={replacement.CurrentState}, providerCalls={fixture.Downloader.Requests.Count}");
        Require(old.CurrentState == DownloadState.Cancelled && old.IsProcessing && replacement.CurrentState == DownloadState.Queued,
            "Repeated SaveCommand keeps the replacement queued until the old external writer acknowledges");
        var independentRequest = fixture.Downloader.Requests.Single(request => request.Destination.Contains("997604", StringComparison.Ordinal));
        await Task.Run(() => independentRequest.Finish(false));
        await UntilAsync(() => independent.CurrentState == DownloadState.Completed && !independent.IsProcessing);
        Require(replacement.CurrentState == DownloadState.Queued && oldRequest.Stream.CanWrite,
            "An unrelated download completes without reusing the blocked destination");
        await Task.Run(() => oldRequest.Finish(lateFailure));
        var latest = await fixture.NextRequestAsync(3);
        Require(!ReferenceEquals(latest, independentRequest) && latest.Destination == oldRequest.Destination,
            "The replacement starts only after acknowledgement and temporary cleanup");
        Require(old.CurrentState == DownloadState.Cancelled && old.ErrorMessage is null && !old.IsProcessing,
            "The old writer's late terminal callback remains cancelled");
        await Task.Run(() => latest.Finish(false));
        await UntilAsync(() => replacement.CurrentState == DownloadState.Completed && !replacement.IsProcessing);
        Require(File.ReadAllText(replacement.Destination) == latest.Output, "The newest output survives old writer cleanup");
        RequirePersisted(replacement, DownloadState.Completed);
        fixture.Report.Add($"PASS replacement/lateFailure={lateFailure}: real SaveCommand, old writer lifetime, independent progress, newest output and SQLite identity");
    }

    private static void RequirePersisted(SingleImageDownloadTaskGroupBase task, DownloadState state)
    {
        var database = App.AppViewModel.AppServiceProvider.GetRequiredService<SQLiteConnection>();
        using var reopened = new SQLiteConnection(database.DatabasePath, SQLiteOpenFlags.ReadOnly);
        var destination = task.DatabaseEntry.Destination;
        var record = reopened.Table<DownloadHistoryEntry>().Where(entry => entry.Destination == destination).ToList().Single();
        Require(record.HistoryEntryId == task.DatabaseEntry.HistoryEntryId && record.State == state
            && record.ErrorMessage == task.ErrorMessage && record.FormatToken == task.DatabaseEntry.FormatToken,
            "Independent SQLite read preserves downloader state, identity, error and format");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _directory;
        private readonly IllustrationReaderFixture _images = new();
        private readonly ExtensionService _extensions = App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();
        private readonly ExtensionsHostModel _extensionHost;
        private readonly DownloadPage _page = new();
        private readonly List<SingleImageDownloadTaskGroupBase> _attempts = [];
        internal Downloader Downloader { get; } = new();
        internal List<string> Report { get; } = [];
        internal Window Window { get; }

        internal Fixture(string directory)
        {
            _directory = directory;
            _extensionHost = new ExtensionsHostModel(new Host(Downloader), []);
            _extensions.HostModels.Add(_extensionHost);
            var settings = App.AppViewModel.AppSettings.DownloadSettings;
            settings.OverwriteDownloadedFile = true;
            settings.DownloadPathMacro = Path.Combine(directory, "downloads", "@{id}.@{ext}");
            var host = new PageTestHost { Content = _page };
            Window = new Window { Width = 800, Height = 850, Content = host.Shell };
            Window.Show();
        }

        internal async Task<SingleImageDownloadTaskGroupBase> SaveAsync(long id)
        {
            var history = App.AppViewModel.HistoryPersistHelper;
            await history.RestoreTask;
            // Keep the original distinct from the thumbnail cache so the real
            // ActiveDownloaders branch must own the transfer.
            var entry = IllustrationReaderFixture.Sample(id, 1) with
            {
                MetaSinglePage = new() { OriginalImageUrl = $"https://i.pximg.net/m3e/downloader-{id}.png" }
            };
            await new IllustrationItemViewModel(entry).SaveCommand.ExecuteAsync(null);
            var task = history.DownloadManager.QueuedTasks.OfType<SingleImageDownloadTaskGroupBase>().Single(candidate => candidate.Id == id.ToString());
            _attempts.Add(task);
            return task;
        }

        internal async Task<Downloader.Request> NextRequestAsync(int count)
        {
            await UntilAsync(() => Downloader.Requests.Count >= count);
            return Downloader.Requests[count - 1];
        }

        internal DownloadItem Row(SingleImageDownloadTaskGroupBase task) => _page.GetVisualDescendants().OfType<DownloadItem>().Single(control =>
            control.DataContext is DownloadItemViewModel model && ReferenceEquals(model.DownloadTask, task));

        public async ValueTask DisposeAsync()
        {
            foreach (var task in _attempts) task.Cancel();
            foreach (var request in Downloader.Requests.Where(request => !request.Finished)) request.Finish(false);
            await UntilAsync(() => _attempts.All(task => !task.IsProcessing));
            File.WriteAllLines(Path.Combine(_directory, "downloader-contracts.txt"), Report);
            Window.Close();
            _extensions.HostModels.Remove(_extensionHost);
            _extensionHost.Dispose();
            _images.Dispose();
        }
    }

    private sealed class Downloader : IDownloaderExtension
    {
        private readonly ConcurrentQueue<Request> _requests = new();
        internal IReadOnlyList<Request> Requests => _requests.ToArray();
        internal bool ThrowOnDownload { get; set; }

        public void Download(IProgressNotifier notifier, string uri, string destination)
        {
            if (ThrowOnDownload)
            {
                File.WriteAllText(destination, "Partial synchronous download");
                throw new InvalidOperationException("Synchronous downloader failure");
            }
            var request = new Request(notifier, destination, _requests.Count + 1);
            _requests.Enqueue(request);
        }
        public void OnExtensionLoaded() { }
        public void OnExtensionUnloaded() { }

        internal sealed class Request
        {
            internal IProgressNotifier Notifier { get; }
            internal string Destination { get; }
            internal FileStream Stream { get; }
            internal string Output { get; }
            internal bool Finished { get; private set; }

            internal Request(IProgressNotifier notifier, string destination, int index)
            {
                Notifier = notifier;
                Destination = destination;
                Output = "Acknowledged download " + index;
                Stream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.Read);
                Stream.Write(Encoding.UTF8.GetBytes("Partial download"));
                Stream.Flush();
            }

            internal void Finish(bool failure, bool noException = false, bool brokenException = false)
            {
                ReleaseOutput();
                if (failure && brokenException) Notifier.Aborted(new BrokenException());
                else if (failure && noException) Notifier.Aborted((IException?) null);
                else if (failure) Notifier.Aborted(new InvalidOperationException("Downloader reported failure"));
                else Notifier.Completed();
            }

            internal void FinishWithException(IException error)
            {
                ReleaseOutput();
                Notifier.Aborted(error);
            }

            private void ReleaseOutput()
            {
                Require(!Finished && Stream.CanWrite, "The downloader holds its output until terminal acknowledgement");
                Stream.Position = 0;
                Stream.SetLength(0);
                Stream.Write(Encoding.UTF8.GetBytes(Output));
                Stream.Dispose();
                Finished = true;
            }
        }
    }

    private sealed class BrokenException : IException
    {
        public string GetTypeName() => "BrokenException";
        public string GetMessage() => throw new InvalidOperationException("Downloader error metadata failed");
        public string? GetSource() => null;
        public string? GetStackTrace() => null;
        public string? GetHelpLink() => null;
        public IException? GetInnerException() => null;
    }

    private sealed class DelayedException : IException, IDisposable
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new();
        public string GetTypeName() => "DelayedException";
        public string GetMessage()
        {
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Error metadata was not released");
            return "Controlled delayed downloader error";
        }
        public string? GetSource() => null;
        public string? GetStackTrace() => null;
        public string? GetHelpLink() => null;
        public IException? GetInnerException() => null;
        public void Dispose() => Release.Dispose();
    }

    private sealed class Host(IExtension downloader) : IExtensionsHost
    {
        public string GetExtensionName() => "Downloader lifecycle fixture";
        public string GetAuthorName() => "Pixeval tests";
        public string GetExtensionLink() => "";
        public string GetHelpLink() => "";
        public string GetDescription() => "Controlled external downloader";
        public string GetSdkVersion() => ExtensionService.CurrentVersion;
        public string GetVersion() => "1.0";
        public IExtension[] GetExtensions(out int count) { count = 1; return [downloader]; }
        public byte[]? GetIcon(out int count) { count = 0; return null; }
        public void Initialize(string cultureName, string tempDirectory, string extensionDirectory, ILogger logger) { }
    }
}
