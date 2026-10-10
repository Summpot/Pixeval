// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Models;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views;
using Pixeval.Views.Capability;
using Pixeval.Views.Home;
using Pixeval.Views.Login;
using Pixeval.Views.Search;
using Pixeval.Views.ViewContainers;
using Pixeval.Views.Viewers;
using Pixeval.Views.Work;

namespace Pixeval.Services;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider? _serviceProvider;

    public NavigationService(IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider;
    }

    public ViewContainerBase? ResolveViewContainer(Control? sourceControl = null)
    {
        if (sourceControl is ViewContainerBase directContainer)
            return directContainer;

        if (sourceControl is not null && TopLevel.GetTopLevel(sourceControl)?.ViewContainer is { } container)
            return container;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var activeWindow = desktop.Windows.FirstOrDefault(static w => w.IsActive)
                ?? desktop.Windows.FirstOrDefault(static w => w.IsVisible)
                ?? desktop.MainWindow;
            if (activeWindow?.Content is ViewContainerBase windowContainer)
                return windowContainer;

            if (activeWindow is not null && TopLevel.GetTopLevel(activeWindow)?.ViewContainer is { } topContainer)
                return topContainer;
        }

        return null;
    }

    public void NavigateTo<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(
        object? parameter = null,
        bool removeCurrentPage = false,
        Control? sourceControl = null)
        where TPage : Page
    {
        NavigateTo(typeof(TPage), parameter, removeCurrentPage, sourceControl);
    }

    public void NavigateTo(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type pageType,
        object? parameter = null,
        bool removeCurrentPage = false,
        Control? sourceControl = null)
    {
        var container = ResolveViewContainer(sourceControl);
        if (container is null)
            return;

        var page = CreatePage(pageType, parameter);
        container.NavigateTo(page, removeCurrentPage);
    }

    public void NavigateToKey(string pageKey, object? parameter = null, bool removeCurrentPage = false, Control? sourceControl = null)
    {
        if (NavigationPageRegistry.TryGetPage(pageKey, out var definition))
        {
            NavigateTo(definition.PageType, parameter, removeCurrentPage, sourceControl);
        }
    }

    public bool TrySelectExisting(Type pageType, Control? sourceControl = null)
    {
        var container = ResolveViewContainer(sourceControl);
        if (container is TabViewContainer tabView)
        {
            return tabView.TrySelectPage(pageType);
        }

        return false;
    }

    public Page CreatePage(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type pageType,
        object? parameter = null)
    {
        if (parameter is null)
        {
            if (pageType == typeof(WorkPostsPage))
                return new WorkPostsPage(CurrentOrFallbackUser());

            if (pageType == typeof(WorkBookmarksPage))
                return new WorkBookmarksPage(CurrentOrFallbackUser());

            if (pageType == typeof(UserFollowingPage))
                return new UserFollowingPage(CurrentUserId());

            if (pageType == typeof(UserMyPixivPage))
                return new UserMyPixivPage(CurrentUserId());

            if (pageType == typeof(RelatedUsersPage))
                return new RelatedUsersPage(CurrentUserId());

            var diService = _serviceProvider?.GetService(pageType) ?? App.Services?.GetService(pageType);
            if (diService is Page diPage)
                return diPage;

            return (Page) Activator.CreateInstance(pageType)!;
        }

        if (pageType == typeof(IllustrationViewerPage))
        {
            if (parameter is IllustrationViewerPageViewModel illustVm)
                return new IllustrationViewerPage(illustVm);
        }

        if (pageType == typeof(NovelViewerPage))
        {
            if (parameter is NovelViewerPageViewModel novelVm)
                return new NovelViewerPage(novelVm);
        }

        if (pageType == typeof(UserViewerPage))
        {
            if (parameter is UserViewerPageViewModel userVm)
                return new UserViewerPage(userVm);
            if (parameter is long userId)
                return new UserViewerPage(new UserViewerPageViewModel(userId));
            if (parameter is SingleUserResponse userDetail)
                return new UserViewerPage(new UserViewerPageViewModel(userDetail));
        }

        if (pageType == typeof(SeriesViewerPage))
        {
            if (parameter is SeriesViewerPageViewModel seriesVm)
                return new SeriesViewerPage(seriesVm);
        }

        if (pageType == typeof(WorkSearchResultPage))
        {
            if (parameter is IllustrationSearchArguments illustArgs)
                return new WorkSearchResultPage(illustArgs);
            if (parameter is NovelSearchArguments novelArgs)
                return new WorkSearchResultPage(novelArgs);
            if (parameter is ValueTuple<string, SimpleWorkType> tuple2)
                return new WorkSearchResultPage(tuple2.Item1, tuple2.Item2);
            if (parameter is ValueTuple<string, IllustrationSearchArguments, NovelSearchArguments, SimpleWorkType> tuple4)
                return new WorkSearchResultPage(tuple4.Item1, tuple4.Item2, tuple4.Item3, tuple4.Item4);
        }

        if (pageType == typeof(UserSearchResultPage) && parameter is string keyword)
            return new UserSearchResultPage(keyword);

        if (pageType == typeof(ArtworkSauceNaoSearchResultPage) && parameter is ValueTuple<string, byte[]> sauceParam)
            return new ArtworkSauceNaoSearchResultPage(sauceParam.Item1, sauceParam.Item2);

        if (pageType == typeof(WorkRecommendedPage) && parameter is WorkType recType)
            return new WorkRecommendedPage(recType);

        if (pageType == typeof(WorkNewPage) && parameter is WorkType newType)
            return new WorkNewPage(newType);

        if (pageType == typeof(WorkPostsPage) && parameter is ValueTuple<User, WorkType> postParam)
            return new WorkPostsPage(postParam.Item1, postParam.Item2);

        if (pageType == typeof(WorkBookmarksPage) && parameter is ValueTuple<User, SimpleWorkType, PrivacyPolicy, string?> bmParam)
            return new WorkBookmarksPage(bmParam.Item1, bmParam.Item2, bmParam.Item3, bmParam.Item4);

        if (pageType == typeof(WorkRankingPage) && parameter is ValueTuple<SimpleWorkType, RankOption, DateTime> rkParam)
            return new WorkRankingPage(rkParam.Item1, rkParam.Item2, rkParam.Item3);

        if (pageType == typeof(WorkFollowingPage) && parameter is ValueTuple<SimpleWorkType, PrivacyPolicy> folParam)
            return new WorkFollowingPage(folParam.Item1, folParam.Item2);

        if (pageType == typeof(WorkMyPixivPage) && parameter is SimpleWorkType mpType)
            return new WorkMyPixivPage(mpType);

        if (pageType == typeof(WorkRelatedPage) && parameter is ValueTuple<long, SimpleWorkType> relParam)
            return new WorkRelatedPage(relParam.Item1, relParam.Item2);

        if (pageType == typeof(UserFollowingPage) && parameter is ValueTuple<long, PrivacyPolicy> ufParam)
            return new UserFollowingPage(ufParam.Item1, ufParam.Item2);

        if (pageType == typeof(UserMyPixivPage) && parameter is long umId)
            return new UserMyPixivPage(umId);

        if (pageType == typeof(BookmarkUsersPage) && parameter is ValueTuple<long, bool, string> bmUsersParam)
            return new BookmarkUsersPage(bmUsersParam.Item1, bmUsersParam.Item2, bmUsersParam.Item3);

        try
        {
            return (Page) Activator.CreateInstance(pageType, parameter)!;
        }
        catch
        {
            var provider = _serviceProvider ?? App.Services;
            if (provider is not null)
                return (Page) ActivatorUtilities.CreateInstance(provider, pageType, parameter);

            throw;
        }
    }

    public Task PushAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(
        Page host,
        object? parameter = null)
        where TPage : Page
    {
        if (host.IsInNavigationPage && host.Parent is NavigationPage frame)
            return frame.PushAsync(CreatePage(typeof(TPage), parameter));

        return Task.CompletedTask;
    }

    private User CurrentOrFallbackUser()
    {
        var session = (_serviceProvider ?? App.Services)?.GetService<IUserSessionService>();
        return session?.CurrentUserEntity
            ?? new User(0, "", "", new ProfileImageUrls(null, null, null, null), false, null, []);
    }

    private long CurrentUserId()
    {
        var session = (_serviceProvider ?? App.Services)?.GetService<IUserSessionService>();
        return session is { CurrentUserId: > 0 } ? session.CurrentUserId : 0;
    }

    #region Strongly-Typed Domain Navigation

    public void NavigateToIllustration(object illustration, IReadOnlyList<object>? source = null, bool needRefresh = false, Control? sourceControl = null)
    {
        var vm = source is not null
            ? new IllustrationViewerPageViewModel(source, IndexOf(source, illustration), needRefresh)
            : new IllustrationViewerPageViewModel(illustration, needRefresh);

        NavigateTo<IllustrationViewerPage>(vm, removeCurrentPage: false, sourceControl);
    }

    public void NavigateToIllustration(string id, string platform = PlatformConstants.Pixiv, Control? sourceControl = null)
    {
        NavigateTo<IllustrationViewerPage>(new IllustrationViewerPageViewModel(id, platform), removeCurrentPage: false, sourceControl);
    }

    public void NavigateToNovel(Novel novel, IReadOnlyList<Novel>? source = null, bool needRefresh = false, Control? sourceControl = null)
    {
        var vm = source is not null
            ? new NovelViewerPageViewModel(source, IndexOf(source, novel), needRefresh)
            : new NovelViewerPageViewModel(novel, needRefresh);

        NavigateTo<NovelViewerPage>(vm, removeCurrentPage: false, sourceControl);
    }

    public void NavigateToNovel(long novelId, Control? sourceControl = null)
    {
        NavigateTo<NovelViewerPage>(new NovelViewerPageViewModel(novelId), removeCurrentPage: false, sourceControl);
    }

    public void NavigateToUser(long userId, Control? sourceControl = null)
    {
        NavigateTo<UserViewerPage>(new UserViewerPageViewModel(userId), removeCurrentPage: false, sourceControl);
    }

    public void NavigateToUser(SingleUserResponse userDetail, Control? sourceControl = null)
    {
        NavigateTo<UserViewerPage>(new UserViewerPageViewModel(userDetail), removeCurrentPage: false, sourceControl);
    }

    public void NavigateToSeries(SimpleWorkType workType, long seriesId, Control? sourceControl = null)
    {
        NavigateTo<SeriesViewerPage>(new SeriesViewerPageViewModel(workType, seriesId), removeCurrentPage: false, sourceControl);
    }

    public void NavigateToSeries(
        SimpleWorkType workType,
        long seriesId,
        Series seriesDetail,
        IWorkEntry? firstWork,
        IWorkViewViewModel worksViewModel,
        Control? sourceControl = null)
    {
        NavigateTo<SeriesViewerPage>(
            new SeriesViewerPageViewModel(workType, seriesId, seriesDetail, firstWork, worksViewModel),
            removeCurrentPage: false,
            sourceControl);
    }

    public void NavigateToWorkSearch(string keyword, SimpleWorkType workType = SimpleWorkType.Illustration, Control? sourceControl = null)
    {
        NavigateTo<WorkSearchResultPage>((keyword, workType), removeCurrentPage: false, sourceControl);
    }

    public void NavigateToWorkSearch(IllustrationSearchArguments arguments, Control? sourceControl = null)
    {
        NavigateTo<WorkSearchResultPage>(arguments, removeCurrentPage: false, sourceControl);
    }

    public void NavigateToWorkSearch(NovelSearchArguments arguments, Control? sourceControl = null)
    {
        NavigateTo<WorkSearchResultPage>(arguments, removeCurrentPage: false, sourceControl);
    }

    public void NavigateToWorkSearch(
        string keyword,
        IllustrationSearchArguments illustrationArgs,
        NovelSearchArguments novelArgs,
        SimpleWorkType workType,
        Control? sourceControl = null)
    {
        NavigateTo<WorkSearchResultPage>((keyword, illustrationArgs, novelArgs, workType), removeCurrentPage: false, sourceControl);
    }

    public void NavigateToUserSearch(string? keyword, Control? sourceControl = null)
    {
        NavigateTo<UserSearchResultPage>(keyword ?? string.Empty, removeCurrentPage: false, sourceControl);
    }

    public void NavigateToHome(bool removeCurrentPage = false, Control? sourceControl = null)
    {
        NavigateTo<HomePage>(null, removeCurrentPage, sourceControl);
    }

    public void NavigateToLogin(bool removeCurrentPage = false, Control? sourceControl = null)
    {
        NavigateTo<LoginPage>(null, removeCurrentPage, sourceControl);
    }

    #endregion

    private static int IndexOf<T>(IReadOnlyList<T> list, T item)
    {
        if (list is IList<T> iList)
            return iList.IndexOf(item);

        for (var i = 0; i < list.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(list[i], item))
                return i;
        }

        return -1;
    }
}
