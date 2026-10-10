// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Services;

namespace Pixeval.ViewModels.Viewers;

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

    public CommentsViewViewModel(
        SimpleWorkType parentType,
        long parentId,
        MakoClient? makoClient = null,
        IUserSessionService? userSessionService = null,
        long? replyToCommentId = null)
    {
        ParentType = parentType;
        ParentId = parentId;
        ReplyToCommentId = replyToCommentId;
        _makoClient = makoClient ?? App.Services?.GetService<MakoClient>() ?? App.AppViewModel.MakoClient;
        _userSessionService = userSessionService ?? App.Services?.GetService<IUserSessionService>();
    }

    public bool IsOwnComment(CommentRecord comment) =>
        _userSessionService is { CurrentUserId: > 0 } && comment.User.Id == _userSessionService.CurrentUserId;

    public CommentsViewViewModel OpenReplies(long commentId) =>
        new(ParentType, ParentId, _makoClient, _userSessionService, commentId);

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

        Source.Insert(0, BlockedContentHelper.Replace(comment));
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

    public void RefreshEngine()
    {
        var engine = ReplyToCommentId is long commentId
            ? _makoClient.WorkCommentReplies(ParentType, commentId)
            : _makoClient.WorkComments(ParentType, ParentId);
        ResetEngine(engine);
    }
}
