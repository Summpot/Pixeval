// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Native.Storage;
using Pixeval.Utilities;
using Pixeval.ViewModels.Search;

namespace Pixeval.ViewModels;

public partial class SearchPageViewModel : ViewModelBase
{
    public static SearchPageViewModel Instance { get; } = new();

    private static MakoClient MakoClient => App.AppViewModel.MakoClient;

    private SearchPageViewModel()
    {
        PinnedTags.CollectionChanged += (_, _) => SyncSearchTags();
        SearchHistories.CollectionChanged += (_, _) => SyncSearchTags();
        SyncSearchTags();
        _ = LoadResourcesAsync();
    }

    private async Task LoadResourcesAsync()
    {
        await Task.WhenAll(RefreshIllustrationTagsAsync(), RefreshNovelTagsAsync(), RefreshSearchOptionsAsync());
    }

    private async Task RefreshIllustrationTagsAsync()
    {
        IllustrationTrendingTags = await MakoClient.GetWorkTrendingTagsAsync(SimpleWorkType.Illustration);
    }

    private async Task RefreshNovelTagsAsync()
    {
        NovelTrendingTags = await MakoClient.GetWorkTrendingTagsAsync(SimpleWorkType.Novel);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrendingTags))]
    public partial SimpleWorkType SelectedTrendingTagsType { get; set; } = App.AppViewModel.AppSettings.SearchSettings.DefaultSimpleWorkType;

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial SimpleWorkType SelectedAdvancedOptionsType { get; set; } = App.AppViewModel.AppSettings.SearchSettings.DefaultSimpleWorkType;

    public IReadOnlyList<TrendingTag> TrendingTags => SelectedTrendingTagsType is SimpleWorkType.Novel ? NovelTrendingTags : IllustrationTrendingTags;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrendingTags))]
    public partial IReadOnlyList<TrendingTag> IllustrationTrendingTags { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrendingTags))]
    public partial IReadOnlyList<TrendingTag> NovelTrendingTags { get; private set; } = [];

    public IllustrationSearchFormViewModel IllustrationForm { get; } = new IllustrationSearchFormViewModel();

    public NovelSearchFormViewModel NovelForm { get; } = new NovelSearchFormViewModel();

    private static ObservableCollection<SearchHistoryRecord> SearchHistories => App.AppViewModel.SearchHistoryEntries;

    private static ObservableCollection<string> PinnedTags => App.AppViewModel.AppSettings.BrowsingExperienceSettings.PinnedTags;

    public ObservableCollection<object> SearchTags { get; } = [];

    private void SyncSearchTags()
    {
        SearchTags.Clear();
        foreach (var tag in PinnedTags)
            SearchTags.Add(tag);
        foreach (var history in SearchHistories)
            SearchTags.Add(history);
    }

    private async Task RefreshSearchOptionsAsync()
    {
        var options = await MakoClient.GetSearchOptionsAsync();
        IllustrationForm.ToolItems = [SearchArgumentsFormViewModelBase.CommonUnspecified, .. options.Illust.Tools];
        NovelForm.LanguageItems = [new("", SearchArgumentsFormViewModelBase.CommonUnspecified), .. options.Novel.Languages];
        NovelForm.GenreItems = [new(0, SearchArgumentsFormViewModelBase.CommonUnspecified), .. options.Novel.Genres];
    }
}
