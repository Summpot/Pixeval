// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement;
using Pixeval.I18N;
using Pixeval.Models;
using Pixeval.Models.Blocking;
using Pixeval.Models.Options;
using Pixeval.Models.Pixiv;
using Pixeval.AppManagement.Settings;
using Pixeval.Native.Booru;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Viewers;

namespace Pixeval.ViewModels.Viewers;

public sealed partial class IllustrationViewerPageViewModel : PagedViewerViewModel, IDisposable, IIllustrationAutoPlayTarget
{
    private readonly MakoClient _makoClient;
    private readonly AppSettings _appSettings;
    private readonly FileLogger? _logger;
    private readonly Dictionary<int, object> _refreshedIllustrations = [];

    private readonly bool _needRefresh;

    private CancellationTokenSource _loadingCts = new();

    private readonly IReadOnlyList<object>? _illustrations;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? LoadErrorMessage { get; private set; }

    [ObservableProperty]
    public partial WorkSeriesInfoViewModel? SeriesInfo { get; private set; }

    public string? LogoUri => CurrentIllustration switch
    {
        Illustration => $"avares://Pixeval/Assets/Platforms/{PlatformConstants.Pixiv}.png",
        BooruPost bp => $"avares://Pixeval/Assets/Platforms/{bp.Platform}.png",
        _ => null
    };

    public ArtworkUiState? CurrentUiState => CurrentIllustration is { } ill ? ArtworkUiStateStore.GetOrCreate(ill) : null;

    public string? Title => CurrentIllustration switch
    {
        Illustration ill => ill.Title,
        BooruPost bp => bp.Title,
        _ => null
    };

    public Uri? WebsiteUri => CurrentIllustration switch
    {
        Illustration ill => ill.WebsiteUri,
        BooruPost bp => bp.WebsiteUri,
        _ => null
    };

    public bool IsBookmarkSupported => CurrentIllustration is Illustration ill && ill.IsBookmarkSupported;

    public bool IsPicGif => CurrentIllustration is Illustration { IsPicGif: true };

    /// <summary>
    /// 
    /// </summary>
    /// <param name="illustrationViewModel"></param>
    /// <param name="needRefresh"></param>
    public IllustrationViewerPageViewModel(
        object illustrationViewModel,
        bool needRefresh,
        MakoClient? makoClient = null,
        AppSettings? appSettings = null,
        FileLogger? logger = null)
    {
        _makoClient = makoClient ?? App.Services!.GetRequiredService<MakoClient>();
        _appSettings = appSettings ?? App.Services!.GetRequiredService<AppSettings>();
        _logger = logger ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<FileLogger>(App.Services!);
        _needRefresh = needRefresh;
        CurrentIllustration = illustrationViewModel;
        CurrentWorkIndex = 0;
    }

    public IllustrationViewerPageViewModel(
        string id,
        string platform,
        MakoClient? makoClient = null,
        AppSettings? appSettings = null,
        FileLogger? logger = null)
    {
        _makoClient = makoClient ?? App.Services!.GetRequiredService<MakoClient>();
        _appSettings = appSettings ?? App.Services!.GetRequiredService<AppSettings>();
        _logger = logger ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<FileLogger>(App.Services!);
        _ = LoadSingleIllustrationAsync(id, platform, _loadingCts.Token);
    }

    private async Task LoadSingleIllustrationAsync(string id, string platform, CancellationToken token)
    {
        var illustration = await LoadIllustrationAsync(id, platform, item => CurrentIllustration = item, token);

        if (illustration is not null && !token.IsCancellationRequested)
            CurrentWorkIndex = 0;
    }

    /// <summary>
    /// 当拥有独立DataProvider引用的时候调用这个构造函数，dispose的时候会自动dispose掉DataProvider
    /// </summary>
    /// <param name="dataProvider"></param>
    /// <param name="currentIllustrationIndex"></param>
    /// <param name="needRefresh"></param>
    /// <param name="makoClient"></param>
    /// <param name="appSettings"></param>
    /// <param name="logger"></param>
    /// <remarks>
    /// illustrations should contain only one item if the illustration is a single
    /// otherwise it contains the entire manga data
    /// </remarks>
    public IllustrationViewerPageViewModel(
        IReadOnlyList<object> illustrations,
        int currentIllustrationIndex,
        bool needRefresh,
        MakoClient? makoClient = null,
        AppSettings? appSettings = null,
        FileLogger? logger = null)
    {
        _makoClient = makoClient ?? App.Services!.GetRequiredService<MakoClient>();
        _appSettings = appSettings ?? App.Services!.GetRequiredService<AppSettings>();
        _logger = logger ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<FileLogger>(App.Services!);
        _needRefresh = needRefresh;
        _illustrations = illustrations;
        CurrentWorkIndex = currentIllustrationIndex;
    }

    #region Current相关

