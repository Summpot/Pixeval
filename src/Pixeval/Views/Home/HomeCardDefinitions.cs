// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Pixeval.AppManagement;
using Pixeval.Controls;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Config;
using Pixeval.Native.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Home;
using Pixeval.Views.Capability;
using Pixeval.Views.Search;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Home;

public static class HomeCardDefinitions
{
    private static readonly FrozenDictionary<HomePageCardSourceKind, HomeCardDefinition> _BySourceKind;

    static HomeCardDefinitions()
    {
        All =
        [
            new(
                HomePageCardSourceKind.WorkRecommended,
                CreateWorkPreviewSourceFactory(card => App.AppViewModel.MakoClient.WorkRecommended(card.WorkType)),
                OpenWorkRecommendedPage,
                card => [GetDescription(card.WorkType)]),
            new(
                HomePageCardSourceKind.WorkBookmarks,
                CreateWorkPreviewSourceFactory(card => App.AppViewModel.MakoClient.WorkBookmarks(card.SimpleWorkType, card.UserId, card.PrivacyPolicy, card.Tag)),
                OpenWorkBookmarksPage,
                card =>
                [
                    $"@{card.UserId}",
                    GetDescription(card.SimpleWorkType),
                    GetDescription(card.PrivacyPolicy),
                    .. string.IsNullOrWhiteSpace(card.Tag) ? [] : new[] { $"#{card.Tag}" }
                ]),
            new(
                HomePageCardSourceKind.WorkRanking,
                CreateWorkPreviewSourceFactory(card => App.AppViewModel.MakoClient.WorkRanking(card.SimpleWorkType, card.RankOption, card.GetRankingDate())),
                OpenWorkRankingPage,
                card =>
                [
                    GetDescription(card.SimpleWorkType),
                    GetRankOptionDescription(card),
                    .. card.UseSpecifiedRankingDate
                        ? new[] { card.GetRankingDate().LocalDateTime.ToString("d", CultureInfo.CurrentCulture) }
                        : []
                ]),
            new(
                HomePageCardSourceKind.WorkNew,
                CreateWorkPreviewSourceFactory(card => App.AppViewModel.MakoClient.WorkNew(card.WorkType)),
                OpenWorkNewPage,
                card => [GetDescription(card.WorkType)]),
            new(
                HomePageCardSourceKind.WorkFollowing,
                CreateWorkPreviewSourceFactory(card => App.AppViewModel.MakoClient.WorkFollowing(card.SimpleWorkType, card.PrivacyPolicy)),
                OpenWorkFollowingPage,
                card => [GetDescription(card.SimpleWorkType), GetDescription(card.PrivacyPolicy)]),
            new(
                HomePageCardSourceKind.WorkMyPixiv,
                CreateWorkPreviewSourceFactory(card => App.AppViewModel.MakoClient.WorkMyPixiv(card.SimpleWorkType)),
                OpenWorkMyPixivPage,
                card => [GetDescription(card.SimpleWorkType)]),
            new(
                HomePageCardSourceKind.WorkRelated,
                CreateWorkPreviewSourceFactory(card => App.AppViewModel.MakoClient.WorkRelated(card.EntryId, card.SimpleWorkType)),
                OpenWorkRelatedPage,
                card => [card.EntryId.ToString(CultureInfo.InvariantCulture), GetDescription(card.SimpleWorkType)]),
            new(
                HomePageCardSourceKind.SingleSeries,
                CreateSingleSeriesPreviewSourceAsync,
                OpenSingleSeries,
                card => [card.SeriesId.ToString(CultureInfo.InvariantCulture), GetDescription(card.SimpleWorkType)]),
            new(
                HomePageCardSourceKind.WorkPosts,
                CreateWorkPreviewSourceFactory(card => App.AppViewModel.MakoClient.WorkPosted(card.WorkType, card.UserId)),
                OpenWorkPostsPage,
                card => [$"@{card.UserId}", GetDescription(card.WorkType)]),
            new(
                HomePageCardSourceKind.WorkSearch,
                CreateWorkPreviewSourceFactory(card => card.SimpleWorkType is SimpleWorkType.Novel
                    ? string.IsNullOrWhiteSpace(card.SearchText)
                        ? App.AppViewModel.MakoClient.Computed(AsyncEnumerable.Empty<object>())
                        : App.AppViewModel.MakoClient.NovelSearch(new NovelSearchArguments(card.SearchText)).ToFetchEngine()
                    : string.IsNullOrWhiteSpace(card.SearchText)
                        ? App.AppViewModel.MakoClient.Computed(AsyncEnumerable.Empty<object>())
                        : App.AppViewModel.MakoClient.IllustrationSearch(new IllustrationSearchArguments(card.SearchText)).ToFetchEngine()),
                OpenWorkSearchPage,
                card => [GetDescription(card.SimpleWorkType), card.SearchText ?? ""]),
            new(
                HomePageCardSourceKind.UserRecommended,
                CreateUserPreviewSourceFactory(_ => App.AppViewModel.MakoClient.UserRecommended().ToFetchEngine()),
                OpenUserRecommendedPage),
            new(
                HomePageCardSourceKind.UserSearch,
                CreateUserPreviewSourceFactory(card => string.IsNullOrWhiteSpace(card.SearchText)
                    ? App.AppViewModel.MakoClient.Computed(AsyncEnumerable.Empty<User>())
                    : App.AppViewModel.MakoClient.UserSearch(card.SearchText).ToFetchEngine()),
                OpenUserSearchPage,
                card => [card.SearchText ?? ""]),
            new(
                HomePageCardSourceKind.UserFollowing,
                CreateUserPreviewSourceFactory(card => App.AppViewModel.MakoClient.UserFollowing(card.UserId, card.PrivacyPolicy)),
                OpenUserFollowingPage,
                card => [$"@{card.UserId}", GetDescription(card.PrivacyPolicy)]),
            new(
                HomePageCardSourceKind.UserFollower,
                CreateUserPreviewSourceFactory(_ => App.AppViewModel.MakoClient.UserFollower()),
                OpenUserFollowerPage),
            new(
                HomePageCardSourceKind.UserMyPixiv,
                CreateUserPreviewSourceFactory(card => App.AppViewModel.MakoClient.UserMyPixiv(card.UserId)),
                OpenUserMyPixivPage,
                card => [$"@{card.UserId}"]),
            new(
                HomePageCardSourceKind.Spotlight,
                CreateSpotlightViewModelAsync,
                OpenSpotlightPage),
            new(
                HomePageCardSourceKind.SingleImage,
                CreateSingleImageViewModelAsync,
                OpenSingleImage,
                card => [card.EntryId.ToString(CultureInfo.InvariantCulture)]),
            new(
                HomePageCardSourceKind.SingleNovel,
                CreateSingleNovelViewModelAsync,
                OpenSingleNovel,
                card => [card.EntryId.ToString(CultureInfo.InvariantCulture)]),
            new(
                HomePageCardSourceKind.SingleUser,
                CreateSingleUserViewModelAsync,
                OpenSingleUser,
                card => [$"@{card.UserId}"])
        ];
        _BySourceKind = All.ToFrozenDictionary(static definition => definition.SourceKind);
    }

