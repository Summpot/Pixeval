using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.I18N;
using Pixeval.Models.Database.Managers;
using Pixeval.ViewModels;
using Pixeval.Views.Home;
using Pixeval.Views.Login;

namespace Pixeval.MigrationValidation;

internal static class LoginLifetimeValidation
{
    internal static async Task RunAsync(string directory)
    {
        await LoginLayoutValidation.RunAsync(directory);
        var client = App.AppViewModel.MakoClient;
        var accounts = App.AppViewModel.AppServiceProvider.GetRequiredService<LoginUserPersistentManager>();
        var requests = Channel.CreateUnbounded<PendingLogin>();
        ConcurrentBag<PendingLogin> pending = [];
        using var fixture = new OfflineSearchFixture(client);
        fixture.AuthResponseFactory = async (request, token) =>
        {
            var body = await request.Content!.ReadAsStringAsync(token);
            var login = new PendingLogin(HttpUtility.ParseQueryString(body)["refresh_token"]!, token);
            pending.Add(login);
            await requests.Writer.WriteAsync(login);
            // Deliberately return a successful response after cancellation to check late completion.
            return await login.Response.Task;
        };
        List<string> completed = [];
        var sequence = 0;
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            await client.ClearTokenAsync();
            var oldId = 900100 + ++sequence * 10;
            var currentId = oldId + 1;
            var page = new LoginPage();
            var model = (LoginPageViewModel) page.DataContext!;
            var navigation = new NavigationPage();
            await navigation.PushAsync(page);
            var container = new SearchCompletionValidation.FixtureViewContainer { Content = navigation };
            var window = new Window { Width = 1000, Height = 720, RequestedThemeVariant = theme, Content = container };
            try
            {
                window.Show();
                Render();
                await model.LoadUsersAsync();
                var button = page.GetVisualDescendants().OfType<Button>().Single(control =>
                    Equals(control.Content, I18NManager.GetResource(LoginPageResources.RefreshTokenButton.Content)));
                var input = page.GetVisualDescendants().OfType<AutoCompleteBox>().Single();
                model.RefreshToken = " ";
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(!model.IsLoginInProgress && pending.Count == (sequence - 1) * 3, "Blank input leaves controls ready and starts no authentication");
                model.RefreshToken = "fixture-old";
                Require(button.Focus(NavigationMethod.Tab), "Token login action can receive keyboard focus");
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                var old = await NextAsync();
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(old.RefreshToken is "fixture-old" && model.IsLoginInProgress, "Login captures the intended token and enters the busy state");
                Require(!button.IsEffectivelyEnabled && !input.IsEffectivelyEnabled, "Login and account input are disabled during authentication");
                container.Content = null;
                Render();
                Require(old.Token.IsCancellationRequested, "Unloading login cancels the actual authentication request");
                Require(!model.IsLoginInProgress, "Unloading releases the page busy state immediately");
                container.Content = navigation;
                Render();
                model.RefreshToken = "fixture-current";
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(model.IsLoginInProgress, "Remounted page can start a replacement while the old response is pending");
                old.Complete(oldId);
                var current = await NextAsync();
                Require(current.RefreshToken is "fixture-current" && model.IsLoginInProgress,
                    "Old completion cannot end the replacement's busy state");
                Require(client.Me is null && accounts.GetByUserId(oldId) is null, "Late canceled credentials do not become the current or saved account");
                Require(container.Navigations is 0, "Canceled login does not navigate");
                Require(pending.Count == (sequence - 1) * 3 + 2, "A queued duplicate submission does not start another authentication");
                completed.Add($"{theme.Key}: busy input/command state, unload cancellation, late response rejection and isolated persistence");

                current.Complete(currentId);
                await UntilAsync(() => !model.IsLoginInProgress);
                Require(client.Me?.Id == currentId && App.AppViewModel.GetCurrentLoginUser()?.UserId == currentId,
                    "Successful login persists and selects only the intended account");
                Require(container.Navigations is 1 && container.LastPageType == typeof(HomePage), "Successful login navigates home exactly once");
                var reopened = new LoginPageViewModel();
                await reopened.LoadUsersAsync();
                Require(reopened.SelectedUser?.UserId == currentId && reopened.RefreshToken == $"fixture-refreshed-{currentId}",
                    "Reopening account selection restores the refreshed token for the persisted current account");
                completed.Add($"{theme.Key}: remount accepts a new login, saves the correct account and navigates once");

                model.RefreshToken = "fixture-failure";
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var failure = await NextAsync();
                failure.Response.SetException(new HttpRequestException("Fixed authentication connection failure"));
                await UntilAsync(() => !model.IsLoginInProgress);
                Render();
                Require(button.IsEffectivelyEnabled && input.IsEffectivelyEnabled && model.RefreshToken is "fixture-failure",
                    "Authentication failure restores controls and keeps editable input");
                Require(accounts.GetByUserId(currentId) is not null, "Authentication failure preserves saved accounts");
                Require(container.Notifications.Any(notification => notification.Type is NotificationType.Error), "Authentication error remains visible");
                using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No login error frame.");
                frame.Save(Path.Combine(directory, $"login-error-{theme.Key}.png"), new PngBitmapEncoderOptions());
                completed.Add($"{theme.Key}: failure remains visible, saved account survives and controls/input can be retried");
            }
            finally
            {
                window.Content = null;
                window.Close();
                foreach (var login in pending)
                    _ = login.Response.TrySetResult(TokenResponse(oldId));
                await UntilAsync(() => !model.IsLoginInProgress);
            }
            continue;

            void Render()
            {
                window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                Dispatcher.UIThread.RunJobs();
            }
        }
        await VerifyOverlappingPagesAsync(false);
        await VerifyOverlappingPagesAsync(true);
        Require(fixture.UnexpectedRequests.IsEmpty, "No request escaped the offline login scenario");
        File.WriteAllLines(Path.Combine(directory, "login-validation.txt"), completed);
        File.WriteAllLines(Path.Combine(directory, "offline-requests.txt"), fixture.RequestLog);
        Console.WriteLine($"Validated {completed.Count} login groups with isolated accounts and controlled HTTP responses.");
        return;

