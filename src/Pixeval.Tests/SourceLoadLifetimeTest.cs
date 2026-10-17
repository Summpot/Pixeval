using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Cache;
using Pixeval.Controls;
using Pixeval.Mako;
using Pixeval.Services.Media;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SourceLoadLifetimeTest
{
    [TestMethod]
    public void BeginLoad_AbandonsPreviousOperationWithoutCancellation()
    {
        using var lifetime = new SourceLoadLifetime();
        var previous = lifetime.BeginLoad();

        _ = lifetime.BeginLoad();

        Assert.IsFalse(previous.Token.IsCancellationRequested);
        var discardedSource = new DisposableSource();
        Assert.IsFalse(previous.TrySetSource(discardedSource));
        Assert.IsTrue(discardedSource.IsDisposed);
        Assert.IsFalse(previous.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void Dispose_CancelsCurrentAndAbandonedOperations()
    {
        var lifetime = new SourceLoadLifetime();
        var abandoned = lifetime.BeginLoad();
        var current = lifetime.BeginLoad();

        lifetime.Dispose();

        Assert.IsTrue(abandoned.Token.IsCancellationRequested);
        Assert.IsTrue(current.Token.IsCancellationRequested);
    }

    [TestMethod]
    public async Task Source_SetCacheBeforeAttach_LoadsWhenAttached()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            var fakeService = new FakeImageProviderService();
            var window = new Window { Width = 300, Height = 300 };
            Source.SetImageProvider(window, fakeService);

            var image = new Image();
            Source.SetCache(image, "https://example.com/before-attach.png");
            Assert.IsFalse(Source.GetLoaded(image));
            Assert.IsNull(image.Source);

            window.Content = image;
            window.Show();
            window.UpdateLayout();

            for (var i = 0; i < 50 && !Source.GetLoaded(image); i++)
            {
                await Task.Delay(20);
                window.UpdateLayout();
            }

            Assert.IsTrue(Source.GetLoaded(image));
            Assert.IsNotNull(image.Source);

            window.Close();
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task Source_WithInheritedImageProvider_LoadsBitmapSuccessfully()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            var fakeService = new FakeImageProviderService();
            var window = new Window { Width = 300, Height = 300 };
            Source.SetImageProvider(window, fakeService);

            var image = new Image();
            window.Content = image;
            window.Show();
            window.UpdateLayout();

            Source.SetCache(image, "https://example.com/test1.png");

            for (var i = 0; i < 50 && !Source.GetLoaded(image); i++)
            {
                await Task.Delay(20);
                window.UpdateLayout();
            }

            Assert.IsTrue(Source.GetLoaded(image));
            Assert.IsNotNull(image.Source);

            window.Close();
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task Source_Rebind_ClearsSourceImmediatelyAndAbandonsPreviousLoad()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            var fakeService = new FakeImageProviderService();
            var window = new Window { Width = 300, Height = 300 };
            Source.SetImageProvider(window, fakeService);

            var image = new Image();
            window.Content = image;
            window.Show();
            window.UpdateLayout();

            Source.SetCache(image, "https://example.com/test1.png");
            for (var i = 0; i < 50 && !Source.GetLoaded(image); i++)
            {
                await Task.Delay(20);
                window.UpdateLayout();
            }
            Assert.IsTrue(Source.GetLoaded(image));
            Assert.IsNotNull(image.Source);

            var tcs = new TaskCompletionSource<Bitmap>();
            fakeService.OnGetBitmap = (platform, key, token) => key == "https://example.com/test2.png" ? tcs.Task : Task.FromResult(FakeImageProviderService.CreateDummyBitmap());

            Source.SetCache(image, "https://example.com/test2.png");

            Assert.IsNull(image.Source);
            Assert.IsFalse(Source.GetLoaded(image));

            tcs.SetResult(FakeImageProviderService.CreateDummyBitmap());
            for (var i = 0; i < 50 && !Source.GetLoaded(image); i++)
            {
                await Task.Delay(20);
                window.UpdateLayout();
            }

            Assert.IsTrue(Source.GetLoaded(image));
            Assert.IsNotNull(image.Source);

            window.Close();
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task Source_DetachFromVisualTree_CancelsInFlightOperation()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            var tcs = new TaskCompletionSource<Bitmap>();
            CancellationToken observedToken = default;
            var fakeService = new FakeImageProviderService
            {
                OnGetBitmap = (platform, key, token) =>
                {
                    observedToken = token;
                    return tcs.Task;
                }
            };

            var window = new Window { Width = 300, Height = 300 };
            Source.SetImageProvider(window, fakeService);

            var image = new Image();
            window.Content = image;
            window.Show();
            window.UpdateLayout();

            Source.SetCache(image, "https://example.com/test3.png");
            await Task.Delay(50);

            Assert.AreNotEqual(default, observedToken);
            Assert.IsFalse(observedToken.IsCancellationRequested);

            window.Content = null;
            window.UpdateLayout();

            Assert.IsTrue(observedToken.IsCancellationRequested);

            window.Close();
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task Source_DetachFromVisualTree_DoesNotDisposeLoadedBitmap()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            var fakeService = new FakeImageProviderService();
            var window = new Window { Width = 300, Height = 300 };
            Source.SetImageProvider(window, fakeService);

            var image = new Image();
            window.Content = image;
            window.Show();
            window.UpdateLayout();

            Source.SetCache(image, "https://example.com/test_loaded.png");
            for (var i = 0; i < 50 && !Source.GetLoaded(image); i++)
            {
                await Task.Delay(20);
                window.UpdateLayout();
            }

            Assert.IsTrue(Source.GetLoaded(image));
            Assert.IsNotNull(image.Source);
            var loadedBitmap = (Bitmap) image.Source;

            // Detach image from visual tree (simulating tab close / element unload)
            window.Content = null;
            window.UpdateLayout();

            // Bitmap should NOT be disposed; accessing Size must not throw ObjectDisposedException
            Assert.IsTrue(loadedBitmap.Size.Width > 0);

            window.Close();
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task MarkdownBox_ResolvesStreamViaInheritedImageProvider()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            var dummyStream = new MemoryStream([1, 2, 3]);
            var fakeService = new FakeImageProviderService
            {
                OnGetImageStream = (platform, key, token) => Task.FromResult<Stream?>(dummyStream)
            };

            var window = new Window { Width = 300, Height = 300 };
            Source.SetImageProvider(window, fakeService);

            var box = new MarkdownBox();
            window.Content = box;
            window.Show();
            window.UpdateLayout();

            var pathResolver = box.Plugins?.PathResolver;
            ArgumentNullException.ThrowIfNull(pathResolver);
            var resolvedStream = await pathResolver.ResolveImageResource("https://example.com/markdown.png")!;
            Assert.AreSame(dummyStream, resolvedStream);

            window.Close();
        }, CancellationToken.None);
    }

    private sealed class DisposableSource : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class FakeImageProviderService : IImageProviderService
    {
        public Func<string, string, CancellationToken, Task<Bitmap>>? OnGetBitmap { get; set; }
        public Func<string, string, CancellationToken, Task<Stream?>>? OnGetImageStream { get; set; }

        public ImageCache ImageCache => throw new NotImplementedException();
        public Bitmap ImageNotAvailable => throw new NotImplementedException();
        public IAnimatedBitmap AnimatedImageNotAvailable => throw new NotImplementedException();

        public async ValueTask<Bitmap> GetBitmapAsync(
            string platform,
            string key,
            IProgress<double>? progress = null,
            int? desiredWidth = null,
            CancellationToken token = default)
        {
            if (OnGetBitmap is not null)
                return await OnGetBitmap(platform, key, token);
            return CreateDummyBitmap();
        }

        public Task<IAnimatedBitmap> GetAnimatedBitmapAsync(
            string platform,
            string key,
            IProgress<double>? progress = null,
            CancellationToken token = default) => throw new NotImplementedException();

        public ValueTask<IAnimatedBitmap> GetSingleImageAsync(
            string platform,
            string url,
            IProgress<double>? progress = null,
            Action<DecodedPreviewFrame>? onPreview = null,
            CancellationToken token = default) => throw new NotImplementedException();

        public ValueTask<IAnimatedBitmap> GetSingleImageAsync(
            string platform,
            Uri frameUri,
            IProgress<double>? progress = null,
            Action<DecodedPreviewFrame>? onPreview = null,
            CancellationToken token = default) => throw new NotImplementedException();

        public IAnimatedBitmap LoadAnimatedBitmap(Stream stream) => throw new NotImplementedException();

        public ValueTask<IAnimatedBitmap> GetUgoiraAnimatedImageAsync(
            string platform,
            Uri zipUri,
            IReadOnlyList<int> delays,
            IProgress<double>? progress = null,
            Action<DecodedPreviewFrame>? onPreview = null,
            CancellationToken token = default) => throw new NotImplementedException();

        public async ValueTask<Stream?> GetImageStreamAsync(
            string platform,
            string key,
            IProgress<double>? progress = null,
            CancellationToken token = default)
        {
            if (OnGetImageStream is not null)
                return await OnGetImageStream(platform, key, token);
            return null;
        }

        public Stream? TryGetStream(string key) => null;
        public bool TryCacheStream(string key, Stream stream) => false;
        public Task PurgeCacheAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task EnforceCacheSizeLimitAsync(CancellationToken token = default) => Task.CompletedTask;
        public void UpdateNetworkOptions(MakoConfigurationDto config) { }

        public static Bitmap CreateDummyBitmap()
        {
            var writeable = new WriteableBitmap(new PixelSize(2, 2), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            return writeable;
        }
    }
}
