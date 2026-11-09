using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using Pixeval.I18N;
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
public sealed class IllustrationRapidPageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task RapidPagingFitsTheSelectedImageWhenEarlierDownloadsFinishLate(bool vertical) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, vertical, closePending: false));

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task ClosingDuringRapidPagingRejectsAllLateImages(bool vertical) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, vertical, closePending: true));

    private static async Task RunAsync(string directory, bool vertical, bool closePending)
    {
        using var fixture = new IllustrationReaderFixture();
        using var images = new DelayedImages(fixture.Http);
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = BrowseMode.Swipe;
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseDirection = vertical ? Orientation.Vertical : Orientation.Horizontal;
        var work = IllustrationReaderFixture.Sample(996910, 3) with
        {
            Width = 1600,
            Height = 400,
            MetaPages = Enumerable.Range(0, 3).Select(index => new MetaPage
            {
                ImageUrls = new() { Original = images.Uri(index), Large = images.Uri(index), Medium = images.Uri(index), SquareMedium = images.Uri(index) }
            }).ToArray()
        };
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(work), false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window
        {
            Width = vertical ? 600 : 1000,
            Height = 850,
            RequestedThemeVariant = vertical ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = host.Shell
        };
        var evidence = new List<object>();
        var loading = Array.Empty<Task>();
        try
        {
            window.Show();
            await UntilAsync(() => images.RequestCount == 3 && model.CurrentImage?.Images.Count == 3);
            await RenderAsync(window);
            var imageModels = model.CurrentImage!.Images.ToArray();
            // Observe the same in-flight tasks the real preload/visible paths started.
            loading = imageModels.Select(image => image.LoadOriginalImageAsync()).ToArray();
            Require(imageModels.All(image => image.IsLoadingImage && !image.HasDisplayImage), "All three differently sized images remain delayed before the paging burst");
            var slider = page.GetVisualDescendants().OfType<PagePreviewSlider>().Single();
            var swipe = page.GetVisualDescendants().OfType<SwipeControl>().Single();
            var interruptedTransitions = 0;
            swipe.TransitionCompleted += (_, args) =>
            {
                if (args.From is SingleImageViewer && args.To is SingleImageViewer && !args.HasRunToCompletion)
                    interruptedTransitions++;
            };
            Require(slider.Focus(NavigationMethod.Tab), "The production page selector owns the rapid page keys");
            var burst = Stopwatch.StartNew();
            Press(window, Key.Right, PhysicalKey.ArrowRight);
            Require(model.CurrentPageIndex == 1, "The first key selects page two exactly once");
            window.UpdateLayout();
            Press(window, Key.Right, PhysicalKey.ArrowRight);
            Require(model.CurrentPageIndex == 2, "The second key selects page three without waiting for its image or the slide");
            window.UpdateLayout();
            Press(window, Key.Home, PhysicalKey.Home);
            Require(model.CurrentPageIndex == 0, "Home interrupts the previous page selection");
            window.UpdateLayout();
            Press(window, Key.End, PhysicalKey.End);
            Require(model.CurrentPageIndex == 2 && slider.SelectedIndex == 2, "End leaves the final page selected after the burst");
            var burstMilliseconds = burst.Elapsed.TotalMilliseconds;
            await UntilAsync(() => interruptedTransitions > 0);
            evidence.Add(new { Event = "paging-burst", Keys = "Right,Right,Home,End", ElapsedMilliseconds = burstMilliseconds, InterruptedTransitions = interruptedTransitions });

            if (closePending)
            {
                host.Shell.ClosePage(page);
                Require(!host.Shell.Pages.Contains(page) && model.CurrentImage is null, "The actual tab close releases the rapidly changing reader");
                Require(images.Pending.All(pending => pending.Token.IsCancellationRequested), "Closing cancels all three outstanding image transports");
                var changes = 0;
                foreach (var image in imageModels)
                {
                    image.PropertyChanged += (_, _) => changes++;
                    image.DisplaySource.Changed += (_, _) => changes++;
                }
                var notifications = host.Notifications.ToArray();
                images.Complete(2);
                images.Complete(0);
                images.Complete(1);
                await Task.WhenAll(loading).WaitAsync(TimeSpan.FromSeconds(10));
                await RenderAsync(window);
                Require(changes == 0 && imageModels.All(image => !image.HasDisplayImage && image.OriginalSource is null),
                    "Responses released after production close cannot repopulate any image or publish late property changes");
                Require(host.Notifications.SequenceEqual(notifications), "The closed reader cannot post late notifications");
                Require(images.Pending.All(pending => pending.Content.Disposed), "Late HTTP response content is disposed even after cancellation");
                evidence.Add(new { Event = "closed-before-responses", Changes = changes, ResponseOrder = "2,0,1" });
            }
            else
            {
                images.Complete(2);
                await loading[2].WaitAsync(TimeSpan.FromSeconds(10));
                await RequireFitAsync(2, "last-page-first-response");
                var selectedSource = imageModels[2].OriginalSource;
                var selectedZoom = imageModels[2].ZoomFactor;
                images.Complete(0);
                images.Complete(1);
                await Task.WhenAll(loading).WaitAsync(TimeSpan.FromSeconds(10));
                await RequireFitAsync(2, "last-page-after-earlier-responses");
                Require(model.CurrentPageIndex == 2 && ReferenceEquals(imageModels[2].OriginalSource, selectedSource)
                    && Math.Abs(imageModels[2].ZoomFactor - selectedZoom) < 0.000001,
                    "Earlier page completions cannot replace or resize the selected page");

                Require(slider.Focus(NavigationMethod.Tab), "The selector remains available after delayed loads");
                Press(window, Key.Home, PhysicalKey.Home);
                await RequireFitAsync(0, "first-wide-page");
                await ImageMenuAsync(EntryViewerPageResources.Mirror);
                await ImageMenuAsync(EntryViewerPageResources.RotateClockwise);
                Require(imageModels[0].IsMirrored && imageModels[0].RotationDegree == 90, "The real image menu changes only the first page's manipulation state");
                Require(slider.Focus(NavigationMethod.Tab), "The selector can leave the manipulated first page");
                Press(window, Key.Right, PhysicalKey.ArrowRight);
                await RequireFitAsync(1, "second-tall-page");
                Require(!imageModels[1].IsMirrored && imageModels[1].RotationDegree == 0 && !imageModels[2].IsMirrored && imageModels[2].RotationDegree == 0,
                    "Other pages retain independent manipulation state");
                Require(slider.Focus(NavigationMethod.Tab), "The selector can rapidly revisit initialized pages");
                Press(window, Key.End, PhysicalKey.End);
                Frame(window);
                Press(window, Key.Home, PhysicalKey.Home);
                await RequireFitAsync(0, "return-to-mirrored-rotated-first-page");
                Require(imageModels[0].IsMirrored && imageModels[0].RotationDegree == 90, "Recreated swipe content retains the first page's mirror and rotation");
                Require(images.RequestCount == 3, "Revisiting initialized pages reuses their images without duplicate network requests");
                host.Shell.ClosePage(page);
                Require(model.CurrentImage is null && imageModels.All(image => !image.HasDisplayImage && image.OriginalSource is null),
                    "Production close releases every image after out-of-order completion and repeated selection");
                Require(images.Pending.All(pending => pending.Content.Disposed), "All completed image HTTP responses release their content");
                evidence.Add(new { Event = "closed-after-revisits", Requests = images.RequestCount });
            }
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Rapid image navigation stays within the fixed offline fixture");

            SingleImageViewer Viewer() => page.FindControl<ImageViewerWrapper>("I")!.SwipeImageViewer!.CurrentPage
                ?? throw new InvalidOperationException("No selected swipe content");

            async Task RequireFitAsync(int index, string name)
            {
                await UntilAsync(() => model.CurrentPageIndex == index && imageModels[index].LoadSuccessfully);
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (true)
                {
                    await RenderAsync(window);
                    var viewer = Viewer();
                    var image = imageModels[index];
                    var size = image.RotationDegree % 180 == 0 ? image.DisplaySource.Size : new Size(image.DisplaySource.Size.Height, image.DisplaySource.Size.Width);
                    var scroll = viewer.ViewerScrollView!;
                    var expected = Math.Min(scroll.Viewport.Width / size.Width, scroll.Viewport.Height / size.Height);
                    if (ReferenceEquals(viewer.DataContext, image) && Math.Abs(scroll.ZoomFactor - expected) < 0.001
                        && Math.Abs(image.ZoomFactor - expected) < 0.001 && viewer.ImageViewer!.Opacity > 0.99)
                    {
                        var bar = page.FindControl<CommandBar>("ZoomPane")!;
                        var expectedText = image.ZoomFactor.ToString("P0", CultureInfo.CurrentCulture);
                        Require(bar.PrimaryCommands.OfType<Control>().SelectMany(control => control.GetVisualDescendants().OfType<TextBlock>())
                            .Any(text => text.Text == expectedText), "The actual toolbar percentage follows the selected image");
                        // Finish only the existing page-slide transition before probing exact pixels.
                        await Task.Delay(320);
                        Frame(window);
                        var visible = RequirePixels(window, viewer, index, size * expected, directory, name);
                        evidence.Add(new { Event = name, Index = index, Source = image.DisplaySource.Size.ToString(), Viewport = scroll.Viewport.ToString(), Zoom = image.ZoomFactor, Visible = visible.ToString() });
                        return;
                    }
                    if (DateTime.UtcNow > deadline)
                        throw new InvalidOperationException($"Selected image fit did not settle: {name}, index={model.CurrentPageIndex}, source={image.DisplaySource.Size}, viewport={scroll.Viewport}, zoom={scroll.ZoomFactor}/{image.ZoomFactor}, expected={expected}, opacity={viewer.ImageViewer?.Opacity}");
                }
            }

            async Task ImageMenuAsync(string resource)
            {
                var viewer = Viewer();
                var point = viewer.TranslatePoint(new Rect(viewer.Bounds.Size).Center, window)!.Value;
                window.MouseMove(point);
                window.MouseDown(point, MouseButton.Right);
                window.MouseUp(point, MouseButton.Right);
                await RenderAsync(window);
                var menu = viewer.ContextMenu!;
                var item = menu.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == I18NManager.GetResource(resource));
                Require(menu.IsOpen && item.IsEffectivelyEnabled && item.Focus(NavigationMethod.Tab), "The current image menu exposes " + resource);
                Press(window, Key.Enter, PhysicalKey.Enter);
                await RenderAsync(window);
                Require(!menu.IsOpen, "Image manipulation closes its context menu");
            }
        }
        finally
        {
            images.CompleteAll();
            host.Shell.CloseAllPages();
            window.Close();
            await Task.WhenAll(loading).WaitAsync(TimeSpan.FromSeconds(10));
            File.WriteAllText(Path.Combine(directory, "rapid-pages.json"), JsonSerializer.Serialize(new
            {
                Direction = vertical ? "Vertical" : "Horizontal",
                Exit = closePending ? "CloseBeforeResponses" : "CompleteRevisitThenClose",
                Requests = images.RequestCount,
                Evidence = evidence,
                Boundary = "Fixed static images, production Swipe/page-selector/close and headless rendering. No native input, GPU, animation decoding or performance budget is claimed."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static void Frame(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static Size RequirePixels(Window window, SingleImageViewer viewer, int index, Size expected, string directory, string name)
    {
        window.MouseMove(new Point(1, 1));
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rapid paging frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
        using var pixels = frame.Lock();
        Require(pixels.Format == PixelFormat.Bgra8888 || pixels.Format == PixelFormat.Rgba8888, "The image probe uses a known rendered pixel format");
        var bytes = new byte[pixels.RowBytes * pixels.Size.Height];
        Marshal.Copy(pixels.Address, bytes, 0, bytes.Length);
        var red = pixels.Format == PixelFormat.Bgra8888 ? 2 : 0;
        var blue = pixels.Format == PixelFormat.Bgra8888 ? 0 : 2;
        var clip = new Rect(viewer.TranslatePoint(default, window)!.Value, viewer.Bounds.Size);
        var left = pixels.Size.Width;
        var right = -1;
        var top = pixels.Size.Height;
        var bottom = -1;
        var counts = new int[3];
        for (var y = Math.Max(0, (int) clip.Top); y < Math.Min(pixels.Size.Height, clip.Bottom); y++)
            for (var x = Math.Max(0, (int) clip.Left); x < Math.Min(pixels.Size.Width, clip.Right); x++)
            {
                var offset = y * pixels.RowBytes + x * 4;
                var r = bytes[offset + red];
                var g = bytes[offset + 1];
                var b = bytes[offset + blue];
                var color = r > 220 && g < 30 && b < 30 ? 0 : r < 30 && g > 220 && b < 30 ? 1 : r < 30 && g < 30 && b > 220 ? 2 : -1;
                if (color < 0) continue;
                counts[color]++;
                if (color != index) continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        Require(counts[index] > 1000 && counts.Where((_, color) => color != index).All(count => count < 20),
            "Only the selected page's actual image color remains visible: " + name + ": " + string.Join(',', counts));
        var actual = new Size(right - left + 1, bottom - top + 1);
        Require(Math.Abs(actual.Width - expected.Width) <= 3 && Math.Abs(actual.Height - expected.Height) <= 3,
            $"The rendered image is fitted to its own pixel aspect ratio: {name}, actual={actual}, expected={expected}");
        return actual;
    }

    private sealed class DelayedImages : IDisposable
    {
        private readonly ConcurrentDictionary<int, PendingImage> _pending = new();
        private readonly byte[][] _bytes = [Png(1600, 400, SKColors.Red), Png(300, 1500, SKColors.Lime), Png(1200, 800, SKColors.Blue)];
        internal int RequestCount => _pending.Count;
        internal IEnumerable<PendingImage> Pending => _pending.Values;
        internal string Uri(int index) => $"https://i.pximg.net/m3e/rapid-pages-{index}.png";

        internal DelayedImages(OfflineSearchFixture http)
        {
            http.ImageResponseFactory = async (request, token) =>
            {
                var index = Enumerable.Range(0, 3).Single(index => request.RequestUri!.AbsoluteUri == Uri(index));
                var pending = new PendingImage(token, new TrackedContent(_bytes[index]));
                Require(_pending.TryAdd(index, pending), "Each image download is shared by preload, preview and repeated page visits");
                // Ignore cancellation at this external boundary to exercise late response rejection.
                await pending.Released.Task;
                return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = pending.Content };
            };
        }

        internal void Complete(int index) => _pending[index].Released.TrySetResult();
        internal void CompleteAll()
        {
            foreach (var pending in _pending.Values) pending.Released.TrySetResult();
        }
        public void Dispose() => CompleteAll();

        private static byte[] Png(int width, int height, SKColor color)
        {
            using var bitmap = new SKBitmap(width, height);
            bitmap.Erase(color);
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }
    }

    private sealed record PendingImage(CancellationToken Token, TrackedContent Content)
    {
        internal TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class TrackedContent(byte[] bytes) : ByteArrayContent(bytes)
    {
        internal bool Disposed { get; private set; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
