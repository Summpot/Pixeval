using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.AppManagement;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Capability;

namespace Pixeval.Views;

public partial class LoginPage : UserControl
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void LoginButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var token = TextBox.Text;
        if (string.IsNullOrWhiteSpace(token))
            return;

        if (await App.AppViewModel.LoginWithRefreshTokenAsync(token))
            TopLevel.GetTopLevel(this)?.ViewContainer?.NavigateTo<RecommendWorksPage>(true);
    }

    private async void OpenBrowserButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var loginUri = App.AppViewModel.CreateBrowserLoginUri();
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        if (!CallbackProtocolRegistrationState.EnsureReadyNow())
        {
            topLevel.ViewContainer?.ShowError(
                I18NManager.GetResource(MiscResources.UnexpectedBehavior),
                CallbackProtocolRegistrationState.LastError ?? "Callback protocol registration failed.");
            return;
        }

        _ = await topLevel.Launcher.LaunchUriAsync(loginUri);

        topLevel.ViewContainer?.ShowInformation(
            I18NManager.GetResource(LoginPageResources.BrowserLoginTipTitle),
            I18NManager.GetResource(LoginPageResources.BrowserLoginTipMessage));
    }
}
