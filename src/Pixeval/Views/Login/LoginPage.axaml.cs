// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Home;
using Pixeval.Views.ViewContainers;

namespace Pixeval.Views.Login;

public partial class LoginPage : IconContentPage
{
    // Every window shares AppViewModel.MakoClient, so an attempt supersedes any other page's attempt.
    // Login handlers and page lifecycle callbacks access this owner on the UI thread.
    private static LoginPage? _ActiveLoginPage;
    private CancellationTokenSource? _loadUsersCancellationTokenSource;
    private LoginAttempt? _loginAttempt;

    public LoginPage() => InitializeComponent();

    private async void LoginButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginPageViewModel viewModel || string.IsNullOrWhiteSpace(viewModel.RefreshToken))
            return;

        if (TryBeginLogin() is not { } attempt)
            return;

        var cancellationToken = attempt.Cancellation.Token;
        try
        {
            var token = attempt.ViewModel.RefreshToken;
            await App.AppViewModel.MakoClient.SetTokenAsync(token, cancellationToken);
            await App.AppViewModel.MakoClient.ValidateTokenAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            LoginNavigate();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ShowAttemptError(attempt, exception);
        }
        finally
        {
            EndLogin(attempt);
        }
    }

    private async void OpenWebView_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TryBeginLogin() is not { } attempt)
            return;

        var cancellationToken = attempt.Cancellation.Token;
        try
        {
            var verifier = PixivAuth.GetCodeVerify();
            if (TopLevel.GetTopLevel(this) is not { ViewContainer: { } viewContainer } topLevel)
                return;

            Uri? callbackUri;
            try
            {
                var result = await WebAuthenticationBroker.AuthenticateAsync(
                    topLevel,
                    new(
                        new(PixivAuth.GenerateWebPageUrl(verifier)),
                        new("pixiv://account/login"))
                    {
                        Mode = WebAuthenticatorMode.NativeWebDialog,
                        NonPersistent = true
                    });
                callbackUri = result.CallbackUri;
            }
            catch (TaskCanceledException)
            {
                // Closing the authentication dialog is not a login failure.
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var code = HttpUtility.ParseQueryString(callbackUri.Query)["code"];
            if (string.IsNullOrWhiteSpace(code))
                return;
            await App.AppViewModel.MakoClient.SetCodeAsync(code, verifier, cancellationToken);
            await App.AppViewModel.MakoClient.ValidateTokenAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            LoginNavigate();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ShowAttemptError(attempt, exception);
        }
        finally
        {
            EndLogin(attempt);
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
            return;
        }
        catch (Exception exception)
        {
            if (!cancellationTokenSource.IsCancellationRequested)
                ShowLoginError(exception);
        }
    }

    public void ShowLoginError(Exception exception)
    {
        App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
            .LogError(nameof(LoginPage), exception);
        if (TopLevel.GetTopLevel(this)?.ViewContainer is { } viewContainer)
            viewContainer.ShowError(
                I18NManager.GetResource(MainPageResources.LoggingIn.Failed),
                LoginErrorHelper.GetMessage(exception));
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
            App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>().LogError(nameof(LoginPage), exception);
    }

    private sealed record LoginAttempt(LoginPageViewModel ViewModel, CancellationTokenSource Cancellation);
}