    /// <summary>
    /// 当前插画
    /// </summary>
    public object? CurrentIllustration
    {
        get
        {
            if (_refreshedIllustrations.TryGetValue(CurrentWorkIndex, out var value))
                return value;

            if (field is not null)
                return field;

            return CurrentWorkIndex < 0 || CurrentWorkIndex >= WorkCount
                ? null
                : _illustrations?[CurrentWorkIndex];
        }
        private set
        {
            if (Equals(value, field))
                return;
            field = value;
            NotifyCurrentIllustrationChanged();
        }
    }

    /// <summary>
    /// 当前图集的ViewModel
    /// </summary>
    public ImageViewerViewModel? CurrentImage
    {
        get;
        private set
        {
            if (field == value)
                return;
            field?.PropertyChanged -= CurrentImageOnPropertyChanged;
            field?.Dispose();
            field = value;
            field?.PropertyChanged += CurrentImageOnPropertyChanged;
            OnPropertyChanged();
            ResetCurrentPageIndex();
            OnPropertyChanged(nameof(PageCount));
            NotifyCurrentIllustrationChanged();
        }
    } = null!;

    /// <summary>
    /// 当前插画的索引
    /// </summary>
    public override int CurrentWorkIndex
    {
        get;
        set
        {
            if (value is -1)
                return;
            if (value == field)
                return;

            field = value;

            _ = LoadCurrentIllustrationAsync();
            OnPropertyChanged();
            NotifyCurrentIllustrationChanged();
            NotifyPageNavigationChanged();
        }
        // 第一次赋值属性时会判断 value == field，如果是0则无法进入set方法体
        // ReSharper disable once MemberInitializerValueIgnored
    } = -1;

    private async Task LoadCurrentIllustrationAsync()
    {
        if (_disposed)
            return;

        var index = CurrentWorkIndex;
        var token = ResetLoadingToken();
        LoadErrorMessage = null;
        SeriesInfo = null;
        CurrentImage = null;

        if (await GetCurrentIllustrationAsync(index) is { } currentIllustration
            && !token.IsCancellationRequested
            && !_disposed
            && index == CurrentWorkIndex)
        {
            CurrentImage = new ImageViewerViewModel(currentIllustration);
            await LoadSeriesInfoAsync(currentIllustration, index, token);
        }

        return;

        async ValueTask<object?> GetCurrentIllustrationAsync(int workIndex)
        {
            if (!_needRefresh)
            {
                IsLoading = false;
                return CurrentIllustration;
            }

            if (_illustrations is not null && _refreshedIllustrations.TryGetValue(workIndex, out var cached))
            {
                IsLoading = false;
                return cached;
            }

            // 需刷新
            (string? platform, string? id) = CurrentIllustration switch
            {
                Illustration ill => (PlatformConstants.Pixiv, ill.Id.ToString()),
                BooruPost bp => (bp.PlatformName, bp.Id),
                _ => (null, null)
            };

            if (platform is null || id is null)
            {
                IsLoading = false;
                return null;
            }

            return await LoadIllustrationAsync(
                id,
                platform,
                item =>
                {
                    // 单项刷新写回当前项，列表刷新只缓存到当前 Viewer，避免污染原始列表。
                    if (_illustrations is null)
                        CurrentIllustration = item;
                    else
                        _refreshedIllustrations[workIndex] = item;
                },
                token);
        }
    }

    private async Task LoadSeriesInfoAsync(object entry, int index, CancellationToken token)
    {
        SeriesInfo = WorkSeriesInfoViewModel.Create(entry, SimpleWorkType.Illustration);
        if (entry is not Illustration { Series: not null } illustration)
            return;

        try
        {
            var response = await _makoClient.GetMangaSeriesContextAsync(illustration.Id);
            token.ThrowIfCancellationRequested();
            if (index == CurrentWorkIndex && !_disposed)
                SeriesInfo = WorkSeriesInfoViewModel.Create(response);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _logger?.LogError(nameof(LoadSeriesInfoAsync), e);
        }
    }

    private CancellationToken ResetLoadingToken()
    {
        _loadingCts.Cancel();
        _loadingCts.Dispose();
        _loadingCts = new();
        return _loadingCts.Token;
    }

