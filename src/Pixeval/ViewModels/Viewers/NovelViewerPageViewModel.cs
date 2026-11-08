using System.Collections.ObjectModel;
using Pixeval.Views.Viewers;
// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using AutoSettingsPage;
using AutoSettingsPage.Avalonia;
using AutoSettingsPage.Models;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Config;
using Pixeval.Localization;
using Pixeval.Pixiv;
using Pixeval.Mako;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Utilities;

namespace Pixeval.ViewModels.Viewers;

public sealed partial class NovelViewerPageViewModel : PagedViewerViewModel, IDisposable
{
    private readonly MakoClient _makoClient;
    private readonly AppSettings _appSettings;
    private readonly StorageEngine _storageEngine;
    private readonly IBlockedContentService? _blockedContent;

    private readonly Dictionary<int, Mako.Novel> _refreshedNovels = [];

    private readonly bool _needRefresh;

    private readonly IReadOnlyList<Mako.Novel>? _novels;
    private readonly long _initialNovelId;
    private readonly ObservableCollection<Page> _panePages = [];
    private Mako.Novel? _paneNovel;

    public IReadOnlyList<Page> PanePages
    {
        get
        {
            var current = CurrentNovel;
            if (_disposed || Equals(current, _paneNovel))
                return _panePages;
            _paneNovel = current;
            if (current is not { } novel)
            {
                _panePages.Clear();
                return _panePages;
            }
            if (_panePages.Count == 0)
                _panePages.Add(new WorkInfoPage(novel));
            else if (_panePages[0] is WorkInfoPage info)
                info.Work = novel;
            if (BlockedContentHelper.IsBlockedPlaceholder(novel))
            {
                while (_panePages.Count > 1)
                    _panePages.RemoveAt(_panePages.Count - 1);
                return _panePages;
            }
            var comments = new CommentsPage { ParentType = SimpleWorkKind.Novel, ParentId = novel.Id };
            var related = new WorkRelatedPage(novel.Id, SimpleWorkKind.Novel, _makoClient) { IsCommandBarCollapsed = true };
            if (_panePages.Count == 1)
            {
                _panePages.Add(comments);
                _panePages.Add(related);
                _panePages.Add(SettingsPage);
            }
            else
            {
                _panePages[1] = comments;
                _panePages[2] = related;
            }
            return _panePages;
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleTranslationCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleTranslationCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial string? LoadErrorMessage { get; private set; }

    [ObservableProperty]
    public partial WorkSeriesInfoViewModel? SeriesInfo { get; private set; }

    public NovelViewerPageViewModel(
        Mako.Novel novelViewModel,
        bool needRefresh,
        MakoClient makoClient,
        AppSettings appSettings,
        StorageEngine storageEngine,
        IBlockedContentService? blockedContent = null)
    {
        _makoClient = makoClient;
        _appSettings = appSettings;
        _storageEngine = storageEngine;
        _blockedContent = blockedContent;
        _needRefresh = needRefresh;
        CurrentNovel = novelViewModel;
        CurrentWorkIndex = 0;
    }

    public NovelViewerPageViewModel(
        long id,
        MakoClient makoClient,
        AppSettings appSettings,
        StorageEngine storageEngine,
        IBlockedContentService? blockedContent = null)
    {
        _makoClient = makoClient;
        _appSettings = appSettings;
        _storageEngine = storageEngine;
        _blockedContent = blockedContent;
        _initialNovelId = id;
        _ = LoadSingleNovelAsync(id, _loadingCts.Token);
    }

    public NovelViewerPageViewModel(
        IReadOnlyList<Mako.Novel> novels,
        int currentNovelIndex,
        bool needRefresh,
        MakoClient makoClient,
        AppSettings appSettings,
        StorageEngine storageEngine,
        IBlockedContentService? blockedContent = null)
    {
        _makoClient = makoClient;
        _appSettings = appSettings;
        _storageEngine = storageEngine;
        _blockedContent = blockedContent;
        _needRefresh = needRefresh;
        _novels = novels;
        CurrentWorkIndex = currentNovelIndex;
    }

    public IReadOnlyList<Mako.Novel>? Novels => _novels;

    public Mako.Novel? CurrentNovel
    {
        get
        {
            if (_refreshedNovels.TryGetValue(CurrentWorkIndex, out var value))
                return value;

            if (field is not null)
                return field;

            return CurrentWorkIndex < 0 || CurrentWorkIndex >= WorkCount
                ? null
                : _novels?[CurrentWorkIndex];
        }
        private set
        {
            if (Equals(value, field))
                return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NovelId));
            OnPropertyChanged(nameof(CurrentUiState));
        }
    }

