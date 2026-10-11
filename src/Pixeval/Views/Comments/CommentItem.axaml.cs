// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Comments;

public partial class CommentItem : UserControl
{
    public CommentItem()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => UpdateReplyButton();
    }

    public event Action<CommentRecord>? OpenRepliesButtonClick;

    public event Action<CommentRecord>? DeleteButtonClick;

    private CommentsViewViewModel? ListViewModel =>
        this.FindAncestorOfType<CommentView>()?.DataContext as CommentsViewViewModel;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UpdateReplyButton();
    }

    private void UpdateReplyButton() =>
        OpenRepliesButton.IsVisible = ListViewModel?.AllowsReplies ?? false;

    private void PosterButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CommentRecord comment && this.AppNavigation is { } navigator)
            navigator.CreateUserPage(comment.User.Id);
    }

    private void OpenRepliesButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CommentRecord comment)
            OpenRepliesButtonClick?.Invoke(comment);
    }

    private void DeleteReplyButton_OnClicked(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => DeleteReplyButton_OnClickedAsync(sender, e));

    private async Task DeleteReplyButton_OnClickedAsync(object? sender, RoutedEventArgs e)
    {
        if (ListViewModel is not { } list || DataContext is not CommentRecord comment)
            return;

        if (await list.DeleteCommentAsync(comment))
            DeleteButtonClick?.Invoke(comment);
    }
}
