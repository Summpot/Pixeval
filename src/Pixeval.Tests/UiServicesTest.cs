using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using Pixeval.Services.Core;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class UiServicesTest
{
    [TestMethod]
    public void NotificationsQueueUntilHostAttaches()
    {
        var host = new UiHost();
        var notifications = new NotificationService(host);

        notifications.ShowSuccess("a");
        notifications.ShowError("b", "c");

        Assert.IsFalse(host.IsAttached);
        Assert.AreEqual(2, notifications.PendingCount);
    }

    [TestMethod]
    public async Task ServicesWithoutHostAreSilentNoOps()
    {
        var host = new UiHost();
        Assert.AreEqual(ContentDialogResult.None, await new DialogService(host).CreateOkCancelAsync("t", null));
        Assert.IsNull(await new ClipboardService(host).GetTextAsync());
        await new LauncherService(host).LaunchUriAsync(new Uri("https://example.com"));
        Assert.AreEqual(0, (await new StoragePickerService(host).OpenFilesAsync(new())).Count);
    }

    [TestMethod]
    public async Task ProviderIsInheritedAndFallsBackToNullServices()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var child = new Border();
            var root = new Border { Child = child };

            Assert.AreSame(NullNotificationService.Instance, child.Notifications);
            Assert.AreSame(NullClipboardService.Instance, child.ClipboardService);
            Assert.IsNull(child.AppNavigation);
            Assert.ThrowsExactly<InvalidOperationException>(() => child.GetRequiredUiService<INotificationService>());

            var notifications = new RecordingNotificationService();
            using var provider = new ServiceCollection()
                .AddSingleton<INotificationService>(notifications)
                .BuildServiceProvider();
            UiServices.SetProvider(root, provider);

            Assert.AreSame(notifications, child.Notifications);
            Assert.AreSame(notifications, child.GetRequiredUiService<INotificationService>());
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task RunInteractionSurfacesFailuresInsteadOfThrowing()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            var notifications = new RecordingNotificationService();
            await using var provider = new ServiceCollection()
                .AddSingleton<INotificationService>(notifications)
                .BuildServiceProvider();
            var control = new Border();
            UiServices.SetProvider(control, provider);

            var completion = new TaskCompletionSource();
            control.RunInteraction(async () =>
            {
                await completion.Task;
                throw new InvalidOperationException("boom");
            });
            completion.SetResult();
            await Task.Yield();
            await Task.Delay(10);

            Assert.AreEqual("boom", notifications.LastError);
        }, CancellationToken.None);
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public string? LastError { get; private set; }

        public void ShowInformation(string title, string? content = null)
        {
        }

        public void ShowSuccess(string title, string? content = null)
        {
        }

        public void ShowWarning(string title, string? content = null)
        {
        }

        public void ShowError(string title, string? content = null) => LastError = content;
    }
}
