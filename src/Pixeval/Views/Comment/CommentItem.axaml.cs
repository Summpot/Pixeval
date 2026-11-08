// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;

namespace Pixeval.Views;

public partial class CommentItem : UserControl
{
    private CancellationTokenSource? _deletion;

    public CommentItem() => InitializeComponent();

    private CommentItemViewModel ViewModel => (CommentItemViewModel) DataContext!;

    public event Action<CommentItemViewModel>? OpenRepliesButtonClick;

    public event Action<CommentItemViewModel>? DeleteButtonClick;

    private void PosterButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.ViewContainer is { } viewContainer)
            viewContainer.CreateUserPage(ViewModel.UserId);
    }

    private void OpenRepliesButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CommentItemViewModel { IsTopComment: true } model)
            OpenRepliesButtonClick?.Invoke(model);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        CancelDeletion();
        base.OnDataContextChanged(e);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CancelDeletion();
        base.OnDetachedFromVisualTree(e);
    }

    private void CancelDeletion()
    {
        var deletion = _deletion;
        _deletion = null;
        deletion?.Cancel();
    }

    private async void DeleteReplyButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (_deletion is not null || DataContext is not CommentItemViewModel { IsMe: true, IsDeleting: false } model)
            return;
        using var deletion = new CancellationTokenSource();
        _deletion = deletion;
        var accountId = App.AppViewModel.MakoClient.Me?.Id;
        try
        {
            var succeeded = await model.DeleteAsync(deletion.Token);
            if (deletion.IsCancellationRequested || !ReferenceEquals(DataContext, model)
                || App.AppViewModel.MakoClient.Me?.Id != accountId)
                return;
            if (succeeded)
                DeleteButtonClick?.Invoke(model);
            else
                ShowDeleteError();
        }
        catch (OperationCanceledException) when (deletion.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (!deletion.IsCancellationRequested && ReferenceEquals(DataContext, model)
                && App.AppViewModel.MakoClient.Me?.Id == accountId)
                ShowDeleteError();
        }
        finally
        {
            if (ReferenceEquals(_deletion, deletion))
                _deletion = null;
        }
    }

    private void ShowDeleteError() =>
        TopLevel.GetTopLevel(this)?.ViewContainer?.ShowError(I18NManager.GetResource(CommentItemResources.DeleteFailed));
}
