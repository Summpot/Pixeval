// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Collections;
using Pixeval.Controls;
using Pixeval.Utilities;

namespace Pixeval.ViewModels;

public abstract partial class WorkViewViewModelBase<T, TViewModel>(FrozenSet<string>? blockedTags) : EntryViewViewModel<T, TViewModel>, IWorkViewViewModel
    where T : class
    where TViewModel : class
{
    public FrozenSet<string> CachedBlockedTags { get; private set; } = blockedTags ?? App.AppViewModel.AppSettings.BrowsingExperienceSettings.BlockedTags.ToFrozenSet();

    [ObservableProperty]
    public partial bool IsSelecting { get; set; }

    public AvaloniaList<object> SelectedEntries { get; } = [];

    public void SetSortDescriptions(params IEnumerable<ISortDescription<object>> descriptions)
    {
        using (View.DeferSortDescriptionsChange())
        {
            View.SortDescriptions.Clear();
            View.SortDescriptions.AddRange(descriptions.Cast<ISortDescription<TViewModel>>());
        }
    }

    protected void SetFilters()
    {
        using (View.DeferFiltersChange())
        {
            View.Filters.Clear();
            if (UserFilter is not null)
                View.Filters.Add(IFilter<TViewModel>.Create(o => UserFilter.Predicate(o), false));
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

    IReadOnlyCollection<object> IOperableViewViewModel.View => View;

    IReadOnlyCollection<object> IOperableViewViewModel.Source => Source;

    public abstract bool RequireAdaptiveGrid { get; }

    public void ResetEngine(IAsyncEnumerable<object>? newEngine, int itemsPerPage = 20, int itemLimit = -1)
    {
        CachedBlockedTags = [.. App.AppViewModel.AppSettings.BrowsingExperienceSettings.BlockedTags.ToFrozenSet()];
        var typedEngine = newEngine as IAsyncEnumerable<T> ?? (newEngine is not null ? CastEngine(newEngine) : null);
        ResetEngine(typedEngine, static (info, _) => (TViewModel) (object) info, itemsPerPage, itemLimit);
        SetFilters();

        static async IAsyncEnumerable<T> CastEngine(IAsyncEnumerable<object> source)
        {
            await foreach (var item in source)
                yield return (T) item;
        }
    }
}
