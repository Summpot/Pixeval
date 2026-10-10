// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.Services;
using Pixeval.Views;

namespace Pixeval.Native.Mako;

public partial class MakoClient
{
    public static DateTimeOffset RankingMaxDateTime => DateTimeOffset.UtcNow.AddDays(-1);

    public async Task<List<BookmarkTag>> GetBookmarkTagsAsync(long uid, SimpleWorkType type, PrivacyPolicy policy, CancellationToken token = default)
    {
        var policyStr = policy is PrivacyPolicy.Private ? "private" : "public";
        var isNovel = type is SimpleWorkType.Novel;
        var tags = await WorkBookmarkTagsAsync(isNovel, uid, policyStr);
        tags.Insert(0, AllBookmarkTag.Instance);
        tags.Insert(1, UncategorizedBookmarkTag.Instance);
        return tags;
    }

    public async Task<bool> SetWorkBookmarkAsync(IWorkEntry entry, bool favorite, bool privately = false, IReadOnlyCollection<string>? tags = null, CancellationToken token = default)
    {
        var isNovel = entry is Novel || (entry is WorkEntry we && we is WorkEntry.NovelWork);
        var policyStr = privately ? "private" : "public";
        var tagList = tags is null ? null : new List<string>(tags);
        var result = await (favorite
            ? PostBookmarkAsync(isNovel, entry.RawId, policyStr, tagList)
            : RemoveBookmarkAsync(isNovel, entry.RawId));
        if (result.Success)
        {
            ArtworkUiStateStore.SetBookmarkState(entry, favorite);
        }
        return result.Success;
    }

    public async Task<bool> SetFollowAsync(long userId, bool isFollowed, bool privately = false, CancellationToken token = default)
    {
        var res = isFollowed
            ? await PostFollowUserAsync(userId, privately ? "private" : "public")
            : await RemoveFollowUserAsync(userId);
        return res.Success;
    }

    public async Task<bool> SetFollowAsync(User user, bool isFollowed, bool privately = false, CancellationToken token = default)
    {
        var result = await SetFollowAsync(user.Id, isFollowed, privately, token);
        if (result)
            UserUiStateStore.SetFollowState(user, isFollowed);
        return result;
    }

    public Task<List<TrendingTag>> GetWorkTrendingTagsAsync(SimpleWorkType type) =>
        WorkTrendingTagsAsync(type is SimpleWorkType.Novel);

    public IAsyncEnumerable<Illustration> IllustrationSearch(IllustrationSearchArguments args) =>
        IllustrationSearchAdvanced(new IllustrationSearchParams(
            args.SearchText,
            args.MatchOption switch
            {
                SearchIllustrationTagMatchOption.ExactMatchForTags => "exact_match_for_tags",
                SearchIllustrationTagMatchOption.TitleAndCaption => "title_and_caption",
                SearchIllustrationTagMatchOption.Keyword => "keyword",
                _ => "partial_match_for_tags"
            },
            args.SortOption switch
            {
                WorkSortOption.PublishDateAscending => "date_asc",
                WorkSortOption.PopularityDescending => "popular_desc",
                _ => "date_desc"
            },
            args.AiType ? 1 : 0,
            args.ContentType switch
            {
                SearchIllustrationContentType.Illustration => "illust",
                SearchIllustrationContentType.Manga => "manga",
                SearchIllustrationContentType.Ugoira => "ugoira",
                _ => null
            },
            args.RatioPattern switch
            {
                SearchIllustrationRatioPattern.Landscape => "horizontal",
                SearchIllustrationRatioPattern.Portrait => "vertical",
                SearchIllustrationRatioPattern.Square => "square",
                _ => null
            },
            args.MergePlainKeywordResults,
            args.IncludeTranslatedTagResults,
            args.IncludePotentialViolationWorks,
            args.StartDate?.ToString("yyyy-MM-dd"),
            args.EndDate?.ToString("yyyy-MM-dd"),
            args.WidthMin,
            args.WidthMax,
            args.HeightMin,
            args.HeightMax,
            string.IsNullOrWhiteSpace(args.Tool) ? null : args.Tool
        ));