        Task<PendingLogin> NextAsync() => requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        async Task VerifyOverlappingPagesAsync(bool oldFails)
        {
            await client.ClearTokenAsync();
            var oldId = oldFails ? 900211 : 900201;
            var currentId = oldId + 1;
            var firstPage = new LoginPage();
            var secondPage = new LoginPage();
            var firstModel = (LoginPageViewModel) firstPage.DataContext!;
            var secondModel = (LoginPageViewModel) secondPage.DataContext!;
            var firstContainer = new SearchCompletionValidation.FixtureViewContainer { Content = firstPage };
            var secondContainer = new SearchCompletionValidation.FixtureViewContainer { Content = secondPage };
            var firstWindow = new Window { Width = 1000, Height = 720, Content = firstContainer };
            var secondWindow = new Window { Width = 1000, Height = 720, Content = secondContainer };
            try
            {
                firstWindow.Show();
                secondWindow.Show();
                firstWindow.UpdateLayout();
                secondWindow.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                await firstModel.LoadUsersAsync();
                await secondModel.LoadUsersAsync();
                var firstButton = LoginButton(firstPage);
                var secondButton = LoginButton(secondPage);
                firstModel.RefreshToken = "fixture-first-window";
                firstButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var first = await NextAsync();
                secondModel.RefreshToken = " ";
                secondButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(!first.Token.IsCancellationRequested && firstModel.IsLoginInProgress,
                    "Blank input in another window must not cancel the active login");
                secondModel.RefreshToken = "fixture-second-window";
                secondButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(first.Token.IsCancellationRequested && !firstModel.IsLoginInProgress && secondModel.IsLoginInProgress,
                    "A newer login on another page cancels the old request and releases only its page busy state");
                if (oldFails)
                    first.Response.SetException(new HttpRequestException("Fixed superseded authentication failure"));
                else
                    first.Complete(oldId);
                var second = await NextAsync();
                Require(second.RefreshToken is "fixture-second-window" && accounts.GetByUserId(oldId) is null,
                    "Late cross-window credentials cannot be persisted over the newer attempt");
                Require(firstContainer.Navigations is 0 && secondModel.IsLoginInProgress,
                    "The old page cannot navigate or clear the new page's busy state");
                Require(!firstContainer.Notifications.Any() && !secondContainer.Notifications.Any(),
                    "An old failure must not notify either window during the current attempt");
                firstWindow.Close();
                Require(!second.Token.IsCancellationRequested && secondModel.IsLoginInProgress,
                    "Closing the superseded page must not cancel the current page's authentication");
                second.Complete(currentId);
                await UntilAsync(() => !secondModel.IsLoginInProgress);
                Require(client.Me?.Id == currentId && accounts.GetByUserId(currentId) is not null,
                    "Only the second window's credentials become the current saved account");
                Require(firstContainer.Navigations is 0 && secondContainer.Navigations is 1,
                    "Only the page owning the current attempt navigates home");
                completed.Add($"Two windows (old failure={oldFails}): blank input does not supersede, newer login cancels older, late credentials/errors stay isolated and only owner navigates");
            }
            finally
            {
                firstWindow.Content = null;
                secondWindow.Content = null;
                firstWindow.Close();
                secondWindow.Close();
                foreach (var login in pending)
                    _ = login.Response.TrySetResult(TokenResponse(900299));
                await UntilAsync(() => !firstModel.IsLoginInProgress && !secondModel.IsLoginInProgress);
            }
        }

        static Button LoginButton(LoginPage page) => page.GetVisualDescendants().OfType<Button>().Single(control =>
            Equals(control.Content, I18NManager.GetResource(LoginPageResources.RefreshTokenButton.Content)));
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Require(condition(), "Login operation did not complete within the controlled fixture deadline");
    }

    private static string TokenResponse(long id) => $$"""
        {
          "access_token":"fixture-access", "refresh_token":"fixture-refreshed-{{id}}",
          "expires_in":3600, "token_type":"bearer", "scope":"",
          "user": {
            "id":{{id}}, "name":"M3E Fixture", "account":"fixture", "mail_address":"",
            "is_premium":false, "x_restrict":0, "is_mail_authorized":true,
            "require_policy_agreement":false,
            "profile_image_urls":{"px_16x16":"", "px_50x50":"", "px_170x170":""}
          }
        }
        """;

    private sealed class PendingLogin(string refreshToken, CancellationToken token)
    {
        internal string RefreshToken { get; } = refreshToken;
        internal CancellationToken Token { get; } = token;
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Complete(long id) => Response.SetResult(TokenResponse(id));
    }

    private static void Require(bool condition, string contract)
    {
        if (!condition)
            throw new InvalidOperationException(contract);
    }
}
