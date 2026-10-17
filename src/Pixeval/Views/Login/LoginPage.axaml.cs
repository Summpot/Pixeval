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
using Pixeval.Config;
using Pixeval.Localization;
using Pixeval.Services.Background;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.Services.Network;
using Pixeval.ViewModels;
using Pixeval.Views.Home;

namespace Pixeval.Views.Login;

public partial class LoginPage : IconContentPage
{
    private CancellationTokenSource? _loadUsersCancellationTokenSource;

    public LoginPage(LoginPageViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void LoginButton_OnClick(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => LoginButton_OnClickAsync(sender, e));

    private async Task LoginButton_OnClickAsync(object? sender, RoutedEventArgs e)
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

            this.GetRequiredUiService<MakoClient>().SetRefreshToken(token);
            var result = await this.GetRequiredUiService<MakoClient>().IdentifyTokenAsync();
            if (result.Success)
            {
                var tokenResponse = this.GetRequiredUiService<MakoClient>().GetTokenResponse()
                    ?? (this.GetRequiredUiService<MakoClient>().GetUser() is { } user
                        ? new TokenResponse("", 0, "Bearer", token, user)
                        : null);
                this.GetRequiredUiService<IUserSessionService>().OnTokenRefreshed(tokenResponse);
                LoginNavigate();
            }
            else
                this.Notifications.ShowError(Strings.MainPageLoggingInFailed());
        }
        catch (Exception exception)
        {
            this.GetRequiredUiService<FileLogger>()
                .LogError(nameof(LoginButton_OnClick), exception);
            {
                this.Notifications.ShowError(exception.GetType().ToString(), exception.Message);
                _ = await this.Dialogs.CreateAcknowledgementAsync(
                    Strings.LoginPageFetchingSessionFailedTitle(),
                    Strings.LoginPageFetchingSessionFailedContent());
            }
        }
        finally
        {
            EndLogin();
        }
    }

    private void OpenWebView_OnClick(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => OpenWebView_OnClickAsync(sender, e));

    private async Task OpenWebView_OnClickAsync(object? sender, RoutedEventArgs e)
    {
        if (!TryBeginLogin())
            return;

        try
        {
            var verifier = PixivAuth.GetCodeVerify();
            if (TopLevel.GetTopLevel(this) is not { } topLevel)
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
                                if (this.GetUiService<AppSettings>()?.NetworkSettings?.ProxySettings is { ProxyType: ProxyType.Custom } proxySettings
                                    && !string.IsNullOrWhiteSpace(proxySettings.Proxy))
                                {
                                    var normalized = ProxyHelper.NormalizeProxyUri(proxySettings.Proxy);
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
            var tokenResponse = await this.GetRequiredUiService<MakoClient>().ExchangeCodeAsync(code, verifier);
            this.GetRequiredUiService<IUserSessionService>().OnTokenRefreshed(tokenResponse);
            LoginNavigate();
        }
        catch (TaskCanceledException)
        {
            // ignored
        }
        catch (Exception exception)
        {
            this.GetRequiredUiService<FileLogger>()
                .LogError(nameof(OpenWebView_OnClick), exception);
            {
                this.Notifications.ShowError(exception.GetType().ToString(), exception.Message);
                _ = await this.Dialogs.CreateAcknowledgementAsync(
                    Strings.LoginPageFetchingSessionFailedTitle(),
                    Strings.LoginPageFetchingSessionFailedContent());
            }
        }
        finally
        {
            EndLogin();
        }
    }

    public void LoginNavigate()
    {
        var nav = this.GetRequiredUiService<INavigationService>();
        nav.NavigateToHome(removeCurrentPage: true, sourceControl: this);
        this.GetRequiredUiService<IWorkSubscriptionService>().QueueSyncAll();
    }

    protected override void OnLoaded(RoutedEventArgs e)

    {

        base.OnLoaded(e);

        this.RunInteraction(() => OnLoadedAsync(e));

    }


    private async Task OnLoadedAsync(RoutedEventArgs e)
    {
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
            this.GetRequiredUiService<FileLogger>()
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