    public IAsyncEnumerable<Novel> NovelSearch(NovelSearchArguments args) =>
        NovelSearchAdvanced(new NovelSearchParams(
            args.SearchText,
            args.MatchOption switch
            {
                SearchNovelTagMatchOption.ExactMatchForTags => "exact_match_for_tags",
                SearchNovelTagMatchOption.Text => "text",
                SearchNovelTagMatchOption.Keyword => "keyword",
                _ => "partial_match_for_tags"
            },
            args.SortOption switch
            {
                WorkSortOption.PublishDateAscending => "date_asc",
                WorkSortOption.PopularityDescending => "popular_desc",
                _ => "date_desc"
            },
            args.AiType ? 1 : 0,
            string.IsNullOrWhiteSpace(args.LangCode) ? null : args.LangCode,
            args.Option switch
            {
                SearchNovelContentLengthOption.TextLength => "text_length",
                SearchNovelContentLengthOption.WordCount => "word_count",
                SearchNovelContentLengthOption.ReadingTime => "reading_time",
                _ => null
            },
            args.ContentLengthMin,
            args.ContentLengthMax,
            args.IsOriginalOnly ? true : null,
            args.GenreId is not null and not 0 ? args.GenreId : null,
            args.IsReplaceableOnly ? true : null,
            args.MergePlainKeywordResults,
            args.IncludeTranslatedTagResults,
            args.IncludePotentialViolationWorks,
            args.StartDate?.ToString("yyyy-MM-dd"),
            args.EndDate?.ToString("yyyy-MM-dd")
        ));

    public async Task<(Series Detail, IWorkEntry? First, IFetchEngine<IWorkEntry> Engine)> GetWorkSeriesAsync(
        SimpleWorkType type,
        long seriesId,
        CancellationToken token = default)
    {
        var isNovel = type is SimpleWorkType.Novel;
        var seriesDetail = await GetWorkSeriesDetailAsync(isNovel, seriesId);
        if (isNovel)
        {
            var engine = NovelSeries(seriesId);
            var works = new List<Novel>();
            await foreach (var item in engine.WithCancellation(token))
            {
                works.Add(item);
                if (works.Count >= 1) break;
            }
            var first = works.Count > 0 ? works[0] : null!;
            return (seriesDetail.Detail, first, NovelSeries(seriesId).ToFetchEngine());
        }
        else
        {
            var engine = WorkSeries(seriesId);
            var works = new List<Illustration>();
            await foreach (var item in engine.WithCancellation(token))
            {
                works.Add(item);
                if (works.Count >= 1) break;
            }
            var first = works.Count > 0 ? works[0] : null!;
            return (seriesDetail.Detail, first, WorkSeries(seriesId).ToFetchEngine());
        }
    }

    public IFetchEngine<IWorkEntry> WorkBookmarks(SimpleWorkType type, long id, PrivacyPolicy privacy, string? tag = null)
    {
        var privacyStr = privacy is PrivacyPolicy.Private ? "private" : "public";
        return (type is SimpleWorkType.Novel
            ? (IAsyncEnumerable<IWorkEntry>) NovelBookmarks(id, privacyStr, tag)
            : WorkBookmarks(id, privacyStr, tag)).ToFetchEngine();
    }

    public IFetchEngine<IWorkEntry> WorkPosted(WorkType type, long id)
    {
        return (type is WorkType.Novel
            ? (IAsyncEnumerable<IWorkEntry>) NovelPosted(id)
            : WorkPosted(id, type is WorkType.Manga ? "manga" : "illust")).ToFetchEngine();
    }

    public IFetchEngine<IWorkEntry> WorkSeries(SimpleWorkType type, long seriesId)
    {
        return (type is SimpleWorkType.Novel
            ? (IAsyncEnumerable<IWorkEntry>) NovelSeries(seriesId)
            : WorkSeries(seriesId)).ToFetchEngine();
    }

    public IFetchEngine<object> WorkRecommended(WorkType type) =>
        (type is WorkType.Novel
            ? (IAsyncEnumerable<object>) NovelRecommended(true, true)
            : WorkRecommended(true, true)).ToFetchEngine();

    public IFetchEngine<object> WorkNew(WorkType type) =>
        (type is WorkType.Novel
            ? (IAsyncEnumerable<object>) NovelNew(null)
            : WorkNew(type is WorkType.Manga ? "manga" : "illust", null)).ToFetchEngine();

