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
using Pixeval.Config;
using Pixeval.I18N;
using Pixeval.Services.Background;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.Services.Network;
using Pixeval.ViewModels;
using Pixeval.Views.Home;

namespace Pixeval.Views.Login;

public partial class LoginPage : IconContentPage
{
    // Every window shares AppViewModel.MakoClient, so an attempt supersedes any other page's attempt.
    // Login handlers and page lifecycle callbacks access this owner on the UI thread.
    private static LoginPage? _ActiveLoginPage;
    private CancellationTokenSource? _loadUsersCancellationTokenSource;
    private LoginAttempt? _loginAttempt;
    private readonly FileLogger _logger = App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>();

    internal Task LoginCompletion { get; private set; } = Task.CompletedTask;

    public LoginPage()
    {
        InitializeComponent();
    }

    private void LoginButton_OnClick(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => LoginButton_OnClickAsync(sender, e));

    private async Task LoginButton_OnClickAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginPageViewModel viewModel || string.IsNullOrWhiteSpace(viewModel.RefreshToken))
            return;

        if (TryBeginLogin() is not { } attempt)
            return;

        var cancellationToken = attempt.Cancellation.Token;
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
                this.Notifications.ShowError(I18NManager.GetResource(MainPageResources.LoggingIn.Failed));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            this.GetRequiredUiService<FileLogger>()
                .LogError(nameof(LoginButton_OnClick), exception);
            {
                this.Notifications.ShowError(exception.GetType().ToString(), exception.Message);
                _ = await this.Dialogs.CreateAcknowledgementAsync(
                    I18NManager.GetResource(LoginPageResources.FetchingSessionFailed.Title),
                    I18NManager.GetResource(LoginPageResources.FetchingSessionFailed.Content));
            }
        }
        finally
        {
            EndLogin(attempt);
        }
    }

    private void OpenWebView_OnClick(object? sender, RoutedEventArgs e) =>
        this.RunInteraction(() => OpenWebView_OnClickAsync(sender, e));

    private async Task OpenWebView_OnClickAsync(object? sender, RoutedEventArgs e)
    {
        if (TryBeginLogin() is not { } attempt)
            return;

        var cancellationToken = attempt.Cancellation.Token;
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
                    I18NManager.GetResource(LoginPageResources.FetchingSessionFailed.Title),
                    I18NManager.GetResource(LoginPageResources.FetchingSessionFailed.Content));
            }
        }
        finally
        {
            EndLogin(attempt);
        }
    }

    public void LoginNavigate()
    {
        var nav = this.GetUiService<INavigationService>() ?? new NavigationService();
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
        CancelLogin();
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

    private LoginAttempt? TryBeginLogin()
    {
        if (!IsLoaded || _loginAttempt is not null || DataContext is not LoginPageViewModel { IsLoginInProgress: false } viewModel)
            return null;

        _ActiveLoginPage?.CancelLogin();
        _loginAttempt = new(viewModel, new());
        LoginCompletion = _loginAttempt.Completion.Task;
        _ActiveLoginPage = this;
        viewModel.IsLoginInProgress = true;
        return _loginAttempt;
    }

    private void EndLogin(LoginAttempt attempt)
    {
        // A remounted page can start another attempt before the canceled request finishes.
        if (ReferenceEquals(_loginAttempt, attempt))
        {
            _loginAttempt = null;
            if (ReferenceEquals(_ActiveLoginPage, this))
                _ActiveLoginPage = null;
            attempt.ViewModel.IsLoginInProgress = false;
        }
        attempt.Cancellation.Dispose();
        attempt.Completion.TrySetResult();
    }

    private void CancelLogin()
    {
        if (_loginAttempt is not { } attempt)
            return;

        _loginAttempt = null;
        if (ReferenceEquals(_ActiveLoginPage, this))
            _ActiveLoginPage = null;
        attempt.Cancellation.Cancel();
        attempt.ViewModel.IsLoginInProgress = false;
    }

    private void ShowAttemptError(LoginAttempt attempt, Exception exception)
    {
        if (ReferenceEquals(_loginAttempt, attempt) && !attempt.Cancellation.IsCancellationRequested)
            ShowLoginError(exception);
        else
            _logger.LogError(nameof(LoginPage), exception);
    }

    private sealed record LoginAttempt(LoginPageViewModel ViewModel, CancellationTokenSource Cancellation)
    {
        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
