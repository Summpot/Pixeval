// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Linq;
using Avalonia;
using Avalonia.Interactivity;
using Mako.Global.Enum;
using Mako.Model;
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

    private readonly long _id;
    private readonly SimpleWorkType _simpleWorkType;

    public WorkRelatedPage() : this(0, PixevalSettings.SimpleWorkType)
    {
    }

    public WorkRelatedPage(long id, SimpleWorkType simpleWorkType, IWorkViewViewModel? viewModel = null)
    {
        _id = id;
        _simpleWorkType = simpleWorkType;
        InitializeComponent();
        if (viewModel is not null)
            WorkContainer.SetViewModel(viewModel);
        else
            ChangeSource();
    }

    private void WorkContainer_OnRefreshRequested(object? sender, RoutedEventArgs e)
    {
        ChangeSource();
    }

    private void ChangeSource()
    {
        if (_id is not 0)
            WorkContainer.ResetEngine(App.AppViewModel.MakoClient.WorkRelated(_id, _simpleWorkType));
        else if (_simpleWorkType is SimpleWorkType.Novel)
            WorkContainer.ResetEngine(App.AppViewModel.MakoClient.Computed(AsyncEnumerable.Empty<Novel>()));
        else
            WorkContainer.ResetEngine(App.AppViewModel.MakoClient.Computed(AsyncEnumerable.Empty<Illustration>()));
    }
}
