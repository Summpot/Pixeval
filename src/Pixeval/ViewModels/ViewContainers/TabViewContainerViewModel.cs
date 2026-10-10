// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Models;
using Pixeval.Models.Navigation;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities.IO.Caching;

namespace Pixeval.ViewModels;

public partial class TabViewContainerViewModel : ViewModelBase, IDisposable
{
    private readonly IUserSessionService _sessionService;
    private readonly MakoClient _makoClient;
    private readonly INavigationService _navigationService;
    private readonly NavigationMenuDocument _navigationMenu;
    private NavigationConfiguration? _navigationConfiguration;
    private CancellationTokenSource? _avatarLoadCancellationTokenSource;
    private bool _isDisposed;

    public ObservableCollection<NavigationMenuItem> HeaderNavigationItems { get; } = [];

    public ObservableCollection<NavigationMenuItem> FooterNavigationItems { get; } = [];

    [ObservableProperty]
    public partial bool CanCreateNewTab { get; private set; }

    public TabViewContainerViewModel() : this(
        App.Services!.GetRequiredService<IUserSessionService>(),
        App.Services!.GetRequiredService<MakoClient>(),
        App.Services!.GetRequiredService<INavigationService>(),
        App.Services!.GetRequiredService<NavigationMenuDocument>())
    {
    }

    public TabViewContainerViewModel(
        IUserSessionService sessionService,
        MakoClient makoClient,
        INavigationService navigationService,
        NavigationMenuDocument navigationMenu)
    {
        _sessionService = sessionService;
        _makoClient = makoClient;
        _navigationService = navigationService;
        _navigationMenu = navigationMenu;
        RebuildNavigation();
        OnUserRefreshed(_sessionService.CurrentUser);
        _sessionService.UserRefreshed += OnUserRefreshed;
    }

    public void RebuildNavigation()
    {
        _navigationConfiguration = NavigationYamlParser.ParseOrDefault(_navigationMenu.Text);
        HeaderNavigationItems.Clear();
        FooterNavigationItems.Clear();
        foreach (var item in _navigationConfiguration.HeaderItems)
            HeaderNavigationItems.Add(item);
        foreach (var item in _navigationConfiguration.FooterItems)
            FooterNavigationItems.Add(item);
        CanCreateNewTab = _navigationConfiguration.NewTabPage is not null;
    }

    [RelayCommand]
    public void OpenNavigationItem(Control? control) => _ = TryOpenNavigationItem(control, openNew: false);

    public bool TryOpenNavigationItem(Control? control, bool openNew)
    {
        if (control is not { DataContext: NavigationPageItem { PageType: { } type } })
            return false;

        if (!openNew && _navigationService.TrySelectExisting(type, control))
            return true;

        _navigationService.NavigateTo(type, null, false, control);
        return true;
    }

    [RelayCommand]
    public void CreateNewTab()
    {
        if (_navigationConfiguration?.NewTabPage is { PageType: { } type })
            _navigationService.NavigateTo(type);
    }

    [RelayCommand]
    public void OpenMyPage()
    {
        var myId = _sessionService.CurrentUserId;
        if (myId <= 0)
        {
            _navigationService.NavigateToLogin();
            return;
        }

        _navigationService.NavigateToUser(myId);
    }

    [RelayCommand]
    public void SwitchAccount()
    {
        _navigationService.NavigateToLogin();
    }

