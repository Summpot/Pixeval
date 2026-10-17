// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using FluentIcons.Common;
using Pixeval.Localization;
using Pixeval.Config;
using Pixeval.Views.Works;
using Pixeval.Views.Download;
using Pixeval.Views.Home;
using Pixeval.Views.Login;
using Pixeval.Views.Search;
using Pixeval.Views.Settings;
using Pixeval.Views.Shell;
using Pixeval.Views.Viewers;

namespace Pixeval.Common;

public static class AvaloniaHelper
{
    static AvaloniaHelper()
    {
    }

    public static void Init()
    {
    }

    extension(TopLevel topLevel)
    {
        public ViewContainerBase? ViewContainer
        {
            get
            {
                var current = topLevel;
                while (current is not null)
                {
                    if (current.Content is ViewContainerBase vc)
                        return vc;

                    if (current is PopupRoot popupRoot)
                    {
                        var target = (popupRoot.Parent as Popup)?.PlacementTarget ?? popupRoot.Parent;
                        if (target is Visual v && TopLevel.GetTopLevel(v) is { } targetTopLevel && targetTopLevel != current)
                        {
                            current = targetTopLevel;
                            continue;
                        }
                    }

                    var parent = current.Parent;
                    if (parent is Popup popup)
                        parent = popup.PlacementTarget ?? popup.Parent;

                    if (parent is not Visual visual || TopLevel.GetTopLevel(visual) is not { } nextTopLevel || nextTopLevel == current)
                        break;

                    current = nextTopLevel;
                }

                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var activeWindow = desktop.Windows.FirstOrDefault(static w => w.IsActive) ?? desktop.MainWindow;
                    if (activeWindow?.Content is ViewContainerBase windowVc)
                        return windowVc;
                }

                return null;
            }
        }
    }

    public static Dictionary<Type, (Symbol Symbol, string Header)> PageTypeToHeaderMap { get; } =
    [
        Page<HomePage>(Symbol.Home, nameof(Strings.MainPageTabHome)),
        Page<LoginPage>(Symbol.PersonKey, nameof(Strings.MainPageTabLogin), false),
        Page<SearchPage>(Symbol.SearchSparkle, nameof(Strings.MainPageTabSearch)),
        Page<WorkRecommendedPage>(Symbol.Calendar, nameof(Strings.MainPageTabWorkRecommended)),
        Page<WorkRankingPage>(Symbol.ArrowTrendingLines, nameof(Strings.MainPageTabWorkRanking)),
        Page<WorkBookmarksPage>(Symbol.Library, nameof(Strings.MainPageTabWorkBookmarks)),
        Page<WorkRelatedPage>(Symbol.LightbulbFilament, nameof(Strings.MainPageTabWorkRelated), true, false),
        Page<SeriesPage>(Symbol.LayerDiagonalPerson, nameof(Strings.MainPageTabSeries)),
        Page<WorkPostsPage>(Symbol.Image, nameof(Strings.MainPageTabWorkPosts)),
        Page<WorkSearchResultPage>(Symbol.SearchSparkle, nameof(Strings.MainPageTabWorkSearchResult), true, false),
        Page<UserFollowingPage>(Symbol.PersonHeart, nameof(Strings.MainPageTabUserFollowing)),
        Page<SpotlightPage>(Symbol.SlideTextSparkle, nameof(Strings.MainPageTabSpotlight)),
        Page<UserRecommendedPage>(Symbol.PeopleCommunity, nameof(Strings.MainPageTabUserRecommended)),
        Page<UserSearchResultPage>(Symbol.Person, nameof(Strings.MainPageTabUserSearchResult), true, false),
        Page<UserFollowerPage>(Symbol.People, nameof(Strings.MainPageTabUserFollower)),
        Page<UserMyPixivPage>(Symbol.PeopleInterwoven, nameof(Strings.MainPageTabUserMyPixiv)),
        Page<RelatedUsersPage>(Symbol.PeopleCommunity, nameof(Strings.MainPageTabRelatedUser)),
        Page<WorkFollowingPage>(Symbol.AlertUrgent, nameof(Strings.MainPageTabWorkFollowing)),
        Page<WorkMyPixivPage>(Symbol.Molecule, nameof(Strings.MainPageTabWorkMyPixiv)),
        Page<WorkNewPage>(Symbol.ArrowSync, nameof(Strings.MainPageTabWorkNew)),
        Page<BrowsingHistoryPage>(Symbol.History, nameof(Strings.MainPageTabBrowsingHistory)),
        Page<WatchLaterPage>(Symbol.Clock, nameof(Strings.MainPageTabWatchLater)),
        Page<DownloadPage>(Symbol.ArrowSquareDown, nameof(Strings.MainPageTabDownload)),
        Page<ExtensionsPage>(Symbol.PuzzlePiece, nameof(Strings.MainPageTabExtensions), false),
        Page<SettingsPage>(Symbol.Settings, nameof(Strings.MainPageTabSettings), false),
        Page<AboutPage>(Symbol.PersonStarburst, nameof(Strings.MainPageTabAbout), false, false),
        Page<HelpPage>(Symbol.ChatBubblesQuestion, nameof(Strings.MainPageTabHelp), false, false),
        Page<IllustrationViewerPage>(Symbol.Image, nameof(Strings.MainPageTabSingleImage), true, false),
        Page<NovelViewerPage>(Symbol.BookOpen, nameof(Strings.MainPageTabSingleNovel), true, false),
        Page<UserViewerPage>(Symbol.Person, nameof(Strings.MainPageTabSingleUser), true, false),
        Page<SeriesViewerPage>(Symbol.ListBar, nameof(Strings.MainPageTabSingleSeries), true, false),
        Page<WorkInfoPage>(Symbol.BookContacts, nameof(Strings.MainPageTabWorkInfo), true, false),
        Page<CommentsPage>(Symbol.ChatMultiple, nameof(Strings.MainPageTabComments), true, false)
    ];

    public static Dictionary<HomePageCardSourceKind, (Symbol Symbol, string Header)> HomeCardSourceKindToHeaderMap { get; } =
        new()
        {
            [HomePageCardSourceKind.WorkRecommended] = PageTypeToHeaderMap[typeof(WorkRecommendedPage)],
            [HomePageCardSourceKind.WorkBookmarks] = PageTypeToHeaderMap[typeof(WorkBookmarksPage)],
            [HomePageCardSourceKind.WorkRanking] = PageTypeToHeaderMap[typeof(WorkRankingPage)],
            [HomePageCardSourceKind.WorkNew] = PageTypeToHeaderMap[typeof(WorkNewPage)],
            [HomePageCardSourceKind.WorkFollowing] = PageTypeToHeaderMap[typeof(WorkFollowingPage)],
            [HomePageCardSourceKind.WorkMyPixiv] = PageTypeToHeaderMap[typeof(WorkMyPixivPage)],
            [HomePageCardSourceKind.WorkRelated] = PageTypeToHeaderMap[typeof(WorkRelatedPage)],
            [HomePageCardSourceKind.WorkPosts] = PageTypeToHeaderMap[typeof(WorkPostsPage)],
            [HomePageCardSourceKind.WorkSearch] = PageTypeToHeaderMap[typeof(WorkSearchResultPage)],
            [HomePageCardSourceKind.UserRecommended] = PageTypeToHeaderMap[typeof(UserRecommendedPage)],
            [HomePageCardSourceKind.UserSearch] = PageTypeToHeaderMap[typeof(UserSearchResultPage)],
            [HomePageCardSourceKind.UserFollowing] = PageTypeToHeaderMap[typeof(UserFollowingPage)],
            [HomePageCardSourceKind.UserFollower] = PageTypeToHeaderMap[typeof(UserFollowerPage)],
            [HomePageCardSourceKind.UserMyPixiv] = PageTypeToHeaderMap[typeof(UserMyPixivPage)],
            [HomePageCardSourceKind.Spotlight] = PageTypeToHeaderMap[typeof(SpotlightPage)],
            [HomePageCardSourceKind.SingleSeries] = PageTypeToHeaderMap[typeof(SeriesViewerPage)],
            [HomePageCardSourceKind.SingleImage] = PageTypeToHeaderMap[typeof(IllustrationViewerPage)],
            [HomePageCardSourceKind.SingleNovel] = PageTypeToHeaderMap[typeof(NovelViewerPage)],
            [HomePageCardSourceKind.SingleUser] = PageTypeToHeaderMap[typeof(UserViewerPage)]
        };

    public static (Symbol Symbol, string Header) GetPageHeader(Type pageType)
    {
        var value = PageTypeToHeaderMap[pageType];
        return (value.Symbol, Strings.GetString(value.Header));
    }

    public static (Symbol Symbol, string Header) GetHomeCardHeader(HomePageCardSourceKind sourceKind)
    {
        var value = HomeCardSourceKindToHeaderMap[sourceKind];
        return (value.Symbol, Strings.GetString(value.Header));
    }

    public static EventHandler<RoutedEventArgs>? LaunchUriTagInWebBrowser { get; }
        = (sender, e) =>
        {
            if (sender is not Control { Tag: { } parameter } control)
                return;
            var launcher = control.LauncherService;
            if (parameter is not Uri uri)
                if (parameter is not string str || !Uri.TryCreate(str, UriKind.RelativeOrAbsolute, out uri!))
                    return;
            _ = launcher.LaunchUriAsync(uri);
        };

    public static EventHandler<RoutedEventArgs>? LaunchFileTagInWebBrowser { get; }
        = (sender, e) =>
        {
            if (sender is not Control { Tag: { } parameter } control)
                return;
            var launcher = control.LauncherService;
            if (parameter is not string path)
                return;
            if (new FileInfo(path) is { Exists: true } file)
                _ = launcher.LaunchFileInfoAsync(file);
            else if (new DirectoryInfo(path) is { Exists: true } directory)
                _ = launcher.LaunchDirectoryInfoAsync(directory);
        };

    public static EventHandler<RoutedEventArgs>? CopyTagToClipboard { get; }
        = (sender, e) =>
        {
            if (sender is not Control { Tag: { } parameter } control)
                return;
            var clipboard = control.ClipboardService;

            if (parameter is not string str)
                if (parameter is Uri uri)
                    str = uri.OriginalString;
                else
                    return;

            var notifications = control.Notifications;
            _ = clipboard.SetTextAsync(str)
                .ContinueWith(_ => notifications.ShowSuccess(Strings.MiscCopied()),
                    TaskScheduler.FromCurrentSynchronizationContext());
        };

    extension(Dictionary<Type, (Symbol Symbol, string Header)> dictionary)
    {
        private void Add(KeyValuePair<Type, (Symbol Symbol, string Header)> item)
        {
            dictionary[item.Key] = item.Value;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TPage"></typeparam>
    /// <param name="icon"></param>
    /// <param name="headerResource"></param>
    /// <param name="needLogin">需要登录后才能打开的页面</param>
    /// <param name="inNavigation">需要参数而无法直接从导航栏打开的页</param>
    /// <returns></returns>
    private static KeyValuePair<Type, (Symbol Symbol, string Header)> Page<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(
        Symbol icon,
        string headerResource,
        bool needLogin = true,
        bool inNavigation = true)
        where TPage : Page
    {
        var header = headerResource;
        if (inNavigation)
        {
            var navigation = new NavigationPageDefinition(
                typeof(TPage).Name[..^4], // XXPage
                typeof(TPage),
                icon,
                headerResource,
                header,
                needLogin);
            NavigationPageRegistry.Pages.Add(navigation);
        }

        return new(typeof(TPage), (icon, header));
    }
}