    public ArtworkUiState? CurrentUiState => CurrentNovel is { } nov ? ArtworkUiStateStore.GetOrCreate(nov) : null;

    public long NovelId => CurrentNovel is { Id: var id } ? id : 0;

    public override int CurrentWorkIndex
    {
        get;
        set
        {
            if (_disposed || value < 0 || value >= WorkCount)
                return;
            if (value == field)
                return;

            field = value;
            _ = LoadCurrentNovelAsync();

            OnPropertyChanged();
            OnPropertyChanged(nameof(NovelId));
            OnPropertyChanged(nameof(CurrentNovel));
        }
        // 第一次赋值属性时会判断 value == field，如果是0则无法进入set方法体
        // ReSharper disable once MemberInitializerValueIgnored
    } = -1;

    private bool _suppressMarkerSync;
    private CancellationTokenSource? _syncMarkerCts;

    public override int CurrentPageIndex
    {
        get;
        set
        {
            if (_disposed || value < 0 || value >= Math.Max(1, PageCount))
                return;
            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentNovel));
            OnPropertyChanged(nameof(CurrentMarkdown));
            OnPropertyChanged(nameof(NextButtonText));
            OnPropertyChanged(nameof(PrevButtonText));
            NextCommand.NotifyCanExecuteChanged();
            PrevCommand.NotifyCanExecuteChanged();
            NextWorkCommand.NotifyCanExecuteChanged();
            PrevWorkCommand.NotifyCanExecuteChanged();

