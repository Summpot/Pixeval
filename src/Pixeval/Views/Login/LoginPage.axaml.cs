// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Pixeval.AppManagement;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Home;

namespace Pixeval.Views.Login;

public partial class LoginPage : ContentPage
{
    private string? _codeVerifier;

    private bool _isLoggingIn;

    public LoginPage()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        PixivLoginActivationHub.Activated += PixivLoginActivationHubOnActivated;
        foreach (var uri in PixivLoginActivationHub.DrainPendingUris())
            _ = LoginWithCallbackUriAsync(uri);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        PixivLoginActivationHub.Activated -= PixivLoginActivationHubOnActivated;
        base.OnUnloaded(e);
    }

    private async void LoginButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginPageViewModel viewModel)
            return;

        var token = viewModel.RefreshToken;
        if (string.IsNullOrWhiteSpace(token))
            return;

        if (PixivLoginActivationHub.TryCreateCallbackUri(token, out var callbackUri))
        {
            await LoginWithCallbackUriAsync(callbackUri);
            return;
        }

        if (_isLoggingIn)
            return;
        _isLoggingIn = true;
        App.AppViewModel.MakoClient.SetToken(token);
        try
        {
            if (await App.AppViewModel.MakoClient.IdentifyTokenAsync())
                LoginNavigate();
        }
        finally
        {
            _isLoggingIn = false;
        }
    }

    private async void OpenBrowserLogin_OnClick(object? sender, RoutedEventArgs e)
    {
        var verifier = PixivAuth.GetCodeVerify();
        _codeVerifier = verifier;
        if (TopLevel.GetTopLevel(this) is not { ViewContainer: { } viewContainer, Launcher: { } launcher })
            return;
        try
        {
            var loginUri = new Uri(PixivAuth.GenerateWebPageUrl(verifier));
            if (!await launcher.LaunchUriAsync(loginUri))
                viewContainer.ShowWarning(
                    I18NManager.GetResource(LoginPageResources.FetchingSessionFailedTitle),
                    loginUri.OriginalString);
        }
        catch (Exception exception)
        {
            await ShowLoginFailedAsync(exception);
        }
    }

    private void PixivLoginActivationHubOnActivated(Uri uri) =>
        Dispatcher.UIThread.Post(() => _ = LoginWithCallbackUriAsync(uri));

    private async Task LoginWithCallbackUriAsync(Uri callbackUri)
    {
        if (_isLoggingIn
            || _codeVerifier is not { Length: > 0 } verifier
            || !PixivLoginActivationHub.TryExtractCode(callbackUri.OriginalString, out var code))
            return;

        _isLoggingIn = true;
        try
        {
            App.AppViewModel.MakoClient.SetCode(code, verifier);
            if (await App.AppViewModel.MakoClient.IdentifyTokenAsync())
                LoginNavigate();
        }
        catch (Exception exception)
        {
            await ShowLoginFailedAsync(exception);
        }
        finally
        {
            _isLoggingIn = false;
        }
    }

    private async Task ShowLoginFailedAsync(Exception exception)
    {
        if (TopLevel.GetTopLevel(this)?.ViewContainer is not { } viewContainer)
            return;

        viewContainer.ShowError(exception.GetType().ToString(), exception.Message);
        _ = await viewContainer.CreateAcknowledgementAsync(
            I18NManager.GetResource(LoginPageResources.FetchingSessionFailedTitle),
            I18NManager.GetResource(LoginPageResources.FetchingSessionFailedContent));
    }

    public void LoginNavigate()
    {
        var viewContainer = TopLevel.GetTopLevel(this)?.ViewContainer;
        viewContainer?.NavigateTo(new HomePage(), true);
        App.AppViewModel.QueueWorkSubscriptionSyncAll();
    }

    private void RefreshTokenBox_OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is AutoCompleteBox box)
            box.IsDropDownOpen = true;
    }
}
