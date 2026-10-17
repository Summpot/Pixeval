// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pixeval.Pixiv;
using Pixeval.Booru;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Shell;

namespace Pixeval.Views.Viewers;

public static class ViewerHelper
{
    extension(INavigationService navigation)
    {
        #region Illustration

        /// <summary>
        /// 此方法无法加载更多插画，加载单张图使用
        /// </summary>
        public void CreateIllustrationPage(string id, string platform)
        {
            navigation.NavigateToIllustration(id, platform);
        }

        /// <summary>
        /// 此方法无法加载更多插画
        /// </summary>
        /// <param name="illustrationViewModel">指定的插画实体</param>
        /// <param name="needRefresh"></param>
        public void CreateIllustrationPage(object illustrationViewModel, bool needRefresh = false)
        {
            navigation.NavigateToIllustration(illustrationViewModel, null, needRefresh);
        }

        /// <summary>
        /// 此方法可以使用<paramref name="items"/>来浏览更多插画
        /// </summary>
        /// <param name="illustrationViewModel">指定的插画实体</param>
        /// <param name="items">指定的插画实体所在列表</param>
        /// <param name="needRefresh">是否需要刷新插画（如从数据库中加载的则需要刷新）</param>
        public void CreateIllustrationPage(object illustrationViewModel, IReadOnlyList<object> items, bool needRefresh = false)
        {
            navigation.NavigateToIllustration(illustrationViewModel, items, needRefresh);
        }

        #endregion

        #region Mako.Novel

        /// <summary>
        /// 此方法无法加载更多小说，加载单个小说使用
        /// </summary>
        public void CreateNovelPage(long id)
        {
            navigation.NavigateToNovel(id);
        }

        /// <summary>
        /// 此方法无法加载更多小说
        /// </summary>
        /// <param name="novel">指定的小说实体</param>
        /// <param name="needRefresh">是否需要刷新小说（如从数据库中加载的则需要刷新）</param>
        public void CreateNovelPage(Mako.Novel novel, bool needRefresh = false)
        {
            navigation.NavigateToNovel(novel, null, needRefresh);
        }

        /// <summary>
        /// 此方法可以使用<paramref name="items"/>来浏览更多小说
        /// </summary>
        /// <param name="novel">指定的小说实体</param>
        /// <param name="items">指定的小说所在列表</param>
        /// <param name="needRefresh">是否需要刷新小说（如从数据库中加载的则需要刷新）</param>
        public void CreateNovelPage(Mako.Novel novel, IReadOnlyList<Mako.Novel> items, bool needRefresh = false)
        {
            navigation.NavigateToNovel(novel, items, needRefresh);
        }

        #endregion

        #region Series

        public void CreateSeriesPage(SimpleWorkKind workType, long seriesId)
        {
            navigation.NavigateToSeries(workType, seriesId);
        }

        public void CreateSeriesPage(
            SimpleWorkKind workType,
            long seriesId,
            Series seriesDetail,
            IWorkEntry? firstWork,
            IWorkViewViewModel worksViewModel)
        {
            navigation.NavigateToSeries(workType, seriesId, seriesDetail, firstWork, worksViewModel);
        }

        #endregion

        #region User

        public void CreateUserPage(long userId)
        {
            navigation.NavigateToUser(userId);
        }

        public void CreateUserPage(SingleUserResponse userDetail)
        {
            navigation.NavigateToUser(userDetail);
        }

        #endregion
    }

    public static async Task<object?> TryGetArtworkAsync(
        string platform,
        string id,
        MakoClient makoClient,
        BooruClient? booruClient,
        FileLogger? logger = null)
    {
        try
        {
            if (string.Equals(platform, PlatformConstants.Pixiv, StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(id, out var illustId))
                    return await makoClient.GetIllustrationAsync(illustId);
            }
            else if (booruClient is not null)
            {
                var booruPlatform = BooruPlatformExtensions.FromPlatformString(platform);
                return await booruClient.GetPostAsync(booruPlatform, id);
            }
        }
        catch (Exception e)
        {
            logger?.LogError(nameof(TryGetArtworkAsync), e);
        }

        return null;
    }
}