            if (!_suppressMarkerSync && CurrentNovel is { Id: var novelId } && novelId > 0 && PageCount > 0)
            {
                SyncMarkerDebounced(novelId, value);
            }
        }
    }

    private void SyncMarkerDebounced(long novelId, int pageIndex)
    {
        _syncMarkerCts?.Cancel();
        _syncMarkerCts?.Dispose();
        var cts = new CancellationTokenSource();
        _syncMarkerCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1500, cts.Token);
                if (cts.Token.IsCancellationRequested || _disposed)
                    return;

                await _makoClient.AddNovelMarkerAsync(novelId, pageIndex + 1);
            }
            catch
            {
                // Silent cloud sync - non-intrusive
            }
        }, cts.Token);
    }

    public override int PageCount => _pageMarkdowns.Count;

    /// <inheritdoc />
    public override int WorkCount => Novels?.Count ?? 1;

    public bool IsMultiPage => PageCount > 1;

    public string CurrentMarkdown => CurrentNovel is { } novel && BlockedContentHelper.IsBlockedPlaceholder(novel)
        ? Strings.BlockedContentWork()
        : PageCount is 0 ? "" : (IsTranslationShown ? _translatedPageMarkdowns! : _pageMarkdowns)[CurrentPageIndex];

    #region Settings

    internal AppSettings AppSettings => _appSettings;

    private AppSettings Settings => _appSettings;
    private ObservableCollection<string>? _subscribedNovelFontFamily;
    private SettingsSubView? _settingsPage;
    private readonly List<INotifyPropertyChanged> _readingSettingsEntries = [];

    private SettingsSubView SettingsPage
    {
        get
        {
            if (_settingsPage is not null)
                return _settingsPage;

            LocalSettingsEntryHelper.Initialize();
            var group = SettingsBuilder.CreateGroupList(_appSettings)
                .NewGroup(t => t.NovelSettings, entries => entries
                    .Color(t => t.NovelBackground)
                    .Color(t => t.NovelFontColor)
                    .Font(t => t.NovelFontFamily)
                    .Enum(t => t.NovelFontWeight)
                    .Int(t => t.NovelFontSize, 5, 100, 1)
                    .Int(t => t.NovelLineHeight, 0, 150, 1)
                    .Int(t => t.NovelMaxWidth, 50, 10000, 50))
                .Build()[0];
            if (!_disposed)
            {
                foreach (var entry in group)
                    if (entry is INotifyPropertyChanged observable)
                    {
                        observable.PropertyChanged += OnReadingSettingChanged;
                        _readingSettingsEntries.Add(observable);
                    }
                ObserveNovelFonts();
            }
            return _settingsPage = new SettingsSubView(group) { Classes = { "compact" } };
        }
    }

    private void OnReadingSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || e.PropertyName is not (null or "Value") || sender is not ISettingsEntry entry)
            return;

        if (entry.Token == nameof(NovelSettingsGroup.NovelFontFamily))
        {
            ObserveNovelFonts();
            OnPropertyChanged(nameof(NovelFontFamilyObject));
        }
        else
            OnPropertyChanged(entry.Token);
    }

    private void ObserveNovelFonts()
    {
        var fonts = Settings.NovelSettings.NovelFontFamily;
        if (ReferenceEquals(fonts, _subscribedNovelFontFamily))
            return;
        if (_subscribedNovelFontFamily is { } previous)
            previous.CollectionChanged -= OnNovelFontFamilyChanged;
        _subscribedNovelFontFamily = fonts;
        fonts.CollectionChanged += OnNovelFontFamilyChanged;
    }

    private void OnNovelFontFamilyChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(NovelFontFamilyObject));

    public uint NovelBackground => Settings.NovelSettings.NovelBackground;

    public uint NovelFontColor => Settings.NovelSettings.NovelFontColor;

    public FontFamily? NovelFontFamilyObject => FontFamilyHelper.Create(Settings.NovelSettings.NovelFontFamily);

    public FontWeight NovelFontWeight => Settings.NovelSettings.NovelFontWeight;

    public int NovelFontSize => Settings.NovelSettings.NovelFontSize;

    public int NovelLineHeight => Settings.NovelSettings.NovelLineHeight;

    public int NovelMaxWidth => Settings.NovelSettings.NovelMaxWidth;

    public void NotifyNovelBackgroundChanged() => OnPropertyChanged(nameof(NovelBackground));
    public void NotifyNovelFontColorChanged() => OnPropertyChanged(nameof(NovelFontColor));
    public void NotifyNovelFontFamilyChanged() => OnPropertyChanged(nameof(NovelFontFamilyObject));
    public void NotifyNovelFontWeightChanged() => OnPropertyChanged(nameof(NovelFontWeight));
    public void NotifyNovelFontSizeChanged() => OnPropertyChanged(nameof(NovelFontSize));
    public void NotifyNovelLineHeightChanged() => OnPropertyChanged(nameof(NovelLineHeight));
    public void NotifyNovelMaxWidthChanged() => OnPropertyChanged(nameof(NovelMaxWidth));

    #endregion

    private List<string> _pageMarkdowns = [];

    private CancellationTokenSource _loadingCts = new();

    private bool CanRetry => !_disposed && !IsLoading && LoadErrorMessage is not null;

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync()
    {
        if (!CanRetry)
            return Task.CompletedTask;
        return CurrentNovel is null
            ? LoadSingleNovelAsync(_initialNovelId, ResetLoadingToken())
            : LoadCurrentNovelAsync();
    }

    private async Task LoadSingleNovelAsync(long id, CancellationToken token)
    {
        var novel = await LoadNovelAsync(id, item => CurrentNovel = item, token);
        if (novel is null || token.IsCancellationRequested)
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
            return;
        }

        if (CurrentWorkIndex is 0)
            await LoadCurrentNovelAsync();
        else
            CurrentWorkIndex = 0;
    }

    private async Task LoadCurrentNovelAsync()
    {
        if (_disposed)
            return;

        var index = CurrentWorkIndex;
        var token = ResetLoadingToken();

        IsLoading = true;
        _translatedPageMarkdowns = null;
        OnPropertyChanged(nameof(IsTranslationAvailable));
        _pageMarkdowns = [];
        IsTranslationShown = false;
        CurrentPageIndex = 0;
        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(IsMultiPage));
        OnPropertyChanged(nameof(TranslationButtonText));
        LoadErrorMessage = null;
        SeriesInfo = null;
        try
        {
            var currentNovel = await GetCurrentNovelAsync(index, token);
            token.ThrowIfCancellationRequested();
            if (currentNovel is null || index != CurrentWorkIndex || _disposed)
                return;

            var novel = currentNovel.Value;
            if (BlockedContentHelper.IsBlockedPlaceholder(novel))
                _pageMarkdowns = [Strings.BlockedContentWork()];
            else
            {
                SeriesInfo = WorkSeriesInfoViewModel.Create(novel, SimpleWorkKind.Novel);
                var content = await _makoClient.GetNovelContentCachedAsync(novel.Id, token);
                token.ThrowIfCancellationRequested();
                if (index != CurrentWorkIndex || _disposed)
                    return;

                SeriesInfo = WorkSeriesInfoViewModel.Create(content, novel.Series);
                _storageEngine.HistoryRepository.AddBrowseHistory(novel);
                var markdowns = await Task.Run(() => BuildPageMarkdowns(content), token);
                token.ThrowIfCancellationRequested();
                if (index != CurrentWorkIndex || _disposed)
                    return;

                _pageMarkdowns = markdowns;

                var targetPage = 0;
                if (content.Marker is { Page: > 0 } marker && marker.Page <= markdowns.Count)
                {
                    targetPage = marker.Page - 1;
                }
                else
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var markers = await _makoClient.GetNovelMarkersAsync();
                            var match = markers.MarkedNovels.FirstOrDefault(x => x.Novel.Id == novel.Id);
                            if (match is { NovelMarker: { Page: > 0 and var page } } && page <= _pageMarkdowns.Count)
                            {
                                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    if (!_disposed && CurrentNovel is { Id: var curId } && curId == novel.Id && CurrentPageIndex == 0)
                                    {
                                        _suppressMarkerSync = true;
                                        CurrentPageIndex = page - 1;
                                        _suppressMarkerSync = false;
                                    }
                                });
                            }
                        }
                        catch
                        {
                            // Silent
                        }
                    });
                }

                _suppressMarkerSync = true;
                CurrentPageIndex = targetPage;
                _suppressMarkerSync = false;
            }
            OnPropertyChanged(nameof(PageCount));
            OnPropertyChanged(nameof(IsMultiPage));
            OnPropertyChanged(nameof(CurrentNovel));
            OnPropertyChanged(nameof(NovelId));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested)
                LoadErrorMessage = e.Message;
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
        }

        return;

        static List<string> BuildPageMarkdowns(NovelContent content)
        {
            var pages = content.RenderMarkdownPages();
            return pages as List<string> ?? [.. pages];
        }
    }

    private async Task<Mako.Novel?> GetCurrentNovelAsync(int index, CancellationToken token)
    {
        if (!_needRefresh)
            return CurrentNovel;

        if (_novels is not null && _refreshedNovels.TryGetValue(index, out var cached))
            return cached;

        if (CurrentNovel is not { Id: var id })
            return null;

        return await LoadNovelAsync(
            id,
            novel =>
            {
                // 单项刷新写回当前项，列表刷新只缓存到当前 Viewer，避免污染原始列表。
                if (_novels is null)
                    CurrentNovel = novel;
                else
                    _refreshedNovels[index] = novel;
            },
            token);
    }

    private CancellationToken ResetLoadingToken()
    {
        _loadingCts.Cancel();
        _loadingCts.Dispose();
        _loadingCts = new CancellationTokenSource();
        return _loadingCts.Token;
    }

    private async Task<Mako.Novel?> LoadNovelAsync(long id, Action<Mako.Novel> onLoaded, CancellationToken token)
    {
        IsLoading = true;
        LoadErrorMessage = null;
        try
        {
            var loaded = await _makoClient.GetNovelAsync(id, token);
            var novel = _blockedContent?.Replace(loaded) ?? loaded;
            token.ThrowIfCancellationRequested();
            if (novel.Id != id)
                throw new InvalidOperationException(Strings.EntryViewerPageLoadFailed());
            onLoaded(novel);
            return novel;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested)
                LoadErrorMessage = e.Message;
            return null;
        }
    }


    #region Dispose

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ToggleTranslationCommand.NotifyCanExecuteChanged();
        IsLoading = false;
        _syncMarkerCts?.Cancel();
        _syncMarkerCts?.Dispose();
        _loadingCts.Cancel();
        _loadingCts.Dispose();
        RetryCommand.NotifyCanExecuteChanged();
        foreach (var entry in _readingSettingsEntries)
            entry.PropertyChanged -= OnReadingSettingChanged;
        _readingSettingsEntries.Clear();
        if (_subscribedNovelFontFamily is { } novelFontFamily)
        {
            novelFontFamily.CollectionChanged -= OnNovelFontFamilyChanged;
            _subscribedNovelFontFamily = null;
        }


    }

    #endregion
}
