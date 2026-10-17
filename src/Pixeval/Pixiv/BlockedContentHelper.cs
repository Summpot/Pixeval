// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Frozen;
using System.Linq;
using Pixeval.Config;
using Pixeval.Pixiv;
using Pixeval.Booru;
using Pixeval.Mako;
using Pixeval.Storage;
using Pixeval.Utilities;

namespace Pixeval.Pixiv;

public readonly record struct BlockedContentSnapshot(
    FrozenSet<string> BlockedTags,
    FrozenSet<long> BlockedUsers);

public interface IBlockedContentService
{
    BlockedContentSnapshot CaptureSnapshot();
    bool IsBlocked(object entry);
    bool IsBlocked(User user);
    T Replace<T>(T entry) where T : notnull;
    bool TryAddOrUpdateBlockedUser(User user);
}

public sealed class BlockedContentService(AppSettings settings, StorageEngine storageEngine) : IBlockedContentService
{
    public BlockedContentSnapshot CaptureSnapshot() =>
        BlockedContentHelper.CaptureSnapshot(settings, storageEngine);

    public bool IsBlocked(object entry) =>
        BlockedContentHelper.IsBlocked(entry, CaptureSnapshot());

    public bool IsBlocked(User user) =>
        BlockedContentHelper.IsBlocked(user, CaptureSnapshot());

    public T Replace<T>(T entry) where T : notnull =>
        BlockedContentHelper.ReplaceEntry(entry, CaptureSnapshot());

    public bool TryAddOrUpdateBlockedUser(User user) =>
        BlockedContentHelper.TryAddOrUpdateBlockedUser(user, storageEngine);
}

public static class BlockedContentHelper
{
    public static BlockedContentSnapshot CaptureSnapshot(AppSettings settings, StorageEngine storageEngine)
    {
        var blockedTags = settings.BrowsingExperienceSettings.BlockedTags
            .ToFrozenSet(StringComparer.Ordinal);
        var blockedUsers = storageEngine.GetAllBlockedUsers()
            .Select(u => u.Id)
            .ToFrozenSet();
        return new(blockedTags, blockedUsers);
    }

    public static bool IsBlocked(object entry, BlockedContentSnapshot snapshot)
    {
        if (BlockedContentModelHelper.IsBlockedPlaceholder(entry))
            return true;

        return entry switch
        {
            Illustration illust => illust.Tags.Any(t => snapshot.BlockedTags.Contains(t.Name)) || IsBlocked(illust.User.Id, snapshot),
            Mako.Novel novel => novel.Tags.Any(t => snapshot.BlockedTags.Contains(t.Name)) || IsBlocked(novel.User.Id, snapshot),
            BooruPost booru => booru.Tags.Any(t => snapshot.BlockedTags.Contains(t.Name)),
            WorkEntry we => IsBlocked(we.AsWorkEntry, snapshot),
            CommentRecord comment => IsBlocked(comment, snapshot),
            _ => false
        };
    }

    public static bool IsBlocked(long userId, BlockedContentSnapshot snapshot) =>
        snapshot.BlockedUsers.Contains(userId);

    public static bool IsBlocked(User user, BlockedContentSnapshot snapshot) =>
        user.Id > 0 && snapshot.BlockedUsers.Contains(user.Id);

    public static bool IsBlocked(CommentRecord comment, BlockedContentSnapshot snapshot) =>
        comment.User.Id > 0 && snapshot.BlockedUsers.Contains(comment.User.Id);

    public static bool IsBlockedPlaceholder(object entry) =>
        BlockedContentModelHelper.IsBlockedPlaceholder(entry);

    public static T ReplaceEntry<T>(T entry, BlockedContentSnapshot snapshot) where T : notnull =>
        BlockedContentModelHelper.Replace(entry, snapshot);

    public static bool TryAddOrUpdateBlockedUser(User user, StorageEngine storageEngine)
    {
        try
        {
            var record = BlockedContentModelHelper.CreateBlockedUserRecord(user);
            storageEngine.AddOrUpdateBlockedUser(record.Id, record.UserName, record.AvatarUrl, record.Account);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
