// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel;
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
    private CancellationTokenSource? _deletion;
    private IUserSessionService? _session;

    public CommentItem()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => { CancelDeletion(); UpdateReplyButton(); };
    }

    public event Action<CommentRecord>? OpenRepliesButtonClick;

    public event Action<CommentRecord>? DeleteButtonClick;

    private CommentsViewViewModel? ListViewModel =>
        this.FindAncestorOfType<CommentView>()?.DataContext as CommentsViewViewModel;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _session = this.GetUiService<IUserSessionService>();
        if (_session is not null) _session.PropertyChanged += SessionChanged;
        UpdateReplyButton();
    }

    private void SessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IUserSessionService.CurrentUserId))
        {
            CancelDeletion();
            UpdateReplyButton();
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        CancelDeletion();
        if (_session is not null) _session.PropertyChanged -= SessionChanged;
        _session = null;
        base.OnUnloaded(e);
    }

    private void CancelDeletion()
    {
        var pending = _deletion;
        _deletion = null;
        pending?.Cancel();
        DeleteButton.IsEnabled = true;
    }

    private void UpdateReplyButton() =>
        UpdateActionVisibility();

    private void UpdateActionVisibility()
    {
        RepliesButton.IsVisible = ListViewModel?.AllowsReplies ?? false;
        DeleteButton.IsVisible = DataContext is CommentRecord comment && ListViewModel?.IsOwnComment(comment) == true;
    }

    private void PosterButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CommentRecord comment && this.AppNavigation is { } navigator)
            navigator.CreateUserPage(comment.User.Id);
    }

    private void OpenRepliesButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (ListViewModel?.AllowsReplies == true && DataContext is CommentRecord comment)
            OpenRepliesButtonClick?.Invoke(comment);
    }

    private void DeleteReplyButton_OnClicked(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => DeleteReplyButton_OnClickedAsync(sender, e));

    private async Task DeleteReplyButton_OnClickedAsync(object? sender, RoutedEventArgs e)
    {
        if (ListViewModel is not { } list || DataContext is not CommentRecord comment)
            return;

        if (!DeleteButton.IsEnabled || !list.IsOwnComment(comment))
            return;
        using var cancellation = new CancellationTokenSource();
        _deletion = cancellation;
        var userId = _session?.CurrentUserId;
        DeleteButton.IsEnabled = false;
        try
        {
            if (await list.DeleteCommentAsync(comment, cancellation.Token)
                && !cancellation.IsCancellationRequested && userId == _session?.CurrentUserId
                && Equals(DataContext, comment) && ReferenceEquals(ListViewModel, list))
                DeleteButtonClick?.Invoke(comment);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!cancellation.IsCancellationRequested)
                this.Notifications.ShowError(Strings.CommentItemDeleteFailed(), error.Message);
        }
        finally
        {
            if (ReferenceEquals(_deletion, cancellation))
            {
                _deletion = null;
                DeleteButton.IsEnabled = true;
            }
        }
    }
}
