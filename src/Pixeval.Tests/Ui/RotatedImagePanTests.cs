using System;
using System.Collections.Generic;
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
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using SkiaSharp;
using SmoothScroll.Avalonia.Controls;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class RotatedImagePanTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(1600, 300, 1)]
    [DataRow(1600, 300, -1)]
    [DataRow(300, 1600, 1)]
    [DataRow(300, 1600, -1)]
    [DataRow(1600, 300, 0)]
    [DataRow(1600, 300, 2)]
    [DataRow(300, 1600, 0)]
    [DataRow(300, 1600, 2)]
    public Task DragReachesBothEndsOfTheDisplayedImage(int width, int height, int quarterTurns) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, width, height, quarterTurns));

    private static async Task RunAsync(string directory, int width, int height, int quarterTurns)
    {
        using var fixture = new IllustrationReaderFixture();
        fixture.Http.ImageResponses[IllustrationReaderFixture.ImageUri] = CornerImage(width, height);
        const string originalUri = "https://i.pximg.net/m3e/rotated-pan-original.png";
        fixture.Http.ImageResponses[originalUri] = CornerImage(width * 2, height * 2, 80);
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = BrowseMode.Swipe;
        var work = IllustrationReaderFixture.Sample(996496, 1) with
        {
            Width = width,
            Height = height,
            MetaSinglePage = new() { OriginalImageUrl = originalUri }
        };
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(work), false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 700, Height = 500, Content = host.Shell };
        List<string> report = [];
        try
        {
            window.Show();
            await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            await RenderAsync(window);
            var image = model.CurrentImage!.CurrentPage!;
            var viewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, image));
            var bar = page.FindControl<CommandBar>("ZoomPane")!;
            var scroll = viewer.ViewerScrollView!;
            for (var turn = 0; turn < Math.Abs(quarterTurns); turn++)
                await ActionAsync(quarterTurns > 0 ? EntryViewerPageResources.RotateClockwise : EntryViewerPageResources.RotateCounterclockwise);
            await ActionAsync(EntryViewerPageResources.ZoomToOriginal);
            await UntilAsync(() => Math.Abs(scroll.ZoomFactor - 1) < 0.001);
            await HideOverflowAsync();
            await RenderAsync(window);
            Require(image.RotationDegree == (quarterTurns * 90 + 360) % 360, "The production toolbar selects the tested rotation");
            var vertical = quarterTurns % 2 == 0 ? height > width : width > height;
            var delta = vertical ? new Vector(0, 4000) : new Vector(4000, 0);
            var visibleCorners = new int[4];
            Capture("original-scale");
            await DragAsync(delta);
            Capture("display-start");
            await DragAsync(-delta);
            Capture("display-end");
            Require(visibleCorners.All(count => count > 400),
                "Dragging along the displayed long axis must reach all four source corners: " + string.Join(",", visibleCorners));
            var max = scroll.ScrollBarMaximum;
            Require(vertical ? max.Y > 0 && max.X < 1 : max.X > 0 && max.Y < 1,
                "Only the displayed overflowing axis is scrollable: " + max);
            await DragAsync(vertical ? new Vector(4000, 0) : new Vector(0, 4000));
            Require(vertical ? Math.Abs(scroll.Offset.X) < 1 : Math.Abs(scroll.Offset.Y) < 1,
                "Dragging the fitting short axis settles without a blank scroll range");
            await DragAsync(delta * 0.04);
            var beforeReplacement = Capture("before-original");
            Require(beforeReplacement.Any(point => point is not null), "The off-center viewport contains a visible interior source marker");
            var offsetBefore = scroll.Offset;
            var extentBefore = scroll.Extent;
            var original = viewer.ContextMenu!.Items.OfType<MenuItem>()
                .Single(item => item.Header?.ToString() == I18NManager.GetResource(EntryViewerPageResources.ViewOriginal));
            var menuPoint = InputPoint();
            window.MouseDown(menuPoint, MouseButton.Right);
            window.MouseUp(menuPoint, MouseButton.Right);
            await RenderAsync(window);
            Require(viewer.ContextMenu.IsOpen && original.IsEffectivelyEnabled && original.Focus(NavigationMethod.Tab),
                "The real image menu offers the full-resolution source");
            Press(window, Key.Enter, PhysicalKey.Enter);
            await image.ViewOriginalCommand.ExecutionTask!;
            await UntilAsync(() => image.DisplaySource.Size.Width == width * 2 && Math.Abs(scroll.ZoomFactor - 0.5) < 0.001);
            await IllustrationTransformValidation.DismissAsync(window);
            await RenderAsync(window);
            var afterReplacement = Capture("after-original");
            Require(Math.Abs(scroll.Offset.X - offsetBefore.X) < 1.1 && Math.Abs(scroll.Offset.Y - offsetBefore.Y) < 1.1
                && Math.Abs(scroll.Extent.Width - extentBefore.Width) < 1.1 && Math.Abs(scroll.Extent.Height - extentBefore.Height) < 1.1,
                "Real source replacement preserves the displayed scroll extent and off-center viewport at half pixel scale");
            for (var marker = 0; marker < beforeReplacement.Length; marker++)
                if (beforeReplacement[marker] is { } before)
                    Require(afterReplacement[marker] is { } after && Math.Abs(after.X - before.X) < 1.5 && Math.Abs(after.Y - before.Y) < 1.5,
                        "The rendered interior marker stays at the same viewport position after the original image replaces its preview");
            host.Shell.NavigateTo(new Pixeval.Views.Search.SearchPage());
            await RenderAsync(window);
            host.CloseDestinationAndReturn();
            await RenderAsync(window);
            Require(ReferenceEquals(image, model.CurrentImage.CurrentPage), "Returning retains the panned image model");
            host.Shell.ClosePage(page);
            Require(!image.HasDisplayImage, "Production tab close releases the panned image");
            report.Add("PASS actual toolbar, original pixel scale, pointer dragging to both displayed ends, short-axis boundary, original-source viewport/marker preservation, navigation and close");

            async Task ActionAsync(string key)
            {
                var button = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => button.IsVisible && button.Label == I18NManager.GetResource(key));
                await ActivateAsync(window, button);
            }

            async Task HideOverflowAsync()
            {
                if (!bar.IsOpen) return;
                var entry = bar.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_OverflowButton");
                Require(entry.Focus(), "The production overflow can close");
                Press(window, Key.Escape, PhysicalKey.Escape);
                await RenderAsync(window);
                Require(!bar.IsOpen, "Escape closes the real command overflow before panning");
            }

            async Task DragAsync(Vector movement)
            {
                var start = InputPoint();
                report.Add($"drag {movement}: start={start}; hit={window.InputHitTest(start)?.GetType().FullName}; state={scroll.State}");
                window.MouseMove(start);
                window.MouseDown(start, MouseButton.Left);
                // The first move establishes the pan recognizer's captured baseline.
                window.MouseMove(start, RawInputModifiers.LeftMouseButton);
                for (var step = 1; step <= 8; step++)
                {
                    window.MouseMove(start + movement * (step / 8d), RawInputModifiers.LeftMouseButton);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                    await Task.Delay(20);
                    if (step is 1 or 8) report.Add($"drag step {step}: state={scroll.State}; offset={scroll.Offset}");
                }
                // Hold at the captured end before release; the production tracker still owns boundary settling.
                await Task.Delay(120);
                window.MouseUp(start + movement, MouseButton.Left);
                await UntilAsync(() => scroll.State == ScrollingInteractionState.Idle);
                await RenderAsync(window);
                window.MouseMove(new Point(1, 1));
            }

            Point InputPoint() => (from y in new[] { 0.5, 0.25, 0.75, 0.1, 0.9 }
                                   from x in new[] { 0.5, 0.25, 0.75, 0.1, 0.9 }
                                   let point = viewer.TranslatePoint(new Point(viewer.Bounds.Width * x, viewer.Bounds.Height * y), window)!.Value
                                   let hit = window.InputHitTest(point) as Visual
                                   where hit is not null && (ReferenceEquals(hit, scroll) || hit.GetVisualAncestors().Contains(scroll))
                                   select (Point?) point).FirstOrDefault()
                    ?? throw new InvalidOperationException("The visible viewport must provide an actual scroll content input target");

            Point?[] Capture(string name)
            {
                using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No panned image frame");
                frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
                using var pixels = frame.Lock();
                var data = new byte[pixels.RowBytes * pixels.Size.Height];
                Marshal.Copy(pixels.Address, data, 0, data.Length);
                Require(pixels.Format == PixelFormat.Bgra8888 || pixels.Format == PixelFormat.Rgba8888, "Known pixel format");
                var rIndex = pixels.Format == PixelFormat.Bgra8888 ? 2 : 0;
                var bIndex = pixels.Format == PixelFormat.Bgra8888 ? 0 : 2;
                var counts = new int[4];
                var markerCounts = new int[2];
                var markerSums = new Vector[2];
                var area = new Rect(viewer.TranslatePoint(default, window)!.Value, viewer.Bounds.Size);
                for (var y = Math.Max(0, (int) area.Top); y < Math.Min(pixels.Size.Height, area.Bottom); y++)
                    for (var x = Math.Max(0, (int) area.Left); x < Math.Min(pixels.Size.Width, area.Right); x++)
                    {
                        var index = y * pixels.RowBytes + x * 4;
                        var r = data[index + rIndex]; var g = data[index + 1]; var b = data[index + bIndex];
                        if (r > 220 && g < 30 && b < 30) counts[0]++;
                        if (r < 30 && g > 220 && b < 30) counts[1]++;
                        if (r < 30 && g < 30 && b > 220) counts[2]++;
                        if (r > 220 && g > 220 && b < 30) counts[3]++;
                        var marker = r < 30 && g > 220 && b > 220 ? 0 : r > 220 && g < 30 && b > 220 ? 1 : -1;
                        if (marker >= 0) { markerCounts[marker]++; markerSums[marker] += new Vector(x, y); }
                    }
                for (var i = 0; i < counts.Length; i++) visibleCorners[i] = Math.Max(visibleCorners[i], counts[i]);
                report.Add($"{name}: source={width}x{height}; angle={image.RotationDegree}; viewport={scroll.Viewport}; extent={scroll.Extent}; offset={scroll.Offset}; maximum={scroll.ScrollBarMaximum}; corners={string.Join(',', counts)}");
                return markerCounts.Select((count, index) => count > 100 ? (Point?) new Point(markerSums[index].X / count, markerSums[index].Y / count) : null).ToArray();
            }
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "rotated-pan-contracts.txt"), report);
            window.Close();
        }
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Panning uses only the fixed offline source");
    }

    private static byte[] CornerImage(int width, int height, int markerSize = 40)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint();
        var colors = new[] { SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Yellow };
        for (var i = 0; i < colors.Length; i++)
        {
            paint.Color = colors[i];
            var x = i % 2 == 0 ? 0 : width - markerSize;
            var y = i / 2 == 0 ? 0 : height - markerSize;
            canvas.DrawRect(new SKRect(x, y, x + markerSize, y + markerSize), paint);
        }
        foreach (var (fraction, color) in new[] { (0.25f, SKColors.Cyan), (0.75f, SKColors.Magenta) })
        {
            paint.Color = color;
            var x = width > height ? width * fraction : width / 2f;
            var y = width > height ? height / 2f : height * fraction;
            canvas.DrawRect(new SKRect(x - markerSize / 2f, y - markerSize / 2f, x + markerSize / 2f, y + markerSize / 2f), paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
