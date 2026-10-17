using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Common.Logging;
using Pixeval.Config;
using Pixeval.Localization;
using Pixeval.Controls;
using Pixeval.Mako;
using Pixeval.Services.Core;
using Pixeval.Services.Media;
using Pixeval.ViewModels.Home;
using Pixeval.ViewModels.Works;
using Pixeval.Views.Home;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class HomeCardRenderingTest
{
    [TestMethod]
    public Task RecommendedUsersAppearAfterLoading() => AssertCardRendersAsync(
        new UserViewViewModel(),
        new User(123, "Author", "author", new ProfileImageUrls(null, AppInfo.ImageNotAvailablePath, null, null), false, null, []));

    [TestMethod]
    public Task SpotlightAppearsAfterLoading() => AssertCardRendersAsync(
        new SpotlightViewViewModel(), new SpotlightArticle { Thumbnail = AppInfo.ImageNotAvailablePath, PureTitle = "Spotlight" });

    [TestMethod]
    public Task FailedAuthorsShowAWorkingRetryButton() => AssertCardRendersAsync(
        new UserViewViewModel(),
        new User(123, "Author", "author", new ProfileImageUrls(null, AppInfo.ImageNotAvailablePath, null, null), false, null, []),
        failInitialPages: true);

    private static async Task AssertCardRendersAsync<T>(EntryViewViewModel<T, T> model, T item, bool failInitialPages = false) where T : notnull
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(CardApplication));
        await session.Dispatch(async () =>
        {
            using var services = new ServiceCollection()
                .AddSingleton(new PixevalSettings(new AppSettings())).BuildServiceProvider();
            var previous = App.Services;
            App.Services = services;
            using var images = new ImageProviderService(new AppSettings(), new FileLogger(System.IO.Path.GetTempPath()));
            var window = new Window { Width = 800, Height = 320 };
            Source.SetImageProvider(window, images);
            try
            {
                model.ResetSource(Many(item));
                var factories = 0;
                Task<HomeCardPreviewSource> CreateSourceAsync(HomePageCardLayout _)
                {
                    if (failInitialPages && ++factories <= 2)
                    {
                        var failed = new UserViewViewModel();
                        failed.ResetSource(FailUsers());
                        return Task.FromResult(new HomeCardPreviewSource(failed));
                    }
                    return Task.FromResult(new HomeCardPreviewSource(model));
                }
                using var card = new HomePageCardControl(new HomePageCardLayout(), 1, 1,
                    new HomeCardDefinition(item is User ? HomePageCardSourceKind.UserRecommended : HomePageCardSourceKind.Spotlight,
                        CreateSourceAsync, (_, _) => { }), true);
                var preview = card.PreviewViewModel;
                window.Content = card;
                window.Show();
                var items = card.GetVisualDescendants().OfType<ItemsControl>().Single();
                await preview.LoadAsync();
                if (failInitialPages)
                {
                    Assert.IsTrue(preview.CanRetry);
                    var retry = card.GetVisualDescendants().OfType<Button>()
                        .Single(button => ReferenceEquals(button.Command, preview.RetryCommand));
                    Assert.IsTrue(retry.IsVisible);
                    Assert.AreEqual(Strings.DownloadItemActionButtonContentRetry(), retry.Content);
                    await preview.RetryCommand.ExecuteAsync(null);
                    Assert.IsFalse(retry.IsVisible);
                }
                for (var i = 0; i < 5; i++)
                {
                    await Task.Yield();
                    using var frame = window.CaptureRenderedFrame();
                }

                Assert.HasCount(20, preview.Items);
                Assert.HasCount(20, items.Items);
                Assert.IsNull(preview.PlaceholderText);
                Assert.IsNotNull(items.ContainerFromIndex(0));
                Assert.IsGreaterThan(0d, items.ContainerFromIndex(0)!.Bounds.Width);
                Assert.IsGreaterThan(0d, items.ContainerFromIndex(0)!.Bounds.Height);
                var button = items.ContainerFromIndex(0)!.GetVisualDescendants().OfType<Button>().Single();
                Assert.IsGreaterThan(0d, button.Bounds.Width);
                Assert.IsGreaterThan(0d, button.Bounds.Height);
                var thumbnail = button.GetVisualDescendants().First(c => c is Image or AvatarImage);
                Assert.IsGreaterThan(0d, thumbnail.Bounds.Width);
                Assert.IsGreaterThan(0d, thumbnail.Bounds.Height);
                Assert.IsTrue(Source.GetLoaded((Control)thumbnail));
            }
            finally
            {
                window.Close();
                App.Services = previous;
            }
            // Select Dispatch(Func<Task<T>>) so assertions after awaits finish before the test returns.
            return true;
        }, CancellationToken.None);
    }

    private static async IAsyncEnumerable<User> FailUsers()
    {
        await Task.Yield();
        yield return await Task.FromException<User>(new HttpRequestException("unavailable"));
    }

    private static async IAsyncEnumerable<T> Many<T>(T item)
    {
        await Task.Yield();
        for (var i = 0; i < 20; i++)
            yield return item switch
            {
                User user => (T)(object)(user with { Id = i + 1 }),
                SpotlightArticle article => (T)(object)(article with { Id = i + 1 }),
                _ => item
            };
    }

    public sealed class CardApplication : Application
    {
        public override void Initialize()
        {
            var app = new App();
            // App.Initialize also starts persistence and background services. Load only its compiled theme.
            typeof(App).GetMethod("!XamlIlPopulateTrampoline", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
                .Invoke(null, new object?[] { app });
            var resources = app.Resources;
            app.Resources = new ResourceDictionary();
            Resources = resources;
            var styles = app.Styles.ToArray();
            app.Styles.Clear();
            foreach (var style in styles)
                Styles.Add(style);
        }

        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<CardApplication>()
            .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
