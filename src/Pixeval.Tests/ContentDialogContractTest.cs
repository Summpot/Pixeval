using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ContentDialogContractTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancellationCompletesWithNoneAndRaisesClosing(bool cancelBeforeShow)
    {
        await RunAsync(async (window, host) =>
        {
            using var cancellation = new CancellationTokenSource();
            if (cancelBeforeShow)
                cancellation.Cancel();
            var dialog = new ContentDialog();
            var closing = 0;
            var closed = 0;
            dialog.Closing += (_, args) =>
            {
                Assert.AreEqual(ContentDialogResult.None, args.Result);
                closing++;
            };
            dialog.Closed += (_, _) => closed++;
            var shown = dialog.ShowAsync(host, cancellation.Token);
            if (!cancelBeforeShow)
                cancellation.Cancel();

            Assert.AreEqual(ContentDialogResult.None, await shown.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.AreEqual(1, closing);
            Assert.AreEqual(1, closed);
            Assert.IsEmpty(host.Dialogs);
            Assert.IsFalse(host.IsOpen);
        });
    }

    [TestMethod]
    public async Task ClosingCanVetoTokenCancellationAndAllowAnExplicitRetry()
    {
        await RunAsync(async (window, host) =>
        {
            using var cancellation = new CancellationTokenSource();
            var dialog = new ContentDialog();
            var closing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var veto = true;
            dialog.Closing += (_, args) =>
            {
                args.Cancel = veto;
                closing.TrySetResult();
            };
            var shown = dialog.ShowAsync(host, cancellation.Token);
            cancellation.Cancel();
            await closing.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsFalse(shown.IsCompleted);
            Assert.IsTrue(host.IsOpen);
            veto = false;
            _ = await dialog.HideAsync(ContentDialogResult.Close);
            Assert.AreEqual(ContentDialogResult.Close, await shown.WaitAsync(TimeSpan.FromSeconds(2)));
        });
    }

    [TestMethod]
    public async Task DuplicateShowFailsWithoutChangingTheOriginalShow()
    {
        await RunAsync(async (window, host) =>
        {
            var dialog = new ContentDialog();
            var shown = dialog.ShowAsync(host);
            _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => dialog.ShowAsync(host));
            Assert.HasCount(1, host.Dialogs);
            Assert.IsFalse(shown.IsCompleted);
            _ = await dialog.HideAsync(ContentDialogResult.Secondary);
            Assert.AreEqual(ContentDialogResult.Secondary, await shown.WaitAsync(TimeSpan.FromSeconds(2)));
        });
    }

    [TestMethod]
    public async Task NestedDialogsRestoreTopmostHitTestingAndFocus()
    {
        await RunAsync(async (window, host) =>
        {
            var parent = new ContentDialog();
            var child = new ContentDialog();
            var firstShow = parent.ShowAsync(host);
            var secondShow = child.ShowAsync(host);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            Assert.IsFalse(parent.IsHitTestVisible);
            Assert.IsTrue(child.IsHitTestVisible);
            Assert.AreSame(child, window.FocusManager!.GetFocusedElement());
            _ = await child.HideAsync(ContentDialogResult.Primary);
            Assert.AreEqual(ContentDialogResult.Primary, await secondShow.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.IsTrue(parent.IsHitTestVisible);
            Assert.AreSame(parent, window.FocusManager.GetFocusedElement());
            Assert.IsFalse(firstShow.IsCompleted);
            _ = await parent.HideAsync();
            Assert.AreEqual(ContentDialogResult.None, await firstShow.WaitAsync(TimeSpan.FromSeconds(2)));
        });
    }

    [TestMethod]
    [DataRow(Key.Enter, ContentDialogResult.Primary)]
    [DataRow(Key.Escape, ContentDialogResult.Close)]
    public async Task UnmodifiedKeysUseAvailableDialogButtons(Key key, ContentDialogResult expected)
    {
        await RunAsync(async (window, host) =>
        {
            var dialog = new ContentDialog
            {
                PrimaryButtonText = "Confirm fixture",
                CloseButtonText = "Close fixture",
                DefaultButton = ContentDialogButton.Primary
            };
            var shown = dialog.ShowAsync(host);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            var physicalKey = key is Key.Enter ? PhysicalKey.Enter : PhysicalKey.Escape;
            window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
            window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
            Assert.AreEqual(expected, await shown.WaitAsync(TimeSpan.FromSeconds(2)));
        });
    }

    [TestMethod]
    public async Task ModifiedKeysAndUnavailableButtonsDoNotClose()
    {
        await RunAsync(async (window, host) =>
        {
            var dialog = new ContentDialog { PrimaryButtonText = "Confirm fixture", DefaultButton = ContentDialogButton.Primary };
            var shown = dialog.ShowAsync(host);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Assert.IsFalse(shown.IsCompleted);
            _ = await dialog.HideAsync();
            _ = await shown.WaitAsync(TimeSpan.FromSeconds(2));
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
                Pixeval.Common.MaterialAnimation.Initialize(window);
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
