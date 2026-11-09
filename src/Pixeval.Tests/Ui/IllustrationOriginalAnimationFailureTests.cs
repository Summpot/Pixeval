using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Mako.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.Utilities.IO.Caching;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using SkiaSharp;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class IllustrationOriginalAnimationFailureTests
{
    private const string ZipUri = "https://i.pximg.net/m3e/original-animation-600x600.zip";
    private const string OriginalUri = "https://i.pximg.net/m3e/original-animation-ugoira0.png";
    private const string SecondUri = "https://i.pximg.net/m3e/original-animation-ugoira1.png";
    private const string UnrelatedUri = "https://i.pximg.net/m3e/original-animation-unrelated.png";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public Task OriginalAnimationFailurePreservesPreviewAndRetries(bool malformed, bool continuous) =>
        UiTestSession.RunAsync(TestContext, directory => RunFailureAsync(directory, malformed, continuous));

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public Task LeavingWhileAnimationOriginalLoadsDiscardsLateFrames(bool changeWork, bool continuous) =>
        UiTestSession.RunAsync(TestContext, directory => RunLeavingAsync(directory, changeWork, continuous));

    private static async Task RunFailureAsync(string directory, bool malformed, bool continuous)
    {
        await using var fixture = await Fixture.CreateAsync(continuous);
        var corrected = false;
        var originalRequests = 0;
        fixture.Http.ImageResponseFactory = (request, _) =>
        {
            var uri = request.RequestUri!.AbsoluteUri;
            Require(uri is OriginalUri or SecondUri, "Only the selected original animation frames are requested");
            originalRequests++;
            return Task.FromResult(new HttpResponseMessage(uri == SecondUri && !corrected && !malformed ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(uri == OriginalUri ? fixture.Red : corrected ? fixture.Green : malformed ? [1, 2, 3, 4] : [])
            });
        };
        await fixture.ShowAsync();
        var image = fixture.Image;
        var previousFrames = image.DisplaySource.Frames.ToArray();
        using var allocations = new IllustrationArchiveFailureTests.StreamEvents();
        List<string> report = [];
        try
        {
            await fixture.BeginOriginalAsync();
            await image.ViewOriginalCommand.ExecutionTask!;
            await RenderAsync(fixture.Window);
            report.Add($"Before retry: malformed={malformed}; transformed={image.TransformedSource is not null}; frames={image.DisplaySource.FrameCount}; requests={originalRequests}; errors={fixture.Host.Notifications.Count(n => n.Type == NotificationType.Error)}; successes={fixture.Host.Notifications.Count(n => n.Type == NotificationType.Success)}; unreleased=[{string.Join(',', allocations.Unreleased)}]");
            Save(fixture.Window, directory, "animation-original-failed");
            Require(image.TransformedSource is null && image.DisplaySource.Frames.SequenceEqual(previousFrames),
                "One failed original animation frame preserves every displayed preview frame instead of publishing a placeholder animation");
            Require(fixture.Host.Notifications.Any(n => n.Type == NotificationType.Error)
                && !fixture.Host.Notifications.Any(n => n.Type == NotificationType.Success) && !image.IsProcessingImage,
                "Partial original animation failure is visible and never reported as success");
            Require(allocations.Unreleased.Length == 0, "Partial original failure explicitly releases all acquired frame streams");
            using (var first = CacheHelper.TryGetStream(OriginalUri))
                Require(first is not null, "The successful first original frame exercised the file cache");
            if (malformed)
                using (var bad = CacheHelper.TryGetStream(SecondUri))
                    Require(bad is not null, "The malformed original frame exercised cached decode failure");
            await IllustrationTransformValidation.DismissAsync(fixture.Window);
            corrected = true;
            var requestsBeforeRetry = originalRequests;
            allocations.Reset();
            await fixture.BeginOriginalAsync();
            await image.ViewOriginalCommand.ExecutionTask!;
            Require(image.TransformedSource is { IsInitialized: true } && image.DisplaySource.FrameCount == 2
                && image.DisplaySource.Delays.SequenceEqual(new[] { 100, 200 }),
                "The original action retries into a complete animation with unchanged frame delays");
            Require(originalRequests == requestsBeforeRetry + 2 && fixture.Host.Notifications.Any(n => n.Type == NotificationType.Success)
                && !fixture.Host.Notifications.Any(n => n.Type == NotificationType.Error),
                "Retry invalidates the failed original's frame cache and reports only complete success");
            using (var unrelated = CacheHelper.TryGetStream(UnrelatedUri))
            {
                Require(unrelated is not null, "Original retry preserves unrelated cached bytes");
                using var bytes = new MemoryStream();
                unrelated!.CopyTo(bytes);
                Require(bytes.ToArray().SequenceEqual(fixture.Green), "Unrelated cache contents are unchanged");
            }
            Require(allocations.CreatedCount >= 2 && allocations.Unreleased.Length == 0, "Successful original decoding releases every source stream");
            await IllustrationTransformValidation.DismissAsync(fixture.Window);
            await RenderAsync(fixture.Window);
            await IllustrationFramePlaybackValidation.ObserveAsync(fixture.Window, directory, "animation-original-recovered", true, 2);
            report.Add($"PASS malformed={malformed}/continuous={continuous}: partial failure retains frames, error feedback, exact original cache retry, complete decoded playback and stream disposal");
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "animation-original-contracts.txt"), report);
            File.WriteAllLines(Path.Combine(directory, "animation-original-requests.txt"), fixture.Http.RequestLog);
        }
    }

    private static async Task RunLeavingAsync(string directory, bool changeWork, bool continuous)
    {
        await using var fixture = await Fixture.CreateAsync(continuous);
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken requestToken = default;
        fixture.Http.ImageResponseFactory = (request, token) =>
        {
            if (request.RequestUri!.AbsoluteUri == OriginalUri)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(fixture.Red) });
            Require(request.RequestUri.AbsoluteUri == SecondUri, "Only the unfinished second frame is delayed");
            requestToken = token;
            requested.TrySetResult();
            return pending.Task; // Deliberately ignore transport cancellation.
        };
        await fixture.ShowAsync();
        var image = fixture.Image;
        using var allocations = new IllustrationArchiveFailureTests.StreamEvents();
        try
        {
            await fixture.BeginOriginalAsync();
            await requested.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var operation = image.ViewOriginalCommand.ExecutionTask!;
            Require(image.IsProcessingImage, "The original action waits for the unfinished animation frame");
            if (changeWork)
            {
                var viewport = fixture.Page.FindControl<ImageViewerWrapper>("I")!.GetVisualDescendants()
                    .OfType<SmoothScroll.Avalonia.Controls.ScrollView>().First();
                Require(viewport.Focus(NavigationMethod.Tab), "The viewport accepts the existing next-work shortcut");
                Press(fixture.Window, Key.Down, PhysicalKey.ArrowDown);
                await UntilAsync(() => fixture.Model.CurrentWorkIndex == 1 && fixture.Model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            }
            else
                fixture.Host.Shell.ClosePage(fixture.Page);
            Require(requestToken.IsCancellationRequested, "Real work navigation or tab close cancels the old original frame request");
            var changes = 0;
            PropertyChangedEventHandler observer = (_, _) => changes++;
            image.PropertyChanged += observer;
            var notifications = fixture.Host.Notifications.ToArray();
            try
            {
                pending.SetResult(new(HttpStatusCode.OK) { Content = new ByteArrayContent(fixture.Green) });
                await operation;
                await RenderAsync(fixture.Window);
                Require(changes == 0 && image.TransformedSource is null && image.DisplaySource.FrameCount == 0
                    && !image.ViewOriginalCommand.CanExecute(null), "Late frame completion cannot revive or mutate the replaced/closed image model");
                Require(fixture.Host.Notifications.SequenceEqual(notifications), "Late original completion does not notify the new reader lifetime");
                Require(allocations.CreatedCount >= 2 && allocations.Unreleased.Length == 0, "Leaving during a partial animation explicitly releases all downloaded frame streams");
                if (changeWork)
                    Require(fixture.Model.CurrentIllustration?.Entry.Id == "996752" && fixture.Model.CurrentImage!.CurrentPage!.LoadSuccessfully,
                        "The new work remains selected and loaded after the old response arrives");
                File.WriteAllLines(Path.Combine(directory, "animation-original-lifetime.txt"),
                    [$"PASS changeWork={changeWork}/continuous={continuous}: formal navigation/close, cancelled token, unchanged old model and notifications, streams={allocations.CreatedCount}, unreleased=[]"]);
            }
            finally { image.PropertyChanged -= observer; }
        }
        finally { pending.TrySetCanceled(); }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly IllustrationReaderFixture _reader;
        private readonly SimpleOperableSourceView<IllustrationItemViewModel> _source;
        internal OfflineSearchFixture Http => _reader.Http;
        internal byte[] Red { get; } = IllustrationFramePlaybackValidation.Png(SKColors.Red);
        internal byte[] Green { get; } = IllustrationFramePlaybackValidation.Png(SKColors.Green);
        internal IllustrationViewerPageViewModel Model { get; }
        internal IllustrationViewerPage Page { get; }
        internal PageTestHost Host { get; }
        internal Window Window { get; }
        internal SingleViewerViewModel Image => Model.CurrentImage!.CurrentPage!;

        private Fixture(IllustrationReaderFixture reader, bool continuous)
        {
            _reader = reader;
            App.AppViewModel.AppSettings.ApplicationSettings.UseFileCache = true;
            App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = continuous ? BrowseMode.Continuous : BrowseMode.Swipe;
            var archive = IllustrationArchiveFailureTests.CreateZip(Red, Green);
            Http.ImageResponses[ZipUri] = archive;
            Http.ImageResponses[ZipUri.Replace("600x600", "1920x1080")] = archive;
            Http.AnimationResponseFactory = (_, _) => Task.FromResult("""
                {"ugoira_metadata":{"zip_urls":{"medium":"https://i.pximg.net/m3e/original-animation-600x600.zip"},
                "frames":[{"file":"000000.png","delay":100},{"file":"000001.png","delay":200}]}}
                """);
            using (var stream = new MemoryStream(Green)) _ = CacheHelper.TryCacheStream(UnrelatedUri, stream);
            var work = IllustrationReaderFixture.Sample(996751, 1) with
            {
                Type = IllustrationType.Ugoira,
                MetaSinglePage = new() { OriginalImageUrl = OriginalUri }
            };
            _source = new([work, IllustrationReaderFixture.Sample(996752, 1)]);
            Model = new(_source.CloneSourceView(), 0, false);
            Page = new(Model);
            Host = new() { Content = Page };
            Window = new() { Width = continuous ? 420 : 700, Height = 760, Content = Host.Shell };
        }

        internal static async Task<Fixture> CreateAsync(bool continuous)
        {
            var reader = new IllustrationReaderFixture();
            await App.AppViewModel.MakoClient.SetTokenAsync("fixture-original-animation");
            await App.AppViewModel.MakoClient.ValidateTokenAsync();
            return new Fixture(reader, continuous);
        }

        internal async Task ShowAsync()
        {
            Window.Show();
            await UntilAsync(() => Image.LoadSuccessfully);
            await RenderAsync(Window);
            Require(Image.DisplaySource.FrameCount == 2, "The initial ZIP preview is a fully decoded two-frame animation");
        }

        internal async Task BeginOriginalAsync()
        {
            var viewer = Page.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, Image));
            var area = Page.FindControl<ImageViewerWrapper>("I")!;
            var position = new Rect(viewer.TranslatePoint(default, Window)!.Value, viewer.Bounds.Size)
                .Intersect(new Rect(area.TranslatePoint(default, Window)!.Value, area.Bounds.Size)).Center;
            Window.MouseDown(position, MouseButton.Right);
            Window.MouseUp(position, MouseButton.Right);
            await RenderAsync(Window);
            var original = viewer.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == I18NManager.GetResource(EntryViewerPageResources.ViewOriginal));
            Require(viewer.ContextMenu.IsOpen && original.IsEffectivelyEnabled && original.Focus(NavigationMethod.Tab), "The animation original action is reachable in the real image menu");
            Press(Window, Key.Enter, PhysicalKey.Enter);
            await RenderAsync(Window);
            Require(!viewer.ContextMenu.IsOpen, "Original activation closes its menu");
        }

        public ValueTask DisposeAsync()
        {
            Window.Close();
            _source.Dispose();
            Require(Http.UnexpectedRequests.IsEmpty, "Original animation scenarios remain offline");
            _reader.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
