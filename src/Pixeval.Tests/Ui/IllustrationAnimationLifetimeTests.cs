using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Models.Options;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using SkiaSharp;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class IllustrationAnimationLifetimeTests
{
    private const int Iterations = 5;
    private const string ZipUri = "https://i.pximg.net/m3e/lifetime-animation-600x600.zip";
    private const string UgoiraImageUri = "https://i.pximg.net/m3e/lifetime-animation-ugoira0.png";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("frames.gif", false)]
    [DataRow("frames.gif", true)]
    [DataRow("frames.webp", false)]
    [DataRow("frames.webp", true)]
    [DataRow("ugoira", false)]
    [DataRow("ugoira", true)]
    public Task ClosingAnimatedReadersReleasesEveryDecodedFrame(string format, bool continuous) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, format, continuous));

    private static async Task RunAsync(string directory, string format, bool continuous)
    {
        using var fixture = new IllustrationReaderFixture();
        var bytes = await PrepareSourceAsync(fixture, format);
        var expectedFrames = format == "ugoira" ? 2 : 3;
        var mode = continuous ? BrowseMode.Continuous : BrowseMode.Swipe;
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = mode;
        // Keep production file caching. It owns compressed bytes; each reader
        // must still release its own decoded frames and rendering controls.
        App.AppViewModel.AppSettings.ApplicationSettings.UseFileCache = true;
        List<ClosedReader> readers = [];
        List<MemorySample> samples = [];
        try
        {
            await ExerciseReadersAsync(directory, format, expectedFrames, readers, samples);
            // The helper has returned, releasing fixture references to the
            // closed Window and its visual tree. Tab closes were already
            // checked while that same window and shell remained alive.
            await RequireCollectedAsync(null, "window-closed", readers, samples);
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Animation lifetime input stays offline");
        }
        finally
        {
            WriteReport();
        }

        void WriteReport()
        {
            var report = new
            {
                Format = format,
                Mode = mode.ToString(),
                Runtime = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription,
                Process = Environment.ProcessId,
                Iterations,
                ExpectedFrames = expectedFrames,
                CompressedBytes = bytes.Length,
                SourceSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                Measurement = "Fixed animation short-cycle collection through production tab/all-tab/window close. Managed bytes follow full GC after helper and dispatcher work return. Private/working-set bytes are process proxies, not native/GPU accounting or a long-running performance budget. Detached visuals and retained playback preference do not measure offscreen compositor timing.",
                Samples = samples,
                FinalReferences = readers.Select(static reader => reader.Snapshot()).ToArray()
            };
            File.WriteAllText(Path.Combine(directory, "animation-lifetime.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task ExerciseReadersAsync(string directory, string format, int expectedFrames,
        List<ClosedReader> readers, List<MemorySample> samples)
    {
        var host = new PageTestHost();
        var window = new Window { Width = 700, Height = 760, Content = host.Shell };
        try
        {
            window.Show();
            await RenderAsync(window);
            samples.Add(await CollectAsync("before-readers", readers));
            for (var iteration = 0; iteration < Iterations; iteration++)
            {
                if (host.Shell.Pages.Count == 0)
                    host.Shell.NavigateTo(new ContentPage { Content = new TextBlock { Text = "Animation lifetime fixture" } });
                var anchor = host.Shell.Pages.Single();
                var close = iteration == Iterations - 1 ? CloseMethod.Window : iteration == Iterations - 2 ? CloseMethod.AllPages : CloseMethod.Page;
                readers.Add(await OpenRenderAndCloseAsync(host, window, anchor, directory, format, expectedFrames, iteration, close));
                if (close != CloseMethod.Window)
                    await RequireCollectedAsync(window, "closed-" + (iteration + 1), readers, samples);
            }
            Require(host.Shell.Pages.Count == 0 && !window.IsVisible, "The final Window.Close clears all production tabs");
        }
        finally
        {
            host.Shell.CloseAllPages();
            window.Close();
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        }
    }

    private static async Task RequireCollectedAsync(Window? window, string stage,
        IReadOnlyList<ClosedReader> readers, List<MemorySample> samples)
    {
        await DrainRenderingAsync(window);
        samples.Add(await CollectAsync(stage, readers));
        // Composition can retain a detached visual for a frame. Bound that tail
        // and assert before another close can hide a previously retained tab.
        for (var pass = 0; pass < 3 && readers.Any(static reader => reader.AnyAlive); pass++)
        {
            await DrainRenderingAsync(window);
            samples.Add(await CollectAsync(stage + "-drained-" + (pass + 1), readers));
        }
        Require(readers.All(static reader => !reader.AnyAlive),
            "Production close and drained rendering release every animated reader, control, source and decoded frame: "
            + string.Join("; ", readers.Select(static reader => reader.Describe())));
    }

    private static async Task<byte[]> PrepareSourceAsync(IllustrationReaderFixture fixture, string format)
    {
        if (format != "ugoira")
        {
            var encoded = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ProgressiveImages", format));
            fixture.Http.ImageResponses["https://i.pximg.net/m3e/lifetime-" + format] = encoded;
            return encoded;
        }

        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-animation-lifetime");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        var red = IllustrationFramePlaybackValidation.Png(SKColors.Red);
        var green = IllustrationFramePlaybackValidation.Png(SKColors.Green);
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
            for (var index = 0; index < 2; index++)
            {
                using var frame = archive.CreateEntry($"{index:D6}.png").Open();
                frame.Write(index == 0 ? red : green);
            }
        var bytes = buffer.ToArray();
        fixture.Http.ImageResponses[ZipUri] = bytes;
        fixture.Http.ImageResponses[ZipUri.Replace("600x600", "1920x1080")] = bytes;
        fixture.Http.ImageResponses[UgoiraImageUri] = red;
        fixture.Http.AnimationResponseFactory = (_, _) => Task.FromResult("""
            {"ugoira_metadata":{"zip_urls":{"medium":"https://i.pximg.net/m3e/lifetime-animation-600x600.zip"},
            "frames":[{"file":"000000.png","delay":100},{"file":"000001.png","delay":200}]}}
            """);
        return bytes;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<ClosedReader> OpenRenderAndCloseAsync(PageTestHost host, Window window, Page anchor,
        string directory, string format, int expectedFrames, int iteration, CloseMethod close)
    {
        var uri = format == "ugoira" ? UgoiraImageUri : "https://i.pximg.net/m3e/lifetime-" + format;
        var work = IllustrationReaderFixture.Sample(996700 + iteration, 1) with
        {
            Type = format == "ugoira" ? IllustrationType.Ugoira : IllustrationType.Illustration,
            ThumbnailUrls = new() { Medium = uri, Large = uri, SquareMedium = uri },
            MetaSinglePage = new() { OriginalImageUrl = uri }
        };
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(work), false);
        var page = new IllustrationViewerPage(model);
        // Do not assign the driver Content/_origin: the retained shell must not
        // gain a fixture-only strong reference to the reader under test.
        host.Shell.NavigateTo(page);
        try
        {
            await UntilAsync(() => model.CurrentImage?.CurrentPage is { LoadSuccessfully: true } or { LoadErrorMessage: not null });
            await RenderAsync(window);
            var image = model.CurrentImage!.CurrentPage!;
            Require(image.LoadSuccessfully && image.OriginalSource is { IsInitialized: true }, "The fixed animation fully decodes: " + image.LoadErrorMessage);
            var source = image.OriginalSource!;
            Require(source.FrameCount == expectedFrames && source.Frames.Count == expectedFrames
                && image.DisplaySource.FrameCount == expectedFrames, "Each reader owns all expected animation frames");
            var frameReferences = source.Frames.Select(static frame => new WeakReference(frame)).ToArray();
            var viewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, image));
            var animated = viewer.GetVisualDescendants().OfType<AnimatedImage>().Single();
            Require(image.IsPlaying && animated.IsPlaying, "A newly opened reader starts playback");
            var prefix = "reader-" + (iteration + 1);
            await IllustrationFramePlaybackValidation.ObserveAsync(window, directory, prefix + "-playing", true, expectedFrames);
            var playingOnReturn = iteration % 2 == 0;
            if (!playingOnReturn)
            {
                var viewport = page.FindControl<ImageViewerWrapper>("I")!.GetVisualDescendants()
                    .OfType<SmoothScroll.Avalonia.Controls.ScrollView>().First();
                Require(viewport.Focus(NavigationMethod.Tab), "The real viewport accepts playback keyboard input");
                Press(window, Key.Space, PhysicalKey.Space);
                await RenderAsync(window);
                Require(!image.IsPlaying && !animated.IsPlaying, "Space pauses both the model and rendered animation");
                await IllustrationFramePlaybackValidation.ObserveAsync(window, directory, prefix + "-paused", false);
            }

            host.Shell.SelectPage(anchor);
            await RenderAsync(window);
            Require(ReferenceEquals(host.Shell.SelectedPage, anchor) && !page.IsLoaded && TopLevel.GetTopLevel(animated) is null,
                "Selecting the anchor actually unloads the reader and detaches its animated visual");
            Require(image.IsPlaying == playingOnReturn && ReferenceEquals(image.OriginalSource, source)
                && source.FrameCount == expectedFrames, "Temporary navigation retains playback preference and all decoded frames");
            host.Shell.SelectPage(page);
            await RenderAsync(window);
            var returnedViewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, image));
            var returnedAnimation = returnedViewer.GetVisualDescendants().OfType<AnimatedImage>().Single();
            Require(ReferenceEquals(host.Shell.SelectedPage, page) && page.IsLoaded
                && ReferenceEquals(image.OriginalSource, source) && image.IsPlaying == playingOnReturn
                && returnedAnimation.IsPlaying == playingOnReturn, "The real tab return retains the same source and playback preference");
            await IllustrationFramePlaybackValidation.ObserveAsync(window, directory, prefix + "-returned", playingOnReturn, expectedFrames);

            var references = new ClosedReader(iteration + 1, close.ToString(), playingOnReturn, new(page), new(model), new(image),
                new(image.DisplaySource), new(source), new(viewer), new(animated), new(returnedViewer), new(returnedAnimation), frameReferences);
            switch (close)
            {
                case CloseMethod.Page:
                    host.Shell.ClosePage(page);
                    break;
                case CloseMethod.AllPages:
                    host.Shell.CloseAllPages();
                    break;
                case CloseMethod.Window:
                    window.Close();
                    break;
            }
            Require(!host.Shell.Pages.Contains(page) && model.CurrentImage is null && image.OriginalSource is null
                && !image.HasDisplayImage && source.FrameCount == 0 && !image.PlayPauseCommand.CanExecute(null),
                "The production close entry releases decoded frames and disables playback without manual model disposal");
            return references;
        }
        finally
        {
            if (host.Shell.Pages.Contains(page)) host.Shell.ClosePage(page);
        }
    }

    private static async Task DrainRenderingAsync(Window? window)
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        if (window?.IsVisible == true) window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        if (window?.IsVisible == true)
            using (window.CaptureRenderedFrame()) { }
        await Task.Delay(16);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
    }

    private static Task<MemorySample> CollectAsync(string stage, IReadOnlyList<ClosedReader> readers) => Task.Run(() =>
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var process = Process.GetCurrentProcess();
        return new MemorySample(stage, GC.GetTotalMemory(false), GC.GetTotalAllocatedBytes(precise: true),
            process.PrivateMemorySize64, process.WorkingSet64, readers.Count(static reader => reader.AnyAlive),
            readers.Sum(static reader => reader.Frames.Count(static frame => frame.IsAlive)));
    });

    private sealed record ClosedReader(int Iteration, string CloseMethod, bool PlayingOnClose, WeakReference Page,
        WeakReference Model, WeakReference Image, WeakReference DisplaySource, WeakReference Source,
        WeakReference Viewer, WeakReference Animation, WeakReference ReturnedViewer, WeakReference ReturnedAnimation, WeakReference[] Frames)
    {
        internal bool AnyAlive => Page.IsAlive || Model.IsAlive || Image.IsAlive || DisplaySource.IsAlive || Source.IsAlive
            || Viewer.IsAlive || Animation.IsAlive || ReturnedViewer.IsAlive || ReturnedAnimation.IsAlive || Frames.Any(static frame => frame.IsAlive);
        internal object Snapshot() => new
        {
            Iteration,
            CloseMethod,
            PlayingOnClose,
            Page = Page.IsAlive,
            Model = Model.IsAlive,
            Image = Image.IsAlive,
            DisplaySource = DisplaySource.IsAlive,
            Source = Source.IsAlive,
            Viewer = Viewer.IsAlive,
            Animation = Animation.IsAlive,
            ReturnedViewer = ReturnedViewer.IsAlive,
            ReturnedAnimation = ReturnedAnimation.IsAlive,
            Frames = Frames.Select(static frame => frame.IsAlive).ToArray()
        };
        internal string Describe() => JsonSerializer.Serialize(Snapshot());
    }

    private sealed record MemorySample(string Stage, long ManagedBytesAfterCollection, long TotalAllocatedBytes,
        long PrivateBytesProxy, long WorkingSetBytesProxy, int LiveReaders, int LiveFrames);

    private enum CloseMethod { Page, AllPages, Window }
}
