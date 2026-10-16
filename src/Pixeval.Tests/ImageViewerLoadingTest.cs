using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Cache;
using Pixeval.Common;
using Pixeval.Config;
using Pixeval.I18N;
using Pixeval.Mako;
using Pixeval.Services.Background;
using Pixeval.Services.Core;
using Pixeval.Services.Media;
using Pixeval.Storage;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ImageViewerLoadingTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task OpeningWorkLoadsAndDisplaysImageWithoutCachedThumbnail(bool continuous)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerApplication));
        await session.Dispatch(async () =>
        {
            I18NManager.Register(new JsonMarkdownLangPlugin(), LanguageHelper.DefaultLanguage);
            using var storage = new StorageEngine(":memory:");
            using var extensions = new ExtensionService(null!, [], new HashSet<string>(), false);
            var images = new LocalImageProvider();
            var settings = new AppSettings();
            settings.BrowsingExperienceSettings.BrowseMode = continuous ? BrowseMode.Continuous : BrowseMode.Swipe;
            settings.BrowsingExperienceSettings.OpenWorkInfoByDefault = true;
            using var mako = new MakoClient(settings.ToMakoConfiguration());
            using var services = new ServiceCollection()
                .AddSingleton(extensions).AddSingleton(storage).AddSingleton(settings)
                .AddSingleton(new PixevalSettings(settings)).AddSingleton(mako)
                .AddSingleton<IImageProviderService>(images).BuildServiceProvider();
            var previous = App.Services;
            App.Services = services;
            var window = new Window { Width = 800, Height = 600 };
            UiServices.SetProvider(window, services);
            try
            {
                using var pageModel = new IllustrationViewerPageViewModel(
                    new object[] { new WorkEntry(new WorkEntry.Illust(DesignHelper.DesignIllustration with { Series = null })) },
                    0, false, mako, settings, storage, images,
                    NullArtworkActionService.Instance, NullNotificationService.Instance, extensions);
                Assert.IsNotNull(pageModel.CurrentImage);
                var model = pageModel.CurrentImage;
                var page = new IllustrationViewerPage(pageModel);
                var wrapper = page.FindControl<ImageViewerWrapper>("I")!;
                var viewer = wrapper.ImageViewer;
                var tabs = new TabView.Avalonia.TabsView
                {
                    Pages = new Avalonia.Collections.AvaloniaList<Page> { new ContentPage() }
                };
                window.Content = tabs;
                window.Show();
                ((IList<Page>) tabs.Pages).Add(page);
                tabs.SelectedIndex = 1;
                Assert.AreSame(model, pageModel.CurrentImage);
                for (var i = 0; i < 100 && !model.Images[0].LoadSuccessfully; i++)
                    await Task.Delay(20);
                for (var i = 0; i < 10; i++)
                {
                    using var frame = window.CaptureRenderedFrame();
                }
                Assert.IsTrue(model.Images[0].LoadSuccessfully);
                Assert.IsTrue(model.Images[0].HasDisplayImage);
                var image = viewer.GetVisualDescendants().OfType<AnimatedImage>().First();
                Assert.AreSame(model.Images[0].DisplaySource, image.Source);
                Assert.IsTrue(image.Bounds.Width > 0 && image.Bounds.Height > 0);
                Assert.AreEqual(1d, image.Opacity);
            }
            finally
            {
                window.Close();
                App.Services = previous;
            }
        }, CancellationToken.None);
    }

    public sealed class ViewerApplication : Application
    {
        public override void Initialize()
        {
            I18NManager.Register(new JsonMarkdownLangPlugin(), LanguageHelper.DefaultLanguage);
            _ = AvaloniaHelper.PageTypeToHeaderMap;
            var app = (App) AvaloniaXamlLoader.Load(new Uri("avares://Pixeval/App.axaml"));
            var resources = app.Resources;
            app.Resources = new ResourceDictionary();
            Resources = resources;
            var styles = app.Styles.ToArray();
            app.Styles.Clear();
            foreach (var style in styles)
                Styles.Add(style);
        }

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ViewerApplication>()
            .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    private sealed class LocalImageProvider : IImageProviderService
    {
        public CacheEngine CacheEngine => throw new NotSupportedException();
        public Bitmap ImageNotAvailable => throw new NotSupportedException();
        public IAnimatedBitmap AnimatedImageNotAvailable => throw new NotSupportedException();
        public Stream? TryGetStream(string key) => null;
        public ValueTask<IAnimatedBitmap> GetSingleImageAsync(string platform, string url, IProgress<double>? progress = null,
            Action<DecodedPreviewFrame>? onPreview = null, CancellationToken token = default) =>
            ValueTask.FromResult(IAnimatedBitmap.Load(File.OpenRead(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "ProgressiveImages", "frames.gif")), true));
        public ValueTask<IAnimatedBitmap> GetSingleImageAsync(string platform, Uri frameUri, IProgress<double>? progress = null,
            Action<DecodedPreviewFrame>? onPreview = null, CancellationToken token = default) =>
            GetSingleImageAsync(platform, frameUri.OriginalString, progress, onPreview, token);
        public ValueTask<Bitmap> GetBitmapAsync(string platform, string key, IProgress<double>? progress = null,
            int? desiredWidth = null, CancellationToken token = default) =>
            ValueTask.FromResult<Bitmap>(new WriteableBitmap(new(2, 2), new(96, 96)));
        public Task<IAnimatedBitmap> GetAnimatedBitmapAsync(string platform, string key, IProgress<double>? progress = null,
            CancellationToken token = default) => GetSingleImageAsync(platform, key, progress, token: token).AsTask();
        public IAnimatedBitmap LoadAnimatedBitmap(Stream stream) => IAnimatedBitmap.Load(stream, true);
        public ValueTask<IAnimatedBitmap> GetUgoiraAnimatedImageAsync(string platform, Uri zipUri, IReadOnlyList<int> delays,
            IProgress<double>? progress = null, Action<DecodedPreviewFrame>? onPreview = null,
            CancellationToken token = default) => throw new NotSupportedException();
        public ValueTask<Stream?> GetImageStreamAsync(string platform, string key, IProgress<double>? progress = null,
            CancellationToken token = default) => throw new NotSupportedException();
        public bool TryCacheStream(string key, Stream stream) => throw new NotSupportedException();
        public Task PurgeCacheAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task EnforceCacheSizeLimitAsync(CancellationToken token = default) => throw new NotSupportedException();
        public void UpdateNetworkOptions(MakoConfigurationDto config) => throw new NotSupportedException();
    }
}
