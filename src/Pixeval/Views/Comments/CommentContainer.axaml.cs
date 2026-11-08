using System;
using System.Threading;
using System.Threading.Tasks;
// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia.Controls;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Views.Comments;

public partial class CommentContainer : ContentPage
{
    public CommentContainer() => InitializeComponent();

    public CommentContainer(CommentsViewViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private Task<bool> ReplyBar_OnSendButtonClick(string content, CancellationToken token) =>
        SubmitAsync((model, cancellation) => model.AddCommentAsync(content, cancellation), token);

    private Task<bool> ReplyBar_OnStickerClick(int stickerId, CancellationToken token) =>
        SubmitAsync((model, cancellation) => model.AddStickerAsync(stickerId, cancellation), token);

    private async Task<bool> SubmitAsync(Func<CommentsViewViewModel, CancellationToken, Task<CommentRecord>> submit, CancellationToken token)
    {
        if (DataContext is not CommentsViewViewModel model)
            return false;
        var session = this.GetUiService<IUserSessionService>();
        var accountId = session?.CurrentUserId;
        var comment = await submit(model, token);
        if (token.IsCancellationRequested || !ReferenceEquals(DataContext, model) || session?.CurrentUserId != accountId)
            return false;
        if (comment.Id == 0)
        {
            this.Notifications.ShowError(Strings.PixivReplyBarSendFailed());
            return false;
        }
        model.AddComment(comment);
        return true;
    }

    private void CommentView_OnOpenRepliesButtonClick(CommentRecord comment) =>
        this.RunInteraction(() => CommentView_OnOpenRepliesButtonClickAsync(comment));

    private async Task CommentView_OnOpenRepliesButtonClickAsync(CommentRecord comment)
    {
        if (DataContext is not CommentsViewViewModel parent)
            return;

        if (IsInNavigationPage && Parent is NavigationPage frame)
            await frame.PushAsync(new CommentContainer(parent.OpenReplies(comment.Id)));
    }
}
