// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Pixeval.Pixiv;
using Pixeval.Mako;

namespace Pixeval.ViewModels.Comments;

public class CommentsViewViewModel : EntryViewViewModel<CommentRecord, CommentRecord>
{
    private readonly Dictionary<long, string> _drafts = [];

    public long ParentId { get; }

    public SimpleWorkType ParentType { get; }

    public long? ReplyToCommentId { get; }

    public bool AllowsReplies => ReplyToCommentId is null;

    public long ReplyTargetId => ReplyToCommentId ?? 0;

    public string ReplyDraft
    {
        get => _drafts.GetValueOrDefault(ReplyTargetId) ?? "";
        set
        {
            var text = value ?? "";
            if (_drafts.TryGetValue(ReplyTargetId, out var current) && current == text)
                return;

            _drafts[ReplyTargetId] = text;
            OnPropertyChanged();
        }
    }

    private readonly MakoClient _makoClient;
    private readonly IUserSessionService? _userSessionService;
    private readonly IBlockedContentService? _blockedContent;

    public CommentsViewViewModel(
        SimpleWorkType parentType,
        long parentId,
        MakoClient makoClient,
        IUserSessionService? userSessionService = null,
        IBlockedContentService? blockedContent = null,
        long? replyToCommentId = null)
        : base(blockedContent)
    {
        ParentType = parentType;
        ParentId = parentId;
        ReplyToCommentId = replyToCommentId;
        _makoClient = makoClient;
        _userSessionService = userSessionService;
        _blockedContent = blockedContent;
    }

    public bool IsOwnComment(CommentRecord comment) =>
        _userSessionService is { CurrentUserId: > 0 } && comment.User.Id == _userSessionService.CurrentUserId;

    public CommentsViewViewModel OpenReplies(long commentId) =>
        new(ParentType, ParentId, _makoClient, _userSessionService, _blockedContent, commentId);

    public async Task<CommentRecord> AddCommentAsync(string content)
    {
        return ReplyToCommentId is long parentCommentId
            ? await _makoClient.AddWorkCommentAsync(ParentType, ParentId, parentCommentId, content)
            : await _makoClient.AddWorkCommentAsync(ParentType, ParentId, content);
    }

    public async Task<CommentRecord> AddStickerAsync(int stampId)
    {
        return ReplyToCommentId is long parentCommentId
            ? await _makoClient.AddWorkCommentAsync(ParentType, ParentId, parentCommentId, stampId)
            : await _makoClient.AddWorkCommentAsync(ParentType, ParentId, stampId);
    }

    public void AddComment(CommentRecord comment)
    {
        if (comment.Id is 0)
            return;

        Source.Insert(0, _blockedContent is { } blocked ? blocked.Replace(comment) : comment);
    }

    public async Task<bool> DeleteCommentAsync(CommentRecord comment)
    {
        if (!IsOwnComment(comment))
            return false;

        return await _makoClient.DeleteWorkCommentAsync(ParentType, comment.Id);
    }

    public void RemoveComment(long commentId)
    {
        for (var i = 0; i < Source.Count; i++)
        {
            if (Source[i].Id != commentId)
                continue;

            Source.RemoveAt(i);
            return;
        }
    }

    public void RefreshSource()
    {
        var engine = ReplyToCommentId is long commentId
            ? _makoClient.WorkCommentReplies(ParentType, commentId)
            : _makoClient.WorkComments(ParentType, ParentId);
        ResetSource(engine);
    }
}
