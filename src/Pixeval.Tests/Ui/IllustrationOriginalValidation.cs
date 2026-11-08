using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Pixeval.Localization;
using Pixeval.Config;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class IllustrationOriginalValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new IllustrationReaderFixture();

        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode =
            System.Globalization.CultureInfo.CurrentUICulture.Name is "en-US" or "ru-RU" ? BrowseMode.Continuous : BrowseMode.Swipe;
        var originalUri = $"https://i.pximg.net/m3e/original-action-{Guid.NewGuid():N}.png";
        TaskCompletionSource<HttpResponseMessage>? pending = null;
        CancellationToken requestToken = default;
        var requests = 0;
        fixture.Http.ImageResponseFactory = (request, token) =>
        {
            Require(request.RequestUri!.AbsoluteUri == originalUri, "Only the requested original is fetched");
            requests++;
            requestToken = token;
            pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return pending.Task;
        };
        var entry = IllustrationReaderFixture.Sample(996410, 1) with { MetaSinglePage = new() { OriginalImageUrl = originalUri } };
        var model = UiTestSession.Create<IllustrationViewerPageViewModel>((entry), false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 700, Height = 760, Content = host.Shell };
        List<string> report = [];
        try
        {
            Pixeval.Common.MaterialAnimation.Initialize(window);
            window.Show();
            await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            await RenderAsync(window);
            var image = model.CurrentImage!.CurrentPage!;
            var viewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, image));
            var original = viewer.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == Strings.EntryViewerPageViewOriginal());
            var frame = image.DisplaySource.Frames[0];
            foreach (var malformed in new[] { false, true })
            {
                await BeginAsync();
                Require(image.IsProcessingImage && !image.ViewOriginalCommand.CanExecute(original), "Pending original load disables duplicate actions");
                pending!.SetResult(new(malformed ? HttpStatusCode.OK : HttpStatusCode.NotFound)
                { Content = new ByteArrayContent(malformed ? [1, 2, 3, 4] : []) });
                await image.ViewOriginalCommand.ExecutionTask!;
                Require(ReferenceEquals(frame, image.DisplaySource.Frames[0]) && image.TransformedSource is null,
                    "A failed or undecodable original preserves the displayed image");
                Require(host.Notifications.Any(n => n.Type == NotificationType.Error)
                    && !host.Notifications.Any(n => n.Type == NotificationType.Success), "Failure is visible and is never reported as success");
                await IllustrationTransformValidation.DismissAsync(window);
            }
            report.Add("PASS HTTP/decode failure, previous image retained, visible feedback and retry gate");
            await BeginAsync();
            pending!.SetResult(new(HttpStatusCode.OK) { Content = new ByteArrayContent(fixture.Http.ImageResponses[IllustrationReaderFixture.ImageUri]) });
            await image.ViewOriginalCommand.ExecutionTask!;
            Require(image.TransformedSource is { IsInitialized: true } && host.Notifications.Any(n => n.Type == NotificationType.Success),
                "The same original action recovers with a decoded image");
            Save(window, directory, "original-recovered");
            report.Add("PASS original retry and decoded success");
            await IllustrationTransformValidation.DismissAsync(window);
            host.Shell.ClosePage(page);
            originalUri = $"https://i.pximg.net/m3e/original-close-{Guid.NewGuid():N}.png";
            entry = IllustrationReaderFixture.Sample(996411, 1) with { MetaSinglePage = new() { OriginalImageUrl = originalUri } };
            model = UiTestSession.Create<IllustrationViewerPageViewModel>((entry), false);
            page = new IllustrationViewerPage(model);
            host.Shell.NavigateTo(page);
            await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            await RenderAsync(window);
            image = model.CurrentImage!.CurrentPage!;
            viewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, image));
            original = viewer.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == Strings.EntryViewerPageViewOriginal());
            await BeginAsync();
            var closedTask = image.ViewOriginalCommand.ExecutionTask!;
            host.Shell.ClosePage(page);
            await UntilAsync(() => requestToken.IsCancellationRequested);
            Require(requestToken.IsCancellationRequested, "Production close cancels the original download token");
            var changes = 0;
            image.PropertyChanged += (_, _) => changes++;
            var notifications = host.Notifications.ToArray();
            pending!.SetResult(new(HttpStatusCode.NotFound) { Content = new ByteArrayContent([]) });
            await closedTask;
            Require(changes == 0 && host.Notifications.SequenceEqual(notifications), "A late failed original cannot notify or mutate the closed reader");
            Require(!image.ViewOriginalCommand.CanExecute(original), "The closed original action remains disabled");
            report.Add("PASS real-close cancellation and late original failure isolation");

            async Task BeginAsync()
            {
                await RenderAsync(window);
                var position = viewer.TranslatePoint(new Rect(viewer.Bounds.Size).Center, window)!.Value;
                window.MouseDown(position, MouseButton.Right);
                window.MouseUp(position, MouseButton.Right);
                await RenderAsync(window);
                Require(viewer.ContextMenu.IsOpen && original.IsEffectivelyEnabled && original.Focus(NavigationMethod.Tab), "The original image action is reachable through the real context menu");
                var previous = requests;
                Press(window, Key.Enter, PhysicalKey.Enter);
                await UntilAsync(() => requests == previous + 1);
                Require(!viewer.ContextMenu.IsOpen, "Original action dismisses its menu");
            }
        }
        finally
        {
            pending?.TrySetCanceled();
            File.WriteAllLines(Path.Combine(directory, "illustration-original-contracts.txt"), report);
            window.Close();
        }
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Original downloads stay offline");
    }
}