    public IFetchEngine<object> WorkRanking(SimpleWorkType type, RankOption rankOption, DateTimeOffset date)
    {
        var mode = rankOption switch
        {
            RankOption.Day => "day",
            RankOption.Week => "week",
            RankOption.Month => "month",
            RankOption.DayMale => "day_male",
            RankOption.DayFemale => "day_female",
            RankOption.DayManga => "day_manga",
            RankOption.WeekManga => "week_manga",
            RankOption.MonthManga => "month_manga",
            RankOption.WeekOriginal => "week_original",
            RankOption.WeekRookie => "week_rookie",
            RankOption.DayR18 => "day_r18",
            RankOption.DayMaleR18 => "day_male_r18",
            RankOption.DayFemaleR18 => "day_female_r18",
            RankOption.WeekR18 => "week_r18",
            RankOption.WeekR18G => "week_r18g",
            RankOption.DayAi => "day_ai",
            RankOption.DayR18Ai => "day_r18_ai",
            RankOption.WeekAi => "week_ai",
            RankOption.WeekAiR18 => "week_ai_r18",
            _ => "day"
        };
        var dateStr = date.ToString("yyyy-MM-dd");
        return (type is SimpleWorkType.Novel
            ? (IAsyncEnumerable<object>) NovelRanking(mode, dateStr)
            : WorkRanking(mode, dateStr)).ToFetchEngine();
    }

    public IFetchEngine<object> WorkFollowing(SimpleWorkType type, PrivacyPolicy privacy)
    {
        var restrict = privacy is PrivacyPolicy.Private ? "private" : "public";
        return (type is SimpleWorkType.Novel
            ? (IAsyncEnumerable<object>) NovelFollowing(restrict)
            : WorkFollowing(restrict)).ToFetchEngine();
    }

    public IFetchEngine<object> WorkMyPixiv(SimpleWorkType type) =>
        (type is SimpleWorkType.Novel
            ? (IAsyncEnumerable<object>) NovelMypixiv()
            : WorkMypixiv()).ToFetchEngine();

    public IFetchEngine<object> WorkRelated(long id, SimpleWorkType type) =>
        (type is SimpleWorkType.Novel
            ? (IAsyncEnumerable<object>) NovelRelated(id)
            : WorkRelated(id)).ToFetchEngine();

    public IFetchEngine<User> UserFollowing(long userId, PrivacyPolicy privacy)
    {
        var restrict = privacy is PrivacyPolicy.Private ? "private" : "public";
        return UserFollowing(userId, restrict).ToFetchEngine();
    }

    public IFetchEngine<User> UserFollower(long? userId = null)
    {
        var targetId = userId ?? (PixevalSettings.MyId > 0 ? PixevalSettings.MyId : throw new InvalidOperationException("User is not logged in"));
        return UserFollower(targetId).ToFetchEngine();
    }

    public IFetchEngine<User> UserMyPixiv(long userId) =>
        UserMypixiv(userId).ToFetchEngine();

    public IAsyncEnumerable<CommentRecord> WorkComments(SimpleWorkType type, long id) =>
        EnumerateCommentsAsync(WorkComments(type is SimpleWorkType.Novel, id));

    public IAsyncEnumerable<CommentRecord> WorkCommentReplies(SimpleWorkType type, long id) =>
        EnumerateCommentsAsync(WorkCommentReplies(type is SimpleWorkType.Novel, id));

    private static async IAsyncEnumerable<CommentRecord> EnumerateCommentsAsync(CommentFetchEngine engine)
    {
        while (await engine.NextAsync() is { } item)
            yield return item;
    }

    public Task<CommentRecord> AddWorkCommentAsync(SimpleWorkType type, long parentId, string content) =>
        AddWorkCommentUnifiedAsync(type is SimpleWorkType.Novel, parentId, content, null, null);

    public Task<CommentRecord> AddWorkCommentAsync(SimpleWorkType type, long parentId, int stampId) =>
        AddWorkCommentUnifiedAsync(type is SimpleWorkType.Novel, parentId, "", null, stampId);

    public Task<CommentRecord> AddWorkCommentAsync(SimpleWorkType type, long parentId, long parentCommentId, string content) =>
        AddWorkCommentUnifiedAsync(type is SimpleWorkType.Novel, parentId, content, parentCommentId, null);

