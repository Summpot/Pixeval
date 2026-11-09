// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mako;
using Mako.Global.Enum;
using Mako.Model;
using Misaki;
using Pixeval.Collections;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.ViewModels;

namespace Pixeval.Utilities;

public static class MakoHelper
{
    public static async Task<List<BookmarkTag>> GetBookmarkTagsAsync(long uid, SimpleWorkType type, PrivacyPolicy policy, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var engine = App.AppViewModel.MakoClient.WorkBookmarkTags(type, uid, policy);
        // Mako enumerators use EngineHandle rather than the enumeration token. This
        // stops further MoveNext calls; an HTTP request already in flight may still finish.
        using var registration = token.Register(engine.EngineHandle.Cancel);
        try
        {
            var tags = await engine.ToListAsync(token);
            token.ThrowIfCancellationRequested();
            if (engine.EngineHandle.LastException is { } exception)
                throw exception;
            tags.Insert(0, AllBookmarkTag.Instance);
            tags.Insert(1, UncategorizedBookmarkTag.Instance);
            return tags;
        }
        finally
        {
            engine.EngineHandle.Complete();
        }
    }

    public static string GetThumbnailUrl(this IWorkEntry workEntry, ThumbnailUrlOption option = ThumbnailUrlOption.Medium)
    {
        return option switch
        {
            ThumbnailUrlOption.Large => workEntry.ThumbnailUrls.Large,
            ThumbnailUrlOption.Medium => workEntry.ThumbnailUrls.Medium,
            ThumbnailUrlOption.SquareMedium => workEntry.ThumbnailUrls.SquareMedium,
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

    public static async Task<bool> SetFollowAsync(User id, bool isFollowed, bool privately = false, CancellationToken token = default)
    {
        var result = await (isFollowed
            ? App.AppViewModel.MakoClient.PostFollowUserAsync(id.Id, privately ? PrivacyPolicy.Private : PrivacyPolicy.Public, token)
            : App.AppViewModel.MakoClient.RemoveFollowUserAsync(id.Id, token));
        if (result)
            id.UserInfo.IsFollowed = isFollowed;
        return id.UserInfo.IsFollowed;
    }

    public static async Task<bool> SetWorkBookmarkAsync(WorkBase entry, bool favorite, bool privately = false, IReadOnlyCollection<string>? tags = null, CancellationToken token = default)
    {
        var simpleWorkType = entry switch
        {
            Illustration => SimpleWorkType.Illustration,
            Novel => SimpleWorkType.Novel,
            _ => throw new ArgumentOutOfRangeException(nameof(entry))
        };
        var client = App.AppViewModel.MakoClient;
        var accountId = client.Me?.Id;
        var invalidated = 0;
        client.TokenRefreshed += OnTokenRefreshed;
        try
        {
            var succeeded = await (favorite
                ? client.PostWorkBookmarkAsync(simpleWorkType, entry.Id, privately ? PrivacyPolicy.Private : PrivacyPolicy.Public, tags, token)
                : client.RemoveWorkBookmarkAsync(simpleWorkType, entry.Id, token));
            if (!succeeded || token.IsCancellationRequested || Volatile.Read(ref invalidated) != 0
                || client.Me?.Id != accountId)
                return false;

            entry.IsFavorite = favorite;
            return true;
        }
        finally
        {
            client.TokenRefreshed -= OnTokenRefreshed;
        }

        void OnTokenRefreshed(MakoClient sender, TokenResponse? response)
        {
            // Account A -> B -> A is still a different request lifetime. OAuth
            // renewal for A alone must keep the operation valid.
            if (response?.User.Id != accountId)
                Interlocked.Exchange(ref invalidated, 1);
        }
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

    extension<T>(Task<IReadOnlyList<T>> task)
    {
        public async IAsyncEnumerable<T> ToAsyncEnumerable()
        {
            foreach (var item in await task)
                yield return item;
        }
    }
}

public class AddNewBookmarkTag : BookmarkTag
{
    public required EventHandler<AddNewBookmarkTag, string> TagAdded;
}

public class AllBookmarkTag : BookmarkTag
{
    public static AllBookmarkTag Instance { get; } = new()
    {
        Name = null!,
        Count = 0
    };

    private static readonly string _TagNameAll = I18NManager.GetResource(MiscResources.TagName.All);

    /// <inheritdoc />
    public override string ToString() => _TagNameAll;
}

public class UncategorizedBookmarkTag : BookmarkTag
{
    public static UncategorizedBookmarkTag Instance { get; } = new()
    {
        Name = "未分類",
        Count = 0
    };

    private static readonly string _TagNameUncategorized = I18NManager.GetResource(MiscResources.TagName.Uncategorized);

    /// <inheritdoc />
    public override string ToString() => _TagNameUncategorized;
}

public class BookmarkDetailBookmarkTag : BookmarkTag
{
    public bool IsRegistered { get; private init; }

    public static BookmarkDetailBookmarkTag Create(BookmarkDetailTag tag) => new()
    {
        Name = tag.Name,
        Count = 0,
        IsRegistered = tag.IsRegistered
    };

    /// <inheritdoc />
    public override string ToString() => Name;
}
