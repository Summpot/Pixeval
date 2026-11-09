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
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
using SmoothScroll.Avalonia.Controls;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class ContinuousOriginalImageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task OriginalReplacementPreservesTheOffCenterContinuousViewport(bool vertical) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, vertical));

    private static async Task RunAsync(string directory, bool vertical)
    {
        using var fixture = new IllustrationReaderFixture();
        var width = vertical ? 1600 : 600;
        var height = vertical ? 600 : 1600;
        const string previewUri = "https://i.pximg.net/m3e/continuous-original-preview.png";
        const string originalUri = "https://i.pximg.net/m3e/continuous-original-full.png";
        const string neighborUri = "https://i.pximg.net/m3e/continuous-original-neighbor.png";
        fixture.Http.ImageResponses[previewUri] = Image(width, height, false);
        fixture.Http.ImageResponses[originalUri] = Image(width * 2, height * 2, true);
        fixture.Http.ImageResponses[neighborUri] = Image(width, height, false, SKColors.Lime);
        var settings = App.AppViewModel.AppSettings.BrowsingExperienceSettings;
        settings.BrowseMode = BrowseMode.Continuous;
        settings.BrowseDirection = vertical ? Orientation.Vertical : Orientation.Horizontal;
        var work = IllustrationReaderFixture.Sample(996499, 2) with
        {
            Width = width,
            Height = height,
            MetaPages =
            [
                new MetaPage { ImageUrls = new() { Original = originalUri, Large = previewUri, Medium = previewUri, SquareMedium = previewUri } },
                new MetaPage { ImageUrls = new() { Original = neighborUri, Large = neighborUri, Medium = neighborUri, SquareMedium = neighborUri } }
            ]
        };
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(work), false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 1000, Height = 850, Content = host.Shell };
        List<string> report = [$"direction={settings.BrowseDirection}; preview={width}x{height}; original={width * 2}x{height * 2}"];
        try
        {
            window.Show();
            await UntilAsync(() => model.CurrentImage?.Images.All(image => image.LoadSuccessfully) == true);
            await RenderAsync(window);
            var images = model.CurrentImage!.Images.ToArray();
            var first = images[0];
            var neighbor = images[1];
            var continuous = page.GetVisualDescendants().OfType<ContinuousImageViewer>().Single();
            var scroll = continuous.FindControl<ScrollView>("ViewerScrollView")!;
            var items = continuous.FindControl<ItemsControl>("ImageItemsControl")!;
            var panel = (Pixeval.Controls.VirtualizingStackPanel) items.ItemsPanelRoot!;
            var selector = page.GetVisualDescendants().OfType<PagePreviewSlider>().Single();
            var bar = page.FindControl<CommandBar>("ZoomPane")!;
            Require(first.DisplaySource.Size == new Size(width, height), "The initial page is the smaller preview source");
            var oldFrame = first.DisplaySource.Frames[0];
            var neighborFrame = neighbor.DisplaySource.Frames[0];
            await MenuAsync(EntryViewerPageResources.Mirror);
            await MenuAsync(vertical ? EntryViewerPageResources.RotateCounterclockwise : EntryViewerPageResources.RotateClockwise);
            var rotation = vertical ? 270 : 90;
            Require(first.IsMirrored && first.RotationDegree == rotation, "Real image menus apply the retained mirror and rotation");
            var originalSize = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button =>
                button.IsVisible && button.Label == I18NManager.GetResource(EntryViewerPageResources.ZoomToOriginal));
            await ActivateAsync(window, originalSize);
            await UntilAsync(() => Math.Abs(continuous.ZoomFactor - 1) < 0.001);
            await DismissAsync();

            var displayed = ImageBounds(Viewer(0), items);
            var length = (vertical ? displayed.Height : displayed.Width) * scroll.ZoomFactor;
            var viewportLength = vertical ? scroll.Viewport.Height : scroll.Viewport.Width;
            var offset = length / 8;
            Require(length > viewportLength + 300 && Math.Abs(offset - (length - viewportLength) / 2) > 60,
                "The rotated non-square page provides a deliberately non-center viewport");
            // Exercise source replacement at a deterministic interior viewport;
            // pointer/trackpad panning itself belongs to the gesture contracts.
            scroll.ScrollTo(vertical ? new(0, offset) : new(offset, 0), false);
            await RenderAsync(window);
            await UntilAsync(() => scroll.State == ScrollingInteractionState.Idle);
            Require(ReferenceEquals(continuous.CurrentPage, first) && model.CurrentPageIndex == 0,
                "The off-center viewport still belongs to the first real page");
            var before = Capture("before-original");
            Require(before.Markers.Any(point => point is not null) && before.OriginalDetailPixels == 0,
                "The preview visibly contains an interior marker and no original-only detail");
            var oldOffset = scroll.Offset;
            var oldExtent = scroll.Extent;
            var oldCanvasZoom = scroll.ZoomFactor;
            var oldPixelScale = Viewer(0).ImageViewer!.SourceScale.X;
            var oldReportedZoom = continuous.ZoomFactor;
            var oldImageBounds = ImageBounds(Viewer(0), items);
            Require(panel.TryGetItemOffset(1, out var oldNeighborOffset),
                "The virtualizing panel retains the measured offscreen neighbor offset");

            await MenuAsync(EntryViewerPageResources.ViewOriginal);
            await first.ViewOriginalCommand.ExecutionTask!;
            await UntilAsync(() => first.DisplaySource.Size == new Size(width * 2, height * 2)
                && Math.Abs(Viewer(0).ImageViewer!.SourceScale.X * 2 - oldPixelScale) < 0.001);
            await DismissAsync();
            var after = Capture("after-original");
            Require(first.TransformedSource is { IsInitialized: true } && !first.IsProcessingImage
                && !ReferenceEquals(oldFrame, first.DisplaySource.Frames[0]), "The actual original command publishes the new decoded source and finishes");
            Require(first.IsMirrored && first.RotationDegree == rotation,
                "Source replacement retains the current page's rotation and mirror");
            Require(Near(scroll.Offset, oldOffset) && Near(scroll.Extent, oldExtent)
                && Math.Abs(scroll.ZoomFactor - oldCanvasZoom) < 0.001,
                "Same-aspect original replacement preserves the canvas zoom, scroll extent and off-center offset");
            var newImageBounds = ImageBounds(Viewer(0), items);
            Require(Near(newImageBounds.Position, oldImageBounds.Position) && Near(newImageBounds.Size, oldImageBounds.Size)
                && panel.TryGetItemOffset(1, out var newNeighborOffset) && Math.Abs(newNeighborOffset - oldNeighborOffset) < 1.5,
                "The displayed first-page geometry and its neighbor's layout position stay fixed");
            Require(Math.Abs(continuous.ZoomFactor * 2 - oldReportedZoom) < 0.001,
                "Twice the source resolution halves the reported pixel scale while retaining displayed geometry");
            for (var index = 0; index < before.Markers.Length; index++)
                if (before.Markers[index] is { } marker)
                    Require(after.Markers[index] is { } replacement && Near(marker, replacement),
                        "The visible interior marker keeps its viewport position after replacement: " + index);
            Require(after.OriginalDetailPixels > 100,
                "Original-only orange detail is visibly rendered inside the retained marker position");
            Require(ReferenceEquals(neighborFrame, neighbor.DisplaySource.Frames[0]) && neighbor.TransformedSource is null
                && neighbor.DisplaySource.Size == new Size(width, height) && !neighbor.IsMirrored && neighbor.RotationDegree == 0,
                "Replacing the first page does not replace or manipulate its neighbor");

            await SelectAsync(Key.End, PhysicalKey.End, 1);
            Require(Capture("neighbor").NeighborPixels > 1000, "The actual page selector visibly reaches the unchanged green neighbor");
            await SelectAsync(Key.Home, PhysicalKey.Home, 0);
            Require(ReferenceEquals(model.CurrentImage.CurrentPage, first) && first.IsMirrored && first.RotationDegree == rotation
                && first.DisplaySource.Size == new Size(width * 2, height * 2), "Returning retains the replaced first page and its own manipulation state");
            Capture("returned");
            host.Shell.ClosePage(page);
            Require(model.CurrentImage is null && images.All(image => !image.HasDisplayImage && image.OriginalSource is null
                && image.TransformedSource is null && !image.ViewOriginalCommand.CanExecute(null)),
                "Production tab close releases both pages and disables the original action");
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Continuous original replacement uses only fixed offline images");
            report.Add("PASS real image menus, non-center pixel position, original-only detail, source resolution and pixel scale, independent neighbor, navigation and production close");

            SingleImageViewer Viewer(int index) => page.GetVisualDescendants().OfType<SingleImageViewer>()
                .Single(viewer => ReferenceEquals(viewer.DataContext, images[index]));

            async Task SelectAsync(Key key, PhysicalKey physicalKey, int expected)
            {
                Require(selector.Focus(NavigationMethod.Tab), "The actual page selector receives focus");
                Press(window, key, physicalKey);
                await UntilAsync(() => model.CurrentPageIndex == expected);
                await RenderAsync(window);
                await UntilAsync(() => scroll.State == ScrollingInteractionState.Idle);
                await RenderAsync(window);
            }

            async Task DismissAsync()
            {
                if (bar.IsOpen)
                {
                    var overflow = bar.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_OverflowButton");
                    Require(overflow.Focus(NavigationMethod.Tab), "The reader overflow can receive Escape");
                    Press(window, Key.Escape, PhysicalKey.Escape);
                    await RenderAsync(window);
                    Require(!bar.IsOpen, "Escape closes the actual reader overflow");
                }
                await IllustrationTransformValidation.DismissAsync(window);
                await RenderAsync(window);
            }

            async Task MenuAsync(string resource)
            {
                await DismissAsync();
                var viewer = Viewer(0);
                var origin = scroll.TranslatePoint(default, window)!.Value;
                var target = (from y in new[] { 0.5, 0.25, 0.75, 0.1, 0.9 }
                              from x in new[] { 0.5, 0.25, 0.75, 0.1, 0.9 }
                              let point = origin + new Vector(scroll.Viewport.Width * x, scroll.Viewport.Height * y)
                              let hit = window.InputHitTest(point) as Visual
                              where hit is not null && (ReferenceEquals(hit, viewer) || hit.GetVisualAncestors().Contains(viewer))
                              select (Point?) point).FirstOrDefault();
                Require(target is not null, "The visible first page has a real context-menu hit target");
                window.MouseMove(target!.Value);
                window.MouseDown(target.Value, MouseButton.Right);
                window.MouseUp(target.Value, MouseButton.Right);
                await RenderAsync(window);
                var item = viewer.ContextMenu!.Items.OfType<MenuItem>().Single(menu => menu.Header?.ToString() == I18NManager.GetResource(resource));
                Require(viewer.ContextMenu.IsOpen && item.IsEffectivelyEnabled && item.Focus(NavigationMethod.Tab),
                    "The actual image menu offers " + resource);
                Press(window, Key.Enter, PhysicalKey.Enter);
                await RenderAsync(window);
                Require(!viewer.ContextMenu.IsOpen, "The image action closes its real menu");
            }

            Probe Capture(string name)
            {
                window.MouseMove(new Point(1, 1));
                using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No continuous original frame");
                frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
                using var pixels = frame.Lock();
                var data = new byte[pixels.RowBytes * pixels.Size.Height];
                Marshal.Copy(pixels.Address, data, 0, data.Length);
                Require(pixels.Format == PixelFormat.Bgra8888 || pixels.Format == PixelFormat.Rgba8888, "Known screenshot pixel format");
                var redIndex = pixels.Format == PixelFormat.Bgra8888 ? 2 : 0;
                var blueIndex = pixels.Format == PixelFormat.Bgra8888 ? 0 : 2;
                var area = new Rect(scroll.TranslatePoint(default, window)!.Value, scroll.Viewport);
                var counts = new int[2];
                var sums = new Vector[2];
                var details = 0;
                var neighborPixels = 0;
                for (var y = Math.Max(0, (int) area.Top); y < Math.Min(pixels.Size.Height, area.Bottom); y++)
                    for (var x = Math.Max(0, (int) area.Left); x < Math.Min(pixels.Size.Width, area.Right); x++)
                    {
                        var index = y * pixels.RowBytes + x * 4;
                        var red = data[index + redIndex]; var green = data[index + 1]; var blue = data[index + blueIndex];
                        var marker = red < 30 && green > 220 && blue > 220 ? 0 : red > 220 && green < 30 && blue > 220 ? 1 : -1;
                        if (marker >= 0) { counts[marker]++; sums[marker] += new Vector(x, y); }
                        if (red > 220 && green is > 90 and < 170 && blue < 30) details++;
                        if (red < 30 && green > 220 && blue < 30) neighborPixels++;
                    }
                var markers = counts.Select((count, index) => count > 100
                    ? (Point?) new Point(sums[index].X / count, sums[index].Y / count) : null).ToArray();
                var diagnosticViewer = page.GetVisualDescendants().OfType<SingleImageViewer>()
                    .FirstOrDefault(viewer => ReferenceEquals(viewer.DataContext, first));
                var firstGeometry = diagnosticViewer?.ImageViewer is { } displayedImage
                    ? $"pixelScale={displayedImage.SourceScale}; imageBounds={ImageBounds(diagnosticViewer, items)}"
                    : "firstViewer=virtualized";
                report.Add($"{name}: source={first.DisplaySource.Size}; angle={first.RotationDegree}; mirror={first.IsMirrored}; canvasZoom={scroll.ZoomFactor}; {firstGeometry}; reportedZoom={continuous.ZoomFactor}; extent={scroll.Extent}; offset={scroll.Offset}; markers={string.Join(';', markers)}; markerPixels={string.Join(',', counts)}; detailPixels={details}; neighborPixels={neighborPixels}");
                return new(markers, details, neighborPixels);
            }
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "continuous-original-contracts.txt"), report);
            window.Close();
        }
    }

    private static bool Near(Point left, Point right) => Math.Abs(left.X - right.X) < 1.5 && Math.Abs(left.Y - right.Y) < 1.5;
    private static bool Near(Vector left, Vector right) => Math.Abs(left.X - right.X) < 1.5 && Math.Abs(left.Y - right.Y) < 1.5;
    private static bool Near(Size left, Size right) => Math.Abs(left.Width - right.Width) < 1.5 && Math.Abs(left.Height - right.Height) < 1.5;

    private static Rect ImageBounds(SingleImageViewer viewer, Visual relativeTo)
    {
        var image = viewer.ImageViewer ?? throw new InvalidOperationException("The plain viewer retains its real image control");
        var corners = new[] { new Point(), new Point(image.Bounds.Width, 0), new Point(0, image.Bounds.Height), new Point(image.Bounds.Width, image.Bounds.Height) }
            .Select(point => image.TranslatePoint(point, relativeTo) ?? throw new InvalidOperationException("Image is detached")).ToArray();
        return new(corners.Min(point => point.X), corners.Min(point => point.Y),
            corners.Max(point => point.X) - corners.Min(point => point.X), corners.Max(point => point.Y) - corners.Min(point => point.Y));
    }

    private static byte[] Image(int width, int height, bool original, SKColor? background = null)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(background ?? SKColors.White);
        if (background is null)
        {
            using var paint = new SKPaint();
            var markerSize = original ? 160 : 80;
            foreach (var (fraction, color) in new[] { (0.25f, SKColors.Cyan), (0.75f, SKColors.Magenta) })
            {
                var x = width > height ? width * fraction : width / 2f;
                var y = width > height ? height / 2f : height * fraction;
                paint.Color = color;
                canvas.DrawRect(new SKRect(x - markerSize / 2f, y - markerSize / 2f, x + markerSize / 2f, y + markerSize / 2f), paint);
                if (!original) continue;
                paint.Color = new(255, 128, 0);
                canvas.DrawRect(new SKRect(x - 20, y - 20, x + 20, y + 20), paint);
            }
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private sealed record Probe(Point?[] Markers, int OriginalDetailPixels, int NeighborPixels);
}
