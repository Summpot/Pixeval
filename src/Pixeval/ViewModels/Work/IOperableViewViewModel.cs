// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Collections;
using Misaki;
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

    AvaloniaList<IArtworkInfo> SelectedEntries { get; }

    void SetSortDescriptions(params IEnumerable<ISortDescription<IArtworkInfo>> descriptions);

    IFilter<IArtworkInfo>? UserFilter { get; set; }

    bool RequireAdaptiveGrid { get; }

    new IReadOnlyCollection<IArtworkInfo> View { get; }

    new IReadOnlyCollection<IArtworkInfo> Source { get; }

    IReadOnlyCollection<object> ISimpleViewViewModel.View => View;

    IReadOnlyCollection<object> ISimpleViewViewModel.Source => Source;
}