    private async void OnUserRefreshed(TokenUser? user)
    {
        if (_isDisposed)
            return;

        _avatarLoadCancellationTokenSource?.Cancel();
        _avatarLoadCancellationTokenSource?.Dispose();
        var cancellationTokenSource = new CancellationTokenSource();
        _avatarLoadCancellationTokenSource = cancellationTokenSource;
        User = user;
        IAnimatedBitmap? avatar = null;
        try
        {
            var avatarUrl = user?.ProfileImageUrls.Px50x50 ?? user?.ProfileImageUrls.Medium;
            if (!string.IsNullOrWhiteSpace(avatarUrl))
            {
                avatar = await CacheHelper.GetAnimatedBitmapAsync(
                    PlatformConstants.Pixiv,
                    avatarUrl,
                    token: cancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            avatar = null;
        }

        if (_isDisposed || cancellationTokenSource.IsCancellationRequested)
        {
            avatar?.Dispose();
            return;
        }

        var previousAvatar = Avatar;
        Avatar = avatar;
        if (!ReferenceEquals(previousAvatar, avatar))
            previousAvatar?.Dispose();
        if (user is null)
        {
            RestrictedCache = false;
            RestrictedModeIdle = true;
            AiShowCache = false;
            AiShowIdle = true;
            return;
        }

        await RefreshAiShowAsync(cancellationTokenSource);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed)
            return;

        _isDisposed = true;
        _sessionService.UserRefreshed -= OnUserRefreshed;
        _avatarLoadCancellationTokenSource?.Cancel();
        _avatarLoadCancellationTokenSource?.Dispose();
        _avatarLoadCancellationTokenSource = null;
        Avatar?.Dispose();
        Avatar = null;
        User = null;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IdText))]
    [NotifyPropertyChangedFor(nameof(Url))]
    public partial TokenUser? User { get; private set; }

    [ObservableProperty] public partial IAnimatedBitmap? Avatar { get; private set; }

    public string? IdText => User?.Id;

    public Uri? Url => User is not null ? new Uri($"https://www.pixiv.net/users/{User.Id}") : null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleRestrictedModeCommand))]
    public partial bool RestrictedModeIdle { get; private set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleAiShowCommand))]
    public partial bool AiShowIdle { get; private set; } = true;

    [ObservableProperty]
    public partial bool RestrictedCache { get; private set; }

    [ObservableProperty] public partial bool AiShowCache { get; private set; }

    [RelayCommand(CanExecute = nameof(AiShowIdle))]
    private Task ToggleAiShowAsync() => ToggleAiShowAsync(false);

    [RelayCommand(CanExecute = nameof(RestrictedModeIdle))]
    private Task ToggleRestrictedModeAsync() => ToggleRestrictedModeAsync(false);

    private async Task ToggleRestrictedModeAsync(bool skipPost)
    {
        if (!RestrictedModeIdle)
            return;
        RestrictedModeIdle = false;
        try
        {
            RestrictedCache = skipPost
                ? (await _makoClient.GetRestrictedModeSettingsAsync()).IsRestrictedModeEnabled
                : (await _makoClient.PostRestrictedModeSettingsAsync(!RestrictedCache)).IsRestrictedModeEnabled;
        }
        finally
        {
            RestrictedModeIdle = true;
        }
    }

    private async Task ToggleAiShowAsync(bool skipPost)
    {
        if (!AiShowIdle)
            return;
        AiShowIdle = false;
        try
        {
            AiShowCache = skipPost
                ? (await _makoClient.GetAiShowSettingsAsync()).ShowAi
                : (await _makoClient.PostAiShowSettingsAsync(!AiShowCache)).ShowAi;
        }
        finally
        {
            AiShowIdle = true;
        }
    }

    private async Task RefreshAiShowAsync(CancellationTokenSource generation)
    {
        if (!IsCurrentGeneration(generation))
            return;

        AiShowIdle = false;
        try
        {
            var aiShow = await _makoClient.GetAiShowSettingsAsync();
            if (IsCurrentGeneration(generation))
                AiShowCache = aiShow.ShowAi;
        }
        finally
        {
            if (IsCurrentGeneration(generation))
                AiShowIdle = true;
        }
    }

    private bool IsCurrentGeneration(CancellationTokenSource generation) =>
        !_isDisposed
        && !generation.IsCancellationRequested
        && ReferenceEquals(_avatarLoadCancellationTokenSource, generation);
}
