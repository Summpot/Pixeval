// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia;
using Pixeval.Views;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Views.Viewers;

public partial class CommentsPage : IconNavigationPage
{
    public static readonly StyledProperty<long> ParentIdProperty =
        AvaloniaProperty.Register<CommentsPage, long>(nameof(ParentId));

    public static readonly StyledProperty<SimpleWorkType> ParentTypeProperty =
        AvaloniaProperty.Register<CommentsPage, SimpleWorkType>(nameof(ParentType));

    private CommentContainer? _container;

    public CommentsPage() : this(null)
    {
    }

    public CommentsPage(CommentsViewViewModel? viewModel)
    {
        InitializeComponent();
        if (viewModel is not null)
        {
            ParentType = viewModel.ParentType;
            ParentId = viewModel.ParentId;
        }

        _container = new CommentContainer { DataContext = viewModel };
        _ = PushAsync(_container);
    }

    public long ParentId
    {
        get => GetValue(ParentIdProperty);
        set => SetValue(ParentIdProperty, value);
    }

    public SimpleWorkType ParentType
    {
        get => GetValue(ParentTypeProperty);
        set => SetValue(ParentTypeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ParentIdProperty || change.Property == ParentTypeProperty || change.Property == IsVisibleProperty)
            EnsureViewModel();
    }

    private void EnsureViewModel()
    {
        if (_container is null)
            return;

        if (!IsVisible || ParentId <= 0)
        {
            _container.DataContext = null;
            return;
        }

        if (_container.DataContext is CommentsViewViewModel existing
            && existing.ParentId == ParentId
            && existing.ParentType == ParentType)
            return;

        _container.DataContext = new CommentsViewViewModel(ParentType, ParentId);
    }
}
