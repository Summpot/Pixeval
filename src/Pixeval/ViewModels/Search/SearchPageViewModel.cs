// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Config;
using Pixeval.I18N;
using Pixeval.Mako;
using Pixeval.Pixiv;
using Pixeval.Services.Core;
using Pixeval.Storage;
using Pixeval.Utilities;
using Pixeval.Views.Search;

namespace Pixeval.ViewModels.Search;

public partial class SearchPageViewModel : ViewModelBase
{
    private static readonly TimeSpan SearchTagCompletionDelay = TimeSpan.FromSeconds(1);

    private readonly MakoClient _makoClient;
    private readonly SearchHistorySession _searchHistory;
    private readonly AppSettings _appSettings;
    private readonly INotificationService _notificationService;
    private readonly IClipboardService _clipboardService;
    private readonly INavigationService _navigationService;

    private CancellationTokenSource? _searchCompletionCancellationTokenSource;
    private int _searchCompletionUpdateVersion;
    private bool _isUpdatingSearchCompletions;
    private string? _searchCompletionSource;

    public SearchPageViewModel(
        MakoClient makoClient,
        SearchHistorySession searchHistory,
        AppSettings appSettings,
        INotificationService notificationService,
        IClipboardService clipboardService,
        INavigationService navigationService,
        IUserSessionService userSession)
    {
        _makoClient = makoClient;
        _searchHistory = searchHistory;
        _appSettings = appSettings;
        _notificationService = notificationService;
        _clipboardService = clipboardService;
        _navigationService = navigationService;
        IllustrationForm = new IllustrationSearchFormViewModel(userSession);
        NovelForm = new NovelSearchFormViewModel(userSession);

        SelectedTrendingTagsType = appSettings.SearchSettings.DefaultSimpleWorkType;
        SelectedAdvancedOptionsType = appSettings.SearchSettings.DefaultSimpleWorkType;

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
        IllustrationTrendingTags = await _makoClient.GetWorkTrendingTagsAsync(SimpleWorkType.Illustration);
    }

