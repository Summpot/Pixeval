using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Misaki;
using Pixeval.Controls;
using Pixeval.Download;
using Pixeval.Models.Download.Tasks;
using Pixeval.Models.Options;
using Pixeval.Tests.Ui;
using Pixeval.ViewModels;
using Pixeval.Views.Download;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class DownloadTaskGroupLifecycleTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task RestartWaitsUntilEveryChildHasStopped(bool cancel) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, cancel));

    private static async Task RunAsync(string directory, bool cancel)
    {
        using var fixture = new IllustrationReaderFixture();
        var history = App.AppViewModel.HistoryPersistHelper;
        await history.RestoreTask;
        history.DownloadManager.ConcurrencyDegree = 2;
        var source = Path.Combine(directory, "source.txt");
        File.WriteAllText(source, "complete child output");
        var group = new ControlledGroup(IllustrationReaderFixture.Sample(996495, 2), directory, source);
        var page = new DownloadPage();
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 700, Height = 700, Content = host.Shell };
        try
        {
            window.Show();
            history.DownloadManager.QueueTask(group);
            await Task.WhenAll(group.Gates.Select(gate => gate.Entered.Task)).WaitAsync(TimeSpan.FromSeconds(10));
            await RenderAsync(window);
            var row = page.GetVisualDescendants().OfType<DownloadItem>().Single(control =>
                control.DataContext is DownloadItemViewModel vm && ReferenceEquals(vm.DownloadTask, group));
            var primary = row.FindControl<CommandBarButton>("PrimaryActionButton")!;
            if (cancel)
                group.Cancel();
            else
                await ActivateAsync(window, primary);
            await RenderAsync(window);
            var stoppedState = cancel ? DownloadState.Cancelled : DownloadState.Paused;
            var token = group.GetToken().Token;
            Require(group.CurrentState == stoppedState && token.IsCancellationRequested,
                "Stopping the group retains its cancelled attempt token");
            Require(group.All(child => child.IsProcessing) && group.IsProcessing && !primary.IsEffectivelyEnabled,
                "The actual group row stays busy while its children are stopping");
            group.Resume();
            group.Reset();
            Require(group.CurrentState == stoppedState && group.GetToken().Token == token
                && group.All(child => child.CurrentState == stoppedState),
                "Premature resume/retry cannot replace the group token or partially queue its children");
            group.Gates[0].Released.SetResult();
            await UntilAsync(() => !group.Children[0].IsProcessing);
            await RenderAsync(window);
            Require(group.IsProcessing && !primary.IsEffectivelyEnabled && group.Children[1].IsProcessing,
                "One stopped child cannot enable the group while another still owns its input");
            group.Gates[1].Released.SetResult();
            await UntilAsync(() => !group.IsProcessing);
            await RenderAsync(window);
            Require(group.CurrentState == stoppedState && primary.IsEffectivelyEnabled,
                "The actual row receives the final child's busy-state change and enables restart");
            Save(window, directory, "group-stopped");
            await ActivateAsync(window, primary);
            await UntilAsync(() => group.CurrentState == DownloadState.Completed);
            Require(!group.GetToken().Token.IsCancellationRequested && group.Gates.All(gate => gate.Attempts == 2)
                && group.Destinations.All(path => File.ReadAllText(path) == "complete child output"),
                "The enabled row restarts every child exactly once and commits complete outputs");
            File.WriteAllText(Path.Combine(directory, "group-lifetime-contracts.txt"),
                $"PASS cancel={cancel}: two controlled child stops, stable token, actual row busy/ready, complete restart");
            host.Shell.ClosePage(page);
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "The group lifecycle fixture stays offline");
        }
        finally
        {
            group.Cancel();
            foreach (var gate in group.Gates) gate.Released.TrySetResult();
            await UntilAsync(() => group.Children.All(child => !child.IsProcessing));
            window.Close();
        }
    }

    private sealed class ControlledGroup : DownloadTaskGroup
    {
        internal Gate[] Gates { get; } = [new(), new()];
        internal ImageDownloadTask[] Children { get; }

        internal ControlledGroup(IArtworkInfo entry, string directory, string source)
            : base(entry, Path.Combine(directory, "group"))
        {
            Children = Enumerable.Range(0, 2).Select(index =>
                new ImageDownloadTask(new Uri(source), Path.Combine(directory, $"child-{index}.txt"))).ToArray();
            for (var index = 0; index < Children.Length; index++)
            {
                var gate = Gates[index];
                AddToTasksSet(Children[index]);
                // The final subscriber gates the real task before its first file read.
                Children[index].DownloadStartedAsync += async _ =>
                {
                    if (Interlocked.Increment(ref gate.Attempts) != 1) return;
                    gate.Entered.SetResult();
                    await gate.Released.Task;
                };
            }
        }

        public override ValueTask InitializeTaskGroupAsync() => ValueTask.CompletedTask;
        public override string OpenLocalDestination => TokenizedDestination;
        protected override Task AfterAllDownloadAsyncOverride(DownloadTaskGroup sender, CancellationToken token = default) => Task.CompletedTask;
        public override void Delete() { foreach (var child in Children) child.Delete(); }
    }

    private sealed class Gate
    {
        internal int Attempts;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
