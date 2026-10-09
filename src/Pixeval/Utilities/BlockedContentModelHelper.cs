// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Misaki;
using Pixeval.AppManagement;
using Pixeval.I18N;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;

namespace Pixeval.Utilities;

public static class BlockedContentModelHelper
{
    // Native entries do not expose extension state, so keep the placeholder marker outside the model.
    private static readonly ConditionalWeakTable<IArtworkInfo, object> _BlockedArtworkMarkers = new();

    private static readonly object _BlockedArtworkMarker = new();

    public static BlockedUserRecord CreateBlockedUserRecord(IUser user)
    {
        var id = user is IIdEntry idEntry && idEntry.Id != 0
            ? idEntry.Id
            : long.TryParse(user.Id, out var parsed) ? parsed : 0;
        var avatarUrl = (user as User)?.AvatarUrl
            ?? (user as TokenUser)?.AvatarUrl
            ?? user.Avatar?.FirstOrDefault()?.ImageUri.OriginalString
            ?? "";
        var account = (user as User)?.Account
            ?? (user as TokenUser)?.Account
            ?? "";
        return new BlockedUserRecord(0, id, user.Name ?? "", avatarUrl, account);
    }

    public static User CreateBlockedUserPreview(BlockedUserRecord entry) => new(
        entry.Id,
        entry.UserName,
        entry.Account ?? "",
        new ProfileImageUrls(null, null, null, string.IsNullOrWhiteSpace(entry.AvatarUrl) ? AppInfo.BlockedContentPath : entry.AvatarUrl),
        false,
        null);

    public static NovelContent CreateBlockedNovelContent(Novel entry) => NovelContent.CreateDefault() with
    {
        Id = entry.Id,
        Title = entry.Title,
        UserId = entry.User.Id,
        CoverUrl = AppInfo.BlockedContentPath,
        Text = I18NManager.GetResource(BlockedContentResources.Work)
    };

    internal static bool IsBlockedPlaceholder(IArtworkInfo entry) =>
        _BlockedArtworkMarkers.TryGetValue(entry, out _);

    internal static T Replace<T>(T entry, BlockedContentSnapshot snapshot) where T : IArtworkInfo
    {
        return BlockedContentHelper.IsBlocked(entry, snapshot)
            ? entry switch
            {
                Illustration illustration => (T) (object) ReplaceIllustration(illustration, snapshot),
                Novel novel => (T) (object) ReplaceNovel(novel, snapshot),
                _ => entry
            }
            : entry;

        static Illustration ReplaceIllustration(Illustration entry, BlockedContentSnapshot snapshot) =>
            MarkBlocked(entry with
            {
                Title = I18NManager.GetResource(BlockedContentResources.Work),
                ImageUrls = CreatePlaceholderImageUrls(),
                MetaSinglePage = new(AppInfo.BlockedContentPath),
                MetaPages = [],
                PageCount = 1,
                Width = 1,
                Height = 1
            });

        static Novel ReplaceNovel(Novel entry, BlockedContentSnapshot snapshot) =>
            MarkBlocked(entry with
            {
                Title = I18NManager.GetResource(BlockedContentResources.Work),
                ImageUrls = CreatePlaceholderImageUrls(),
                PageCount = 1
            });

        static ImageUrls CreatePlaceholderImageUrls() => new(
            AppInfo.BlockedContentPath,
            AppInfo.BlockedContentPath,
            AppInfo.BlockedContentPath,
            AppInfo.BlockedContentPath);
    }

    internal static User Replace(User entry, BlockedContentSnapshot snapshot) =>
        BlockedContentHelper.IsBlocked(entry.Id, snapshot)
            ? entry with
            {
                Name = I18NManager.GetResource(BlockedContentResources.User),
                ProfileImageUrls = new ProfileImageUrls(
                    AppInfo.BlockedContentPath,
                    AppInfo.BlockedContentPath,
                    AppInfo.BlockedContentPath,
                    AppInfo.BlockedContentPath)
            }
            : entry;

    internal static SingleUserResponse Replace(SingleUserResponse entry, BlockedContentSnapshot snapshot) =>
        BlockedContentHelper.IsBlocked(entry.User.Id, snapshot)
            ? entry with
            {
                User = Replace(entry.User, snapshot),
                Profile = entry.Profile with { BackgroundImageUrl = AppInfo.BlockedContentPath }
            }
            : entry;

    internal static Comment Replace(Comment entry, BlockedContentSnapshot snapshot) =>
        BlockedContentHelper.IsBlocked(entry, snapshot)
            ? entry with
            {
                Content = I18NManager.GetResource(BlockedContentResources.Comment),
                User = Replace(entry.User, snapshot)
            }
            : entry;

    private static T MarkBlocked<T>(T entry)
        where T : class, IArtworkInfo
    {
        if (!_BlockedArtworkMarkers.TryGetValue(entry, out _))
            _BlockedArtworkMarkers.Add(entry, _BlockedArtworkMarker);
        return entry;
    }
}
