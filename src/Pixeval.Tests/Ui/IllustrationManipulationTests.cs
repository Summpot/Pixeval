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
using Avalonia.Styling;
using Avalonia.VisualTree;
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
public sealed class IllustrationManipulationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("zh-Hans", false)]
    [DataRow("en-US", true)]
    [DataRow("fr-FR", false)]
    [DataRow("ru-RU", true)]
    public Task ImageMenusPreservePerPageManipulation(string culture, bool continuous) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, continuous), culture);

    private static async Task RunAsync(string directory, bool continuous)
    {
        using var fixture = new IllustrationReaderFixture();
        fixture.Http.ImageResponses[IllustrationReaderFixture.ImageUri] = Quadrants();
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = continuous ? BrowseMode.Continuous : BrowseMode.Swipe;
        var direction = System.Globalization.CultureInfo.CurrentUICulture.Name is "fr-FR" or "ru-RU"
            ? Orientation.Vertical : Orientation.Horizontal;
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseDirection = direction;
        var work = IllustrationReaderFixture.Sample(996490, 2) with { Width = 256, Height = 256 };
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(work), false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 1000, Height = 850, RequestedThemeVariant = ThemeVariant.Light, Content = host.Shell };
        List<string> report = [$"mode={(continuous ? BrowseMode.Continuous : BrowseMode.Swipe)}; direction={direction}"];
        try
        {
            window.Show();
            await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            await RenderAsync(window);
            var first = model.CurrentImage!.CurrentPage!;
            var wrapper = page.FindControl<ImageViewerWrapper>("I")!;
            var bar = page.FindControl<CommandBar>("ZoomPane")!;
            await CheckPixelsAsync("initial", "RGBY");

            var mirrorChanges = 0;
            first.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(SingleViewerViewModel.IsMirrored)) mirrorChanges++; };
            await MenuAsync(EntryViewerPageResources.Mirror);
            Require(first.IsMirrored && mirrorChanges == 1, "The image menu mirrors exactly once");
            await CheckPixelsAsync("mirrored", "GRYB");
            await MenuAsync(EntryViewerPageResources.RotateClockwise);
            Require(first.RotationDegree == 90, "The image menu rotates clockwise exactly once");
            await CheckPixelsAsync("mirrored-clockwise", "YGBR");
            await MenuAsync(EntryViewerPageResources.RotateCounterclockwise);
            Require(first.RotationDegree == 0, "Counterclockwise undoes the menu rotation");
            await MenuAsync(EntryViewerPageResources.Mirror, pointer: true);
            Require(!first.IsMirrored && mirrorChanges == 2, "Pointer activation clears the same mirror state once");
            await CheckPixelsAsync("restored", "RGBY");
            await MenuAsync(EntryViewerPageResources.RotateCounterclockwise);
            Require(first.RotationDegree == 270, "Counterclockwise wraps from zero to 270 degrees");
            await CheckPixelsAsync("counterclockwise", "GYRB");
            await MenuAsync(EntryViewerPageResources.RotateClockwise);
            report.Add("PASS actual menu Enter/pointer, mirror toggle, both rotations, wraparound and rendered quadrant positions");

            window.Width = 420;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            await RenderAsync(window);
            if (!continuous)
            {
                await ToolbarAsync(EntryViewerPageResources.ZoomToOriginal);
                await ZoomSettledAsync(1);
                await ToolbarAsync(EntryViewerPageResources.ZoomIn);
                await ZoomSettledAsync(1.2);
                await ToolbarAsync(EntryViewerPageResources.ZoomOut);
                await ZoomSettledAsync(1);
                await ToolbarAsync(EntryViewerPageResources.ZoomToFit);
                var viewport = Viewer().ViewerScrollView!.Viewport;
                var size = first.DisplaySource.Size;
                await ZoomSettledAsync(Math.Min(viewport.Width / size.Width, viewport.Height / size.Height));
                await ToolbarAsync(EntryViewerPageResources.Mirror);
                Require(first.IsMirrored && mirrorChanges == 3, "The overflow toggle shares the menu mirror state");
                await ToolbarAsync(EntryViewerPageResources.RotateClockwise);
                Require(first.RotationDegree == 90, "Narrow toolbar rotation acts on the current page");
                await CloseOverflowAsync();
                var mirrorMenu = await OpenMenuAsync(EntryViewerPageResources.Mirror);
                Require(mirrorMenu.IsChecked, "Reopening the image menu reflects the toolbar toggle");
                Press(window, Key.Escape, PhysicalKey.Escape);
                await RenderAsync(window);
                Require(!Viewer().ContextMenu!.IsOpen && first.IsMirrored, "Escape dismisses the image menu without toggling it");
                report.Add("PASS narrow overflow zoom original/in/out/fit in model and scroll viewport, shared mirror/rotation and Escape");
            }
            else
            {
                await MenuAsync(EntryViewerPageResources.Mirror);
                await MenuAsync(EntryViewerPageResources.RotateClockwise);
            }

            var selector = page.GetVisualDescendants().OfType<PagePreviewSlider>().Single();
            Require(selector.Focus(NavigationMethod.Tab), "The actual page selector remains reachable after image actions");
            Press(window, Key.End, PhysicalKey.End);
            await UntilAsync(() => model.CurrentPageIndex == 1 && model.CurrentImage.CurrentPage!.LoadSuccessfully);
            await RenderAsync(window);
            var second = model.CurrentImage.CurrentPage!;
            Require(!ReferenceEquals(first, second) && !second.IsMirrored && second.RotationDegree == 0,
                "Another page starts with independent rotation and mirror state");
            await MenuAsync(EntryViewerPageResources.RotateCounterclockwise);
            Require(second.RotationDegree == 270 && first.RotationDegree == 90 && first.IsMirrored,
                "The new page's menu cannot change the previous page");
            Require(selector.Focus(NavigationMethod.Tab), "The selector can return to the first page");
            Press(window, Key.Home, PhysicalKey.Home);
            await UntilAsync(() => model.CurrentPageIndex == 0);
            await RenderAsync(window);
            Require(ReferenceEquals(first, model.CurrentImage.CurrentPage) && first.IsMirrored && first.RotationDegree == 90,
                "Returning to an image retains its own manipulation state");
            Save(window, directory, "per-page-manipulation");

            await OpenMenuAsync(EntryViewerPageResources.Mirror);
            var originalMenu = Viewer().ContextMenu!;
            host.Shell.NavigateTo(new Pixeval.Views.Search.SearchPage());
            await RenderAsync(window);
            Require(!page.IsLoaded && !originalMenu.IsOpen, "Leaving the real reader closes its image popup");
            host.CloseDestinationAndReturn();
            await RenderAsync(window);
            Require(ReferenceEquals(first, model.CurrentImage.CurrentPage) && first.IsMirrored && first.RotationDegree == 90,
                "Temporary navigation preserves manipulation without reopening the old menu");
            Require(!Viewer().ContextMenu!.IsOpen, "Returning does not reopen an obsolete menu");
            window.Width = 1000;
            await RenderAsync(window);
            if (!continuous) await ToolbarAsync(EntryViewerPageResources.ZoomToFit);
            await CloseOverflowAsync();
            await CheckPixelsAsync("returned", "YGBR");
            await OpenMenuAsync(EntryViewerPageResources.Mirror);
            var closingMenu = Viewer().ContextMenu!;
            host.Shell.ClosePage(page);
            await RenderAsync(window);
            Require(!closingMenu.IsOpen && model.CurrentImage is null && !first.HasDisplayImage && !second.HasDisplayImage,
                "Production close dismisses image menus and releases every page's source");
            Require(!first.RotateClockwiseCommand.CanExecute(null) && !first.MirrorCommand.CanExecute(null),
                "Closed images no longer offer manipulation commands");
            report.Add("PASS independent page state, actual selector, temporary navigation and real close with an open image menu");

            SingleImageViewer Viewer() => page.GetVisualDescendants().OfType<SingleImageViewer>()
                .Single(control => ReferenceEquals(control.DataContext, model.CurrentImage!.CurrentPage));

            async Task<MenuItem> OpenMenuAsync(string resource)
            {
                await CloseOverflowAsync();
                await RenderAsync(window);
                var viewer = Viewer();
                var visible = new Rect(viewer.TranslatePoint(default, window)!.Value, viewer.Bounds.Size)
                    .Intersect(new Rect(wrapper.TranslatePoint(default, window)!.Value, wrapper.Bounds.Size));
                Require(visible.Width > 20 && visible.Height > 20, "The current image has a visible target for its menu");
                window.MouseMove(visible.Center);
                window.MouseDown(visible.Center, MouseButton.Right);
                window.MouseUp(visible.Center, MouseButton.Right);
                await RenderAsync(window);
                var item = viewer.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == I18NManager.GetResource(resource));
                Require(viewer.ContextMenu.IsOpen && item.IsEffectivelyEnabled && item.Focus(NavigationMethod.Tab),
                    "The actual image menu exposes " + resource);
                return item;
            }

            async Task MenuAsync(string resource, bool pointer = false)
            {
                var item = await OpenMenuAsync(resource);
                var menu = Viewer().ContextMenu!;
                if (pointer)
                {
                    var point = item.TranslatePoint(new Rect(item.Bounds.Size).Center, window)!.Value;
                    window.MouseDown(point, MouseButton.Left);
                    window.MouseUp(point, MouseButton.Left);
                }
                else Press(window, Key.Enter, PhysicalKey.Enter);
                await RenderAsync(window);
                Require(!menu.IsOpen, "Executing an image menu command dismisses its popup");
            }

            async Task ToolbarAsync(string resource)
            {
                var button = bar.PrimaryCommands.OfType<Button>().Single(control => control.IsVisible && control switch
                {
                    CommandBarButton command => command.Label == I18NManager.GetResource(resource),
                    CommandBarToggleButton toggle => toggle.Label == I18NManager.GetResource(resource),
                    _ => false
                });
                await ActivateAsync(window, button);
            }

            async Task ZoomSettledAsync(double expected) => await UntilAsync(() =>
                Math.Abs(first.ZoomFactor - expected) < 0.001 && Math.Abs(Viewer().ViewerScrollView!.ZoomFactor - expected) < 0.001);

            async Task CloseOverflowAsync()
            {
                if (!bar.IsOpen) return;
                var overflow = bar.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "PART_OverflowButton");
                Require(overflow.Focus(NavigationMethod.Tab), "The overflow entry can dismiss its menu");
                Press(window, Key.Escape, PhysicalKey.Escape);
                await RenderAsync(window);
            }

            async Task CheckPixelsAsync(string name, string expected)
            {
                await RenderAsync(window);
                window.MouseMove(new Point(1, 1));
                using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No image manipulation frame");
                frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
                using var pixels = frame.Lock();
                var data = new byte[pixels.RowBytes * pixels.Size.Height];
                Marshal.Copy(pixels.Address, data, 0, data.Length);
                Require(pixels.Format == PixelFormat.Bgra8888 || pixels.Format == PixelFormat.Rgba8888, "Known pixel format");
                var red = pixels.Format == PixelFormat.Bgra8888 ? 2 : 0;
                var blue = pixels.Format == PixelFormat.Bgra8888 ? 0 : 2;
                var viewer = Viewer();
                var clip = new Rect(viewer.TranslatePoint(default, window)!.Value,
                        viewer.TranslatePoint(new Point(viewer.Bounds.Width, viewer.Bounds.Height), window)!.Value)
                    .Intersect(new Rect(wrapper.TranslatePoint(default, window)!.Value, wrapper.Bounds.Size));
                var points = "RGBY".ToDictionary(color => color, _ => new List<Point>());
                for (var y = Math.Max(0, (int) clip.Top); y < Math.Min(pixels.Size.Height, clip.Bottom); y += 3)
                    for (var x = Math.Max(0, (int) clip.Left); x < Math.Min(pixels.Size.Width, clip.Right); x += 3)
                    {
                        var offset = y * pixels.RowBytes + x * 4;
                        var r = data[offset + red]; var g = data[offset + 1]; var b = data[offset + blue];
                        var color = r > 220 && g < 30 && b < 30 ? 'R'
                            : r < 30 && g > 220 && b < 30 ? 'G'
                            : r < 30 && g < 30 && b > 220 ? 'B'
                            : r > 220 && g > 220 && b < 30 ? 'Y' : '\0';
                        if (color != '\0') points[color].Add(new Point(x, y));
                    }
                Require(points.Values.All(values => values.Count > 100), "Every quadrant remains visibly rendered: " + name);
                var centers = points.ToDictionary(pair => pair.Key, pair => new Point(pair.Value.Average(p => p.X), pair.Value.Average(p => p.Y)));
                report.Add(name + ": " + string.Join("; ", centers.Select(pair => $"{pair.Key}={pair.Value}")));
                Require(centers[expected[0]].X < centers[expected[1]].X && centers[expected[2]].X < centers[expected[3]].X
                    && centers[expected[0]].Y < centers[expected[2]].Y && centers[expected[1]].Y < centers[expected[3]].Y,
                    "Rendered quadrants follow the requested mirror/rotation: " + name + "=" + expected);
            }
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "image-manipulation-contracts.txt"), report);
            window.Close();
        }
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Image manipulation stays offline");
    }

    private static byte[] Quadrants()
    {
        using var bitmap = new SKBitmap(256, 256);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint();
        var colors = new[] { SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Yellow };
        for (var i = 0; i < colors.Length; i++)
        {
            paint.Color = colors[i];
            canvas.DrawRect(new SKRect(i % 2 * 128, i / 2 * 128, (i % 2 + 1) * 128, (i / 2 + 1) * 128), paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
