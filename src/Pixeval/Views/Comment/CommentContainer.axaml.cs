// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Pixeval.Models.Pixiv;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Views;

public partial class CommentContainer : ContentPage
{
    public CommentContainer() => InitializeComponent();

    public CommentContainer(CommentItemViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private Task<bool> ReplyBar_OnSendButtonClick(string replyContent, CancellationToken token) =>
        SubmitAsync((model, cancellation) => model.AddCommentAsync(replyContent, cancellation), token);

    private Task<bool> ReplyBar_OnStickerClick(int stickerId, CancellationToken token) =>
        SubmitAsync((model, cancellation) => model.AddStickerAsync(stickerId, cancellation), token);

    private async Task<bool> SubmitAsync(Func<CommentsViewViewModel, CancellationToken, Task<Comment>> submit, CancellationToken token)
    {
        if (DataContext is not CommentsViewViewModel viewModel || App.AppViewModel.MakoClient.Me?.Id is not { } accountId)
            return false;
        var comment = await submit(viewModel, token);
        if (token.IsCancellationRequested || !ReferenceEquals(DataContext, viewModel) || App.AppViewModel.MakoClient.Me?.Id != accountId)
            return false;
        if (comment.Id is 0)
        {
            TopLevel.GetTopLevel(this)?.ViewContainer?.ShowError(I18NManager.GetResource(PixivReplyBarResources.SendFailed));
            return false;
        }
        viewModel.AddComment(comment);
        return true;
    }

    private async void CommentView_OnOpenRepliesButtonClick(CommentItemViewModel viewModel)
    {
        if (IsInNavigationPage && Parent is NavigationPage frame)
            await frame.PushAsync(new CommentContainer(viewModel));
    }
}
