using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Pixeval.Config;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class IllustrationImageLoadValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new IllustrationReaderFixture();

        List<string> report = [];
        foreach (var mode in new[] { BrowseMode.Swipe, BrowseMode.Continuous })
        {
            App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = mode;
            var uri = $"https://i.pximg.net/m3e/retry-image-{mode}.png";
            TaskCompletionSource<HttpResponseMessage>? pending = null;
            CancellationToken token = default;
            var requests = 0;
            fixture.Http.ImageResponseFactory = (request, cancellation) =>
            {
                Require(request.RequestUri!.AbsoluteUri == uri, "Only the selected image is requested");
                requests++;
                token = cancellation;
                pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
                return pending.Task;
            };
            Illustration Sample() => IllustrationReaderFixture.Sample(996440, 1) with
            {
                ImageUrls = new() { Medium = uri, Large = uri, SquareMedium = uri },
                MetaSinglePage = new() { OriginalImageUrl = uri }
            };
            var model = UiTestSession.Create<IllustrationViewerPageViewModel>((Sample()), false);
            var page = new IllustrationViewerPage(model);
            var host = new PageTestHost { Content = page };
            var window = new Window
            {
                Width = mode == BrowseMode.Swipe ? 700 : 360,
                Height = 760,
                RequestedThemeVariant = mode == BrowseMode.Swipe ? ThemeVariant.Light : ThemeVariant.Dark,
                Content = host.Shell
            };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                await UntilAsync(() => requests == 1);
                var image = model.CurrentImage!.CurrentPage!;
                Require(image.IsLoading && !image.LoadSuccessfully, "The actual image request presents a loading state");
                var shared = image.LoadOriginalImageAsync();
                Require(requests == 1, "Preload and display consumers share the same pending request");
                pending!.SetResult(new(HttpStatusCode.NotFound) { Content = new ByteArrayContent([]) });
                await shared;
                Require(!image.IsLoading && !image.LoadSuccessfully && image.LoadErrorMessage is not null,
                    "A failed image is a recoverable error, not a successful placeholder or endless spinner");
                await RenderAsync(window);
                Save(window, directory, $"image-load-error-{mode}");
                var viewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, image));
                var retry = viewer.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_RetryButton");
                await ActivateAsync(window, retry);
                await UntilAsync(() => requests == 2);
                Require(image.IsLoading && !image.RetryImageCommand.CanExecute(null), "The real retry button starts exactly one request and gates duplicates");
                pending!.SetResult(new(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3, 4]) });
                await image.RetryImageCommand.ExecutionTask!;
                Require(image.LoadErrorMessage is not null && !image.LoadSuccessfully, "Decode failure remains recoverable");
                var corrupt = App.AppViewModel.AppServiceProvider.GetRequiredService<Pixeval.Services.Media.IImageProviderService>().ImageCache.Get(uri);
                Require(corrupt is not null, "The decode fixture exercises an actual cached corrupt response");
                await RenderAsync(window);
                await ActivateAsync(window, retry);
                await UntilAsync(() => requests == 3);
                pending!.SetResult(new(HttpStatusCode.OK) { Content = new ByteArrayContent(fixture.Http.ImageResponses[IllustrationReaderFixture.ImageUri]) });
                await image.RetryImageCommand.ExecutionTask!;
                Require(image.LoadSuccessfully && !image.IsLoading && image.LoadErrorMessage is null && image.HasDisplayImage,
                    "Retry invalidates only the failed cache entry and publishes the decoded image");
                await RenderAsync(window);
                Save(window, directory, $"image-load-recovered-{mode}");
                report.Add($"PASS {mode}: shared loading, HTTP/decode errors, real retry, invalidated corrupt cache and decoded recovery");

                host.Shell.ClosePage(page);
                uri = $"https://i.pximg.net/m3e/close-image-{mode}.png";
                model = UiTestSession.Create<IllustrationViewerPageViewModel>((Sample()), false);
                page = new IllustrationViewerPage(model);
                host.Shell.NavigateTo(page);
                await UntilAsync(() => requests == 4);
                image = model.CurrentImage!.CurrentPage!;
                var closing = image.LoadOriginalImageAsync();
                host.Shell.ClosePage(page);
                await UntilAsync(() => token.IsCancellationRequested);
                Require(token.IsCancellationRequested, "Production close cancels the pending image request");
                var changed = 0;
                image.PropertyChanged += (_, _) => changed++;
                pending!.SetResult(new(HttpStatusCode.OK) { Content = new ByteArrayContent(fixture.Http.ImageResponses[IllustrationReaderFixture.ImageUri]) });
                await closing;
                Require(changed == 0 && !image.HasDisplayImage && !image.RetryImageCommand.CanExecute(null), "Late image completion cannot revive the closed reader");
                report.Add($"PASS {mode}: real-close cancellation, no late image/state and disabled retry");
            }
            finally { pending?.TrySetCanceled(); window.Close(); }
        }
        File.WriteAllLines(Path.Combine(directory, "illustration-image-load-contracts.txt"), report);
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Image error/retry requests stay offline");
    }
}
