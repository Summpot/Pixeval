// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.Controls;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Views;

public partial class CommentView : UserControl
{
    private CommentsViewViewModel? _viewModel;

    public static readonly DirectProperty<CommentView, bool> IsLoadInterruptedProperty =
        AvaloniaProperty.RegisterDirect<CommentView, bool>(nameof(IsLoadInterrupted), view => view.IsLoadInterrupted);

    public bool IsLoadInterrupted
    {
        get;
        private set => SetAndRaise(IsLoadInterruptedProperty, ref field, value);
    }

    public CommentView()
    {
        InitializeComponent();
        // Loading completion is the point at which the current source exposes its
        // interruption state. The collection itself does not notify this flag.
        ListBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == IncrementalLoadingBehavior.IsLoadingMoreProperty)
                IsLoadInterrupted = !ListBox.GetValue(IncrementalLoadingBehavior.IsLoadingMoreProperty)
                    && _viewModel?.View.IsInterrupted == true;
        };
    }

    public event Action<CommentItemViewModel>? OpenRepliesButtonClick;

    private void CommentItem_OnOpenRepliesButtonClick(CommentItemViewModel viewModel) => OpenRepliesButtonClick?.Invoke(viewModel);

    private void CommentItem_OnDeleteButtonClick(CommentItemViewModel viewModel) => (DataContext as CommentsViewViewModel)?.DeleteComment(viewModel);

    private void CommentView_OnDataContextChanged(object? sender, EventArgs e)
    {
        var model = DataContext as CommentsViewViewModel;
        // Navigation temporarily detaches a retained page and clears its inherited
        // context. Keep that page's source until an actual replacement arrives.
        if (model is null)
            return;
        if (ReferenceEquals(_viewModel, model))
            return;
        _viewModel?.Dispose();
        _viewModel = model;
        IsLoadInterrupted = false;
        model.RefreshEngine();
        if (IsLoaded)
            RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, model));
    }

    private void RefreshButton_OnClick(object? sender, RoutedEventArgs e)
    {
        IsLoadInterrupted = false;
        _viewModel?.RefreshEngine();
    }

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
