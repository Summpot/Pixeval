using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
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
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class RotatedImageFitTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(800, 200, true)]
    [DataRow(800, 200, false)]
    [DataRow(200, 800, true)]
    [DataRow(200, 800, false)]
    public Task FitKeepsRotatedImageCornersInsideViewport(int width, int height, bool clockwise) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, width, height, clockwise));

    [TestMethod]
    [DataRow(800, 200, true)]
    [DataRow(200, 800, false)]
    public Task InitialFitUsesTheRetainedImageRotation(int width, int height, bool clockwise) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, width, height, clockwise, retainedRotation: true));

    private static async Task RunAsync(string directory, int width, int height, bool clockwise, bool retainedRotation = false)
    {
        using var fixture = new IllustrationReaderFixture();
        fixture.Http.ImageResponses[IllustrationReaderFixture.ImageUri] = CornerImage(width, height);
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = BrowseMode.Swipe;
        var work = IllustrationReaderFixture.Sample(996495, 1) with { Width = width, Height = height };
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(work), false);
        // A new visual can attach to an image model whose manipulation state is
        // already retained. Exercise that initial-fit path before any fit command.
        if (retainedRotation) model.CurrentImage!.CurrentPage!.RotationDegree = clockwise ? 90 : 270;
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
            var expected = Math.Min(scroll.Viewport.Width / height, scroll.Viewport.Height / width);
            if (retainedRotation)
            {
                Require(Math.Abs(scroll.ZoomFactor - expected) < 0.001 && Math.Abs(image.ZoomFactor - expected) < 0.001,
                    "Initial attachment fits the model's retained rotation without a command");
                CheckCorners("initial-rotated-fit");
            }
            else await ActionAsync(clockwise ? EntryViewerPageResources.RotateClockwise : EntryViewerPageResources.RotateCounterclockwise);
            Require(image.RotationDegree == (clockwise ? 90 : 270), "The real toolbar rotates the original non-square image once");
            await ActionAsync(EntryViewerPageResources.ZoomToFit);
            await HideOverflowAsync();
            await RenderAsync(window);
            Save(window, directory, "rotated-fit");
            report.Add($"source={width}x{height}; angle={image.RotationDegree}; viewport={scroll.Viewport}; zoom={scroll.ZoomFactor}; expected={expected}");
            Require(Math.Abs(scroll.ZoomFactor - expected) < 0.001 && Math.Abs(image.ZoomFactor - expected) < 0.001,
                "Fit uses the rotated width/height in both the actual viewport and the displayed percentage: " + report[^1]);
            CheckCorners("rotated-fit");
            await ActionAsync(EntryViewerPageResources.ZoomToOriginal);
            await UntilAsync(() => Math.Abs(scroll.ZoomFactor - 1) < 0.001);
            await ActionAsync(EntryViewerPageResources.ZoomToFit);
            await HideOverflowAsync();
            await RenderAsync(window);
            Require(Math.Abs(scroll.ZoomFactor - expected) < 0.001, "Fit remains available after returning to the original pixel scale");
            CheckCorners("refit");
            host.Shell.NavigateTo(new Pixeval.Views.Search.SearchPage());
            await RenderAsync(window);
            host.CloseDestinationAndReturn();
            await RenderAsync(window);
            Require(ReferenceEquals(image, model.CurrentImage.CurrentPage) && image.RotationDegree == (clockwise ? 90 : 270),
                "Temporary navigation preserves the rotated image identity");
            await ActionAsync(EntryViewerPageResources.ZoomToFit);
            await HideOverflowAsync();
            await RenderAsync(window);
            CheckCorners("returned-fit");
            host.Shell.ClosePage(page);
            Require(!image.HasDisplayImage, "Actual tab close releases the non-square image");
            report.Add("PASS clockwise/counterclockwise fit, actual zoom, four rendered corners, original-size/refit and production navigation/close");

            async Task ActionAsync(string key)
            {
                var button = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => button.IsVisible && button.Label == I18NManager.GetResource(key));
                await ActivateAsync(window, button);
            }

            async Task HideOverflowAsync()
            {
                if (!bar.IsOpen) return;
                var entry = bar.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_OverflowButton");
                Require(entry.Focus(), "The real overflow entry can close its popup");
                Press(window, Avalonia.Input.Key.Escape, Avalonia.Input.PhysicalKey.Escape);
                await RenderAsync(window);
                window.MouseMove(new Point(1, 1));
            }

            void CheckCorners(string name)
            {
                using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rotated image frame");
                frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
                using var pixels = frame.Lock();
                var data = new byte[pixels.RowBytes * pixels.Size.Height];
                Marshal.Copy(pixels.Address, data, 0, data.Length);
                Require(pixels.Format == PixelFormat.Bgra8888 || pixels.Format == PixelFormat.Rgba8888, "Known pixel format");
                var rIndex = pixels.Format == PixelFormat.Bgra8888 ? 2 : 0;
                var bIndex = pixels.Format == PixelFormat.Bgra8888 ? 0 : 2;
                var counts = new int[4];
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
                    }
                report.Add(name + ": visible corner pixels=" + string.Join(",", counts));
                Require(counts.All(count => count > 100), "Every original corner must remain visibly inside the fitted image viewport: " + report[^1]);
            }
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "rotated-fit-contracts.txt"), report);
            window.Close();
        }
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Image fit uses only the fixed offline source");
    }

    private static byte[] CornerImage(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint();
        var colors = new[] { SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Yellow };
        for (var i = 0; i < colors.Length; i++)
        {
            paint.Color = colors[i];
            var x = i % 2 == 0 ? 0 : width - 40;
            var y = i / 2 == 0 ? 0 : height - 40;
            canvas.DrawRect(new SKRect(x, y, x + 40, y + 40), paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
