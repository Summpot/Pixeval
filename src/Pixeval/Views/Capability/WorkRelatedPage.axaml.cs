// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Interactivity;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Capability;

public partial class WorkRelatedPage : IconContentPage
{
    public static readonly DirectProperty<WorkRelatedPage, bool> IsCommandBarCollapsedProperty = AvaloniaProperty.RegisterDirect<WorkRelatedPage, bool>(
        nameof(IsCommandBarCollapsed),
        o => o.IsCommandBarCollapsed,
        (o, v) => o.IsCommandBarCollapsed = v);

    public bool IsCommandBarCollapsed
    {
        get;
        set => SetAndRaise(IsCommandBarCollapsedProperty, ref field, value);
    }

    public static readonly StyledProperty<long> WorkIdProperty =
        AvaloniaProperty.Register<WorkRelatedPage, long>(nameof(WorkId));

    public static readonly StyledProperty<SimpleWorkType> WorkTypeProperty =
        AvaloniaProperty.Register<WorkRelatedPage, SimpleWorkType>(nameof(WorkType));

    private long _id;
    private SimpleWorkType _simpleWorkType;
    private bool _suppressSourceChange;
    private bool _hasViewModel;

    public WorkRelatedPage()
    {
        InitializeComponent();
    }

    public WorkRelatedPage(long id, SimpleWorkType simpleWorkType, IWorkViewViewModel? viewModel = null)
    {
        InitializeComponent();
        if (viewModel is not null)
        {
            _hasViewModel = true;
            WorkContainer.SetViewModel(viewModel);
        }

        _suppressSourceChange = true;
        WorkType = simpleWorkType;
        WorkId = id;
        _suppressSourceChange = false;
        if (!_hasViewModel)
            ChangeSource();
    }

    public long WorkId
    {
        get => GetValue(WorkIdProperty);
        set => SetValue(WorkIdProperty, value);
    }

    public SimpleWorkType WorkType
    {
        get => GetValue(WorkTypeProperty);
        set => SetValue(WorkTypeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WorkIdProperty)
            _id = change.GetNewValue<long>();
        else if (change.Property == WorkTypeProperty)
            _simpleWorkType = change.GetNewValue<SimpleWorkType>();
        else if (change.Property != IsVisibleProperty)
            return;

        if (!_suppressSourceChange && !_hasViewModel)
            ChangeSource();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    private void ChangeSource()
    {
        var engine = !IsVisible || _id is 0
            ? (IAsyncEnumerable<object>) AsyncEnumerable.Empty<object>()
            : App.AppViewModel.MakoClient.WorkRelated(_id, _simpleWorkType);
        WorkContainer.ResetEngine(engine);
    }
}
