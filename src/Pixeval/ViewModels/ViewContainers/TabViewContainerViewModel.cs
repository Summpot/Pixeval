// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Models;
using Pixeval.Utilities.IO.Caching;

namespace Pixeval.ViewModels;

public partial class TabViewContainerViewModel : ViewModelBase, IDisposable
{
    private CancellationTokenSource? _avatarLoadCancellationTokenSource;
    private bool _isDisposed;

    public TabViewContainerViewModel()
    {
        OnUserRefreshed(App.AppViewModel.MakoClient.GetUser());
        App.AppViewModel.UserRefreshed += OnUserRefreshed;
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

        // await ToggleRestrictedModeAsync(true);
        await RefreshAiShowAsync(cancellationTokenSource);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_isDisposed)
            return;

        _isDisposed = true;
        App.AppViewModel.UserRefreshed -= OnUserRefreshed;
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
                ? (await App.AppViewModel.MakoClient.GetRestrictedModeSettingsAsync()).IsRestrictedModeEnabled
                : (await App.AppViewModel.MakoClient.PostRestrictedModeSettingsAsync(!RestrictedCache)).IsRestrictedModeEnabled;
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
                ? (await App.AppViewModel.MakoClient.GetAiShowSettingsAsync()).ShowAi
                : (await App.AppViewModel.MakoClient.PostAiShowSettingsAsync(!AiShowCache)).ShowAi;
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
            var aiShow = await App.AppViewModel.MakoClient.GetAiShowSettingsAsync();
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
