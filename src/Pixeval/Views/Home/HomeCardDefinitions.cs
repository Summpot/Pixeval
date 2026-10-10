// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement;
using Pixeval.Controls;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Config;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Home;
using Pixeval.Views.Capability;
using Pixeval.Views.Search;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Home;

public sealed class HomeCardDefinitions
{
    private readonly MakoClient _makoClient;
    private readonly INavigationService _navigation;
    private readonly IUserSessionService _session;
    private readonly FrozenDictionary<HomePageCardSourceKind, HomeCardDefinition> _bySourceKind;

    public HomeCardDefinitions(MakoClient makoClient, INavigationService navigation, IUserSessionService session)
    {
        _makoClient = makoClient;
        _navigation = navigation;
        _session = session;
        All =
        [
            new(
                HomePageCardSourceKind.WorkRecommended,
                CreateWorkPreviewSourceFactory(card => _makoClient.WorkRecommended(card.WorkType)),
                OpenWorkRecommendedPage,
                card => [GetDescription(card.WorkType)]),
            new(
                HomePageCardSourceKind.WorkBookmarks,
                CreateWorkPreviewSourceFactory(card => _makoClient.WorkBookmarks(card.SimpleWorkType, card.UserId, card.PrivacyPolicy, card.Tag)),
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
                CreateWorkPreviewSourceFactory(card => _makoClient.WorkRanking(card.SimpleWorkType, card.RankOption, card.GetRankingDate())),
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
                CreateWorkPreviewSourceFactory(card => _makoClient.WorkNew(card.WorkType)),
                OpenWorkNewPage,
                card => [GetDescription(card.WorkType)]),
            new(
                HomePageCardSourceKind.WorkFollowing,
                CreateWorkPreviewSourceFactory(card => _makoClient.WorkFollowing(card.SimpleWorkType, card.PrivacyPolicy)),
                OpenWorkFollowingPage,
                card => [GetDescription(card.SimpleWorkType), GetDescription(card.PrivacyPolicy)]),
            new(
                HomePageCardSourceKind.WorkMyPixiv,
                CreateWorkPreviewSourceFactory(card => _makoClient.WorkMyPixiv(card.SimpleWorkType)),
                OpenWorkMyPixivPage,
                card => [GetDescription(card.SimpleWorkType)]),
            new(
                HomePageCardSourceKind.WorkRelated,
                CreateWorkPreviewSourceFactory(card => _makoClient.WorkRelated(card.EntryId, card.SimpleWorkType)),
                OpenWorkRelatedPage,
                card => [card.EntryId.ToString(CultureInfo.InvariantCulture), GetDescription(card.SimpleWorkType)]),
            new(
                HomePageCardSourceKind.SingleSeries,
                CreateSingleSeriesPreviewSourceAsync,
                OpenSingleSeries,
                card => [card.SeriesId.ToString(CultureInfo.InvariantCulture), GetDescription(card.SimpleWorkType)]),
            new(
                HomePageCardSourceKind.WorkPosts,
                CreateWorkPreviewSourceFactory(card => _makoClient.WorkPosted(card.WorkType, card.UserId)),
                OpenWorkPostsPage,
                card => [$"@{card.UserId}", GetDescription(card.WorkType)]),
            new(
                HomePageCardSourceKind.WorkSearch,
                CreateWorkPreviewSourceFactory(card => card.SimpleWorkType is SimpleWorkType.Novel
                    ? string.IsNullOrWhiteSpace(card.SearchText)
                        ? _makoClient.Computed(AsyncEnumerable.Empty<object>())
                        : _makoClient.NovelSearch(new NovelSearchArguments(card.SearchText)).ToFetchEngine()
                    : string.IsNullOrWhiteSpace(card.SearchText)
                        ? _makoClient.Computed(AsyncEnumerable.Empty<object>())
                        : _makoClient.IllustrationSearch(new IllustrationSearchArguments(card.SearchText)).ToFetchEngine()),
                OpenWorkSearchPage,
                card => [GetDescription(card.SimpleWorkType), card.SearchText ?? ""]),
            new(
                HomePageCardSourceKind.UserRecommended,
                CreateUserPreviewSourceFactory(_ => _makoClient.UserRecommended().ToFetchEngine()),
                OpenUserRecommendedPage),
            new(
                HomePageCardSourceKind.UserSearch,
                CreateUserPreviewSourceFactory(card => string.IsNullOrWhiteSpace(card.SearchText)
                    ? _makoClient.Computed(AsyncEnumerable.Empty<User>())
                    : _makoClient.UserSearch(card.SearchText).ToFetchEngine()),
                OpenUserSearchPage,
                card => [card.SearchText ?? ""]),
            new(
                HomePageCardSourceKind.UserFollowing,
                CreateUserPreviewSourceFactory(card => _makoClient.UserFollowing(card.UserId, card.PrivacyPolicy)),
                OpenUserFollowingPage,
                card => [$"@{card.UserId}", GetDescription(card.PrivacyPolicy)]),
            new(
                HomePageCardSourceKind.UserFollower,
                CreateUserPreviewSourceFactory(_ =>
                {
                    var userId = _session.CurrentUserId;
                    if (userId <= 0)
                        throw new InvalidOperationException("User is not logged in");
                    return _makoClient.UserFollower(userId).ToFetchEngine();
                }),
                OpenUserFollowerPage),
            new(
                HomePageCardSourceKind.UserMyPixiv,
                CreateUserPreviewSourceFactory(card => _makoClient.UserMyPixiv(card.UserId)),
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
        _bySourceKind = All.ToFrozenDictionary(static definition => definition.SourceKind);
    }

    public IReadOnlyList<HomeCardDefinition> All { get; }

    public HomeCardDefinition Get(HomePageCardSourceKind sourceKind) =>
        _bySourceKind.TryGetValue(sourceKind, out var definition)
            ? definition
            : _bySourceKind[HomePageCardSourceKind.WorkRecommended];

    public string BuildTitle(HomePageCardLayout card) => Get(card.SourceKind).BuildTitle(card);

    public static void OpenPreviewItem(TopLevel topLevel, object? parameter, ISimpleViewViewModel? vm)
    {
        switch (parameter, vm)
        {
            case (Novel novel, NovelViewViewModel viewViewModel):
                topLevel.ViewContainer?.CreateNovelPage(novel, (IReadOnlyList<Novel>) viewViewModel.View);
                break;
            case (object work, IllustrationViewViewModel viewViewModel):
                topLevel.ViewContainer?.CreateIllustrationPage(work, (IReadOnlyList<object>) viewViewModel.View);
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

    private Task<HomeCardPreviewSource> CreateSpotlightViewModelAsync(HomePageCardLayout card)
    {
        var engine = _makoClient.Spotlight().ToFetchEngine();
        return Task.FromResult(new HomeCardPreviewSource(CreateSpotlightViewModel(engine)));
    }

    private async Task<HomeCardPreviewSource> CreateSingleSeriesPreviewSourceAsync(HomePageCardLayout card)
    {
        var (detail, firstWork, engine) = await _makoClient.GetWorkSeriesAsync(card.SimpleWorkType, card.SeriesId);
        IWorkViewViewModel viewModel = card.SimpleWorkType is SimpleWorkType.Novel
            ? new NovelViewViewModel()
            : new IllustrationViewViewModel();
        viewModel.ResetEngine(engine);
        return new(viewModel, new SingleSeriesOpeningContext(detail, firstWork));
    }

    private async Task<HomeCardPreviewSource> CreateSingleImageViewModelAsync(HomePageCardLayout card)
    {
        var engine = _makoClient.Computed(Single(await _makoClient.GetIllustrationFromIdAsync(card.EntryId)));
        return new(CreateIllustrationViewModel(engine));
    }

    private async Task<HomeCardPreviewSource> CreateSingleNovelViewModelAsync(HomePageCardLayout card)
    {
        var engine = _makoClient.Computed(Single(await _makoClient.GetNovelFromIdAsync(card.EntryId)));
        return new(CreateNovelViewModel(engine));
    }

    private async Task<HomeCardPreviewSource> CreateSingleUserViewModelAsync(HomePageCardLayout card)
    {
        var userDetail = await _makoClient.GetUserFromIdAsync(card.UserId);
        var engine = _makoClient.Computed(Single(userDetail.User));
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

    private void OpenWorkRecommendedPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateTo<WorkRecommendedPage>(card.WorkType, sourceControl: topLevel);
    }

    private void OpenWorkNewPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateTo<WorkNewPage>(card.WorkType, sourceControl: topLevel);
    }

    private void OpenWorkPostsPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateTo<WorkPostsPage>((CreateUserBasicInfo(card), card.WorkType), sourceControl: topLevel);
    }

    private void OpenWorkBookmarksPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateTo<WorkBookmarksPage>(
            (CreateUserBasicInfo(card), card.SimpleWorkType, card.PrivacyPolicy, card.Tag),
            sourceControl: topLevel);
    }

    private void OpenWorkRankingPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateTo<WorkRankingPage>(
            (card.SimpleWorkType, card.RankOption, card.GetRankingDate().LocalDateTime),
            sourceControl: topLevel);
    }

