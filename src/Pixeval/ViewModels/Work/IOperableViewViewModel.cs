// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Collections;
using Pixeval.Collections;

namespace Pixeval.ViewModels;

public interface ISimpleViewViewModel : INotifyPropertyChanged
{
    IReadOnlyCollection<object> View { get; }

    IReadOnlyCollection<object> Source { get; }
}

public interface IOperableViewViewModel : ISimpleViewViewModel
{
    bool IsSelecting { get; set; }

    AvaloniaList<object> SelectedEntries { get; }

    void SetSortDescriptions(params IEnumerable<ISortDescription<object>> descriptions);

    IFilter<object>? UserFilter { get; set; }

    bool RequireAdaptiveGrid { get; }

    new IReadOnlyCollection<object> View { get; }

    new IReadOnlyCollection<object> Source { get; }

    IReadOnlyCollection<object> ISimpleViewViewModel.View => View;

    IReadOnlyCollection<object> ISimpleViewViewModel.Source => Source;
}
