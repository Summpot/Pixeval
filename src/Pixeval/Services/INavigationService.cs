// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Avalonia.Controls;
using Pixeval.Models;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.ViewModels;
using Pixeval.Views.ViewContainers;

namespace Pixeval.Services;

public interface INavigationService
{
    /// <summary>
    /// Resolves the effective view container from the specified control or active application window.
    /// </summary>
    ViewContainerBase? ResolveViewContainer(Control? sourceControl = null);

    /// <summary>
    /// Navigates to the specified page type.
    /// </summary>
    void NavigateTo<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(
        object? parameter = null,
        bool removeCurrentPage = false,
        Control? sourceControl = null)
        where TPage : Page;

    /// <summary>
    /// Navigates to the specified page type.
    /// </summary>
    void NavigateTo(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type pageType,
        object? parameter = null,
        bool removeCurrentPage = false,
        Control? sourceControl = null);

    /// <summary>
    /// Navigates to a page by its registry key (e.g. from Navigation YAML).
    /// </summary>
    void NavigateToKey(string pageKey, object? parameter = null, bool removeCurrentPage = false, Control? sourceControl = null);

    /// <summary>
    /// Attempts to select an existing tab of the given page type if available.
    /// </summary>
    bool TrySelectExisting(Type pageType, Control? sourceControl = null);

    /// <summary>
    /// Creates a page instance for the given page type and parameter.
    /// </summary>
    Page CreatePage([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type pageType, object? parameter = null);

    /// <summary>
    /// Pushes a page created by <see cref="CreatePage"/> onto the navigation frame that hosts <paramref name="host"/>.
    /// </summary>
    Task PushAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(
        Page host,
        object? parameter = null)
        where TPage : Page;

    #region Strongly-Typed Domain Navigation

    void NavigateToIllustration(object illustration, IReadOnlyList<object>? source = null, bool needRefresh = false, Control? sourceControl = null);

    void NavigateToIllustration(string id, string platform = PlatformConstants.Pixiv, Control? sourceControl = null);

    void NavigateToNovel(Novel novel, IReadOnlyList<Novel>? source = null, bool needRefresh = false, Control? sourceControl = null);

    void NavigateToNovel(long novelId, Control? sourceControl = null);

    void NavigateToUser(long userId, Control? sourceControl = null);

    void NavigateToUser(SingleUserResponse userDetail, Control? sourceControl = null);

    void NavigateToSeries(SimpleWorkType workType, long seriesId, Control? sourceControl = null);

    void NavigateToSeries(
        SimpleWorkType workType,
        long seriesId,
        Series seriesDetail,
        IWorkEntry? firstWork,
        IWorkViewViewModel worksViewModel,
        Control? sourceControl = null);

    void NavigateToWorkSearch(string keyword, SimpleWorkType workType = SimpleWorkType.Illustration, Control? sourceControl = null);

    void NavigateToWorkSearch(IllustrationSearchArguments arguments, Control? sourceControl = null);

    void NavigateToWorkSearch(NovelSearchArguments arguments, Control? sourceControl = null);

    void NavigateToWorkSearch(
        string keyword,
        IllustrationSearchArguments illustrationArgs,
        NovelSearchArguments novelArgs,
        SimpleWorkType workType,
        Control? sourceControl = null);

    void NavigateToUserSearch(string? keyword, Control? sourceControl = null);

    void NavigateToHome(bool removeCurrentPage = false, Control? sourceControl = null);

    void NavigateToLogin(bool removeCurrentPage = false, Control? sourceControl = null);

    #endregion
}
