// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Collections;
using Pixeval.Models.Database;
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

    private static ObservableCollection<SearchHistoryRecord> SearchHistories => App.AppViewModel.HistoryPersistHelper.SearchHistoryEntries;

    private static ObservableCollection<string> PinnedTags => App.AppViewModel.AppSettings.BrowsingExperienceSettings.PinnedTags;

    public CompositeObservableCollection<object> SearchTags { get; } = new(PinnedTags, SearchHistories);

    private async Task RefreshSearchOptionsAsync()
    {
        var options = await MakoClient.GetSearchOptionsAsync();
        IllustrationForm.ToolItems =
        [
            .. IllustrationForm.ToolItems,
            .. options.Illust.Tools
        ];
        NovelForm.LanguageItems =
        [
            .. NovelForm.LanguageItems,
            .. options.Novel.Languages
        ];
        NovelForm.GenreItems =
        [
            .. NovelForm.GenreItems,
            .. options.Novel.Genres
        ];
    }
}
