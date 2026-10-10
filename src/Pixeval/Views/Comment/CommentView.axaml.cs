// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.Native.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Views;

public partial class CommentView : UserControl
{
    public CommentView() => InitializeComponent();

    public event Action<CommentRecord>? OpenRepliesButtonClick;

    private void CommentItem_OnOpenRepliesButtonClick(CommentRecord comment) => OpenRepliesButtonClick?.Invoke(comment);

    private void CommentItem_OnDeleteButtonClick(CommentRecord comment) =>
        (DataContext as CommentsViewViewModel)?.RemoveComment(comment.Id);

    private void CommentView_OnDataContextChanged(object? sender, EventArgs e) => (DataContext as CommentsViewViewModel)?.RefreshEngine();

    #region Disposal

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is CommentsViewViewModel vm)
            RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, vm));
    }

    #endregion
}
