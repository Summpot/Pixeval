// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Frozen;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Booru;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;
using Pixeval.Utilities;

namespace Pixeval.Models.Blocking;

public readonly record struct BlockedContentSnapshot(
    FrozenSet<string> BlockedTags,
    FrozenSet<long> BlockedUsers);

public static class BlockedContentHelper
{
    public static BlockedContentSnapshot CaptureSnapshot() =>
        CaptureSnapshot(
            App.Services!.GetRequiredService<AppSettings>(),
            App.Services!.GetRequiredService<StorageEngine>());

    public static BlockedContentSnapshot CaptureSnapshot(AppSettings settings, StorageEngine storageEngine)
    {
        var blockedTags = settings.BrowsingExperienceSettings.BlockedTags
            .ToFrozenSet(StringComparer.Ordinal);
        var blockedUsers = storageEngine.GetAllBlockedUsers()
            .Select(u => u.Id)
            .ToFrozenSet();
        return new(blockedTags, blockedUsers);
    }

    public static bool IsBlocked(object entry) => IsBlocked(entry, CaptureSnapshot());

    public static bool IsBlocked(object entry, BlockedContentSnapshot snapshot)
    {
        if (BlockedContentModelHelper.IsBlockedPlaceholder(entry))
            return true;

        return entry switch
        {
            Illustration illust => illust.Tags.Any(t => snapshot.BlockedTags.Contains(t.Name)) || IsBlocked(illust.User.Id, snapshot),
            Novel novel => novel.Tags.Any(t => snapshot.BlockedTags.Contains(t.Name)) || IsBlocked(novel.User.Id, snapshot),
            BooruPost booru => booru.Tags.Any(t => snapshot.BlockedTags.Contains(t.Name)),
            WorkEntry we => IsBlocked(we.AsWorkEntry, snapshot),
            CommentRecord comment => IsBlocked(comment, snapshot),
            _ => false
        };
    }

    public static bool IsBlocked(long userId, BlockedContentSnapshot snapshot) =>
        snapshot.BlockedUsers.Contains(userId);

    public static bool IsBlocked(User user) => IsBlocked(user, CaptureSnapshot());

    public static bool IsBlocked(User user, BlockedContentSnapshot snapshot) =>
        user.Id > 0 && snapshot.BlockedUsers.Contains(user.Id);

    public static bool IsBlocked(CommentRecord comment) => IsBlocked(comment, CaptureSnapshot());

    public static bool IsBlocked(CommentRecord comment, BlockedContentSnapshot snapshot) =>
        comment.User.Id > 0 && snapshot.BlockedUsers.Contains(comment.User.Id);

    public static bool IsBlockedPlaceholder(object entry) =>
        BlockedContentModelHelper.IsBlockedPlaceholder(entry);

    public static T Replace<T>(T entry) where T : class =>
        BlockedContentModelHelper.Replace(entry, CaptureSnapshot());

    public static T ReplaceEntry<T>(T entry, BlockedContentSnapshot snapshot) where T : class =>
        BlockedContentModelHelper.Replace(entry, snapshot);

    public static bool TryAddOrUpdateBlockedUser(User user)
    {
        try
        {
            var record = BlockedContentModelHelper.CreateBlockedUserRecord(user);
            App.Services!.GetRequiredService<StorageEngine>().AddOrUpdateBlockedUser(record.Id, record.UserName, record.AvatarUrl, record.Account);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
