// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Models;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Booru;
using Pixeval.Native.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.ViewContainers;

namespace Pixeval.Views.Viewers;

public static class ViewerHelper
{
    /// <param name="control"></param>
    extension(ViewContainerBase control)
    {
        #region Illustration

        /// <summary>
        /// 此方法无法加载更多插画，加载单张图使用
        /// </summary>
        public void CreateIllustrationPage(string id, string platform)
        {
            control.NavigateTo(new IllustrationViewerPage(new(id, platform)));
        }

        /// <summary>
        /// 此方法无法加载更多插画
        /// </summary>
        /// <param name="illustrationViewModel">指定的插画实体</param>
        /// <param name="needRefresh"></param>
        public void CreateIllustrationPage(object illustrationViewModel, bool needRefresh = false)
        {
            control.NavigateTo(new IllustrationViewerPage(new(illustrationViewModel, needRefresh)));
        }

        /// <summary>
        /// 此方法可以使用<paramref name="sourceView"/>来加载更多插画
        /// </summary>
        /// <param name="illustrationViewModel">指定的插画实体</param>
        /// <param name="sourceView">指定的插画实体所在的SourceView</param>
        /// <param name="needRefresh">是否需要刷新插画（如从数据库中加载的则需要刷新）</param>
        public void CreateIllustrationPage(object illustrationViewModel, ISourceView<object> sourceView, bool needRefresh = false)
        {
            var index = sourceView.View.IndexOf(illustrationViewModel);
            control.NavigateTo(new IllustrationViewerPage(new(sourceView, index, needRefresh)));
        }

        #endregion

        #region Novel

        /// <summary>
        /// 此方法无法加载更多小说，加载单个小说使用
        /// </summary>
        public void CreateNovelPage(long id)
        {
            control.NavigateTo(new NovelViewerPage(new(id)));
        }

        /// <summary>
        /// 此方法无法加载更多小说
        /// </summary>
        /// <param name="novel">指定的小说实体</param>
        /// <param name="needRefresh">是否需要刷新小说（如从数据库中加载的则需要刷新）</param>
        public void CreateNovelPage(Novel novel, bool needRefresh = false)
        {
            control.NavigateTo(new NovelViewerPage(new(novel, needRefresh)));
        }

        /// <summary>
        /// 此方法可以使用<paramref name="sourceView"/>来加载更多小说
        /// </summary>
        /// <param name="novel">指定的小说实体</param>
        /// <param name="sourceView">指定的小说所在的SourceView</param>
        /// <param name="needRefresh">是否需要刷新小说（如从数据库中加载的则需要刷新）</param>
        public void CreateNovelPage(Novel novel, ISourceView<Novel> sourceView, bool needRefresh = false)
        {
            var index = sourceView.View.IndexOf(novel);
            control.NavigateTo(new NovelViewerPage(new(sourceView, index, needRefresh)));
        }

        #endregion

        #region Series

        public void CreateSeriesPage(SimpleWorkType workType, long seriesId)
        {
            control.NavigateTo(new SeriesViewerPage(new(workType, seriesId)));
        }

        public void CreateSeriesPage(
            SimpleWorkType workType,
            long seriesId,
            Series seriesDetail,
            IWorkEntry? firstWork,
            IWorkViewViewModel worksViewModel)
        {
            control.NavigateTo(new SeriesViewerPage(new(workType, seriesId, seriesDetail, firstWork, worksViewModel)));
        }

        #endregion

        #region User

        public void CreateUserPage(long userId)
        {
            control.NavigateTo(new UserViewerPage(new(userId)));
        }

        public void CreateUserPage(SingleUserResponse userDetail)
        {
            var viewModel = new UserViewerPageViewModel(userDetail);
            control.NavigateTo(new UserViewerPage(viewModel));
        }

        #endregion
    }

    public static async Task<object?> TryGetArtworkAsync(string platform, string id)
    {
        try
        {
            if (string.Equals(platform, PlatformConstants.Pixiv, StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(id, out var illustId))
                    return await App.AppViewModel.MakoClient.GetIllustrationAsync(illustId);
            }
            else
            {
                var booruPlatform = BooruPlatformExtensions.FromPlatformString(platform);
                var client = App.AppViewModel.AppServiceProvider.GetRequiredService<BooruClient>();
                return await client.GetPostAsync(booruPlatform, id);
            }
        }
        catch (Exception e)
        {
            var logger = App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>();
            logger.LogError(nameof(TryGetArtworkAsync), e);
        }

        return null;
    }
}
