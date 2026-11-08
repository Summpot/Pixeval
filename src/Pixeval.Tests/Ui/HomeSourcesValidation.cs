using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Services.Core;
using Pixeval.Config;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Home;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Works;
using Pixeval.Views.Home;
using Pixeval.Views.Search;
using Pixeval.Views.Viewers;

namespace Pixeval.Tests.Ui;

internal static class HomeSourcesValidation
{
    private const long SourceId = 992007;
    private const string ImageUri = "https://i.pximg.net/m3e/home-source.png";
    private const string Title = "首页固定来源 / Fixed home source";

    internal static async Task RunAsync(string directory)
    {
        var app = App.AppViewModel;
        using var fixture = new OfflineSearchFixture(app.MakoClient);
        fixture.BookmarkResponseFactory = (_, _) => Task.FromResult("{\"bookmark_tags\":[],\"next_url\":null}");
        app.MakoClient.SetRefreshToken("fixture-home-sources");
        await FixtureAuthentication.IdentifyAsync(app.MakoClient);
        using (var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var buffer = new MemoryStream())
        {
            await asset.CopyToAsync(buffer);
            fixture.ImageResponses[ImageUri] = buffer.ToArray();
        }
        ConcurrentQueue<Uri> requests = new();
        Func<HttpRequestMessage, CancellationToken, Task<string>> response = (request, _) =>
        {
            requests.Enqueue(request.RequestUri!);
            return Task.FromResult(Response(request.RequestUri!.AbsolutePath));
        };
        fixture.WorkFeedResponseFactory = fixture.BookmarkWorksResponseFactory = fixture.RelatedResponseFactory =
            fixture.UserResponseFactory = fixture.SeriesResponseFactory = fixture.SpotlightResponseFactory = fixture.SearchResponseFactory = response;
        fixture.RankingResponseFactory = uri => { requests.Enqueue(uri); return Response(uri.AbsolutePath); };
        // Mako.Novel body caching belongs to the shared work model, independently of the
        // home source. This mode observes destination identity, not reader content.
        fixture.WorkCardResponseFactory = (_, _) => throw new HttpRequestException("Fixed unavailable novel body; destination identity only.");
        var logger = app.AppServiceProvider.GetRequiredService<FileLogger>();
        List<string> report = [];
        // Mako may return default response records after a transport failure. They
        // must not turn a failed single-user/series source into a successful ID 0.
        foreach (var kind in new[] { HomePageCardSourceKind.SingleImage, HomePageCardSourceKind.SingleNovel,
                     HomePageCardSourceKind.SingleUser, HomePageCardSourceKind.SingleSeries })
        {
            fixture.SeriesResponseFactory = (_, _) => throw new HttpRequestException("Fixed home detail failure");
            var card = new HomePageCardLayout(kind, 0, 0) { EntryId = SourceId, UserId = SourceId, SeriesId = SourceId };
            using var preview = new HomeCardPreviewViewModel(card, App.AppViewModel.AppServiceProvider.GetRequiredService<HomeCardDefinitions>().Get(kind).CreatePreviewSourceAsync);
            await preview.LoadAsync();
            Require(preview.Items.Count == 0 && preview.ViewModel is null
                && preview.PlaceholderText == Strings.HomePageCardPreviewFailedTextBlockText(),
                "A failed " + kind + " detail stays a failed preview instead of a successful default record");
            report.Add($"PASS {kind}: failed details never publish a default record");
        }

        // Detail factories must cancel the actual request, not merely ignore the result.
        foreach (var kind in new[] { HomePageCardSourceKind.SingleImage, HomePageCardSourceKind.SingleNovel,
                     HomePageCardSourceKind.SingleUser, HomePageCardSourceKind.SingleSeries })
        {
            var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.SeriesResponseFactory = async (request, token) =>
            {
                started.TrySetResult(token);
                return await completion.Task;
            };
            var card = new HomePageCardLayout(kind, 0, 0) { EntryId = SourceId, UserId = SourceId, SeriesId = SourceId };
            var control = UiTestSession.Create<HomePageCardControl>(card, 1, 1);
            var load = control.PreviewViewModel.LoadAsync();
            var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            control.Dispose();
            await NovelReaderValidation.UntilAsync(() => token.IsCancellationRequested);
            var cancelled = token.IsCancellationRequested;
            completion.TrySetResult(Response(ExpectedRequest(card).Path));
            await load;
            Require(control.PreviewViewModel.ViewModel is null && control.PreviewViewModel.Items.Count == 0,
                "Closed details reject late successful data");
            Require(cancelled, "Closing a " + kind + " preview cancels its in-flight detail HTTP request");
            report.Add($"PASS {kind}: detail cancellation and late response isolation");
        }
        fixture.SeriesResponseFactory = response;
        foreach (var novel in new[] { false, true })
            foreach (var definition in App.AppViewModel.AppServiceProvider.GetRequiredService<HomeCardDefinitions>().All)
            {
                app.HomePageCards.Clear();
                AppInfo.SaveHomePageCards(app.HomePageCards);
                requests.Clear();
                var page = UiTestSession.Create<HomePage>();
                var host = new PageTestHost
                {
                    Content = page,
                    NavigationObserver = target =>
                    {
                        Require(target.GetType() == Destination(definition.SourceKind), "The home source opens its original destination type");
                        if (target.DataContext is UserViewerPageViewModel user)
                            Require(user.Id == SourceId, "Single-user navigation retains its ID");
                        if (target.DataContext is SeriesViewerPageViewModel series)
                            Require(series.Id == SourceId && series.WorkKind == (novel ? SimpleWorkKind.Novel : SimpleWorkKind.Illustration),
                                "Series navigation retains its ID and work type");
                    }
                };
                var window = new Window { Width = novel ? 360 : 1000, Height = 800, RequestedThemeVariant = novel ? ThemeVariant.Dark : ThemeVariant.Light, Content = host.Shell };
                var key = $"{definition.SourceKind}-{(novel ? "novel" : "illustration")}";
                try
                {
                    Pixeval.Common.MaterialAnimation.Initialize(window);
                    window.Show();
                    page.ViewModel.IsEditMode = true;
                    await RenderAsync(window);
                    var templateButton = page.FindControl<ItemsControl>("CardLibrary")!.GetVisualDescendants().OfType<Button>()
                        .Single(button => ReferenceEquals(button.DataContext, definition));
                    await ActivateAsync(window, templateButton);
                    Configure(page.ViewModel, novel);
                    Require(page.ViewModel.TryCreateCardFromSourceParameters(out var draft), "Valid configured source produces a draft");
                    await ActivateAsync(window, page.FindControl<Button>("AddConfiguredCardButton")!);
                    Require(app.HomePageCards.Count == 1 && page.ViewModel.HasSelectedCard && !page.ViewModel.IsAddingConfiguredCard,
                        "The actual Add action creates and selects exactly one card and restores its enabled state");
                    var saved = app.HomePageCards.Single();
                    draft = draft!.Value with { ColumnSpan = saved.ColumnSpan, RowSpan = saved.RowSpan };
                    Require(NativeFixtureData.Serialize(draft) == NativeFixtureData.Serialize(saved), "Adding preserves every configured source value");
                    var persisted = AppInfo.LoadHomePageCards(logger)!.Single();
                    Require(NativeFixtureData.Serialize(saved) == NativeFixtureData.Serialize(persisted), "Original YAML roundtrip preserves source kind, parameters and layout");
                    var control = page.FindControl<Grid>("HomeGrid")!.Children.OfType<HomePageCardControl>().Single();
                    await control.PreviewViewModel.LoadAsync();
                    await RenderAsync(window);
                    Require(control.PreviewViewModel.Items.Count == 1 && control.PreviewViewModel.PlaceholderText is null,
                        "The added source renders one real preview model: " + key + " / " + control.PreviewViewModel.PlaceholderText);
                    var query = ExpectedRequest(saved);
                    Require(requests.Count == 1, "One initial source request, including the preloaded series response: " + string.Join(", ", requests));
                    var actual = requests.Single();
                    Require(actual.AbsolutePath == query.Path, "The saved source uses its original endpoint: " + actual.AbsolutePath + " expected " + query.Path);
                    var parameters = HttpUtility.ParseQueryString(actual.Query);
                    foreach (var (name, value) in query.Parameters)
                        Require(parameters[name] == value, $"The source retains {name}={value}: {actual.PathAndQuery}");
                    if (saved.SourceKind == HomePageCardSourceKind.WorkRecommended)
                        Require(parameters[novel ? "include_ranking_novels" : "include_ranking_illusts"] == "true"
                            && parameters["include_privacy_policy"] == "true", "Home recommendations retain ranking/privacy flags");
                    if (saved.SourceKind is HomePageCardSourceKind.WorkNew or HomePageCardSourceKind.WorkPosts && !novel)
                        Require(parameters[saved.SourceKind == HomePageCardSourceKind.WorkNew ? "content_type" : "type"] == "manga",
                            "Home manga sources retain their subtype");
                    using (var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No home source frame"))
                        frame.Save(Path.Combine(directory, key + ".png"), new PngBitmapEncoderOptions());
                    page.ViewModel.IsEditMode = false;
                    await RenderAsync(window);
                    using (var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No home preview frame"))
                        frame.Save(Path.Combine(directory, key + "-preview.png"), new PngBitmapEncoderOptions());
                    await ActivateAsync(window, control.FindControl<Button>("OpenCardButton")!);
                    await control.OpenCardPageCommand.ExecutionTask!;
                    host.CloseDestinationAndReturn();
                    Require(host.Navigations == 1 && control.PreviewViewModel.Items.Count == 1,
                        "Opening and immediately closing a destination preserves the home owner's shared preview");
                    Require(requests.Count(uri => uri.AbsolutePath == actual.AbsolutePath) == 1,
                        "A preloaded destination does not refetch its source details: " + string.Join(", ", requests));
                    page.ViewModel.IsEditMode = true;
                    await RenderAsync(window);
                    Require(control.Focus(NavigationMethod.Tab), "The card is keyboard selectable after returning to editing");
                    window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                    window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                    var oldSource = control.PreviewViewModel.ViewModel!.Source;
                    page.DeleteSelectedCardCommand.Execute(null);
                    await RenderAsync(window);
                    Require(app.HomePageCards.Count == 0 && AppInfo.LoadHomePageCards(logger)!.Count == 0
                        && control.PreviewViewModel.ViewModel is null && oldSource.Count == 0,
                        "Deleting the selected source persists removal and releases its preview");
                    report.Add($"PASS {key}: actual Add, exact HTTP parameters, preview, YAML roundtrip, single navigation/shared source and delete/disposal");
                }
                finally
                {
                    page.Dispose();
                    host.Content = null;
                    window.Close();
                }
            }

        Require(fixture.UnexpectedRequests.IsEmpty, "Every source stays inside the offline request matrix: " + string.Join(", ", fixture.UnexpectedRequests));
        File.WriteAllLines(Path.Combine(directory, "home-source-contracts.txt"), report);
        File.WriteAllLines(Path.Combine(directory, "home-source-requests.txt"), fixture.RequestLog);
        Console.WriteLine($"Validated {report.Count} home-source groups.");
    }

    private static Type Destination(HomePageCardSourceKind kind) => kind switch
    {
        HomePageCardSourceKind.WorkRecommended => typeof(WorkRecommendedPage),
        HomePageCardSourceKind.WorkNew => typeof(WorkNewPage),
        HomePageCardSourceKind.WorkBookmarks => typeof(WorkBookmarksPage),
        HomePageCardSourceKind.WorkRanking => typeof(WorkRankingPage),
        HomePageCardSourceKind.WorkFollowing => typeof(WorkFollowingPage),
        HomePageCardSourceKind.WorkMyPixiv => typeof(WorkMyPixivPage),
        HomePageCardSourceKind.WorkRelated => typeof(WorkRelatedPage),
        HomePageCardSourceKind.WorkPosts => typeof(WorkPostsPage),
        HomePageCardSourceKind.WorkSearch => typeof(WorkSearchResultPage),
        HomePageCardSourceKind.UserRecommended => typeof(UserRecommendedPage),
        HomePageCardSourceKind.UserSearch => typeof(UserSearchResultPage),
        HomePageCardSourceKind.UserFollowing => typeof(UserFollowingPage),
        HomePageCardSourceKind.UserFollower => typeof(UserFollowerPage),
        HomePageCardSourceKind.UserMyPixiv => typeof(UserMyPixivPage),
        HomePageCardSourceKind.Spotlight => typeof(SpotlightPage),
        HomePageCardSourceKind.SingleImage => typeof(IllustrationViewerPage),
        HomePageCardSourceKind.SingleNovel => typeof(NovelViewerPage),
        HomePageCardSourceKind.SingleUser => typeof(UserViewerPage),
        HomePageCardSourceKind.SingleSeries => typeof(SeriesViewerPage),
        _ => throw new InvalidOperationException("Unlisted destination")
    };

    private static void Configure(HomePageViewModel model, bool novel)
    {
        foreach (var choice in model.ActiveParameterEditors.OfType<HomeCardChoiceParameterEditorViewModel>())
            choice.Value = choice.Kind switch
            {
                HomeCardParameterKinds.WorkKind => novel ? WorkKind.Novel : WorkKind.Manga,
                HomeCardParameterKinds.SimpleWorkKind => novel ? SimpleWorkKind.Novel : SimpleWorkKind.Illustration,
                HomeCardParameterKinds.PrivacyRestrict => PrivacyRestrict.Private,
                HomeCardParameterKinds.RankMode => RankMode.Day,
                _ => choice.Value
            };
        foreach (var text in model.ActiveParameterEditors.OfType<HomeCardTextParameterEditorViewModel>())
            text.Text = text.Kind switch
            {
                HomeCardParameterKinds.SearchText => "alpha 中文",
                HomeCardParameterKinds.Tag => "标签 / fixed tag",
                HomeCardParameterKinds.UserId when model.PendingTemplate!.HasParameter(HomeCardParameterKinds.PrivacyRestrict) => "900001",
                _ => SourceId.ToString()
            };
        if (model.ActiveParameterEditors.OfType<HomeCardRankingDateParameterEditorViewModel>().FirstOrDefault() is { } date)
        {
            date.UseSpecifiedDate = true;
            date.SelectedDate = new DateTime(2021, 4, 5);
        }
    }

    private static (string Path, Dictionary<string, string> Parameters) ExpectedRequest(HomePageCardLayout card)
    {
        var work = card.WorkKind == WorkKind.Novel ? "novel" : "illust";
        var simple = card.SimpleWorkKind == SimpleWorkKind.Novel ? "novel" : "illust";
        var id = SourceId.ToString();
        return card.SourceKind switch
        {
            HomePageCardSourceKind.WorkRecommended => ($"/v1/{work}/recommended", new() { ["filter"] = "for_android" }),
            HomePageCardSourceKind.WorkNew => ($"/v1/{work}/new", new() { ["filter"] = "for_android" }),
            HomePageCardSourceKind.WorkBookmarks => ($"/v1/user/bookmarks/{simple}", new() { ["user_id"] = "900001", ["restrict"] = "private", ["tag"] = card.Tag! }),
            HomePageCardSourceKind.WorkRanking => ($"/v1/{simple}/ranking", new() { ["mode"] = "day", ["date"] = "2021-04-05" }),
            HomePageCardSourceKind.WorkFollowing => ($"/{(simple == "illust" ? "v2" : "v1")}/{simple}/follow", new() { ["restrict"] = "private" }),
            HomePageCardSourceKind.WorkMyPixiv => ($"/v2/{simple}/mypixiv", new()),
            HomePageCardSourceKind.WorkRelated => ($"/{(simple == "illust" ? "v2" : "v1")}/{simple}/related", new() { [$"{simple}_id"] = id }),
            HomePageCardSourceKind.WorkPosts => ($"/v1/user/{work}s", new() { ["user_id"] = id }),
            HomePageCardSourceKind.WorkSearch => ($"/v1/search/{simple}", new() { ["word"] = card.SearchText! }),
            HomePageCardSourceKind.UserRecommended => ("/v1/user/recommended", new()),
            HomePageCardSourceKind.UserSearch => ("/v1/search/user", new() { ["word"] = card.SearchText! }),
            HomePageCardSourceKind.UserFollowing => ("/v1/user/following", new() { ["user_id"] = "900001", ["restrict"] = "private" }),
            HomePageCardSourceKind.UserFollower => ("/v1/user/follower", new()),
            HomePageCardSourceKind.UserMyPixiv => ("/v1/user/mypixiv", new() { ["user_id"] = id }),
            HomePageCardSourceKind.Spotlight => ("/v1/spotlight/articles", new()),
            HomePageCardSourceKind.SingleImage => ("/v1/illust/detail", new() { ["illust_id"] = id }),
            HomePageCardSourceKind.SingleNovel => ("/v2/novel/detail", new() { ["novel_id"] = id }),
            HomePageCardSourceKind.SingleUser => ("/v2/user/detail", new() { ["user_id"] = id }),
            HomePageCardSourceKind.SingleSeries => ($"/{(simple == "illust" ? "v1" : "v2")}/{simple}/series", new() { [simple == "illust" ? "illust_series_id" : "series_id"] = id }),
            _ => throw new InvalidOperationException("Unlisted source")
        };
    }

    private static string Response(string path)
    {
        var image = Illustration.CreateDefault() with
        {
            Id = SourceId,
            Title = Title,
            PageCount = 1,
            Width = 640,
            Height = 480,
            User = User.CreateDefault() with { Id = 900007, Name = "Fixture author" },
            ImageUrls = new() { Medium = ImageUri, SquareMedium = ImageUri, Large = ImageUri },
            MetaSinglePage = new() { OriginalImageUrl = ImageUri }
        };
        var novel = Mako.Novel.CreateDefault() with { Id = SourceId, Title = Title, User = image.User, ImageUrls = image.ImageUrls };
        var user = image.User with { Id = SourceId, Name = Title, ProfileImageUrls = new() { Medium = ImageUri } };
        return path switch
        {
            "/v1/illust/detail" => NativeFixtureData.Serialize(new { illust = image }),
            "/v2/novel/detail" => NativeFixtureData.Serialize(new { novel }),
            "/v2/user/detail" => NativeFixtureData.Serialize(SingleUserResponse.CreateDefault() with
            { User = User.CreateDefault() with { Id = SourceId, Name = Title, ProfileImageUrls = new() { Medium = ImageUri } } }),
            "/v1/illust/series" => NativeFixtureData.Serialize(new { illust_series_detail = Series.CreateDefault() with { Id = SourceId, Title = Title }, illust_series_first_illust = image, illusts = new[] { image }, next_url = (string?) null }),
            "/v2/novel/series" => NativeFixtureData.Serialize(new { novel_series_detail = Series.CreateDefault() with { Id = SourceId, Title = Title }, novel_series_first_novel = novel, novel_series_latest_novel = novel, novels = new[] { novel }, next_url = (string?) null }),
            "/v1/user/recommended" or "/v1/search/user" or "/v1/user/following" or "/v1/user/follower" or "/v1/user/mypixiv" or "/v1/user/related" => NativeFixtureData.Serialize(new { user_previews = new[] { new { user } }, next_url = (string?) null }),
            "/v1/spotlight/articles" => NativeFixtureData.Serialize(new { spotlight_articles = new[] { new SpotlightArticle { Id = SourceId, Title = Title, PureTitle = Title, Thumbnail = ImageUri, ArticleUrl = "https://www.pixivision.net/zh/a/992007", Category = SpotlightCategory.Spotlight, SubcategoryLabel = "Fixture", PublishDate = DateTimeOffset.UnixEpoch.ToString("O") } }, next_url = (string?) null }),
            _ when path.Contains("novel", StringComparison.Ordinal) => NativeFixtureData.Serialize(new { novels = new[] { novel }, next_url = (string?) null }),
            _ => NativeFixtureData.Serialize(new { illusts = new[] { image }, next_url = (string?) null })
        };
    }

    private static async Task ActivateAsync(Window window, Control control)
    {
        control.BringIntoView();
        await RenderAsync(window);
        Require(control.Focus(NavigationMethod.Tab), "Home actions remain keyboard reachable");
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        await RenderAsync(window);
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(180);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }
}
