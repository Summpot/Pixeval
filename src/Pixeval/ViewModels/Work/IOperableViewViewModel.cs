// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Collections;
using Pixeval.Models.Options;

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

    void SetSortOption(LocalSortOption sortOption);

    Predicate<object>? UserFilter { get; set; }

    bool RequireAdaptiveGrid { get; }

    new IReadOnlyCollection<object> View { get; }

    new IReadOnlyCollection<object> Source { get; }

    IReadOnlyCollection<object> ISimpleViewViewModel.View => View;

    IReadOnlyCollection<object> ISimpleViewViewModel.Source => Source;
}
