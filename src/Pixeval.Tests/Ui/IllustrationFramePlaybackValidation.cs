using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Pixeval.Models.Options;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Search;
using Pixeval.Views.Viewers;
using SkiaSharp;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class IllustrationFramePlaybackValidation
{
    internal static async Task RunAsync(string directory, string file, bool continuous)
    {
        using var fixture = new IllustrationReaderFixture();
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = continuous ? BrowseMode.Continuous : BrowseMode.Swipe;
        var uri = "https://i.pximg.net/m3e/" + file;
        fixture.Http.ImageResponses[uri] = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ProgressiveImages", file));
        // Sources can describe a single image even when the encoded response has multiple frames.
        var entry = IllustrationReaderFixture.Sample(996460, 1) with
        {
            ThumbnailUrls = new() { Medium = uri, Large = uri, SquareMedium = uri },
            MetaSinglePage = new() { OriginalImageUrl = uri }
        };
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(entry), false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 700, Height = 760, Content = host.Shell, RequestedThemeVariant = ThemeVariant.Dark };
        try
        {
            window.Show();
            await UntilAsync(() => model.CurrentImage?.CurrentPage is { LoadSuccessfully: true } or { LoadErrorMessage: not null });
            await RenderAsync(window);
            var image = model.CurrentImage!.CurrentPage!;
            Require(image.LoadSuccessfully && image.DisplaySource.FrameCount == 3, "The real image pipeline decodes all three " + file + " frames");
            Require(image.IsPicGif && image.PlayPauseCommand.CanExecute(null), "Decoded animation exposes playback controls even with single-image metadata");
            Require(!image.CopyCommand.CanExecute(null) && !image.SaveAsCommand.CanExecute(null)
                && !image.TransformExtensionCommand.CanExecute(null), "Animated content cannot be silently flattened by single-frame actions");
            await ObserveAsync(window, directory, file + "-playing", true, 3);
            if (!continuous)
            {
                var toggle = page.GetVisualDescendants().OfType<CommandBarToggleButton>().Single(b => ReferenceEquals(b.Command, image.PlayPauseCommand));
                Require(toggle.IsVisible, "The floating toolbar uses decoded animation capability");
                await ActivateAsync(window, toggle);
            }
            else
            {
                Require(page.FindControl<ImageViewerWrapper>("I")!.GetVisualDescendants().OfType<SmoothScroll.Avalonia.Controls.ScrollView>().First().Focus(NavigationMethod.Tab),
                    "The continuous viewport accepts playback keyboard input");
                Press(window, Key.Space, PhysicalKey.Space);
                await RenderAsync(window);
            }
            Require(!image.IsPlaying, "The real playback entry pauses the decoded animation");
            await ObserveAsync(window, directory, file + "-paused", false);
            host.Shell.NavigateTo(new SearchPage());
            await RenderAsync(window);
            host.CloseDestinationAndReturn();
            await RenderAsync(window);
            Require(!image.IsPlaying, "Navigation preserves the paused playback preference");
            await ObserveAsync(window, directory, file + "-returned-paused", false);
            var viewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(v => ReferenceEquals(v.DataContext, image));
            var viewport = page.FindControl<ImageViewerWrapper>("I")!.GetVisualDescendants().OfType<SmoothScroll.Avalonia.Controls.ScrollView>().First();
            Require(viewport.Focus(NavigationMethod.Tab), "Playback focus can return to the real viewport");
            Press(window, Key.Space, PhysicalKey.Space);
            await RenderAsync(window);
            var area = page.FindControl<ImageViewerWrapper>("I")!;
            var position = new Rect(viewer.TranslatePoint(default, window)!.Value, viewer.Bounds.Size)
                .Intersect(new Rect(area.TranslatePoint(default, window)!.Value, area.Bounds.Size)).Center;
            window.MouseDown(position, MouseButton.Right);
            window.MouseUp(position, MouseButton.Right);
            await RenderAsync(window);
            Require(image.IsPlaying && viewer.ContextMenu!.Items.OfType<MenuItem>().Single(m => ReferenceEquals(m.Command, image.PlayPauseCommand)).IsVisible,
                "Space resumes and the context menu exposes the same animated source");
            Press(window, Key.Escape, PhysicalKey.Escape);
            await RenderAsync(window);
            await ObserveAsync(window, directory, file + "-resumed", true, 3);
            var decoded = image.OriginalSource!;
            host.Shell.ClosePage(page);
            Require(decoded.FrameCount == 0 && !image.PlayPauseCommand.CanExecute(null), "Actual tab close releases the GIF/WebP frames and disables playback");
        }
        finally { window.Close(); }
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Animated image input stays offline");
    }

    internal static byte[] Png(SKColor color)
    {
        using var bitmap = new SKBitmap(128, 128);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    internal static async Task ObserveAsync(Window window, string directory, string name, bool playing, int expectedColors = 2)
    {
        var clock = Stopwatch.StartNew();
        List<string> samples = [];
        HashSet<string> states = [];
        var transitions = 0;
        string? previous = null;
        try
        {
            do
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No animation frame");
                var red = 0;
                var green = 0;
                var blue = 0;
                using (var pixels = frame.Lock())
                {
                    var data = new byte[pixels.RowBytes * pixels.Size.Height];
                    Marshal.Copy(pixels.Address, data, 0, data.Length);
                    Require(pixels.Format == PixelFormat.Bgra8888 || pixels.Format == PixelFormat.Rgba8888, "Pixel probe requires a known 32-bit format");
                    var redIndex = pixels.Format == PixelFormat.Bgra8888 ? 2 : 0;
                    var blueIndex = pixels.Format == PixelFormat.Bgra8888 ? 0 : 2;
                    for (var y = 0; y < pixels.Size.Height; y += 4)
                        for (var x = 0; x < pixels.Size.Width; x += 4)
                        {
                            var offset = y * pixels.RowBytes + x * 4;
                            if (data[offset + redIndex] > 220 && data[offset + 1] < 30 && data[offset + blueIndex] < 30) red++;
                            if (data[offset + redIndex] < 30 && data[offset + 1] > 100 && data[offset + blueIndex] < 30) green++;
                            if (data[offset + redIndex] < 30 && data[offset + 1] < 30 && data[offset + blueIndex] > 220) blue++;
                        }
                }
                var state = red > 128 && red > Math.Max(green, blue) * 2 ? "red"
                    : green > 128 && green > Math.Max(red, blue) * 2 ? "green"
                    : blue > 128 && blue > Math.Max(red, green) * 2 ? "blue" : "missing";
                samples.Add($"{clock.Elapsed.TotalMilliseconds:F1}ms: {state}, red={red}, green={green}, blue={blue}");
                Require(state != "missing", "The animated frame must be visibly rendered: " + samples[^1]);
                if (states.Add(state)) frame.Save(Path.Combine(directory, name + "-" + state + ".png"), new PngBitmapEncoderOptions());
                if (previous is not null && state != previous) transitions++;
                previous = state;
                if (!playing) Require(states.Count == 1, "Paused pixels cannot advance: " + name);
                if (playing && states.Count == expectedColors && transitions >= expectedColors) break;
                await Task.Delay(25);
            } while (clock.Elapsed < TimeSpan.FromMilliseconds(playing ? 4000 : 700));
            Require(!playing || states.Count == expectedColors && transitions >= expectedColors, "Playing pixels must advance and loop: " + name);
        }
        finally { File.WriteAllLines(Path.Combine(directory, name + "-samples.txt"), samples); }
    }
}
