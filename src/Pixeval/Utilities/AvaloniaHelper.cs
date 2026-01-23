// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using Pixeval.I18N;
using Pixeval.Views;
using Pixeval.Views.Capability;
using Pixeval.Views.ViewContainers;

namespace Pixeval.Utilities;

public static class AvaloniaHelper
{
    extension(TopLevel topLevel)
    {
        public ViewContainerBase? ViewContainer => topLevel.Content as ViewContainerBase;

        public static TopLevel? GetTopLevelForFlyout(Visual? visual) => TopLevel.GetTopLevel(TopLevel.GetTopLevel(visual)?.Parent?.Parent as Visual);
    }

    extension(ViewContainerBase control)
    {
        public void NavigateTo<TParameter>(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type pageType, 
            TParameter parameter,
            bool removeCurrent = false)
        {
            var (icon, header) = _PageIconMapping[pageType];
            control.NavigateTo(pageType, new SymbolIcon
            {
                Symbol = icon,
                FontSize = 16,
                IconVariant = IconVariant.Color
            }, header, parameter, removeCurrent);
        }

        public void NavigateTo(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
            Type pageType,
            bool removeCurrent = false)
            => control.NavigateTo<object?>(pageType, null, removeCurrent);

        public void NavigateTo<TPage, TParameter>(TParameter parameter, bool removeCurrent = false) where TPage : Control, new() =>
            control.NavigateTo(typeof(TPage), parameter, removeCurrent);

        public void NavigateTo<TPage>(bool removeCurrent = false) where TPage : Control, new()
            => control.NavigateTo<TPage, object?>(null, removeCurrent);
    }

    private static readonly FrozenDictionary<Type, (Symbol Icon, string Header)> _PageIconMapping = new Dictionary<Type, (Symbol Icon, string Header)>
    {
        [typeof(LoginPage)] = (Symbol.PersonKey, I18NManager.GetResource(MainPageResources.LoginTabContent)),
        [typeof(RecommendWorksPage)] = (Symbol.Calendar, I18NManager.GetResource(MainPageResources.RecommendationsTabContent)),
        [typeof(RankingsPage)] = (Symbol.ArrowTrendingLines, I18NManager.GetResource(MainPageResources.RankingsTabContent)),
        [typeof(BookmarksPage)] = (Symbol.Library, I18NManager.GetResource(MainPageResources.BookmarksTabContent)),
        [typeof(FollowingsPage)] = (Symbol.PersonHeart, I18NManager.GetResource(MainPageResources.FollowingsTabContent)),
        [typeof(SpotlightsPage)] = (Symbol.SlideTextSparkle, I18NManager.GetResource(MainPageResources.SpotlightsTabContent)),
        [typeof(RecommendUsersPage)] = (Symbol.PeopleCommunity, I18NManager.GetResource(MainPageResources.RecommendUsersTabContent)),
        [typeof(RecentWorkPostsPage)] = (Symbol.AlertUrgent, I18NManager.GetResource(MainPageResources.RecentPostsTabContent)),
        [typeof(NewWorksPage)] = (Symbol.ArrowSync, I18NManager.GetResource(MainPageResources.NewWorksTabContent)),
        [typeof(SearchUsersPage)] = (Symbol.Person, I18NManager.GetResource(MainPageResources.SearchUsersResult)),
        [typeof(SearchWorksPage)] = (Symbol.SearchSparkle, I18NManager.GetResource(MainPageResources.SearchWorksResult)),
        // [typeof(FeedsPage)] = (Symbol.Molecule, I18NManager.GetResource(MainPageResources.FeedTabContent)),
        // [typeof(BrowsingHistoryPage)] = (Symbol.History, I18NManager.GetResource(MainPageResources.HistoriesTabContent)),
        // [typeof(DownloadPage)] = (Symbol.ArrowSquareDown, I18NManager.GetResource(MainPageResources.DownloadListTabContent))
        // [typeof(ExtensionsPage)] = (Symbol.PuzzlePiece, I18NManager.GetResource(MainPageResources.ExtensionsTabContent))
        // [typeof(HelpPage)] = (Symbol.ChatBubblesQuestion, I18NManager.GetResource(MainPageResources.HelpTabContent)),
        // [typeof(AboutPage)] = (Symbol.PersonStarburst, I18NManager.GetResource(MainPageResources.AboutTabContent)),
        [typeof(SettingsPage)] = (Symbol.Settings, I18NManager.GetResource(MainPageResources.SettingsTabContent)),
    }.ToFrozenDictionary();

    public static IReadOnlyList<NavigationInfo> HeaderItems { get; } = new[]
        {
            typeof(RecommendWorksPage),
            typeof(RankingsPage),
            typeof(BookmarksPage),
            typeof(FollowingsPage),
            typeof(SpotlightsPage),
            typeof(RecommendUsersPage),
            typeof(RecentWorkPostsPage),
            typeof(NewWorksPage)
        }
        .Select(t =>
        {
            var value = _PageIconMapping[t];
            return new NavigationInfo(t, value.Icon, value.Header);
        })
        .ToArray();

    public static IReadOnlyList<NavigationInfo> FooterItems { get; } = new[]
        {
            // typeof(BrowsingHistoryPage),
            // typeof(DownloadPage),
            // typeof(ExtensionsPage),
            // typeof(HelpPage),
            // typeof(AboutPage),
            typeof(SettingsPage)
        }
        .Select(t =>
        {
            var value = _PageIconMapping[t];
            return new NavigationInfo(t, value.Icon, value.Header);
        })
        .ToArray();
}

public record NavigationInfo(Type PageType, Symbol Icon, string Header);
