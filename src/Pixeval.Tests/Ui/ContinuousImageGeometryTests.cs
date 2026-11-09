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
using ImagePanel = Pixeval.Controls.VirtualizingStackPanel;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class ContinuousImageGeometryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public Task RotatedPagesKeepTheirPixelsInsideTheirOwnContinuousLayout(bool vertical, bool clockwise) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, vertical, clockwise));

    private static async Task RunAsync(string directory, bool vertical, bool clockwise)
    {
        using var fixture = new IllustrationReaderFixture();
        var width = vertical ? 1600 : 600;
        var height = vertical ? 600 : 1600;
        var uris = Enumerable.Range(0, 3).Select(index => $"https://i.pximg.net/m3e/continuous-geometry-{index}.png").ToArray();
        fixture.Http.ImageResponses[uris[0]] = Image(width, height, true, SKColors.White);
        fixture.Http.ImageResponses[uris[1]] = Image(width, height, false, SKColors.Cyan);
        fixture.Http.ImageResponses[uris[2]] = Image(width, height, false, SKColors.Gray);
        var settings = App.AppViewModel.AppSettings.BrowsingExperienceSettings;
        settings.BrowseMode = BrowseMode.Continuous;
        settings.BrowseDirection = vertical ? Orientation.Vertical : Orientation.Horizontal;
        var work = IllustrationReaderFixture.Sample(996498, 3) with
        {
            Width = width,
            Height = height,
            MetaPages = uris.Select(uri => new MetaPage
            {
                ImageUrls = new() { Original = uri, Medium = uri, Large = uri, SquareMedium = uri }
            }).ToArray()
        };
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(work), false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 1000, Height = 850, Content = host.Shell };
        List<string> report = [$"direction={settings.BrowseDirection}; source={width}x{height}; clockwise={clockwise}"];
        List<string> failures = [];
        try
        {
            window.Show();
            await UntilAsync(() => model.CurrentImage?.Images.Take(2).All(image => image.LoadSuccessfully) == true);
            await RenderAsync(window);
            var images = model.CurrentImage!.Images.ToArray();
            var first = images[0];
            var continuous = page.GetVisualDescendants().OfType<ContinuousImageViewer>().Single();
            var scroll = continuous.FindControl<ScrollView>("ViewerScrollView")!;
            var items = continuous.FindControl<ItemsControl>("ImageItemsControl")!;
            var panel = (ImagePanel) items.ItemsPanelRoot!;
            var bar = page.FindControl<CommandBar>("ZoomPane")!;
            var selector = page.GetVisualDescendants().OfType<PagePreviewSlider>().Single();
            // Load the offscreen page through normal navigation before measuring
            // geometry; a virtualized reader need not preload every page at once.
            await SelectAsync(Key.End, PhysicalKey.End, 2);
            await SelectAsync(Key.Home, PhysicalKey.Home, 0);
            Require(images.All(image => image.LoadSuccessfully), "Real navigation prepares all three fixed source dimensions");
            CheckLayout("initial");
            Capture("initial");

            await MenuAsync(EntryViewerPageResources.Mirror);
            await MenuAsync(clockwise ? EntryViewerPageResources.RotateClockwise : EntryViewerPageResources.RotateCounterclockwise);
            Require(first.IsMirrored && first.RotationDegree == (clockwise ? 90 : 270),
                "The real image menu mirrors and rotates the non-square first page exactly once");
            CheckLayout("rotated");
            Capture("rotated");

            await OriginalSizeAsync();
            CheckLayout("original-size");
            var cornerCounts = new int[4];
            var imageBounds = ImageBounds(Viewer(0), panel);
            var zoom = scroll.ZoomFactor;
            var viewportLength = vertical ? scroll.Viewport.Height : scroll.Viewport.Width;
            // Observe both ends through the actual scroll presenter. This checks
            // reachable pixels and clamping, not native pointer/trackpad input.
            foreach (var (name, offset) in new[]
            {
                ("first-start", Math.Max(0, Start(imageBounds) * zoom)),
                ("first-end", Math.Max(0, End(imageBounds) * zoom - viewportLength))
            })
            {
                scroll.ScrollTo(vertical ? new(0, offset) : new(offset, 0), false);
                await RenderAsync(window);
                var counts = Capture(name);
                for (var index = 0; index < cornerCounts.Length; index++)
                    cornerCounts[index] = Math.Max(cornerCounts[index], counts[index]);
            }
            Check(cornerCounts.All(count => count > 200),
                "All four source corners remain reachable in the rotated first page: " + string.Join(',', cornerCounts));

            await SelectAsync(Key.Home, PhysicalKey.Home, 0);
            await SelectAsync(Key.Right, PhysicalKey.ArrowRight, 1);
            var second = Viewer(1);
            var secondBounds = ImageInViewport(second);
            var visibleSecond = secondBounds.Intersect(new Rect(scroll.Viewport));
            Check(visibleSecond.Width > 100 && visibleSecond.Height > 100,
                "The page selector positions the next real non-square image inside the viewport: " + secondBounds);
            var leading = Start(new Rect(second.TranslatePoint(default, items)!.Value * scroll.ZoomFactor - scroll.Offset,
                second.Bounds.Size * scroll.ZoomFactor));
            var offsetAtSecond = vertical ? scroll.Offset.Y : scroll.Offset.X;
            var maximum = vertical ? scroll.ScrollBarMaximum.Y : scroll.ScrollBarMaximum.X;
            Check(Math.Abs(leading) < 2 || Math.Abs(offsetAtSecond - maximum) < 2,
                "The selected page aligns its leading edge or stops at the actual end of the scroll range");
            Require(!images[1].IsMirrored && images[1].RotationDegree == 0,
                "The next page retains independent manipulation state");
            Check(Capture("next-page")[4] > 1000,
                "The selected next page's own cyan pixels are visibly rendered after navigation");
            await SelectAsync(Key.End, PhysicalKey.End, 2);
            Check(ImageInViewport(Viewer(2)).Intersects(new Rect(scroll.Viewport)),
                "The last page remains reachable after the first page changes shape");
            await SelectAsync(Key.Home, PhysicalKey.Home, 0);
            Require(ReferenceEquals(first, model.CurrentImage.CurrentPage) && first.IsMirrored
                && first.RotationDegree == (clockwise ? 90 : 270), "Returning keeps the first page's own rotation and mirror");
            CheckLayout("returned");

            await MenuAsync(clockwise ? EntryViewerPageResources.RotateCounterclockwise : EntryViewerPageResources.RotateClockwise);
            await MenuAsync(EntryViewerPageResources.Mirror);
            await OriginalSizeAsync();
            CheckLayout("restored");
            var restored = ImageInViewport(Viewer(0));
            Check(Math.Abs(restored.Width - width) < 1.5 && Math.Abs(restored.Height - height) < 1.5,
                "Undoing the transforms and selecting original size restores the source pixel dimensions: " + restored.Size);
            Capture("restored");
            host.Shell.ClosePage(page);
            Require(model.CurrentImage is null && images.All(image => !image.HasDisplayImage),
                "Production tab close releases every page after rotation, scrolling, and navigation");
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Continuous geometry uses only fixed offline images");
            Require(failures.Count == 0, string.Join(Environment.NewLine, failures));
            report.Add("PASS actual image menus, transformed layout, reachable corner pixels, original-size ratio, independent page navigation, undo, and production close");

            SingleImageViewer Viewer(int index) => page.GetVisualDescendants().OfType<SingleImageViewer>()
                .Single(viewer => ReferenceEquals(viewer.DataContext, images[index]));

            Rect ImageInViewport(SingleImageViewer viewer)
            {
                // ScrollView's zoom is a composition scale, not a Visual tree
                // transform included by TranslatePoint. Apply it exactly once.
                var bounds = ImageBounds(viewer, items);
                return new(bounds.Position * scroll.ZoomFactor - scroll.Offset, bounds.Size * scroll.ZoomFactor);
            }

            void CheckLayout(string name)
            {
                var viewer = Viewer(0);
                Require(viewer.ImageViewer?.Source is { IsInitialized: true },
                    "Continuous keeps the real AnimatedImage reference for layout and pixel scale");
                var animatedImage = viewer.ImageViewer!;
                var image = ImageBounds(viewer, panel);
                var allocation = new Rect(viewer.TranslatePoint(default, panel)!.Value, viewer.Bounds.Size);
                Require(panel.TryGetItemOffset(1, out var next), "The real virtualizing panel exposes the next measured page offset");
                var transform = animatedImage.GetVisualAncestors().OfType<LayoutTransformControl>().FirstOrDefault();
                report.Add($"{name}: image={image}; allocation={allocation}; childBounds={animatedImage.Bounds}; layoutMatrix={transform?.LayoutTransform?.Value}; renderMatrix={animatedImage.RenderTransform?.Value}; next={next}; canvasZoom={scroll.ZoomFactor}; pixelScale={animatedImage.SourceScale}; reportedZoom={continuous.ZoomFactor}; extent={scroll.Extent}; offset={scroll.Offset}");
                Check(image.Left >= allocation.Left - 1 && image.Top >= allocation.Top - 1
                    && image.Right <= allocation.Right + 1 && image.Bottom <= allocation.Bottom + 1,
                    name + ": the rotated pixels stay inside their own page allocation");
                Check(next - End(image) >= panel.Spacing - 1,
                    name + ": the next page starts after the transformed image and the existing page spacing");
            }

            async Task OriginalSizeAsync()
            {
                var original = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button =>
                    button.IsVisible && button.Label == I18NManager.GetResource(EntryViewerPageResources.ZoomToOriginal));
                await ActivateAsync(window, original);
                await UntilAsync(() => Math.Abs(continuous.ZoomFactor - 1) < 0.001);
                await CloseOverflowAsync();
                await IllustrationTransformValidation.DismissAsync(window);
                await RenderAsync(window);
                Check(Math.Abs(Viewer(0).ImageViewer!.SourceScale.X * scroll.ZoomFactor - 1) < 0.001,
                    "Original size uses the displayed image's real pixel scale");
                Check(bar.GetVisualDescendants().OfType<TextBlock>().Any(text => text.IsEffectivelyVisible && text.Text == "100%"),
                    "The continuous toolbar displays 100% at original pixel scale");
            }

            async Task SelectAsync(Key key, PhysicalKey physicalKey, int expected)
            {
                Require(selector.Focus(NavigationMethod.Tab), "The real page selector receives keyboard focus");
                Press(window, key, physicalKey);
                await UntilAsync(() => model.CurrentPageIndex == expected && images[expected].LoadSuccessfully);
                await RenderAsync(window);
                await UntilAsync(() => scroll.State == ScrollingInteractionState.Idle);
                await RenderAsync(window);
            }

            async Task MenuAsync(string resource)
            {
                await CloseOverflowAsync();
                await IllustrationTransformValidation.DismissAsync(window);
                await RenderAsync(window);
                var viewer = Viewer(0);
                var origin = scroll.TranslatePoint(default, window)!.Value;
                var target = (from y in new[] { 0.5, 0.25, 0.75, 0.1, 0.9 }
                              from x in new[] { 0.5, 0.25, 0.75, 0.1, 0.9 }
                              let point = origin + new Vector(scroll.Viewport.Width * x, scroll.Viewport.Height * y)
                              let hit = window.InputHitTest(point) as Visual
                              where hit is not null && (ReferenceEquals(hit, viewer) || hit.GetVisualAncestors().Contains(viewer))
                              select (Point?) point).FirstOrDefault();
                Require(target is not null, "The first image has an actual hit-tested context-menu target");
                window.MouseMove(target!.Value);
                window.MouseDown(target.Value, MouseButton.Right);
                window.MouseUp(target.Value, MouseButton.Right);
                await RenderAsync(window);
                var item = viewer.ContextMenu!.Items.OfType<MenuItem>().Single(menu => menu.Header?.ToString() == I18NManager.GetResource(resource));
                Require(viewer.ContextMenu.IsOpen && item.IsEffectivelyEnabled && item.Focus(NavigationMethod.Tab),
                    "The production image menu offers " + resource);
                Press(window, Key.Enter, PhysicalKey.Enter);
                await RenderAsync(window);
                Require(!viewer.ContextMenu.IsOpen, "The actual image action dismisses its menu");
            }

            async Task CloseOverflowAsync()
            {
                if (!bar.IsOpen) return;
                var overflow = bar.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_OverflowButton");
                Require(overflow.Focus(NavigationMethod.Tab), "The reader overflow entry can dismiss its menu");
                Press(window, Key.Escape, PhysicalKey.Escape);
                await RenderAsync(window);
                Require(!bar.IsOpen, "Escape closes the real reader command overflow");
            }

            int[] Capture(string name)
            {
                window.MouseMove(new Point(1, 1));
                using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No continuous reader frame");
                frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
                using var pixels = frame.Lock();
                var data = new byte[pixels.RowBytes * pixels.Size.Height];
                Marshal.Copy(pixels.Address, data, 0, data.Length);
                Require(pixels.Format == PixelFormat.Bgra8888 || pixels.Format == PixelFormat.Rgba8888, "Known screenshot pixel format");
                var redIndex = pixels.Format == PixelFormat.Bgra8888 ? 2 : 0;
                var blueIndex = pixels.Format == PixelFormat.Bgra8888 ? 0 : 2;
                var area = new Rect(scroll.TranslatePoint(default, window)!.Value, scroll.Viewport);
                var counts = new int[5];
                for (var y = Math.Max(0, (int) area.Top); y < Math.Min(pixels.Size.Height, area.Bottom); y++)
                    for (var x = Math.Max(0, (int) area.Left); x < Math.Min(pixels.Size.Width, area.Right); x++)
                    {
                        var index = y * pixels.RowBytes + x * 4;
                        var red = data[index + redIndex]; var green = data[index + 1]; var blue = data[index + blueIndex];
                        if (red > 220 && green < 30 && blue < 30) counts[0]++;
                        if (red < 30 && green > 220 && blue < 30) counts[1]++;
                        if (red < 30 && green < 30 && blue > 220) counts[2]++;
                        if (red > 220 && green > 220 && blue < 30) counts[3]++;
                        if (red < 30 && green > 220 && blue > 220) counts[4]++;
                    }
                report.Add($"{name}: cornerPixels={string.Join(',', counts)}; offset={scroll.Offset}; extent={scroll.Extent}");
                return counts;
            }
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "continuous-geometry-contracts.txt"), report.Concat(failures.Select(failure => "FAIL " + failure)));
            window.Close();
        }

        void Check(bool condition, string contract)
        {
            if (!condition) failures.Add(contract);
        }
        double Start(Rect bounds) => vertical ? bounds.Top : bounds.Left;
        double End(Rect bounds) => vertical ? bounds.Bottom : bounds.Right;
    }

    private static Rect ImageBounds(SingleImageViewer viewer, Visual relativeTo)
    {
        var image = viewer.ImageViewer ?? throw new InvalidOperationException("Continuous must retain its AnimatedImage reference");
        var corners = new[] { new Point(), new Point(image.Bounds.Width, 0), new Point(0, image.Bounds.Height), new Point(image.Bounds.Width, image.Bounds.Height) }
            .Select(point => image.TranslatePoint(point, relativeTo) ?? throw new InvalidOperationException("Image is detached")).ToArray();
        return new(corners.Min(point => point.X), corners.Min(point => point.Y),
            corners.Max(point => point.X) - corners.Min(point => point.X), corners.Max(point => point.Y) - corners.Min(point => point.Y));
    }

    private static byte[] Image(int width, int height, bool corners, SKColor background)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(background);
        if (corners)
        {
            using var paint = new SKPaint();
            var colors = new[] { SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Yellow };
            for (var index = 0; index < colors.Length; index++)
            {
                paint.Color = colors[index];
                var x = index % 2 == 0 ? 0 : width - 60;
                var y = index / 2 == 0 ? 0 : height - 60;
                canvas.DrawRect(new SKRect(x, y, x + 60, y + 60), paint);
            }
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
