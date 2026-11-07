using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ContentDialogHostLifetimeTest
{
    [TestMethod]
    public async Task QueuedCancellationDoesNotCloseTheNextShow()
    {
        await RunAsync(async (window, host) =>
        {
            using var cancellation = new CancellationTokenSource();
            var dialog = new ContentDialog();
            Task<ContentDialogResult>? nextShow = null;
            var closed = 0;
            dialog.Closed += (_, _) =>
            {
                if (++closed is 1)
                    nextShow = dialog.ShowAsync(host);
            };
            var shown = dialog.ShowAsync(host, cancellation.Token);
            _ = await dialog.HideAsync(ContentDialogResult.Primary);
            // The old show continuation is queued before this cancellation callback.
            cancellation.Cancel();
            Assert.AreEqual(ContentDialogResult.Primary, await shown.WaitAsync(TimeSpan.FromSeconds(2)));
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Assert.IsNotNull(nextShow);
            Assert.IsFalse(nextShow.IsCompleted);
            _ = await dialog.HideAsync(ContentDialogResult.Secondary);
            Assert.AreEqual(ContentDialogResult.Secondary, await nextShow.WaitAsync(TimeSpan.FromSeconds(2)));
        });
    }

    [TestMethod]
    public async Task ClosingTheWindowCompletesEveryDialogAndDetachesTheHost()
    {
        await RunAsync(async (window, host) =>
        {
            var parent = new ContentDialog();
            var child = new ContentDialog();
            var closed = 0;
            parent.Closed += (_, _) => closed++;
            child.Closed += (_, _) => closed++;
            // A dead window cannot keep a modal operation alive through a close veto.
            parent.Closing += (_, args) => args.Cancel = true;
            var first = parent.ShowAsync(host);
            var second = child.ShowAsync(host);

            window.Close();

            Assert.AreEqual(ContentDialogResult.None, await first.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.AreEqual(ContentDialogResult.None, await second.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.AreEqual(2, closed);
            Assert.IsEmpty(host.Dialogs);
            Assert.IsFalse(host.IsOpen);
            Assert.IsNull(AdornerLayer.GetAdornedElement(host));
            _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new ContentDialog().ShowAsync(host));
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ClosingTheWindowReleasesDeferralsAndIsolatesLateCompletion(bool deferClosing)
    {
        await RunAsync(async (window, host) =>
        {
            var dialog = new ContentDialog();
            ContentDialogDeferral? deferral = null;
            var shouldDefer = true;
            var closingCalls = 0;
            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (shouldDefer && !deferClosing)
                    deferral = args.GetDeferral();
            };
            dialog.Closing += (_, args) =>
            {
                closingCalls++;
                if (shouldDefer && deferClosing)
                    deferral = args.GetDeferral();
            };
            var shown = dialog.ShowAsync(host);
            var hide = dialog.HideAsync(ContentDialogResult.Primary);
            Assert.IsNotNull(deferral);
            Assert.IsFalse(hide.IsCompleted);
            window.Close();
            Assert.AreEqual(ContentDialogResult.None, await shown.WaitAsync(TimeSpan.FromSeconds(2)));
            _ = await hide.WaitAsync(TimeSpan.FromSeconds(2));

            var replacement = new Window { Width = 640, Height = 480, Content = new Border() };
            try
            {
                replacement.Show();
                replacement.UpdateLayout();
                var nextHost = new ContentDialogHost(replacement);
                shouldDefer = false;
                var nextShow = dialog.ShowAsync(nextHost);
                var previousCalls = closingCalls;
                deferral.Complete();
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                Assert.IsFalse(nextShow.IsCompleted);
                Assert.AreEqual(previousCalls, closingCalls);
                _ = await dialog.HideAsync(ContentDialogResult.Secondary);
                Assert.AreEqual(ContentDialogResult.Secondary, await nextShow.WaitAsync(TimeSpan.FromSeconds(2)));
            }
            finally
            {
                replacement.Close();
            }
        });
    }

    private static async Task RunAsync(Func<Window, ContentDialogHost, Task> verify)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.DispatchAsync(async () =>
        {
            var window = new Window { Width = 640, Height = 480, Content = new Border() };
            try
            {
                window.Show();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                await verify(window, new ContentDialogHost(window));
            }
            finally
            {
                window.Close();
            }
        });
    }
}
