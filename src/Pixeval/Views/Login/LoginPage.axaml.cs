// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement;
using Pixeval.I18N;
using Pixeval.Native.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Home;

namespace Pixeval.Views.Login;

public partial class LoginPage : IconContentPage
{
    private CancellationTokenSource? _loadUsersCancellationTokenSource;

    public LoginPage()
    {
        InitializeComponent();
    }

    private async void LoginButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!TryBeginLogin())
            return;

        try
        {
            if (DataContext is not LoginPageViewModel viewModel)
                return;

            var token = viewModel.RefreshToken;
            if (string.IsNullOrWhiteSpace(token))
                return;

            App.AppViewModel.MakoClient.SetRefreshToken(token);
            var result = await App.AppViewModel.MakoClient.IdentifyTokenAsync();
            if (result.Success)
            {
                var tokenResponse = App.AppViewModel.MakoClient.GetTokenResponse()
                    ?? (App.AppViewModel.MakoClient.GetUser() is { } user
                        ? new TokenResponse("", 0, "Bearer", token, user)
                        : null);
                App.AppViewModel.OnTokenRefreshed(tokenResponse);
                LoginNavigate();
            }
            else if (TopLevel.GetTopLevel(this)?.ViewContainer is { } viewContainer)
                viewContainer.ShowError(I18NManager.GetResource(MainPageResources.LoggingIn.Failed));
        }
        catch (Exception exception)
        {
            App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
                .LogError(nameof(LoginButton_OnClick), exception);
            if (TopLevel.GetTopLevel(this)?.ViewContainer is { } viewContainer)
            {
                viewContainer.ShowError(exception.GetType().ToString(), exception.Message);
                _ = await viewContainer.CreateAcknowledgementAsync(
                    I18NManager.GetResource(LoginPageResources.FetchingSessionFailed.Title),
                    I18NManager.GetResource(LoginPageResources.FetchingSessionFailed.Content));
            }
        }
        finally
        {
            EndLogin();
        }
    }

    private async void OpenWebView_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!TryBeginLogin())
            return;

        try
        {
            var verifier = PixivAuth.GetCodeVerify();
            if (TopLevel.GetTopLevel(this) is not { ViewContainer: { } viewContainer } topLevel)
                return;

            var result = await WebAuthenticationBroker.AuthenticateAsync(
                topLevel,
                new(
                    new(PixivAuth.GenerateWebPageUrl(verifier)),
                    new("pixiv://account/login"))
                {
                    Mode = WebAuthenticatorMode.NativeWebDialog,
                    NonPersistent = true,
                    NativeWebDialogFactory = () =>
                    {
                        var dialog = new NativeWebDialog
                        {
                            Title = "Pixiv",
                            CanUserResize = true
                        };
                        dialog.Resize(600, 700);
                        dialog.EnvironmentRequested += (_, args) =>
                        {
                            if (args is WindowsWebView2EnvironmentRequestedEventArgs winArgs)
                            {
                                var userDataFolder = Path.Combine(AppInfo.CacheFolder, "WebView2");
                                Directory.CreateDirectory(userDataFolder);
                                winArgs.UserDataFolder = userDataFolder;
                                // For System proxy, WebView2 natively uses Windows system proxy.
                                // For Custom proxy, format host:port without trailing slash or internal quotes.
                                if (App.AppViewModel?.AppSettings?.NetworkSettings?.ProxySettings is { ProxyType: Models.Options.ProxyType.Custom } proxySettings
                                    && !string.IsNullOrWhiteSpace(proxySettings.Proxy))
                                {
                                    var normalized = MakoHelper.NormalizeProxyUri(proxySettings.Proxy);
                                    if (normalized is not null && Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
                                    {
                                        var cleanProxy = $"{uri.Scheme}://{uri.Authority}";
                                        winArgs.AdditionalBrowserArguments = $"--proxy-server={cleanProxy}";
                                    }
                                }
                            }
                        };
                        return dialog;
                    }
                });

            if (result.CallbackUri is not { } callbackUri)
                return;
            var code = HttpUtility.ParseQueryString(callbackUri.Query)["code"];
            if (string.IsNullOrWhiteSpace(code))
                return;
            var tokenResponse = await App.AppViewModel.MakoClient.ExchangeCodeAsync(code, verifier);
            App.AppViewModel.OnTokenRefreshed(tokenResponse);
            LoginNavigate();
        }
        catch (TaskCanceledException)
        {
            // ignored
        }
        catch (Exception exception)
        {
            App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
                .LogError(nameof(OpenWebView_OnClick), exception);
            if (TopLevel.GetTopLevel(this)?.ViewContainer is { } viewContainer)
            {
                viewContainer.ShowError(exception.GetType().ToString(), exception.Message);
                _ = await viewContainer.CreateAcknowledgementAsync(
                    I18NManager.GetResource(LoginPageResources.FetchingSessionFailed.Title),
                    I18NManager.GetResource(LoginPageResources.FetchingSessionFailed.Content));
            }
        }
        finally
        {
            EndLogin();
        }
    }

    public void LoginNavigate()
    {
        var viewContainer = TopLevel.GetTopLevel(this)?.ViewContainer;
        viewContainer?.NavigateTo(new HomePage(), true);
        App.AppViewModel.QueueWorkSubscriptionSyncAll();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _loadUsersCancellationTokenSource?.Cancel();
        _loadUsersCancellationTokenSource?.Dispose();
        var cancellationTokenSource = new CancellationTokenSource();
        _loadUsersCancellationTokenSource = cancellationTokenSource;
        try
        {
            if (DataContext is LoginPageViewModel viewModel)
                await viewModel.LoadUsersAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
                .LogError(nameof(LoginPageViewModel.LoadUsersAsync), exception);
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _loadUsersCancellationTokenSource?.Cancel();
        _loadUsersCancellationTokenSource?.Dispose();
        _loadUsersCancellationTokenSource = null;
        base.OnUnloaded(e);
    }

    private void RefreshTokenBox_OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is AutoCompleteBox box)
            box.IsDropDownOpen = true;
    }

    private bool TryBeginLogin()
    {
        if (DataContext is not LoginPageViewModel viewModel)
            return false;

        viewModel.IsLoginInProgress = true;
        return true;
    }

    private void EndLogin()
    {
        if (DataContext is not LoginPageViewModel viewModel)
            return;

        viewModel.IsLoginInProgress = false;
    }
}