    public static IReadOnlyList<HomeCardDefinition> All { get; }

    public static HomeCardDefinition Get(HomePageCardSourceKind sourceKind) =>
        _BySourceKind.TryGetValue(sourceKind, out var definition)
            ? definition
            : _BySourceKind[HomePageCardSourceKind.WorkRecommended];

    public static string BuildTitle(HomePageCardLayout card) => Get(card.SourceKind).BuildTitle(card);

    public static void OpenPreviewItem(TopLevel topLevel, object? parameter, ISimpleViewViewModel? vm)
    {
        switch (parameter, vm)
        {
            case (Novel novel, NovelViewViewModel viewViewModel):
                topLevel.ViewContainer?.CreateNovelPage(novel, viewViewModel.DataProvider.CloneRef());
                break;
            case (object work, IllustrationViewViewModel viewViewModel):
                topLevel.ViewContainer?.CreateIllustrationPage(work, viewViewModel.DataProvider.CloneRef());
                break;
            case (User user, _):
                topLevel.ViewContainer?.CreateUserPage(user.RawId);
                break;
            case (SpotlightArticle article, _):
                if (topLevel.Launcher is { } launcher)
                    _ = launcher.LaunchUriAsync(new(article.ArticleUrl));
                break;
        }
    }

    private static Func<HomePageCardLayout, Task<HomeCardPreviewSource>> CreateWorkPreviewSourceFactory(
        Func<HomePageCardLayout, IFetchEngine<object>> engineFactory) =>
        card => Task.FromResult(CreateWorkPreviewSource(engineFactory(card)));

