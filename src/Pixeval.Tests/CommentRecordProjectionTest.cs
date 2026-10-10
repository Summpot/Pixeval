// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Frozen;
using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Native.Storage;
using Pixeval.Services;
using Pixeval.Utilities.Network;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Tests;

[TestClass]
public sealed class CommentRecordProjectionTest
{
    [TestMethod]
    public void PostedAtStampAndDisplayTextComeFromTheRecord()
    {
        var comment = Sample(7, "hello", "2024-05-06T07:08:09Z", new StampInfo(301, "https://example/stamp.jpg"));

        Assert.AreEqual(new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero), comment.PostedAt);
        Assert.IsTrue(comment.IsStamp);
        Assert.AreEqual("https://example/stamp.jpg", comment.StampUrl);
        Assert.AreEqual("hello", comment.DisplayText);
    }

    [TestMethod]
    public void MissingStampIsNotAStampAndBadDateFallsBackToNow()
    {
        var comment = Sample(1, "text", "not-a-date", null);

        Assert.IsFalse(comment.IsStamp);
        Assert.IsNull(comment.StampUrl);
        Assert.IsTrue((comment.PostedAt - DateTimeOffset.UtcNow).Duration() < TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void BlockedAuthorCommentIsBlocked()
    {
        var comment = Sample(7, "hello", "2024-05-06T07:08:09Z", null);
        var blocked = new BlockedContentSnapshot(FrozenSet<string>.Empty, new long[] { 7 }.ToFrozenSet());
        var clear = new BlockedContentSnapshot(FrozenSet<string>.Empty, FrozenSet<long>.Empty);

        Assert.IsTrue(BlockedContentHelper.IsBlocked(comment, blocked));
        Assert.IsTrue(BlockedContentHelper.IsBlocked((object) comment, blocked));
        Assert.IsFalse(BlockedContentHelper.IsBlocked(comment, clear));
        Assert.IsFalse(BlockedContentHelper.IsBlocked(Sample(0, "hello", "2024-05-06T07:08:09Z", null), blocked));
    }

    [TestMethod]
    public void ReplyDraftIsIndexedByCommentIdAndOwnershipUsesSession()
    {
        using var client = new MakoClient(ProxyHelper.CreateMakoConfiguration(new PixivDomainFrontingSettings()));
        var list = new CommentsViewViewModel(SimpleWorkType.Illustration, 10, client, new StubSession(7));

        Assert.IsTrue(list.AllowsReplies);
        list.ReplyDraft = "top";
        Assert.AreEqual("top", list.ReplyDraft);
        Assert.AreEqual(0, list.ReplyTargetId);

        var replies = list.OpenReplies(99);
        Assert.IsFalse(replies.AllowsReplies);
        Assert.AreEqual(99, replies.ReplyTargetId);
        Assert.AreEqual("", replies.ReplyDraft);
        replies.ReplyDraft = "reply";
        Assert.AreEqual("reply", replies.ReplyDraft);
        Assert.AreEqual("top", list.ReplyDraft);

        var mine = Sample(7, "mine", "2024-01-01T00:00:00Z", null);
        var other = Sample(8, "other", "2024-01-01T00:00:00Z", null);
        Assert.IsTrue(list.IsOwnComment(mine));
        Assert.IsFalse(list.IsOwnComment(other));

        var loggedOut = new CommentsViewViewModel(SimpleWorkType.Illustration, 10, client, new StubSession(0));
        Assert.IsFalse(loggedOut.IsOwnComment(mine));
    }

    private static CommentRecord Sample(long userId, string text, string date, StampInfo? stamp) =>
        new(1, text, date, new User(userId, "alice", "alice", new ProfileImageUrls(null, null, null, null), false, null), false, stamp);

    private sealed class StubSession(long userId) : IUserSessionService
    {
        public TokenUser? CurrentUser => null;

        public User? CurrentUserEntity => null;

        public long CurrentUserId => userId;

        public bool IsLoggedIn => userId > 0;

        public event Action<TokenUser?>? UserRefreshed
        {
            add { }
            remove { }
        }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public void OnTokenRefreshed(TokenResponse? tokenResponse)
        {
        }

        public LoginUserRecord? GetCurrentLoginUser() => null;
    }
}