    private void OpenWorkFollowingPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateTo<WorkFollowingPage>((card.SimpleWorkType, card.PrivacyPolicy), sourceControl: topLevel);
    }

    private void OpenWorkMyPixivPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateTo<WorkMyPixivPage>(card.SimpleWorkType, sourceControl: topLevel);
    }

    private void OpenWorkRelatedPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateTo<WorkRelatedPage>((card.EntryId, card.SimpleWorkType), sourceControl: topLevel);
    }

    private void OpenSingleSeries(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateToSeries(card.SimpleWorkType, card.SeriesId, topLevel);
    }

    private void OpenWorkSearchPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        var searchText = card.SearchText ?? "";
        _navigation.NavigateToWorkSearch(
            searchText,
            new IllustrationSearchArguments(searchText),
            new NovelSearchArguments(searchText),
            card.SimpleWorkType,
            topLevel);
    }

    private void OpenUserRecommendedPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateTo<UserRecommendedPage>(sourceControl: topLevel);
    }

    private void OpenUserSearchPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateToUserSearch(card.SearchText, topLevel);
    }

    private void OpenUserFollowingPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateTo<UserFollowingPage>((card.UserId, card.PrivacyPolicy), sourceControl: topLevel);
    }

    private void OpenUserFollowerPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateTo<UserFollowerPage>(sourceControl: topLevel);
    }

    private void OpenUserMyPixivPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateTo<UserMyPixivPage>(card.UserId, sourceControl: topLevel);
    }

    private void OpenSpotlightPage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        _navigation.NavigateTo<SpotlightPage>(sourceControl: topLevel);
    }

    private void OpenSingleImage(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        if (source.GetViewModel<IllustrationViewViewModel>().Source.FirstOrDefault() is { } viewModel)
            _navigation.NavigateToIllustration(viewModel, sourceControl: topLevel);
    }

    private void OpenSingleNovel(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel)
    {
        if (source.GetViewModel<NovelViewViewModel>().Source.FirstOrDefault() is { } viewModel)
            _navigation.NavigateToNovel(viewModel, sourceControl: topLevel);
    }

    private void OpenSingleUser(HomePageCardLayout card, HomeCardPreviewSource source, TopLevel topLevel) =>
        _navigation.NavigateToUser(source.GetOpeningContext<SingleUserOpeningContext>().UserDetail, topLevel);

    private User CreateUserBasicInfo(HomePageCardLayout card)
    {
        var myUser = _session.CurrentUserEntity;
        var myId = _session.CurrentUserId;
        return myUser is { } me && card.UserId == myId
            ? me
            : new User(
                card.UserId,
                BuildTitle(card),
                "",
                new ProfileImageUrls(null, null, null, AppInfo.ImageNotAvailablePath),
                false,
                null);
    }

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