    private static Func<HomePageCardLayout, Task<HomeCardPreviewSource>> CreateUserPreviewSourceFactory(
        Func<HomePageCardLayout, IFetchEngine<User>> engineFactory) =>
        card => Task.FromResult(CreateUserPreviewSource(engineFactory(card)));

    private static Task<HomeCardPreviewSource> CreateSpotlightViewModelAsync(HomePageCardLayout card)
    {
        var engine = App.AppViewModel.MakoClient.Spotlight().ToFetchEngine();
        return Task.FromResult(new HomeCardPreviewSource(CreateSpotlightViewModel(engine)));
    }

    private static async Task<HomeCardPreviewSource> CreateSingleSeriesPreviewSourceAsync(HomePageCardLayout card)
    {
        var (detail, firstWork, engine) = await App.AppViewModel.MakoClient.GetWorkSeriesAsync(card.SimpleWorkType, card.SeriesId);
        IWorkViewViewModel viewModel = card.SimpleWorkType is SimpleWorkType.Novel
            ? new NovelViewViewModel()
            : new IllustrationViewViewModel();
        viewModel.ResetEngine(engine);
        return new(viewModel, new SingleSeriesOpeningContext(detail, firstWork));
    }

    private static async Task<HomeCardPreviewSource> CreateSingleImageViewModelAsync(HomePageCardLayout card)
    {
        var engine = App.AppViewModel.MakoClient.Computed(Single(await App.AppViewModel.MakoClient.GetIllustrationFromIdAsync(card.EntryId)));
        return new(CreateIllustrationViewModel(engine));
    }

    private static async Task<HomeCardPreviewSource> CreateSingleNovelViewModelAsync(HomePageCardLayout card)
    {
        var engine = App.AppViewModel.MakoClient.Computed(Single(await App.AppViewModel.MakoClient.GetNovelFromIdAsync(card.EntryId)));
        return new(CreateNovelViewModel(engine));
    }

    private static async Task<HomeCardPreviewSource> CreateSingleUserViewModelAsync(HomePageCardLayout card)
    {
        var userDetail = await App.AppViewModel.MakoClient.GetUserFromIdAsync(card.UserId);
        var engine = App.AppViewModel.MakoClient.Computed(Single(userDetail.User));
        return new(CreateUserViewModel(engine), new SingleUserOpeningContext(userDetail));
    }

    private static HomeCardPreviewSource CreateWorkPreviewSource(IFetchEngine<object> engine) =>
        new(engine is IFetchEngine<Novel> novelEngine
            ? CreateNovelViewModel(novelEngine)
            : CreateIllustrationViewModel(engine));

    private static HomeCardPreviewSource CreateUserPreviewSource(IFetchEngine<User> engine) =>
        new(CreateUserViewModel(engine));

    private static IllustrationViewViewModel CreateIllustrationViewModel(IFetchEngine<object> engine)
    {
        var viewModel = new IllustrationViewViewModel();
        viewModel.ResetEngine(engine);
        return viewModel;
    }

    private static NovelViewViewModel CreateNovelViewModel(IFetchEngine<Novel> engine)
    {
        var viewModel = new NovelViewViewModel();
        viewModel.ResetEngine(engine);
        return viewModel;
    }

    private static UserViewViewModel CreateUserViewModel(IFetchEngine<User> engine)
    {
        var viewModel = new UserViewViewModel();
        viewModel.ResetEngine(engine);
        return viewModel;
    }

    private static SpotlightViewViewModel CreateSpotlightViewModel(IFetchEngine<SpotlightArticle> engine)
    {
        var viewModel = new SpotlightViewViewModel();
        viewModel.ResetEngine(engine);
        return viewModel;
    }

