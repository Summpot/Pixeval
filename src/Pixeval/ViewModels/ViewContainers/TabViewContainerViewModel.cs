// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mako;
using Mako.Model;
using Misaki;
using Pixeval.Utilities.IO.Caching;

namespace Pixeval.ViewModels;

public partial class TabViewContainerViewModel : ViewModelBase, IDisposable
{
    private CancellationTokenSource? _avatarLoadCancellationTokenSource;
    private CancellationTokenSource? _accountLifetime;
    private bool _isAiShowRequestPending;
    private bool _isDisposed;

    public TabViewContainerViewModel()
    {
        OnUserRefreshed(App.AppViewModel.MakoClient.Me);
        App.AppViewModel.MakoClient.TokenRefreshed += OnTokenRefreshed;
    }

    private void OnTokenRefreshed(MakoClient sender, TokenResponse? e)
    {
        OnUserRefreshed(e?.User);
    }

    private async void OnUserRefreshed(TokenUser? user)
    {
        if (_isDisposed)
            return;

        if (User?.Id != user?.Id)
        {
            _accountLifetime?.Cancel();
            _accountLifetime?.Dispose();
            _accountLifetime = user is null ? null : new();
            _isAiShowRequestPending = false;
        }
        var accountLifetime = _accountLifetime;
        // OAuth renewal also refreshes this profile. It must not cancel the
        // settings request that triggered renewal or race it with another GET.
        var refreshAiShow = !_isAiShowRequestPending;
        if (refreshAiShow)
            AiShowIdle = user is null;
        _avatarLoadCancellationTokenSource?.Cancel();
        _avatarLoadCancellationTokenSource?.Dispose();
        var cancellationTokenSource = new CancellationTokenSource();
        _avatarLoadCancellationTokenSource = cancellationTokenSource;
        User = user;
        IAnimatedBitmap? avatar;
        try
        {
            avatar = user is null
                ? null
                : await CacheHelper.GetAnimatedBitmapAsync(
                    IPlatformInfo.Pixiv,
                    user.ProfileImageUrls.Px50X50,
                    token: cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            return;
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

        // await ToggleRestrictedModeAsync(true);
        if (refreshAiShow && accountLifetime is not null)
            await RefreshAiShowAsync(accountLifetime);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed)
            return;

        _isDisposed = true;
        App.AppViewModel.MakoClient.TokenRefreshed -= OnTokenRefreshed;
        _avatarLoadCancellationTokenSource?.Cancel();
        _avatarLoadCancellationTokenSource?.Dispose();
        _avatarLoadCancellationTokenSource = null;
        _accountLifetime?.Cancel();
        _accountLifetime?.Dispose();
        _accountLifetime = null;
        Avatar?.Dispose();
        Avatar = null;
        User = null;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IdText))]
    [NotifyPropertyChangedFor(nameof(Url))]
    [NotifyCanExecuteChangedFor(nameof(ToggleAiShowCommand))]
    public partial TokenUser? User { get; private set; }

    [ObservableProperty] public partial IAnimatedBitmap? Avatar { get; private set; }

    public string? IdText => User?.Id.ToString();

    public Uri? Url => User?.WebsiteUri;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleRestrictedModeCommand))]
    public partial bool RestrictedModeIdle { get; private set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleAiShowCommand))]
    public partial bool AiShowIdle { get; private set; } = true;

    [ObservableProperty]
    public partial bool RestrictedCache { get; private set; }

    [ObservableProperty] public partial bool AiShowCache { get; private set; }

    private bool CanToggleAiShow => !_isDisposed && User is not null && AiShowIdle;

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
                ? await App.AppViewModel.MakoClient.GetRestrictedModeSettingsAsync()
                : await App.AppViewModel.MakoClient.PostRestrictedModeSettingsAsync(!RestrictedCache);
        }
        finally
        {
            RestrictedModeIdle = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanToggleAiShow), AllowConcurrentExecutions = true)]
    private async Task ToggleAiShowAsync()
    {
        if (!CanToggleAiShow || _accountLifetime is not { } generation
            || !IsCurrentAccount(generation))
            return;
        _isAiShowRequestPending = true;
        AiShowIdle = false;
        try
        {
            var aiShow = await App.AppViewModel.MakoClient.PostAiShowSettingsAsync(!AiShowCache, generation.Token);
            if (IsCurrentAccount(generation))
                AiShowCache = aiShow;
        }
        finally
        {
            if (IsCurrentAccount(generation))
            {
                _isAiShowRequestPending = false;
                AiShowIdle = true;
            }
        }
    }

    private async Task RefreshAiShowAsync(CancellationTokenSource generation)
    {
        if (!IsCurrentAccount(generation))
            return;

        _isAiShowRequestPending = true;
        AiShowIdle = false;
        try
        {
            var aiShow = await App.AppViewModel.MakoClient.GetAiShowSettingsAsync(generation.Token);
            if (IsCurrentAccount(generation))
                AiShowCache = aiShow;
        }
        finally
        {
            if (IsCurrentAccount(generation))
            {
                _isAiShowRequestPending = false;
                AiShowIdle = true;
            }
        }
    }

    private bool IsCurrentAccount(CancellationTokenSource generation) =>
        !_isDisposed
        && !generation.IsCancellationRequested
        && ReferenceEquals(_accountLifetime, generation);
}
