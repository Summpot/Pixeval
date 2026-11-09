using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
public sealed class IllustrationLifetimeTests
{
    private const int ImageWidth = 4096;
    private const int ImageHeight = 2160;
    private const int Iterations = 5;
    private const string OriginalUri = "https://i.pximg.net/m3e/lifetime-4096.png";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task ClosingReadersReleasesDecodedFourKImages(bool continuous) => UiTestSession.RunAsync(TestContext,
        directory => RunAsync(directory, continuous));

    private static async Task RunAsync(string directory, bool continuous)
    {
        using var fixture = new IllustrationReaderFixture();
        var png = CreateImage();
        fixture.Http.ImageResponses[OriginalUri] = png;
        var mode = continuous ? BrowseMode.Continuous : BrowseMode.Swipe;
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = mode;
        // Keep normal compressed file caching. Every reader still owns a newly
        // decoded source; cache entries/Skia allocator capacity are not leaks.
        App.AppViewModel.AppSettings.ApplicationSettings.UseFileCache = true;
        var host = new PageTestHost();
        var window = new Window { Width = 900, Height = 700, Content = host.Shell };
        // The driver never stores a tested reader in Content/_origin. A small
        // anchor tab keeps close semantics independent of closing the last tab.
        host.Shell.NavigateTo(new ContentPage { Content = new TextBlock { Text = "Image lifetime fixture" } });
        List<ClosedReader> closed = [];
        List<MemorySample> samples = [];
        try
        {
            window.Show();
            await RenderAsync(window);
            samples.Add(await CollectAsync("before-readers", closed));
            for (var iteration = 0; iteration < Iterations; iteration++)
            {
                closed.Add(await OpenRenderAndCloseAsync(host, window, directory, iteration));
                await DrainRenderingAsync(window);
                samples.Add(await CollectAsync("closed-" + (iteration + 1), closed));
            }
            // A compositor frame may briefly retain the most recently detached
            // visual. Record each immediate result, then drain a bounded number
            // of frames before evaluating the final weak references.
            for (var pass = 0; pass < 3 && closed.Any(static reader => reader.AnyAlive); pass++)
            {
                await DrainRenderingAsync(window);
                samples.Add(await CollectAsync("drained-" + (pass + 1), closed));
            }
            WriteReport();
            Require(closed.All(static reader => !reader.AnyAlive),
                "After real ClosePage and drained rendering, no reader page, model, image model, original source or decoded frame remains: "
                + string.Join("; ", closed.Select(static reader => reader.Describe())));
            Require(host.Shell.Pages.Count == 1, "Only the fixture anchor remains in the production shell");
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Image lifetime sampling uses only the isolated fixed source");
        }
        finally
        {
            host.Shell.CloseAllPages();
            window.Close();
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
            samples.Add(await CollectAsync("window-closed", closed));
            WriteReport();
        }

        void WriteReport()
        {
            var report = new
            {
                Mode = mode.ToString(),
                Runtime = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription,
                Process = Environment.ProcessId,
                Width = ImageWidth,
                Height = ImageHeight,
                Iterations,
                CompressedBytes = png.Length,
                PngSha256 = Convert.ToHexString(SHA256.HashData(png)),
                Measurement = "Managed bytes are sampled after the reader helper and dispatcher work return, followed by full GC. Private/working-set bytes are process proxies, not native/GPU resource accounting. No timing or historical performance comparison.",
                Samples = samples,
                FinalReferences = closed.Select(static reader => reader.Snapshot()).ToArray()
            };
            File.WriteAllText(Path.Combine(directory, "illustration-lifetime.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<ClosedReader> OpenRenderAndCloseAsync(PageTestHost host, Window window, string directory, int iteration)
    {
        var work = IllustrationReaderFixture.Sample(996600 + iteration, 1) with
        {
            Width = ImageWidth,
            Height = ImageHeight,
            ThumbnailUrls = new() { Medium = OriginalUri, Large = OriginalUri, SquareMedium = OriginalUri },
            MetaSinglePage = new() { OriginalImageUrl = OriginalUri }
        };
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(work), false);
        var page = new IllustrationViewerPage(model);
        host.Shell.NavigateTo(page);
        try
        {
            await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            await RenderAsync(window);
            var image = model.CurrentImage!.CurrentPage!;
            var original = image.OriginalSource!;
            Require(original.Size == new Size(ImageWidth, ImageHeight) && original.Frames.Count == 1
                && original.Frames[0].PixelSize == new PixelSize(ImageWidth, ImageHeight), "Each reader owns a fully decoded fixed 4K source");
            var viewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, image));
            using (var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No lifetime frame"))
            {
                Require(CountImagePixels(frame, viewer, window) > 1000, "The decoded source is visibly rendered before closing");
                if (iteration is 0 or Iterations - 1)
                    frame.Save(Path.Combine(directory, $"reader-{iteration + 1}.png"), new PngBitmapEncoderOptions());
            }
            var references = new ClosedReader(iteration + 1, new(page), new(model), new(image), new(original), new(original.Frames[0]));
            host.Shell.ClosePage(page);
            Require(!host.Shell.Pages.Contains(page) && model.CurrentImage is null
                && image.OriginalSource is null && !image.HasDisplayImage,
                "Production ClosePage removes the tab and releases its decoded source without manual model disposal");
            return references;
        }
        finally
        {
            if (host.Shell.Pages.Contains(page)) host.Shell.ClosePage(page);
        }
    }

    private static async Task DrainRenderingAsync(Window window)
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
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
            process.PrivateMemorySize64, process.WorkingSet64, readers.Count(static reader => reader.Page.IsAlive),
            readers.Count(static reader => reader.Model.IsAlive), readers.Count(static reader => reader.Image.IsAlive),
            readers.Count(static reader => reader.Source.IsAlive), readers.Count(static reader => reader.Frame.IsAlive));
    });

    private static int CountImagePixels(WriteableBitmap bitmap, Control viewer, Window window)
    {
        using var pixels = bitmap.Lock();
        var data = new byte[pixels.RowBytes * pixels.Size.Height];
        Marshal.Copy(pixels.Address, data, 0, data.Length);
        Require(pixels.Format == PixelFormat.Bgra8888 || pixels.Format == PixelFormat.Rgba8888, "Known rendered pixel format");
        var red = pixels.Format == PixelFormat.Bgra8888 ? 2 : 0;
        var blue = pixels.Format == PixelFormat.Bgra8888 ? 0 : 2;
        var area = new Rect(viewer.TranslatePoint(default, window)!.Value, viewer.Bounds.Size);
        var count = 0;
        for (var y = Math.Max(0, (int) area.Top); y < Math.Min(pixels.Size.Height, area.Bottom); y += 2)
            for (var x = Math.Max(0, (int) area.Left); x < Math.Min(pixels.Size.Width, area.Right); x += 2)
            {
                var offset = y * pixels.RowBytes + x * 4;
                if (Math.Abs(data[offset + red] - 34) < 3 && Math.Abs(data[offset + 1] - 167) < 3
                    && Math.Abs(data[offset + blue] - 203) < 3) count++;
            }
        return count;
    }

    private static byte[] CreateImage()
    {
        using var bitmap = new SKBitmap(ImageWidth, ImageHeight);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(34, 167, 203));
        using var paint = new SKPaint { Color = SKColors.DarkBlue };
        canvas.DrawRect(new SKRect(0, 0, ImageWidth, 160), paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private sealed record ClosedReader(int Iteration, WeakReference Page, WeakReference Model, WeakReference Image, WeakReference Source, WeakReference Frame)
    {
        internal bool AnyAlive => Page.IsAlive || Model.IsAlive || Image.IsAlive || Source.IsAlive || Frame.IsAlive;
        internal object Snapshot() => new { Iteration, Page = Page.IsAlive, Model = Model.IsAlive, Image = Image.IsAlive, Source = Source.IsAlive, Frame = Frame.IsAlive };
        internal string Describe() => $"{Iteration}: page={Page.IsAlive}, model={Model.IsAlive}, image={Image.IsAlive}, source={Source.IsAlive}, frame={Frame.IsAlive}";
    }

    private sealed record MemorySample(string Stage, long ManagedBytesAfterCollection, long TotalAllocatedBytes,
        long PrivateBytesProxy, long WorkingSetBytesProxy, int LivePages, int LiveModels, int LiveImages, int LiveSources, int LiveFrames);
}
