// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Collections;
using Pixeval.Models.Pixiv;
using Pixeval.Utilities;

namespace Pixeval.ViewModels;

public sealed partial class SimpleOperableViewViewModel<TViewModel> : ViewModelBase, IOperableViewViewModel, IDisposable
    where TViewModel : class
{
    private bool _isDisposed;

    public SimpleOperableViewViewModel(IReadOnlyCollection<object> source, bool needRefreshOnOpen = false)
    {
        NeedRefreshOnOpen = needRefreshOnOpen;
        SourceView = new(source);
        SetFilters();
    }

    public SimpleOperableSourceView<TViewModel> SourceView { get; }

    public bool NeedRefreshOnOpen { get; }

    private static IFilter<object> TypeFilter { get; } = IFilter<object>.Create(entry => entry is TViewModel, false);

    /// <inheritdoc />
    [ObservableProperty]
    public partial bool IsSelecting { get; set; }

    /// <inheritdoc />
    public AvaloniaList<object> SelectedEntries { get; } = [];

    public void SetSortDescriptions(params IEnumerable<ISortDescription<object>> descriptions)
    {
        using (SourceView.View.DeferSortDescriptionsChange())
        {
            SourceView.View.SortDescriptions.Clear();
            SourceView.View.SortDescriptions.AddRange(descriptions);
        }
    }

    private void SetFilters()
    {
        using (SourceView.View.DeferFiltersChange())
        {
            SourceView.View.Filters.Clear();
            SourceView.View.Filters.Add(TypeFilter);
            if (UserFilter is not null)
                SourceView.View.Filters.Add(UserFilter);
        }
    }

    public IFilter<object>? UserFilter
    {
        get;
        set
        {
            if (Equals(field, value))
                return;

            field = value;
            SetFilters();
        }
    }

    /// <inheritdoc />
    IReadOnlyCollection<object> IOperableViewViewModel.View => SourceView.View;

    /// <inheritdoc />
    public IReadOnlyCollection<object> Source => SourceView.Source;

    /// <inheritdoc />
    public bool RequireAdaptiveGrid => typeof(TViewModel) == typeof(Pixeval.Native.Mako.Novel);

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        SourceView.Dispose();
    }
}
