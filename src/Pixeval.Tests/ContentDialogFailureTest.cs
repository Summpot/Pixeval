using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ContentDialogFailureTest
{
    [TestMethod]
    [DataRow(ContentDialogResult.Primary)]
    [DataRow(ContentDialogResult.Secondary)]
    [DataRow(ContentDialogResult.Close)]
    public async Task ButtonHandlerFailureReachesCallerAndAllowsRetry(ContentDialogResult result)
    {
        await RunAsync(async host =>
        {
            var dialog = new ContentDialog();
            var shown = dialog.ShowAsync(host);
            var attempts = 0;
            void FailFirstAttempt(ContentDialog _, ContentDialogButtonClickEventArgs args)
            {
                if (++attempts is 1)
                    throw new InvalidOperationException("fixture button failure");
            }
            dialog.PrimaryButtonClick += FailFirstAttempt;
            dialog.SecondaryButtonClick += FailFirstAttempt;
            dialog.CloseButtonClick += FailFirstAttempt;

            var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => dialog.HideAsync(result));
            Assert.AreEqual("fixture button failure", exception.Message);
            Assert.IsFalse(shown.IsCompleted);
            Assert.IsTrue(host.IsOpen);

            _ = await dialog.HideAsync(result);
            Assert.AreEqual(result, await shown.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.AreEqual(2, attempts);
            Assert.IsFalse(host.IsOpen);
        });
    }

    [TestMethod]
    public async Task ClosingHandlerFailureReachesCallerAndAllowsRetry()
    {
        await RunAsync(async host =>
        {
            var dialog = new ContentDialog();
            var shown = dialog.ShowAsync(host);
            var attempts = 0;
            dialog.Closing += (_, _) =>
            {
                if (++attempts is 1)
                    throw new InvalidOperationException("fixture closing failure");
            };

            _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => dialog.HideAsync());
            Assert.IsFalse(shown.IsCompleted);
            _ = await dialog.HideAsync();

            Assert.AreEqual(ContentDialogResult.None, await shown.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.AreEqual(2, attempts);
            Assert.IsEmpty(host.Dialogs);
        });
    }

    [TestMethod]
    public async Task ClosedHandlerFailureDoesNotPoisonTheNextShow()
    {
        await RunAsync(async host =>
        {
            var dialog = new ContentDialog();
            var shown = dialog.ShowAsync(host);
            var closed = 0;
            dialog.Closed += (_, _) =>
            {
                if (++closed is 1)
                    throw new InvalidOperationException("fixture closed failure");
            };
            _ = await dialog.HideAsync(ContentDialogResult.Primary);
            _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => shown.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.IsFalse(host.IsOpen);
            Assert.IsEmpty(host.Dialogs);

            var secondShow = dialog.ShowAsync(host);
            _ = await dialog.HideAsync(ContentDialogResult.Secondary);
            Assert.AreEqual(ContentDialogResult.Secondary, await secondShow.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.AreEqual(2, closed);
        });
    }

    [TestMethod]
    public async Task CanceledDeferralAllowsRetryWithoutCompletingTheShow()
    {
        await RunAsync(async host =>
        {
            var dialog = new ContentDialog();
            var shown = dialog.ShowAsync(host);
            ContentDialogDeferral? deferral = null;
            ContentDialogButtonClickEventArgs? deferredArgs = null;
            var attempts = 0;
            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (++attempts is not 1)
                    return;
                deferredArgs = args;
                deferral = args.GetDeferral();
            };

            var closing = dialog.HideAsync(ContentDialogResult.Primary);
            Assert.IsFalse(closing.IsCompleted);
            Assert.IsFalse(shown.IsCompleted);
            _ = await dialog.HideAsync(ContentDialogResult.Primary);
            Assert.AreEqual(1, attempts);
            Assert.IsNotNull(deferredArgs);
            deferredArgs.Cancel = true;
            Assert.IsNotNull(deferral);
            deferral.Complete();
            _ = await closing;
            Assert.IsFalse(shown.IsCompleted);

            _ = await dialog.HideAsync(ContentDialogResult.Primary);
            Assert.AreEqual(ContentDialogResult.Primary, await shown.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.AreEqual(2, attempts);
        });
    }

    private static async Task RunAsync(Func<ContentDialogHost, Task> verify)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.DispatchAsync(async () =>
        {
            var window = new Window { Width = 640, Height = 480, Content = new Border() };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                await verify(new ContentDialogHost(window));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }
}
