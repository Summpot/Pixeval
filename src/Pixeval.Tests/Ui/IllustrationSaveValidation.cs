using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Platform;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Pixeval.Localization;
using Pixeval.Config;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class IllustrationSaveValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new IllustrationReaderFixture();
        var provider = new IllustrationSaveFixture();
        AvaloniaLocator.CurrentMutable.Bind<IStorageProviderFactory>().ToConstant(provider);
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = BrowseMode.Swipe;
        List<string> report = [];
        var model = UiTestSession.Create<IllustrationViewerPageViewModel>((IllustrationReaderFixture.Sample(996301, 1)), false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 700, Height = 760, Content = host.Shell };
        try
        {
            Pixeval.Common.MaterialAnimation.Initialize(window);
            window.Show();
            await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            await RenderAsync(window);
            var image = model.CurrentImage!.CurrentPage!;
            var viewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, image));
            var save = viewer.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == Strings.EntryViewerPageSaveAs());
            Require(save.Header?.ToString() == Strings.EntryViewerPageSaveAs(), "The existing localized save-displayed-image action remains available");

            await BeginAsync();
            Require(provider.Options is { DefaultExtension: "png", SuggestedFileName: "996301" }
                && provider.Options.FileTypeChoices is [{ Patterns: ["*.png"] }], "Save keeps the original PNG filter and artwork identity");
            Require(image.SaveAsCommand.IsRunning && !image.SaveAsCommand.CanExecute(save), "A pending picker disables duplicate saves");
            provider.Complete(null);
            await image.SaveAsCommand.ExecutionTask!;
            Require(!host.Notifications.Any(notification => notification.Type is NotificationType.Success or NotificationType.Error), "Picker cancellation produces no success or error");
            report.Add("PASS actual context-menu entry, original PNG options, duplicate gate and picker cancellation");

            var output = new IllustrationSaveFixture.File();
            await BeginAsync();
            provider.Complete(output);
            await image.SaveAsCommand.ExecutionTask!;
            Require(output.Opens == 1 && output.Disposed && output.Output.Disposed, "Successful save releases the storage file and output stream exactly once");
            using (var data = new MemoryStream(output.Output.ToArray()))
            using (var bitmap = new Bitmap(data))
                Require(bitmap.PixelSize == image.DisplaySource.Frames![0].PixelSize, "The written PNG decodes to the displayed image dimensions");
            Require(host.Notifications.Any(notification => notification.Type == NotificationType.Success), "A completed and flushed save shows success");
            File.WriteAllBytes(Path.Combine(directory, "saved-displayed.png"), output.Output.ToArray());
            report.Add("PASS decoded displayed PNG, success notification and stream/file ownership");
            await DismissNotificationsAsync();

            provider.CanSave = false;
            var calls = provider.Calls;
            await BeginAsync(expectPicker: false);
            await image.SaveAsCommand.ExecutionTask!;
            Require(provider.Calls == calls && ErrorCount() == 1, "A platform without save capability reports an error without opening a picker");
            await DismissNotificationsAsync();
            provider.CanSave = true;
            await BeginAsync();
            provider.Fail();
            await image.SaveAsCommand.ExecutionTask!;
            Require(ErrorCount() == 1, "Picker failures are handled and visible");
            await DismissNotificationsAsync();
            var failedOpen = new IllustrationSaveFixture.File { FailOpen = true };
            await BeginAsync();
            provider.Complete(failedOpen);
            await image.SaveAsCommand.ExecutionTask!;
            Require(failedOpen.Disposed && ErrorCount() == 1, "Output access failures release the file and show an error");
            await DismissNotificationsAsync();
            var failedWrite = new IllustrationSaveFixture.File();
            failedWrite.Output.FailWrite = true;
            await BeginAsync();
            provider.Complete(failedWrite);
            await image.SaveAsCommand.ExecutionTask!;
            Require(failedWrite.Disposed && failedWrite.Output.Disposed && ErrorCount() == 1,
                "Write failures release both handles and show a recoverable error");
            Save(window, directory, "image-save-errors");
            report.Add("PASS unsupported storage, picker/open/write errors and retryable command");
            await DismissNotificationsAsync();

            await BeginAsync();
            var closedTask = image.SaveAsCommand.ExecutionTask!;
            host.Shell.ClosePage(page);
            var notifications = host.Notifications.Count();
            var changes = 0;
            image.PropertyChanged += (_, _) => changes++;
            var late = new IllustrationSaveFixture.File();
            provider.Complete(late);
            await closedTask;
            Require(late.Disposed && late.Opens == 0 && changes == 0 && host.Notifications.Count() == notifications,
                "A picker completing after production close disposes the selection without opening it or notifying the closed reader");
            Require(!image.SaveAsCommand.CanExecute(save), "A disposed image cannot be saved again");
            report.Add("PASS real close while picker is pending and late storage selection isolation");

            var next = UiTestSession.Create<IllustrationViewerPageViewModel>((IllustrationReaderFixture.Sample(996302, 1)), false);
            var nextPage = new IllustrationViewerPage(next);
            host.Shell.NavigateTo(nextPage);
            await UntilAsync(() => next.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            await RenderAsync(window);
            image = next.CurrentImage!.CurrentPage!;
            viewer = nextPage.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, image));
            save = viewer.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == Strings.EntryViewerPageSaveAs());
            await BeginAsync();
            var pendingOpen = new IllustrationSaveFixture.File { PendingOpen = new(TaskCreationOptions.RunContinuationsAsynchronously) };
            provider.Complete(pendingOpen);
            await UntilAsync(() => pendingOpen.Opens == 1);
            closedTask = image.SaveAsCommand.ExecutionTask!;
            host.Shell.ClosePage(nextPage);
            notifications = host.Notifications.Count();
            pendingOpen.PendingOpen.SetResult(pendingOpen.Output);
            await closedTask;
            Require(pendingOpen.Disposed && pendingOpen.Output.Disposed && pendingOpen.Output.ToArray().Length == 0
                && host.Notifications.Count() == notifications, "A late output stream after close is disposed without writing or reporting success");
            report.Add("PASS real close while opening output and late stream ownership");

            int ErrorCount() => host.Notifications.Count(notification => notification.Type == NotificationType.Error);

            async Task DismissNotificationsAsync()
            {
                await RenderAsync(window);
                foreach (var card in window.GetVisualDescendants().OfType<NotificationCard>().ToArray())
                {
                    if (!card.IsClosing)
                    {
                        var close = card.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_CloseButton");
                        Require(close.Focus(NavigationMethod.Tab), "Acknowledged save feedback can be dismissed by keyboard");
                        Press(window, Key.Space, PhysicalKey.Space);
                    }
                    await UntilAsync(() => !window.GetVisualDescendants().Contains(card));
                }
            }

            async Task BeginAsync(bool expectPicker = true)
            {
                var callsBefore = provider.Calls;
                await RenderAsync(window);
                var position = viewer.TranslatePoint(new Rect(viewer.Bounds.Size).Center, window)!.Value;
                window.MouseDown(position, MouseButton.Right);
                window.MouseUp(position, MouseButton.Right);
                await RenderAsync(window);
                Require(viewer.ContextMenu!.IsOpen && save.IsEffectivelyEnabled && save.Focus(NavigationMethod.Tab), "The real image menu exposes a keyboard reachable save action");
                Require(ReferenceEquals(save.Command, image.SaveAsCommand), "Opening the image menu binds the original save command");
                Press(window, Key.Enter, PhysicalKey.Enter);
                await RenderAsync(window);
                Require(!viewer.ContextMenu.IsOpen, "Executing save dismisses the image menu");
                if (expectPicker)
                    Require(provider.Calls == callsBefore + 1, "The menu opens exactly one platform save request");
            }
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "illustration-save-contracts.txt"), report);
            window.Close();
        }
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Saving the displayed image stays offline");
    }
}
