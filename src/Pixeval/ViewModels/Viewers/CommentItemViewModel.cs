// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Utilities;
using Pixeval.Views;

namespace Pixeval.ViewModels.Viewers;

public sealed partial class CommentItemViewModel : CommentsViewViewModel
{
    [ObservableProperty]
    public partial bool IsDeleting { get; private set; }

    public CommentItemViewModel(Comment comment, SimpleWorkType parentType, long parentId, bool isTopComment)
        : base(parentType, parentId)
    {
        Comment = BlockedContentHelper.Replace(comment);
        IsTopComment = isTopComment;
    }

    /// <summary>
    /// 若为<see langword="true"/>，表示这个评论是作品下面的评论；
    /// 否则表示这个评论是另一个评论下面的回复。
    /// Pixiv目前只支持两层评论
    /// </summary>
    public bool IsTopComment { get; }

    public Comment Comment { get; }

    public bool HasReplies => Comment.HasReplies;

    [MemberNotNullWhen(true, nameof(StampUrl))]
    public bool IsStamp => Comment.Stamp is not null;

    public string? StampUrl => Comment.Stamp?.StampUrl;

    public DateTimeOffset Date => Comment.Date;

    public string User => Comment.User.Name;

    public long UserId => Comment.User.Id;

    public string CommentContent => CommentImageHelper.GetContents(Comment.Content);

    public bool IsMe => App.AppViewModel.MakoClient.Me?.Id == UserId;

    public long Id => Comment.Id;

    public override async Task<Comment> AddCommentAsync(string content, CancellationToken token = default)
    {
        if (!IsTopComment)
            return Comment.CreateDefault();
        return await App.AppViewModel.MakoClient.AddWorkCommentAsync(ParentType, ParentId, Id, content, token);
    }

    public override async Task<Comment> AddStickerAsync(int stampId, CancellationToken token = default)
    {
        if (!IsTopComment)
            return Comment.CreateDefault();
        return await App.AppViewModel.MakoClient.AddWorkCommentAsync(ParentType, ParentId, Id, stampId, token);
    }

    public async Task<bool> DeleteAsync(CancellationToken token = default)
    {
        if (!IsMe || IsDeleting || token.IsCancellationRequested)
            return false;
        var accountId = App.AppViewModel.MakoClient.Me?.Id;
        IsDeleting = true;
        try
        {
            return await App.AppViewModel.MakoClient.DeleteWorkCommentAsync(ParentType, Id, token)
                && !token.IsCancellationRequested && App.AppViewModel.MakoClient.Me?.Id == accountId;
        }
        finally
        {
            IsDeleting = false;
        }
    }

    public override void AddComment(Comment comment) => Source.Insert(0, new CommentItemViewModel(comment, ParentType, ParentId, false));

    public override void RefreshEngine()
    {
        ResetEngine(App.AppViewModel.MakoClient.WorkCommentReplies(ParentType, Id),
            (comment, _) => new(comment, ParentType, ParentId, false));
    }
}
