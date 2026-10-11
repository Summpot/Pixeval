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

    private void ReplyBar_OnSendButtonClick(string replyContent) =>
        this.RunInteraction(() => ReplyBar_OnSendButtonClickAsync(replyContent));

    private async Task ReplyBar_OnSendButtonClickAsync(string replyContent)
    {
        if (DataContext is not CommentsViewViewModel viewModel)
            return;
        AddComment(await viewModel.AddCommentAsync(replyContent), viewModel);
    }

    private void ReplyBar_OnStickerClick(int stickerId) =>
        this.RunInteraction(() => ReplyBar_OnStickerClickAsync(stickerId));

    private async Task ReplyBar_OnStickerClickAsync(int stickerId)
    {
        if (DataContext is not CommentsViewViewModel viewModel)
            return;
        AddComment(await viewModel.AddStickerAsync(stickerId), viewModel);
    }

    private void AddComment(CommentRecord comment, CommentsViewViewModel viewModel)
    {
        if (comment.Id is 0)
            this.Notifications.ShowError("评论发送失败"); //TODO i18n
        else
            viewModel.AddComment(comment);
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