    private async Task RefreshNovelTagsAsync()
    {
        NovelTrendingTags = await _makoClient.GetWorkTrendingTagsAsync(SimpleWorkType.Novel);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrendingTags))]
    public partial SimpleWorkType SelectedTrendingTagsType { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    partial void OnSearchTextChanged(string value)
    {
        if (!_isUpdatingSearchCompletions)
            UpdateSearchCompletions(value);
    }

    [ObservableProperty]
    public partial SimpleWorkType SelectedAdvancedOptionsType { get; set; }

    public IReadOnlyList<TrendingTag> TrendingTags => SelectedTrendingTagsType is SimpleWorkType.Novel ? NovelTrendingTags : IllustrationTrendingTags;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrendingTags))]
    public partial IReadOnlyList<TrendingTag> IllustrationTrendingTags { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrendingTags))]
    public partial IReadOnlyList<TrendingTag> NovelTrendingTags { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<SearchCompletionItem> SearchCompletionItems { get; set; } = [];

    [ObservableProperty]
    public partial SearchCompletionItem? SelectedSearchCompletion { get; set; }

    public IllustrationSearchFormViewModel IllustrationForm { get; }

    public NovelSearchFormViewModel NovelForm { get; }

    private ObservableCollection<SearchHistoryRecord> SearchHistories => _searchHistory.Entries;

    private ObservableCollection<string> PinnedTags => _appSettings.BrowsingExperienceSettings.PinnedTags;

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
        var options = await _makoClient.GetSearchOptionsAsync();
        IllustrationForm.ToolItems = [SearchArgumentsFormViewModelBase.CommonUnspecified, .. options.Illust.Tools];
        NovelForm.LanguageItems = [new("", SearchArgumentsFormViewModelBase.CommonUnspecified), .. options.Novel.Languages];
        NovelForm.GenreItems = [new(0, SearchArgumentsFormViewModelBase.CommonUnspecified), .. options.Novel.Genres];
    }

    public void UpdateSearchCompletions(string? text)
    {
        var source = text ?? "";
        if (string.Equals(source, _searchCompletionSource, StringComparison.Ordinal))
            return;

        _searchCompletionSource = source;
        ++_searchCompletionUpdateVersion;
        _isUpdatingSearchCompletions = true;
        try
        {
            var normalized = source.Trim();
            if (normalized.Length is 0)
            {
                ClearSearchCompletionItems();
                return;
            }

            _searchCompletionCancellationTokenSource?.Cancel();
            _searchCompletionCancellationTokenSource?.Dispose();
            _searchCompletionCancellationTokenSource = new();

            var version = _searchCompletionUpdateVersion;
            var immediateSuggestions = CreateImmediateSearchCompletions(normalized);
            SetSearchCompletionItems(immediateSuggestions);
            _ = LoadTagCompletionsAsync(source, immediateSuggestions, version, _searchCompletionCancellationTokenSource.Token);
        }
        finally
        {
            _isUpdatingSearchCompletions = false;
        }
    }

    public static IReadOnlyList<SearchCompletionItem> CreateImmediateSearchCompletions(string normalized)
    {
        var suggestions = new List<SearchCompletionItem>();
        if (normalized.All(char.IsDigit))
        {
            suggestions.Add(new(
                SearchCompletionKind.OpenIllustration,
                I18NManager.GetResource(SearchResources.OpenId.Illustration),
                normalized));
            suggestions.Add(new(
                SearchCompletionKind.OpenNovel,
                I18NManager.GetResource(SearchResources.OpenId.Novel),
                normalized));
            suggestions.Add(new(
                SearchCompletionKind.OpenUser,
                I18NManager.GetResource(SearchResources.OpenId.User),
                normalized));
        }

        suggestions.Add(new(
            SearchCompletionKind.SearchUser,
            I18NManager.GetResource(SearchResources.SearchUser),
            normalized));

        return suggestions;
    }

    private async Task LoadTagCompletionsAsync(
        string source,
        IReadOnlyList<SearchCompletionItem> immediateSuggestions,
        int version,
        CancellationToken token)
    {
        try
        {
            var keyword = GetLastKeyword(source);
            if (keyword.Length is 0)
                return;

            await Task.Delay(SearchTagCompletionDelay, token).ConfigureAwait(false);
            var tags = await _makoClient.GetAutoCompletionForKeyword(keyword, true, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (version != _searchCompletionUpdateVersion)
                return;

            var suggestions = immediateSuggestions
                .Concat(tags.Select(tag => new SearchCompletionItem(
                    SearchCompletionKind.Tag,
                    tag.Name,
                    tag.TranslatedName)))
                .ToArray();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version == _searchCompletionUpdateVersion && string.Equals(source, SearchText ?? "", StringComparison.Ordinal))
                    SetSearchCompletionItems(suggestions);
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }

    public void SetSearchCompletionItems(IReadOnlyList<SearchCompletionItem> suggestions)
    {
        if (suggestions.Count is 0)
        {
            ClearSearchCompletionItems();
            return;
        }

        if (!SearchCompletionItems.SequenceEqual(suggestions))
        {
            SearchCompletionItems = suggestions;
            SelectedSearchCompletion = null;
        }
    }

    public void ClearSearchCompletionItems()
    {
        _searchCompletionCancellationTokenSource?.Cancel();
        _searchCompletionCancellationTokenSource = null;
        SearchCompletionItems = [];
        SelectedSearchCompletion = null;
    }

    public static string GetLastKeyword(string text)
    {
        var lastSpaceIndex = text.LastIndexOf(' ');
        return lastSpaceIndex < 0 ? text : text[(lastSpaceIndex + 1)..];
    }

    public static string ApplyKeywordCompletion(string text, string keyword)
    {
        var lastSpaceIndex = text.LastIndexOf(' ');
        return lastSpaceIndex < 0
            ? keyword
            : string.Concat(text.AsSpan(0, lastSpaceIndex + 1), keyword);
    }

    public bool CommitCompletion(SearchCompletionItem? completion)
    {
        if (completion is null)
            return false;

        ++_searchCompletionUpdateVersion;
        ClearSearchCompletionItems();
        _searchCompletionSource = null;

        switch (completion.Kind)
        {
            case SearchCompletionKind.OpenIllustration:
                if (TryGetSearchId(out var illustId))
                {
                    _navigationService.NavigateToIllustration(illustId.ToString(), PlatformConstants.Pixiv);
                    return true;
                }
                return false;

            case SearchCompletionKind.OpenNovel:
                if (TryGetSearchId(out var novelId))
                {
                    _navigationService.NavigateToNovel(novelId);
                    return true;
                }
                return false;

            case SearchCompletionKind.OpenUser:
                if (TryGetSearchId(out var userId))
                {
                    _navigationService.NavigateToUser(userId);
                    return true;
                }
                return false;

            case SearchCompletionKind.SearchUser:
                _navigationService.NavigateToUserSearch(SearchText?.Trim());
                return true;

            case SearchCompletionKind.Tag:
                SearchText = ApplyKeywordCompletion(SearchText ?? "", completion.Text);
                return true;

            default:
                throw new ArgumentOutOfRangeException(nameof(completion));
        }
    }

    private bool TryGetSearchId(out long id) => long.TryParse(SearchText?.Trim(), out id);

    [RelayCommand]
    public void Search(bool advanced = false)
    {
        var searchText = SearchText?.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            _notificationService.ShowWarning(
                I18NManager.GetResource(MainPageResources.SearchKeywordCannotBeBlank.Title),
                I18NManager.GetResource(MainPageResources.SearchKeywordCannotBeBlank.Content));
            return;
        }

        try
        {
            if (!advanced)
            {
                _searchHistory.Add(searchText);
                _navigationService.NavigateToWorkSearch(searchText, SelectedAdvancedOptionsType);
                return;
            }

            if (SelectedAdvancedOptionsType is SimpleWorkType.Novel)
            {
                if (!NovelForm.TryValidate(out var title, out var content))
                {
                    _notificationService.ShowWarning(title, content);
                    return;
                }

                var arguments = NovelForm.BuildArguments(searchText);
                _searchHistory.Add(searchText);
                _navigationService.NavigateToWorkSearch(arguments);
            }
            else
            {
                if (!IllustrationForm.TryValidate(out var title, out var content))
                {
                    _notificationService.ShowWarning(title, content);
                    return;
                }

                var arguments = IllustrationForm.BuildArguments(searchText);
                _searchHistory.Add(searchText);
                _navigationService.NavigateToWorkSearch(arguments);
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowError(I18NManager.GetResource(SearchResources.Validation.SearchFailed.Title), ex.Message);
        }
    }

    [RelayCommand]
    public async Task CopyAsync(object? parameter)
    {
        var value = parameter switch
        {
            SearchHistoryRecord entry => entry.Value,
            string tag => tag,
            _ => null
        };
        if (value is null)
            return;

        await _clipboardService.SetTextAsync(value);
        _notificationService.ShowSuccess(I18NManager.GetResource(MiscResources.Copied));
    }

    [RelayCommand]
    public void AddPinnedTag(SearchHistoryRecord? entry)
    {
        if (entry is not { } record)
            return;

        var pinnedTags = _appSettings.BrowsingExperienceSettings.PinnedTags;
        if (!pinnedTags.Contains(record.Value))
            pinnedTags.Add(record.Value);
        _ = _searchHistory.Entries.Remove(record);
    }

    [RelayCommand]
    public void Unpin(string? tag)
    {
        if (string.IsNullOrEmpty(tag))
            return;

        if (_appSettings.BrowsingExperienceSettings.PinnedTags.Remove(tag))
            _searchHistory.Add(tag);
    }

    [RelayCommand]
    public void DeleteHistory(SearchHistoryRecord? entry)
    {
        if (entry is not { } record)
            return;

        _ = _searchHistory.Entries.Remove(record);
    }

    [RelayCommand]
    public void OpenTrendingTag(TrendingTag? tag)
    {
        if (tag is not { } t)
            return;

        _navigationService.NavigateToWorkSearch(t.Tag, SelectedTrendingTagsType);
    }

    [RelayCommand]
    public void OpenHistory(SearchHistoryRecord? entry)
    {
        if (entry is not { } record)
            return;

        SearchText = record.Value;
        Search(false);
    }

    [RelayCommand]
    public void OpenPinnedTag(string? tag)
    {
        if (string.IsNullOrEmpty(tag))
            return;

        SearchText = tag;
        Search(false);
    }
}
