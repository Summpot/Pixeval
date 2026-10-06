// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Misaki;
using Pixeval.Collections;
using Pixeval.I18N;
using Pixeval.Models.Pixiv;
using Pixeval.Models.Options;
using Pixeval.Native.Mako;
using Pixeval.ViewModels;
using Pixeval.Views;

namespace Pixeval.Utilities;

public static class MakoHelper
{
    public const string AppApiHost = "app-api.pixiv.net";
    public const string OAuthHost = "oauth.secure.pixiv.net";
    public const string WebApiHost = "www.pixiv.net";
    public const string AccountHost = "accounts.pixiv.net";
    public const string ImageHost = "i.pximg.net";
    public const string ImageHost2 = "s.pximg.net";

    public static DateTimeOffset RankingMaxDateTime => DateTimeOffset.UtcNow.AddDays(-1);

    public static MakoConfigurationDto CreateMakoConfiguration(
        AppManagement.Settings.PixivDomainFrontingSettings domainFronting,
        int cooldownMs = 700,
        ulong splitDelayMs = 100,
        string? proxyUrl = null,
        string? targetFilter = "for_android",
        string? mirrorHost = null,
        string? webCookie = null)
    {
        var mappings = new Dictionary<string, List<string>>
        {
            [AppApiHost] = [.. domainFronting.PixivAppApiNameResolver],
            [OAuthHost] = [.. domainFronting.PixivOAuthNameResolver],
            [WebApiHost] = [.. domainFronting.PixivWebApiNameResolver],
            [AccountHost] = [.. domainFronting.PixivAccountNameResolver],
            [ImageHost] = [.. domainFronting.PixivImageNameResolver],
            [ImageHost2] = [.. domainFronting.PixivImageNameResolver2]
        };

        return new MakoConfigurationDto(
            domainFronting.EnablePixivDomainFronting,
            (ulong) Math.Max(0, cooldownMs),
            splitDelayMs,
            mappings,
            proxyUrl,
            targetFilter,
            mirrorHost,
            webCookie);
    }

    public static async Task<List<BookmarkTag>> GetBookmarkTagsAsync(long uid, SimpleWorkType type, PrivacyPolicy policy, CancellationToken token = default)
    {
        var policyStr = policy is PrivacyPolicy.Private ? "private" : "public";
        var isNovel = type is SimpleWorkType.Novel;
        var tags = await App.AppViewModel.MakoClient.WorkBookmarkTagsAsync(isNovel, uid, policyStr);
        tags.Insert(0, AllBookmarkTag.Instance);
        tags.Insert(1, UncategorizedBookmarkTag.Instance);
        return tags;
    }

    public static string GetThumbnailUrl(this IArtworkInfo workEntry, ThumbnailUrlOption option = ThumbnailUrlOption.Medium)
    {
        return option switch
        {
            ThumbnailUrlOption.Large => workEntry.Thumbnails.PickClosestHeight(600)?.ImageUri.OriginalString ?? "",
            ThumbnailUrlOption.Medium => workEntry.Thumbnails.PickClosestHeight(300)?.ImageUri.OriginalString ?? "",
            ThumbnailUrlOption.SquareMedium => workEntry.Thumbnails.PickClosestHeight(180)?.ImageUri.OriginalString ?? "",
            _ => throw new ArgumentOutOfRangeException(nameof(option))
        };
    }

    public static IEnumerable<ISortDescription<IWorkViewModel>> GetSortDescription(LocalSortOption sortOption)
    {
        if (sortOption is LocalSortOption.DoNotSort)
            yield break;
        yield return sortOption switch
        {
            LocalSortOption.PopularityDescending => ISortDescription<IWorkViewModel>.Create(t => t.Entry.TotalFavorite, true),
            LocalSortOption.PublishDateDescending => ISortDescription<IWorkViewModel>.Create(t => t.Entry.CreateDate, true),
            LocalSortOption.PublishDateAscending => ISortDescription<IWorkViewModel>.Create(t => t.Entry.CreateDate),
            LocalSortOption.DoNotSort or _ => throw new ArgumentOutOfRangeException(nameof(sortOption))
        };
        yield return ISortDescription<IWorkViewModel>.Create(t => t.Entry.Id);
    }

