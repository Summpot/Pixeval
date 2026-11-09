using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
public sealed class IllustrationProgressiveLifetimeTests
{
    private const string PendingUri = "https://i.pximg.net/m3e/progressive-lifetime.gif";
    private const string CurrentUri = "https://i.pximg.net/m3e/progressive-current.png";
    private const int PrefixLength = 100;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public Task LeavingAVisibleProgressivePreviewIsolatesLateImageBytes(bool continuous, bool closePage) =>
        UiTestSession.RunAsync(TestContext, directory => RunAsync(directory, continuous, closePage));

    private static async Task RunAsync(string directory, bool continuous, bool closePage)
    {
        using var fixture = new IllustrationReaderFixture();
        var encoded = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ProgressiveImages", "frames.gif"));
        var green = IllustrationFramePlaybackValidation.Png(SKColors.Green);
        fixture.Http.ImageResponses[CurrentUri] = green;
        using var input = new DeferredImageStream(encoded);
        var requests = 0;
        fixture.Http.ImageResponseFactory = (request, _) =>
        {
            Require(request.RequestUri!.AbsoluteUri == PendingUri, "Only the incomplete animation uses the delayed image response");
            requests++;
            var content = new StreamContent(input);
            content.Headers.ContentLength = encoded.Length;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = content });
        };
        var mode = continuous ? BrowseMode.Continuous : BrowseMode.Swipe;
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = mode;
        App.AppViewModel.AppSettings.ApplicationSettings.UseFileCache = true;
        using var works = new SimpleOperableSourceView<IllustrationItemViewModel>([
            IllustrationReaderFixture.Sample(996800, 1) with
            {
                ThumbnailUrls = new() { Medium = PendingUri, Large = PendingUri, SquareMedium = PendingUri },
                MetaSinglePage = new() { OriginalImageUrl = PendingUri }
            },
            IllustrationReaderFixture.Sample(996801, 1) with
            {
                ThumbnailUrls = new() { Medium = CurrentUri, Large = CurrentUri, SquareMedium = CurrentUri },
                MetaSinglePage = new() { OriginalImageUrl = CurrentUri }
            }
        ]);
        var model = new IllustrationViewerPageViewModel(works.CloneSourceView(), 0, false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost();
        var window = new Window { Width = 700, Height = 760, Content = host.Shell };
        host.Shell.NavigateTo(new ContentPage { Content = new TextBlock { Text = "Progressive lifetime fixture" } });
        host.Shell.NavigateTo(page);
        WeakReference? preview = null;
        var propertyChanges = 0;
        var displayChanges = 0;
        var previewVisible = false;
        var previewCollected = false;
        var lateInputDisposed = false;
        Task? loading = null;
        try
        {
            window.Show();
            await UntilAsync(() => requests == 1 && model.CurrentImage?.CurrentPage is { IsLoadingImage: true });
            await RenderAsync(window);
            var image = model.CurrentImage!.CurrentPage!;
            var viewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, image));
            Require(TopLevel.GetTopLevel(viewer) == window, "The first network chunk is released only after the real reader is mounted");
            loading = image.LoadOriginalImageAsync();
            Require(requests == 1, "The visible image and preload consumers share one actual download");
            input.FirstChunkAllowed.TrySetResult();
            await input.WaitingForRemainingBytes.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await UntilAsync(() => image.HasDisplayImage);
            await RenderAsync(window);
            Require(!loading.IsCompleted && image.IsLoadingImage && !image.LoadSuccessfully && image.OriginalSource is null,
                "The visible frame is a progressive preview while the full image is still downloading");
            Require(input.BytesReturned == PrefixLength && input.Reads == 2 && image.DisplaySource.FrameCount == 1,
                "Only the first GIF frame has arrived; the remaining encoded frames are still held by the HTTP stream");
            RequireVisibleColor(window, directory, "pending-red-preview", red: true);
            previewVisible = true;
            preview = ObservePreview(image);
            using (var partialCache = CacheHelper.TryGetStream(PendingUri))
                Require(partialCache is null, "An incomplete transport has not been published as a cache file");

            SingleViewerViewModel? currentImage = null;
            if (closePage)
            {
                host.Shell.ClosePage(page);
                Require(model.CurrentImage is null && !host.Shell.Pages.Contains(page), "The production tab close removes the pending reader");
            }
            else
            {
                var navigationFocus = page.GetVisualDescendants().OfType<ToggleButton>()
                    .Single(button => Equals(ToolTip.GetTip(button), I18NManager.GetResource(EntryViewerPageResources.Pin)));
                Require(navigationFocus.Focus(NavigationMethod.Tab), "A real reader action accepts the work navigation shortcut");
                Press(window, Key.Down, PhysicalKey.ArrowDown);
                await UntilAsync(() => model.CurrentWorkIndex == 1 && model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
                currentImage = model.CurrentImage!.CurrentPage!;
                Require(!ReferenceEquals(currentImage, image) && model.CurrentIllustration?.Id == "996801",
                    "Down changes to the next real work and its independent image model");
            }
            await RenderAsync(window);
            Require(input.ReadToken.IsCancellationRequested && !image.HasDisplayImage && image.OriginalSource is null
                && !image.RetryImageCommand.CanExecute(null), "Leaving the work cancels its pending read and releases the displayed preview");
            Require(!input.Disposed, "The deliberately cancellation-insensitive HTTP read still owns its response stream until it returns");
            image.PropertyChanged += (_, _) => propertyChanges++;
            image.DisplaySource.Changed += (_, _) => displayChanges++;
            var notifications = host.Notifications.ToArray();
            var currentSource = currentImage?.OriginalSource;
            input.RemainingBytesAllowed.TrySetResult();
            await loading.WaitAsync(TimeSpan.FromSeconds(10));
            await RenderAsync(window);
            lateInputDisposed = input.Disposed;
            Require(input.Disposed && input.CancellationObservedOnRelease && input.BytesReturned == encoded.Length,
                "The late read returns its bytes after cancellation and the production transport disposes the response stream");
            Require(propertyChanges == 0 && displayChanges == 0 && !image.HasDisplayImage && image.OriginalSource is null
                && host.Notifications.SequenceEqual(notifications), "Late bytes and progress cannot update or notify the obsolete image lifetime");
            using (var lateCache = CacheHelper.TryGetStream(PendingUri))
                Require(lateCache is null, "A cancelled partial download cannot enter the normal file cache after its late read returns");
            if (currentImage is not null)
            {
                Require(ReferenceEquals(model.CurrentImage!.CurrentPage, currentImage)
                    && ReferenceEquals(currentImage.OriginalSource, currentSource) && currentImage.LoadSuccessfully,
                    "The late previous download cannot replace the selected work's completed image");
                RequireVisibleColor(window, directory, "current-green-after-late-read", red: false);
                using var cached = CacheHelper.TryGetStream(CurrentUri);
                Require(cached is not null, "Normal caching remains active for the current work");
                using var copied = new MemoryStream();
                cached!.CopyTo(copied);
                Require(copied.ToArray().SequenceEqual(green), "The current work retains its exact cached response after the old request finishes");
            }
            Require(requests == 1, "Leaving and completing the old reader never starts another copy of its image request");
            for (var pass = 0; pass < 3 && preview.IsAlive; pass++)
            {
                await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                using (window.CaptureRenderedFrame()) { }
                await Task.Run(static () => { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); });
            }
            previewCollected = !preview.IsAlive;
            Require(previewCollected, "The rendered progressive bitmap is collectible after the real work/tab lifetime ends");
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "All progressive lifetime requests stay offline");
        }
        finally
        {
            input.FirstChunkAllowed.TrySetResult();
            input.RemainingBytesAllowed.TrySetResult();
            host.Shell.CloseAllPages();
            window.Close();
            if (loading is { IsCompleted: false })
                await loading.WaitAsync(TimeSpan.FromSeconds(10));
            File.WriteAllText(Path.Combine(directory, "progressive-lifetime.json"), JsonSerializer.Serialize(new
            {
                Mode = mode.ToString(),
                Exit = closePage ? "ClosePage" : "NextWorkKeyboard",
                SourceSha256 = Convert.ToHexString(SHA256.HashData(encoded)),
                PrefixLength,
                TotalBytes = encoded.Length,
                Requests = requests,
                input.Reads,
                input.BytesReturned,
                input.CancellationObservedOnRelease,
                PreviewVisible = previewVisible,
                PreviewCollected = previewCollected,
                LateInputDisposed = lateInputDisposed,
                PropertyChangesAfterLeaving = propertyChanges,
                DisplayChangesAfterLeaving = displayChanges,
                Boundary = "A real first-frame GIF preview and delayed remaining HTTP bytes, in the production reader. This does not measure decoder-mid-init cancellation, original-resolution replacement, all virtualized page combinations or native/GPU memory."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ObservePreview(SingleViewerViewModel image) => new(image.DisplaySource.Frames.Single());

    private static void RequireVisibleColor(Window window, string directory, string name, bool red)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No progressive reader frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
        using var pixels = frame.Lock();
        var bytes = new byte[pixels.RowBytes * pixels.Size.Height];
        Marshal.Copy(pixels.Address, bytes, 0, bytes.Length);
        Require(pixels.Format == PixelFormat.Bgra8888 || pixels.Format == PixelFormat.Rgba8888, "The pixel probe uses a known rendered format");
        var redIndex = pixels.Format == PixelFormat.Bgra8888 ? 2 : 0;
        var blueIndex = pixels.Format == PixelFormat.Bgra8888 ? 0 : 2;
        var expected = 0;
        var other = 0;
        for (var y = 0; y < pixels.Size.Height; y += 4)
            for (var x = 0; x < pixels.Size.Width; x += 4)
            {
                var offset = y * pixels.RowBytes + x * 4;
                var isRed = bytes[offset + redIndex] > 220 && bytes[offset + 1] < 30 && bytes[offset + blueIndex] < 30;
                var isGreen = bytes[offset + redIndex] < 30 && bytes[offset + 1] > 100 && bytes[offset + blueIndex] < 30;
                if (red ? isRed : isGreen) expected++;
                if (red ? isGreen : isRed) other++;
            }
        Require(expected > 128 && expected > other * 2, "The actual reader displays the expected " + (red ? "red preview" : "green current image"));
    }

    private sealed class DeferredImageStream(byte[] bytes) : Stream
    {
        internal TaskCompletionSource FirstChunkAllowed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource WaitingForRemainingBytes { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource RemainingBytesAllowed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken ReadToken { get; private set; }
        internal bool CancellationObservedOnRelease { get; private set; }
        internal int Reads { get; private set; }
        internal int BytesReturned { get; private set; }
        internal bool Disposed { get; private set; }
        public override bool CanRead => !Disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.Length;
        public override long Position { get => BytesReturned; set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            Reads++;
            ReadToken = cancellationToken;
            var end = bytes.Length;
            if (BytesReturned == 0)
            {
                await FirstChunkAllowed.Task;
                end = PrefixLength;
            }
            else if (BytesReturned == PrefixLength)
            {
                WaitingForRemainingBytes.TrySetResult();
                // Intentionally ignore transport cancellation. The real consumer
                // must reject these bytes and still release the response stream.
                await RemainingBytesAllowed.Task;
                CancellationObservedOnRelease = cancellationToken.IsCancellationRequested;
            }
            var length = Math.Min(buffer.Length, end - BytesReturned);
            bytes.AsMemory(BytesReturned, length).CopyTo(buffer);
            BytesReturned += length;
            return length;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