    private static void OpenWorkRecommendedPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new WorkRecommendedPage(card.WorkType, source.TakeViewModel<IWorkViewViewModel>()));
    }

    private static void OpenWorkNewPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new WorkNewPage(card.WorkType, source.TakeViewModel<IWorkViewViewModel>()));
    }

    private static void OpenWorkPostsPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new WorkPostsPage(CreateUserBasicInfo(card), card.WorkType, source.TakeViewModel<IWorkViewViewModel>()));
    }

    private static void OpenWorkBookmarksPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new WorkBookmarksPage(
            CreateUserBasicInfo(card),
            card.SimpleWorkType,
            card.PrivacyPolicy,
            card.Tag,
            source.TakeViewModel<IWorkViewViewModel>()));
    }

    private static void OpenWorkRankingPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new WorkRankingPage(
            card.SimpleWorkType,
            card.RankOption,
            card.GetRankingDate().LocalDateTime,
            source.TakeViewModel<IWorkViewViewModel>()));
    }

    private static void OpenWorkFollowingPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new WorkFollowingPage(card.SimpleWorkType, card.PrivacyPolicy, source.TakeViewModel<IWorkViewViewModel>()));
    }

    private static void OpenWorkMyPixivPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new WorkMyPixivPage(card.SimpleWorkType, source.TakeViewModel<IWorkViewViewModel>()));
    }

    private static void OpenWorkRelatedPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new WorkRelatedPage(card.EntryId, card.SimpleWorkType, source.TakeViewModel<IWorkViewViewModel>()));
    }

    private static void OpenSingleSeries(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        var context = source.GetOpeningContext<SingleSeriesOpeningContext>();
        topLevel.ViewContainer?.CreateSeriesPage(
            card.SimpleWorkType,
            card.SeriesId,
            context.SeriesDetail,
            context.FirstWork,
            source.TakeViewModel<IWorkViewViewModel>());
    }

    private static void OpenWorkSearchPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        var searchText = card.SearchText ?? "";
        topLevel.ViewContainer?.NavigateTo(new WorkSearchResultPage(
            searchText,
            new IllustrationSearchArguments(searchText),
            new NovelSearchArguments(searchText),
            card.SimpleWorkType,
            source.TakeViewModel<IWorkViewViewModel>()));
    }

    private static void OpenUserRecommendedPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new UserRecommendedPage(source.TakeViewModel<UserViewViewModel>()));
    }

    private static void OpenUserSearchPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new UserSearchResultPage(card.SearchText, source.TakeViewModel<UserViewViewModel>()));
    }

    private static void OpenUserFollowingPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new UserFollowingPage(card.UserId, card.PrivacyPolicy, source.TakeViewModel<UserViewViewModel>()));
    }

    private static void OpenUserFollowerPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new UserFollowerPage(source.TakeViewModel<UserViewViewModel>()));
    }

    private static void OpenUserMyPixivPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new UserMyPixivPage(card.UserId, source.TakeViewModel<UserViewViewModel>()));
    }

    private static void OpenSpotlightPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        topLevel.ViewContainer?.NavigateTo(new SpotlightPage(source.TakeViewModel<SpotlightViewViewModel>()));
    }

    private static void OpenSingleImage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        if (source.GetViewModel<IllustrationViewViewModel>().Source.FirstOrDefault() is { } viewModel)
            topLevel.ViewContainer?.CreateIllustrationPage(viewModel);
    }

    private static void OpenSingleNovel(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        if (source.GetViewModel<NovelViewViewModel>().Source.FirstOrDefault() is { } viewModel)
            topLevel.ViewContainer?.CreateNovelPage(viewModel);
    }

    private static void OpenSingleUser(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel) =>
        topLevel.ViewContainer?.CreateUserPage(source.GetOpeningContext<SingleUserOpeningContext>().UserDetail);

    private static User CreateUserBasicInfo(HomePageCardLayout card) =>
        PixevalSettings.MyUser is { } me && card.UserId == PixevalSettings.MyId
            ? me
            : new User(
                card.UserId,
                BuildTitle(card),
                "",
                new ProfileImageUrls(null, null, null, AppInfo.ImageNotAvailablePath),
                false,
                null);

    private sealed record SingleSeriesOpeningContext(
        Series SeriesDetail,
        IWorkEntry? FirstWork);

    private sealed record SingleUserOpeningContext(SingleUserResponse UserDetail);

    private static async IAsyncEnumerable<T> Single<T>(T item)
    {
        yield return item;
        await Task.CompletedTask;
    }

    private static string GetDescription<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        SymbolComboBoxItem.GetResource(value);

    private static string GetRankOptionDescription(HomePageCardLayout card) =>
        SymbolComboBoxItem.GetResource(card.RankOption, card.SimpleWorkType);
}