    public Task<CommentRecord> AddWorkCommentAsync(SimpleWorkType type, long parentId, long parentCommentId, int stampId) =>
        AddWorkCommentUnifiedAsync(type is SimpleWorkType.Novel, parentId, "", parentCommentId, stampId);

    public async Task<bool> DeleteWorkCommentAsync(SimpleWorkType type, long commentId)
    {
        var res = await DeleteWorkCommentUnifiedAsync(type is SimpleWorkType.Novel, commentId);
        return res.Success;
    }

    public async Task<MangaSeriesContextResponse> GetMangaSeriesContextAsync(long id, CancellationToken token = default)
    {
        var res = await GetWorkSeriesContextAsync(id);
        return new MangaSeriesContextResponse
        {
            Detail = res.Series ?? new Series(id, "", null, null, null, 0, null, null),
            Context = new MangaSeriesContext
            {
                ContentOrder = res.Context.ContentOrder,
                Previous = res.Context.PrevIllust,
                Next = res.Context.NextIllust
            }
        };
    }

    public async Task<bool> PostWorkSeriesWatchlistAsync(SimpleWorkType type, long id, CancellationToken token = default)
    {
        var res = await SetSeriesWatchlistAsync(type is SimpleWorkType.Novel, id, true);
        return res.Success;
    }

    public async Task<bool> RemoveWorkSeriesWatchlistAsync(SimpleWorkType type, long id, CancellationToken token = default)
    {
        var res = await SetSeriesWatchlistAsync(type is SimpleWorkType.Novel, id, false);
        return res.Success;
    }

    public IAsyncEnumerable<Series> WorkSeriesWatchlist(SimpleWorkType type) =>
        WorkSeriesWatchlist(type is SimpleWorkType.Novel);

    public IFetchEngine<SpotlightArticle> Spotlight(string category = "all")
    {
        var engine = SpotlightArticles(category);
        return engine.ToFetchEngine(engine.Cancel);
    }

    public IFetchEngine<T> Computed<T>(IAsyncEnumerable<T> source) =>
        source.ToFetchEngine();

    public async Task<SingleUserResponse> GetUserFromIdAsync(long userId, CancellationToken token = default) =>
        await GetUserDetailAsync(userId);

    public async Task<Illustration> GetIllustrationFromIdAsync(long id, CancellationToken token = default) =>
        await GetIllustrationAsync(id);

    public async Task<Novel> GetNovelFromIdAsync(long id, CancellationToken token = default) =>
        await GetNovelAsync(id);

    public async Task<IReadOnlyList<Tag>> GetAutoCompletionForKeyword(
        string word,
        bool mergePlainKeywordResult = true,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(word))
            return [];
        return await SearchAutocompleteAsync(word);
    }

    public Task<BookmarkDetail> GetWorkBookmarkDetailAsync(
        SimpleWorkType type,
        long id,
        CancellationToken token = default) =>
        GetBookmarkDetailAsync(type is SimpleWorkType.Novel, id);

    public async IAsyncEnumerable<BookmarkTag> WorkBookmarkTags(
        SimpleWorkType type,
        long uid,
        PrivacyPolicy policy)
    {
        var tags = await GetBookmarkTagsAsync(uid, type, policy);
        foreach (var tag in tags)
            yield return tag;
    }

    public IFetchEngine<object> SearchBookmarkWorks(
        SimpleWorkType type,
        PrivacyPolicy policy = PrivacyPolicy.Public,
        string? bookmarkTag = null,
        string? workTag = null,
        string? bookmarkPeriod = null,
        string? order = null)
    {
        var restrict = policy is PrivacyPolicy.Private ? "private" : "public";
        return (type is SimpleWorkType.Novel
            ? (IAsyncEnumerable<object>) SearchBookmarkNovel(restrict, bookmarkTag, workTag, bookmarkPeriod, order)
            : SearchBookmarkIllust(restrict, bookmarkTag, workTag, bookmarkPeriod, order)).ToFetchEngine();
    }

    public IFetchEngine<object> WorkBrowsingHistory(SimpleWorkType type) =>
        (type is SimpleWorkType.Novel
            ? (IAsyncEnumerable<object>) BrowsingHistoryNovels()
            : BrowsingHistoryIllusts()).ToFetchEngine();
}