    private async Task<object?> LoadIllustrationAsync(string id, string platform, Action<object> onLoaded, CancellationToken token)
    {
        IsLoading = true;
        LoadErrorMessage = null;
        try
        {
            var entry = await ViewerHelper.TryGetArtworkAsync(platform, id);
            token.ThrowIfCancellationRequested();
            if (entry is null)
            {
                LoadErrorMessage = I18NManager.GetResource(EntryViewerPageResources.LoadFailed);
                return null;
            }

            var item = BlockedContentHelper.Replace(entry);
            onLoaded(item);
            return item;
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
        finally
        {
            if (!token.IsCancellationRequested)
                IsLoading = false;
        }
    }

    /// <summary>
    /// 当前插画的页面索引
    /// </summary>
    public override int CurrentPageIndex
    {
        get => CurrentImage?.SelectedPageIndex ?? 0;
        set
        {
            if (CurrentImage is null)
                return;

            CurrentImage.SelectedPageIndex = value;
            // 不检查值是否变化，强制触发更新事件
            NotifyCurrentPageIndexChanged();
        }
    }

    private void ResetCurrentPageIndex()
    {
        CurrentImage?.SelectedPageIndex = 0;

        NotifyCurrentPageIndexChanged();
    }

    private void NotifyCurrentPageIndexChanged()
    {
        OnPropertyChanged(nameof(CurrentPageIndex));
        NotifyPageNavigationChanged();
    }

    private void NotifyCurrentIllustrationChanged()
    {
        OnPropertyChanged(nameof(CurrentIllustration));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(WebsiteUri));
        OnPropertyChanged(nameof(CurrentUiState));
        OnPropertyChanged(nameof(IsBookmarkSupported));
        OnPropertyChanged(nameof(IsPicGif));
        OnPropertyChanged(nameof(LogoUri));
    }

    private void NotifyPageNavigationChanged()
    {
        OnPropertyChanged(nameof(PrevButtonText));
        OnPropertyChanged(nameof(NextButtonText));
        PrevCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        PrevWorkCommand.NotifyCanExecuteChanged();
        NextWorkCommand.NotifyCanExecuteChanged();
    }

    private void CurrentImageOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImageViewerViewModel.SelectedPageIndex))
            CurrentPageIndex = CurrentImage?.SelectedPageIndex ?? 0;
    }

    public override int PageCount => CurrentImage?.PageCount ?? 1;

    public override int WorkCount => Illustrations?.Count ?? 1;

    /// <summary>
    /// 插画列表
    /// </summary>
    public IReadOnlyList<object>? Illustrations => _illustrations;

    #endregion

    #region AutoPlay

    public int AutoPlayInterval
    {
        get => _appSettings.BrowsingExperienceSettings.AutoPlay.IllustrationViewerAutoPlayInterval;
        set
        {
            value = int.Clamp(value, 1, 60);
            if (_appSettings.BrowsingExperienceSettings.AutoPlay.IllustrationViewerAutoPlayInterval == value)
                return;

            _appSettings.BrowsingExperienceSettings.AutoPlay.IllustrationViewerAutoPlayInterval = value;
            SaveAutoPlaySettings();
            OnPropertyChanged();
        }
    }

    public IllustrationViewerAutoPlayMode AutoPlayMode
    {
        get => _appSettings.BrowsingExperienceSettings.AutoPlay.IllustrationViewerAutoPlayMode;
        set
        {
            if (_appSettings.BrowsingExperienceSettings.AutoPlay.IllustrationViewerAutoPlayMode == value)
                return;

            _appSettings.BrowsingExperienceSettings.AutoPlay.IllustrationViewerAutoPlayMode = value;
            SaveAutoPlaySettings();
            OnPropertyChanged();
        }
    }

    public IllustrationViewerAutoPlayScope AutoPlayScope
    {
        get => _appSettings.BrowsingExperienceSettings.AutoPlay.IllustrationViewerAutoPlayScope;
        set
        {
            if (_appSettings.BrowsingExperienceSettings.AutoPlay.IllustrationViewerAutoPlayScope == value)
                return;

            _appSettings.BrowsingExperienceSettings.AutoPlay.IllustrationViewerAutoPlayScope = value;
            SaveAutoPlaySettings();
            OnPropertyChanged();
        }
    }

    [ObservableProperty] public partial bool IsAutoPlaying { get; set; }

    public void MoveAutoPlayNext()
    {
        switch (AutoPlayScope, AutoPlayMode)
        {
            case (IllustrationViewerAutoPlayScope.CurrentWork, IllustrationViewerAutoPlayMode.Sequential):
                if (CurrentPageIndex < PageCount - 1)
                    CurrentPageIndex++;
                else
                    IsAutoPlaying = false;
                break;
            case (IllustrationViewerAutoPlayScope.CurrentWork, IllustrationViewerAutoPlayMode.Loop):
                CurrentPageIndex = (CurrentPageIndex + 1) % PageCount;
                break;
            case (IllustrationViewerAutoPlayScope.AllWorks, IllustrationViewerAutoPlayMode.Sequential):
                if (NextAction is PagedBehavior.None)
                    IsAutoPlaying = false;
                else
                    NextCommand.Execute(null);
                break;
            case (IllustrationViewerAutoPlayScope.AllWorks, IllustrationViewerAutoPlayMode.Loop):
                if (NextAction is PagedBehavior.None)
                    CurrentWorkIndex = 0;
                else
                    NextCommand.Execute(null);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void SaveAutoPlaySettings() => AppInfo.SaveAppSettings(_appSettings);

    #endregion

    #region Dispose

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        IsLoading = false;
        _loadingCts.Cancel();
        _loadingCts.Dispose();
        IsAutoPlaying = false;
        CurrentImage = null!;
    }

    #endregion
}