    public static async Task<bool> SetWorkBookmarkAsync(IWorkEntry entry, bool favorite, bool privately = false, IReadOnlyCollection<string>? tags = null, CancellationToken token = default)
    {
        var isNovel = entry is Novel or INovelEntry || (entry is WorkEntry we && we is WorkEntry.NovelWork);
        var policyStr = privately ? "private" : "public";
        var tagList = tags is null ? null : new List<string>(tags);
        var result = await (favorite
            ? App.AppViewModel.MakoClient.PostBookmarkAsync(isNovel, entry.RawId, policyStr, tagList)
            : App.AppViewModel.MakoClient.RemoveBookmarkAsync(isNovel, entry.RawId));
        if (result.Success)
        {
            switch (entry)
            {
                case Illustration i:
                    i.IsFavorite = favorite;
                    break;
                case Novel n:
                    n.IsFavorite = favorite;
                    break;
                case WorkEntry w:
                    w.IsFavorite = favorite;
                    break;
            }
        }
        return entry switch
        {
            Illustration i => i.IsFavorite,
            Novel n => n.IsFavorite,
            WorkEntry w => w.IsFavorite,
            _ => favorite
        };
    }

    public static string? ToMakoProxy(ProxyType type, string? proxy) =>
        type switch
        {
            ProxyType.System => null,
            ProxyType.Custom => NormalizeProxyUri(proxy) ?? "",
            ProxyType.None => "",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

    public static string? NormalizeProxyUri(string? proxy)
    {
        if (string.IsNullOrWhiteSpace(proxy))
            return null;

        var uri = proxy.Trim();
        if (!uri.Contains("://", StringComparison.Ordinal))
            uri = "http://" + uri;

        return Uri.IsWellFormedUriString(uri, UriKind.Absolute) ? uri : null;
    }

    public static string? GetEffectiveProxyUrl() =>
        GetEffectiveProxyUrl(App.AppViewModel?.AppSettings?.NetworkSettings);

    public static string? GetEffectiveProxyUrl(AppManagement.Settings.NetworkSettingsGroup? networkSettings)
    {
        if (networkSettings is null)
            return null;

        switch (networkSettings.ProxySettings.ProxyType)
        {
            case Models.Options.ProxyType.Custom:
                return NormalizeProxyUri(networkSettings.ProxySettings.Proxy);
            case Models.Options.ProxyType.System:
                try
                {
                    var probeUri = new Uri("https://app-api.pixiv.net");
                    var systemProxy = SystemProxyProvider.GetCurrent();
                    if (!systemProxy.IsBypassed(probeUri))
                    {
                        var proxy = systemProxy.GetProxy(probeUri);
                        if (proxy is not null && proxy != probeUri)
                        {
                            return proxy.AbsoluteUri;
                        }
                    }
                }
                catch
                {
                    try
                    {
                        var probeUri = new Uri("https://app-api.pixiv.net");
                        var defaultProxy = System.Net.Http.HttpClient.DefaultProxy;
                        if (!defaultProxy.IsBypassed(probeUri))
                        {
                            var proxy = defaultProxy.GetProxy(probeUri);
                            if (proxy is not null && proxy != probeUri)
                            {
                                return proxy.AbsoluteUri;
                            }
                        }
                    }
                    catch
                    {
                        // ignore proxy resolution failures
                    }
                }
                return null;
            case Models.Options.ProxyType.None:
            default:
                return null;
        }
    }

    extension<T>(IPreloadableList<T> list)
    {
        public async ValueTask TryPreloadListAsync(IPlatformInfo platform, CancellationToken token = default)
        {
            if (!list.IsPreloaded)
                await list.PreloadListAsync(App.AppViewModel.GetRequiredPlatformService<IGetArtworkService>(platform.Platform), token);
        }

        public async ValueTask TryPreloadListAsync(string platform, CancellationToken token = default)
        {
            if (!list.IsPreloaded)
                await list.PreloadListAsync(App.AppViewModel.GetRequiredPlatformService<IGetArtworkService>(platform), token);
        }
    }

    public static Task<List<TrendingTag>> GetWorkTrendingTagsAsync(this MakoClient client, SimpleWorkType type) =>
        client.WorkTrendingTagsAsync(type is SimpleWorkType.Novel);

    public static Task<SearchOptions> GetSearchOptionsAsync(this MakoClient client, CancellationToken token = default)
    {
        return Task.FromResult(new SearchOptions
        {
            IllustrationOptions = new IllustrationSearchOptions
            {
                Tools = new SearchOptionsStructure<string>(["Photoshop", "SAI", "CLIP STUDIO PAINT"])
            },
            NovelOptions = new NovelSearchOptions
            {
                Genres = new SearchOptionsStructure<SearchOptionsGenre>([]),
                Languages = new SearchOptionsStructure<SearchOptionsLanguage>([])
            }
        });
    }

    public static IAsyncEnumerable<Illustration> IllustrationSearch(this MakoClient client, IllustrationSearchArguments args) =>
        client.IllustrationSearch(args.SearchText, args.MatchOption switch
        {
            SearchIllustrationTagMatchOption.ExactMatchForTags => "exact_match_for_tags",
            SearchIllustrationTagMatchOption.TitleAndCaption => "title_and_caption",
            SearchIllustrationTagMatchOption.Keyword => "keyword",
            _ => "partial_match_for_tags"
        }, args.SortOption switch
        {
            WorkSortOption.PublishDateAscending => "date_asc",
            WorkSortOption.PopularityDescending => "popular_desc",
            _ => "date_desc"
        });

    public static IAsyncEnumerable<Novel> NovelSearch(this MakoClient client, NovelSearchArguments args) =>
        client.NovelSearch(args.SearchText, args.MatchOption switch
        {
            SearchNovelTagMatchOption.ExactMatchForTags => "exact_match_for_tags",
            SearchNovelTagMatchOption.Text => "text",
            SearchNovelTagMatchOption.Keyword => "keyword",
            _ => "partial_match_for_tags"
        }, args.SortOption switch
        {
            WorkSortOption.PublishDateAscending => "date_asc",
            WorkSortOption.PopularityDescending => "popular_desc",
            _ => "date_desc"
        });

    public static async Task<(Series Detail, IWorkEntry First, IFetchEngine<IWorkEntry> Engine)> GetWorkSeriesAsync(
        this MakoClient client,
        SimpleWorkType type,
        long seriesId,
        CancellationToken token = default)
    {
        if (type is SimpleWorkType.Novel)
        {
            var engine = client.NovelSeries(seriesId);
            var works = new List<Novel>();
            await foreach (var item in engine.WithCancellation(token))
            {
                works.Add(item);
                if (works.Count >= 1) break;
            }
            var first = works.Count > 0 ? works[0] : null!;
            var detail = new Series(
                seriesId,
                first?.Title ?? "",
                first?.User,
                null,
                null,
                works.Count,
                first?.Id,
                null);
            return (detail, first, client.NovelSeries(seriesId).ToFetchEngine());
        }
        else
        {
            var engine = client.WorkSeries(seriesId);
            var works = new List<Illustration>();
            await foreach (var item in engine.WithCancellation(token))
            {
                works.Add(item);
                if (works.Count >= 1) break;
            }
            var first = works.Count > 0 ? works[0] : null!;
            var detail = new Series(
                seriesId,
                first?.Title ?? "",
                first?.User,
                null,
                first?.Thumbnails.FirstOrDefault()?.ImageUri.OriginalString,
                works.Count,
                first?.Id,
                null);
            return (detail, first, client.WorkSeries(seriesId).ToFetchEngine());
        }
    }

    public static IFetchEngine<IWorkEntry> WorkBookmarks(this MakoClient client, SimpleWorkType type, long id, PrivacyPolicy privacy, string? tag = null)
    {
        var privacyStr = privacy is PrivacyPolicy.Private ? "private" : "public";
        return (type is SimpleWorkType.Novel
            ? (IAsyncEnumerable<IWorkEntry>) client.NovelBookmarks(id, privacyStr, tag)
            : client.WorkBookmarks(id, privacyStr, tag)).ToFetchEngine();
    }

    public static IFetchEngine<IWorkEntry> WorkPosted(this MakoClient client, WorkType type, long id)
    {
        return (type is WorkType.Novel
            ? (IAsyncEnumerable<IWorkEntry>) client.NovelPosted(id)
            : client.WorkPosted(id, type is WorkType.Manga ? "manga" : "illust")).ToFetchEngine();
    }

    public static IFetchEngine<IWorkEntry> WorkSeries(this MakoClient client, SimpleWorkType type, long seriesId)
    {
        return (type is SimpleWorkType.Novel
            ? (IAsyncEnumerable<IWorkEntry>) client.NovelSeries(seriesId)
            : client.WorkSeries(seriesId)).ToFetchEngine();
    }

    public static NovelFetchEngine NovelMyPixiv(this MakoClient client) => client.NovelMypixiv();
    public static IllustrationFetchEngine WorkMyPixiv(this MakoClient client) => client.WorkMypixiv();

    public static IFetchEngine<IArtworkInfo> WorkRecommended(this MakoClient client, WorkType type) =>
        (type is WorkType.Novel
            ? (IAsyncEnumerable<IArtworkInfo>) client.NovelRecommended(true, true)
            : client.WorkRecommended(true, true)).ToFetchEngine();

    public static IFetchEngine<IArtworkInfo> WorkNew(this MakoClient client, WorkType type) =>
        (type is WorkType.Novel
            ? (IAsyncEnumerable<IArtworkInfo>) client.NovelNew(null)
            : client.WorkNew(type is WorkType.Manga ? "manga" : "illust", null)).ToFetchEngine();

    public static IFetchEngine<IArtworkInfo> WorkRanking(this MakoClient client, SimpleWorkType type, RankOption rankOption, DateTimeOffset date)
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
            ? (IAsyncEnumerable<IArtworkInfo>) client.NovelRanking(mode, dateStr)
            : client.WorkRanking(mode, dateStr)).ToFetchEngine();
    }

    public static IFetchEngine<IArtworkInfo> WorkFollowing(this MakoClient client, SimpleWorkType type, PrivacyPolicy privacy)
    {
        var restrict = privacy is PrivacyPolicy.Private ? "private" : "public";
        return (type is SimpleWorkType.Novel
            ? (IAsyncEnumerable<IArtworkInfo>) client.NovelFollowing(restrict)
            : client.WorkFollowing(restrict)).ToFetchEngine();
    }

    public static IFetchEngine<IArtworkInfo> WorkMyPixiv(this MakoClient client, SimpleWorkType type) =>
        (type is SimpleWorkType.Novel
            ? (IAsyncEnumerable<IArtworkInfo>) client.NovelMypixiv()
            : client.WorkMypixiv()).ToFetchEngine();

    public static IFetchEngine<IArtworkInfo> WorkRelated(this MakoClient client, long id, SimpleWorkType type) =>
        type is SimpleWorkType.Novel
            ? AsyncEnumerable.Empty<IArtworkInfo>().ToFetchEngine()
            : client.WorkRelated(id).ToFetchEngine();

    public static IFetchEngine<User> UserFollowing(this MakoClient client, long userId, PrivacyPolicy privacy)
    {
        var restrict = privacy is PrivacyPolicy.Private ? "private" : "public";
        return client.UserFollowing(userId, restrict).ToFetchEngine();
    }

    public static IFetchEngine<User> UserFollower(this MakoClient client, long? userId = null)
    {
        var targetId = userId ?? (PixevalSettings.MyId > 0 ? PixevalSettings.MyId : throw new InvalidOperationException("User is not logged in"));
        return client.UserFollower(targetId).ToFetchEngine();
    }

    public static IFetchEngine<User> UserMyPixiv(this MakoClient client, long userId) =>
        client.UserMypixiv(userId).ToFetchEngine();

    public static IFetchEngine<User> UserRecommended(this MakoClient client) =>
        client.UserRecommended().ToFetchEngine();

    public static IFetchEngine<User> UserSearch(this MakoClient client, string word) =>
        client.UserSearch(word).ToFetchEngine();

    public static async Task<bool> SetFollowAsync(long userId, bool isFollowed, bool privately = false, CancellationToken token = default)
    {
        var res = isFollowed
            ? await App.AppViewModel.MakoClient.PostFollowUserAsync(userId, privately ? "private" : "public")
            : await App.AppViewModel.MakoClient.RemoveFollowUserAsync(userId);
        return res.Success;
    }

    public static Task<bool> SetFollowAsync(User user, bool isFollowed, bool privately = false, CancellationToken token = default) =>
        SetFollowAsync(user.Id, isFollowed, privately, token);

    public static IAsyncEnumerable<Comment> WorkComments(this MakoClient client, SimpleWorkType type, long id) =>
        client.FetchWorkCommentsAsync(type is SimpleWorkType.Novel, id);

    private static async IAsyncEnumerable<Comment> FetchWorkCommentsAsync(this MakoClient client, bool isNovel, long id)
    {
        long? offset = null;
        while (true)
        {
            var res = await client.GetWorkCommentsAsync(isNovel, id, offset);
            foreach (var item in res.Comments)
            {
                yield return new Comment
                {
                    Id = item.Id,
                    Content = item.Comment,
                    Date = DateTimeOffset.TryParse(item.Date, out var dt) ? dt : DateTimeOffset.UtcNow,
                    User = item.User,
                    HasReplies = item.HasReplies,
                    Stamp = item.Stamp is { } st ? new Stamp { StampId = st.StampId, StampUrl = st.StampUrl } : null
                };
            }
            if (string.IsNullOrEmpty(res.NextUrl))
                break;
            var match = System.Text.RegularExpressions.Regex.Match(res.NextUrl, @"[?&]offset=(\d+)");
            if (match.Success && long.TryParse(match.Groups[1].Value, out var nextOffset))
                offset = nextOffset;
            else
                break;
        }
    }

    public static IAsyncEnumerable<Comment> WorkCommentReplies(this MakoClient client, SimpleWorkType type, long id) =>
        client.FetchWorkCommentRepliesAsync(id);

    private static async IAsyncEnumerable<Comment> FetchWorkCommentRepliesAsync(this MakoClient client, long commentId)
    {
        long? offset = null;
        while (true)
        {
            var res = await client.GetWorkCommentRepliesAsync(commentId, offset);
            foreach (var item in res.Comments)
            {
                yield return new Comment
                {
                    Id = item.Id,
                    Content = item.Comment,
                    Date = DateTimeOffset.TryParse(item.Date, out var dt) ? dt : DateTimeOffset.UtcNow,
                    User = item.User,
                    HasReplies = item.HasReplies,
                    Stamp = item.Stamp is { } st ? new Stamp { StampId = st.StampId, StampUrl = st.StampUrl } : null
                };
            }
            if (string.IsNullOrEmpty(res.NextUrl))
                break;
            var match = System.Text.RegularExpressions.Regex.Match(res.NextUrl, @"[?&]offset=(\d+)");
            if (match.Success && long.TryParse(match.Groups[1].Value, out var nextOffset))
                offset = nextOffset;
            else
                break;
        }
    }

    public static async Task<Comment> AddWorkCommentAsync(this MakoClient client, SimpleWorkType type, long parentId, string content)
    {
        var res = await client.AddWorkCommentAsync(type is SimpleWorkType.Novel, parentId, content, null, null);
        if (res is { } item)
        {
            return new Comment
            {
                Id = item.Id,
                Content = item.Comment,
                Date = DateTimeOffset.TryParse(item.Date, out var dt) ? dt : DateTimeOffset.UtcNow,
                User = item.User,
                HasReplies = item.HasReplies,
                Stamp = item.Stamp is { } st ? new Stamp { StampId = st.StampId, StampUrl = st.StampUrl } : null
            };
        }
        return new Comment
        {
            Id = 0,
            Content = content,
            Date = DateTimeOffset.UtcNow,
            User = PixevalSettings.Me is { } me ? new User(long.Parse(me.Id), me.Name, me.Account, new ProfileImageUrls(null, null, null, null), false, null) : null!,
            HasReplies = false,
            Stamp = null
        };
    }

    public static async Task<Comment> AddWorkCommentAsync(this MakoClient client, SimpleWorkType type, long parentId, int stampId)
    {
        var res = await client.AddWorkCommentAsync(type is SimpleWorkType.Novel, parentId, "", null, stampId);
        if (res is { } item)
        {
            return new Comment
            {
                Id = item.Id,
                Content = item.Comment,
                Date = DateTimeOffset.TryParse(item.Date, out var dt) ? dt : DateTimeOffset.UtcNow,
                User = item.User,
                HasReplies = item.HasReplies,
                Stamp = item.Stamp is { } st ? new Stamp { StampId = st.StampId, StampUrl = st.StampUrl } : null
            };
        }
        return new Comment
        {
            Id = 0,
            Content = "",
            Date = DateTimeOffset.UtcNow,
            User = PixevalSettings.Me is { } me ? new User(long.Parse(me.Id), me.Name, me.Account, new ProfileImageUrls(null, null, null, null), false, null) : null!,
            HasReplies = false,
            Stamp = new Stamp { StampId = stampId, StampUrl = "" }
        };
    }

    public static async Task<Comment> AddWorkCommentAsync(this MakoClient client, SimpleWorkType type, long parentId, long parentCommentId, string content)
    {
        var res = await client.AddWorkCommentAsync(type is SimpleWorkType.Novel, parentId, content, parentCommentId, null);
        if (res is { } item)
        {
            return new Comment
            {
                Id = item.Id,
                Content = item.Comment,
                Date = DateTimeOffset.TryParse(item.Date, out var dt) ? dt : DateTimeOffset.UtcNow,
                User = item.User,
                HasReplies = item.HasReplies,
                Stamp = item.Stamp is { } st ? new Stamp { StampId = st.StampId, StampUrl = st.StampUrl } : null
            };
        }
        return new Comment
        {
            Id = 0,
            Content = content,
            Date = DateTimeOffset.UtcNow,
            User = PixevalSettings.Me is { } me ? new User(long.Parse(me.Id), me.Name, me.Account, new ProfileImageUrls(null, null, null, null), false, null) : null!,
            HasReplies = false,
            Stamp = null
        };
    }

    public static async Task<Comment> AddWorkCommentAsync(this MakoClient client, SimpleWorkType type, long parentId, long parentCommentId, int stampId)
    {
        var res = await client.AddWorkCommentAsync(type is SimpleWorkType.Novel, parentId, "", parentCommentId, stampId);
        if (res is { } item)
        {
            return new Comment
            {
                Id = item.Id,
                Content = item.Comment,
                Date = DateTimeOffset.TryParse(item.Date, out var dt) ? dt : DateTimeOffset.UtcNow,
                User = item.User,
                HasReplies = item.HasReplies,
                Stamp = item.Stamp is { } st ? new Stamp { StampId = st.StampId, StampUrl = st.StampUrl } : null
            };
        }
        return new Comment
        {
            Id = 0,
            Content = "",
            Date = DateTimeOffset.UtcNow,
            User = PixevalSettings.Me is { } me ? new User(long.Parse(me.Id), me.Name, me.Account, new ProfileImageUrls(null, null, null, null), false, null) : null!,
            HasReplies = false,
            Stamp = new Stamp { StampId = stampId, StampUrl = "" }
        };
    }

    public static async Task<bool> DeleteWorkCommentAsync(this MakoClient client, SimpleWorkType type, long commentId)
    {
        var res = await client.DeleteWorkCommentAsync(type is SimpleWorkType.Novel, commentId);
        return res.Success;
    }

    public static async Task<MangaSeriesContextResponse> GetMangaSeriesContextAsync(this MakoClient client, long id, CancellationToken token = default)
    {
        var res = await client.GetWorkSeriesContextAsync(id);
        return new MangaSeriesContextResponse
        {
            Detail = res.Series ?? new Series(id, "", null, null, null, 0, null, null),
            Context = new MangaSeriesContext
            {
                ContentOrder = res.Context.ContentOrder,
                Previous = res.Context.PrevIllust!,
                Next = res.Context.NextIllust!
            }
        };
    }

    public static async Task<bool> PostWorkSeriesWatchlistAsync(this MakoClient client, SimpleWorkType type, long id, CancellationToken token = default)
    {
        var res = await client.AddSeriesWatchlistAsync(id);
        return res.Success;
    }

    public static async Task<bool> RemoveWorkSeriesWatchlistAsync(this MakoClient client, SimpleWorkType type, long id, CancellationToken token = default)
    {
        var res = await client.DeleteSeriesWatchlistAsync(id);
        return res.Success;
    }

    public static IAsyncEnumerable<Series> WorkSeriesWatchlist(this MakoClient client, SimpleWorkType type) =>
        client.WorkSeriesWatchlist(type is SimpleWorkType.Novel);

    public static IFetchEngine<SpotlightArticle> Spotlight(this MakoClient client, string category = "all")
    {
        var engine = client.SpotlightArticles(category);
        return engine.ToFetchEngine(engine.Cancel);
    }

    public static IFetchEngine<T> Computed<T>(this MakoClient client, IAsyncEnumerable<T> source) =>
        source.ToFetchEngine();

    public static async Task<SingleUserResponse> GetUserFromIdAsync(this MakoClient client, long userId, CancellationToken token = default) =>
        await client.GetUserDetailAsync(userId);

    public static async Task<Illustration> GetIllustrationFromIdAsync(this MakoClient client, long id, CancellationToken token = default) =>
        await client.GetIllustrationAsync(id);

    public static async Task<Novel> GetNovelFromIdAsync(this MakoClient client, long id, CancellationToken token = default) =>
        await client.GetNovelAsync(id);

    public static async Task<IReadOnlyList<Tag>> GetAutoCompletionForKeyword(
        this MakoClient client,
        string word,
        bool mergePlainKeywordResult = true,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(word))
            return [];
        return await client.SearchAutocompleteAsync(word);
    }

    public static Task<BookmarkDetail> GetWorkBookmarkDetailAsync(
        this MakoClient client,
        SimpleWorkType type,
        long id,
        CancellationToken token = default) =>
        client.GetBookmarkDetailAsync(type is SimpleWorkType.Novel, id);

    public static async IAsyncEnumerable<BookmarkTag> WorkBookmarkTags(
        this MakoClient client,
        SimpleWorkType type,
        long uid,
        PrivacyPolicy policy)
    {
        var tags = await GetBookmarkTagsAsync(uid, type, policy);
        foreach (var tag in tags)
            yield return tag;
    }
}

public record AddNewBookmarkTag() : BookmarkTag("", 0, false)
{
    public EventHandler<AddNewBookmarkTag, string>? TagAdded;
}

public record AllBookmarkTag() : BookmarkTag(null!, 0, false)
{
    public static AllBookmarkTag Instance { get; } = new();

    private static readonly string _TagNameAll = I18NManager.GetResource(MiscResources.TagName.All);

    /// <inheritdoc />
    public override string ToString() => _TagNameAll;
}

public record UncategorizedBookmarkTag() : BookmarkTag("未分類", 0, false)
{
    public static UncategorizedBookmarkTag Instance { get; } = new();

    private static readonly string _TagNameUncategorized = I18NManager.GetResource(MiscResources.TagName.Uncategorized);

    /// <inheritdoc />
    public override string ToString() => _TagNameUncategorized;
}

public record BookmarkDetailBookmarkTag(string TagName, long TagCount, bool IsRegistered) : BookmarkTag(TagName, TagCount, IsRegistered)
{
    public static BookmarkDetailBookmarkTag Create(BookmarkTag tag) => new(tag.Name, tag.Count, tag.IsRegistered);

    /// <inheritdoc />
    public override string ToString() => Name;
}
