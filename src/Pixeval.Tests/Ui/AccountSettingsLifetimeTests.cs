using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.ViewModels;
using Pixeval.Views.ViewContainers;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

[TestClass]
[DoNotParallelize]
[TestCategory("UI")]
public sealed class AccountSettingsLifetimeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task PreviousAccountWriteCannotReplaceCurrentSettingsOrReleaseItsBusyState(bool oldCompletesFirst) =>
        UiTestSession.RunAsync(TestContext, async directory =>
        {
            using var responses = new SettingsResponses();
            var shell = App.CreateViewContainer();
            var model = (TabViewContainerViewModel) shell.DataContext!;
            var window = Open(shell, oldCompletesFirst);
            try
            {
                await responses.LoginAsync(900501);
                (await responses.NextAsync(HttpMethod.Get)).Complete(true);
                await UntilAsync(() => model.AiShowIdle && model.AiShowCache);
                await ToggleAsync(window, shell, model);
                var previous = await responses.NextAsync(HttpMethod.Post);
                var previousTask = model.ToggleAiShowCommand.ExecutionTask!;
                Assert.AreEqual("false", previous.Body);
                Assert.AreEqual("fixture-access-900501", previous.AccessToken);
                Assert.IsFalse(model.ToggleAiShowCommand.CanExecute(null));

                await responses.LoginAsync(900502);
                var current = await responses.NextAsync(HttpMethod.Get);
                Assert.IsTrue(previous.Token.IsCancellationRequested, "Changing accounts cancels the old transport.");
                Assert.AreEqual("fixture-access-900502", current.AccessToken);
                Assert.AreEqual(900502L, model.User!.Id);
                Assert.IsFalse(model.AiShowIdle);
                if (oldCompletesFirst)
                {
                    previous.Complete(false);
                    await previousTask;
                    Assert.IsFalse(model.AiShowIdle, "The old write cannot release a different account's refresh.");
                    Assert.IsFalse(model.ToggleAiShowCommand.CanExecute(null));
                }
                current.Complete(true);
                await UntilAsync(() => model.AiShowIdle);
                Assert.IsTrue(model.AiShowCache, "Only the current account owns the setting value.");
                Assert.IsTrue(model.ToggleAiShowCommand.CanExecute(null), "An abandoned account write must not block the current account.");
                await ToggleAsync(window, shell, model);
                var next = await responses.NextAsync(HttpMethod.Post);
                var nextTask = model.ToggleAiShowCommand.ExecutionTask!;
                Assert.AreEqual("false", next.Body, "The next menu action uses the current account's value.");
                Assert.AreEqual("fixture-access-900502", next.AccessToken);
                if (!oldCompletesFirst)
                {
                    previous.Complete(false);
                    await previousTask;
                    Assert.IsFalse(model.AiShowIdle, "The obsolete write cannot release the current write's busy state.");
                    Assert.IsFalse(model.ToggleAiShowCommand.CanExecute(null));
                    Assert.IsTrue(model.AiShowCache, "The obsolete write cannot publish over the current account.");
                }
                next.Complete(false);
                await nextTask;
                Assert.IsFalse(model.AiShowCache);
                Assert.AreEqual(2, responses.PostCount);
                Assert.IsEmpty(responses.Http.UnexpectedRequests);
                File.WriteAllText(Path.Combine(directory, "account-settings-contract.txt"),
                    $"Old write completes first: {oldCompletesFirst}; current identity, value, busy state and next menu payload preserved.");
            }
            finally
            {
                responses.CompletePending();
                window.Close();
            }
        }, oldCompletesFirst ? "zh-Hans" : "ru-RU");

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task LogoutAndWindowCloseRejectLateSettingsWrite(bool closeWindow) =>
        UiTestSession.RunAsync(TestContext, async directory =>
        {
            using var responses = new SettingsResponses();
            var shell = App.CreateViewContainer();
            var model = (TabViewContainerViewModel) shell.DataContext!;
            var window = Open(shell, closeWindow);
            try
            {
                await responses.LoginAsync(900503);
                (await responses.NextAsync(HttpMethod.Get)).Complete(false);
                await UntilAsync(() => model.AiShowIdle);
                await ToggleAsync(window, shell, model);
                var previous = await responses.NextAsync(HttpMethod.Post);
                var completion = model.ToggleAiShowCommand.ExecutionTask!;
                Assert.AreEqual("true", previous.Body);
                if (closeWindow)
                    window.Close();
                else
                    await App.AppViewModel.MakoClient.ClearTokenAsync();
                await UntilAsync(() => model.User is null);
                Assert.IsTrue(previous.Token.IsCancellationRequested, "Leaving the account cancels its transport.");
                var changes = 0;
                model.PropertyChanged += (_, _) => changes++;
                var valueAfterLeaving = model.AiShowCache;
                previous.Complete(true);
                await completion;
                Assert.AreEqual(valueAfterLeaving, model.AiShowCache, "A departed account cannot publish a late write.");
                Assert.AreEqual(0, changes, "No setting or busy-state notification may escape the expired generation.");
                Assert.IsFalse(model.ToggleAiShowCommand.CanExecute(null));
                if (closeWindow)
                    Assert.IsNull(shell.DataContext, "The production window close releases its account view model.");
                else
                {
                    await responses.LoginAsync(900504);
                    (await responses.NextAsync(HttpMethod.Get)).Complete(true);
                    await UntilAsync(() => model.AiShowIdle && model.User?.Id == 900504);
                    Assert.IsTrue(model.AiShowCache);
                    Assert.IsTrue(model.ToggleAiShowCommand.CanExecute(null));
                }
                File.WriteAllText(Path.Combine(directory, "account-settings-contract.txt"),
                    $"Close window: {closeWindow}; late write rejected without changing state; no real account was used.");
                Assert.IsEmpty(responses.Http.UnexpectedRequests);
            }
            finally
            {
                responses.CompletePending();
                window.Close();
            }
        });

    private static Window Open(TabViewContainer shell, bool narrow)
    {
        shell.NavigateTo(new ContentPage { Content = new TextBlock { Text = "Account setting lifetime fixture" } });
        var window = new Window
        {
            Width = narrow ? 480 : 1000,
            Height = 720,
            RequestedThemeVariant = narrow ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = shell
        };
        window.Show();
        return window;
    }

    [TestMethod]
    public Task TokenRenewalForTheSameAccountDoesNotCancelItsSettingsWrite() =>
        UiTestSession.RunAsync(TestContext, async directory =>
        {
            using var responses = new SettingsResponses { ExpiresIn = 3 };
            var shell = App.CreateViewContainer();
            var model = (TabViewContainerViewModel) shell.DataContext!;
            var window = Open(shell, false);
            try
            {
                await responses.LoginAsync(900505);
                (await responses.NextAsync(HttpMethod.Get)).Complete(true);
                await UntilAsync(() => model.AiShowIdle);
                responses.ExpiresIn = 3600;
                // Let the fixed short-lived token expire; renew through the real
                // OAuth provider rather than invoking its callbacks ourselves.
                await Task.Delay(TimeSpan.FromSeconds(3.1));
                await ToggleAsync(window, shell, model);
                var pending = await responses.NextAsync(HttpMethod.Post);
                Assert.AreEqual("false", pending.Body);
                Assert.AreEqual("fixture-access-900505", pending.AccessToken);
                Assert.IsFalse(pending.Token.IsCancellationRequested, "Same-account renewal must not cancel the write that requested a new access token.");
                pending.Complete(false);
                await model.ToggleAiShowCommand.ExecutionTask!;
                Assert.IsFalse(model.AiShowCache);
                Assert.IsTrue(model.AiShowIdle);
                Assert.AreEqual(900505L, model.User!.Id);
                Assert.AreEqual(2, responses.Http.RequestLog.Count(line => line == "POST /auth/token"));
                Assert.AreEqual(1, responses.Http.RequestLog.Count(line => line == "GET /v1/user/ai-show-settings"),
                    "Refreshing the profile during a write must not race it with another settings read.");
                Assert.IsEmpty(responses.Http.UnexpectedRequests);
                File.WriteAllText(Path.Combine(directory, "account-settings-contract.txt"),
                    "The production OAuth provider renewed the same account during a real menu POST; the request and account value survived without an overlapping GET.");
            }
            finally
            {
                responses.CompletePending();
                window.Close();
            }
        });

    [TestMethod]
    public Task ChangedAccountCannotWriteBeforeItsAvatarAndSettingsAreReady() =>
        UiTestSession.RunAsync(TestContext, async directory =>
        {
            using var responses = new SettingsResponses();
            var shell = App.CreateViewContainer();
            var model = (TabViewContainerViewModel) shell.DataContext!;
            var window = Open(shell, true);
            using var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png"));
            using var buffer = new MemoryStream();
            asset.CopyTo(buffer);
            var avatarResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(buffer.ToArray()) };
            var avatarRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var avatar = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            responses.Http.ImageResponseFactory = (request, _) =>
            {
                avatarResponse.RequestMessage = request;
                avatarRequested.TrySetResult();
                return avatar.Task;
            };
            try
            {
                await responses.LoginAsync(900506);
                (await responses.NextAsync(HttpMethod.Get)).Complete(true);
                await UntilAsync(() => model.AiShowIdle);
                responses.AvatarUri = "https://i.pximg.net/m3e/account-settings-avatar.png";
                await responses.LoginAsync(900507);
                await avatarRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.AreEqual(900507L, model.User!.Id);
                Assert.IsFalse(model.AiShowIdle);
                Assert.IsFalse(model.ToggleAiShowCommand.CanExecute(null));
                var item = await OpenAiItemAsync(window, shell, model);
                Assert.IsFalse(item.IsEffectivelyEnabled, "The account menu cannot reuse the previous account's setting while the new avatar is pending.");
                avatar.SetResult(avatarResponse);
                var current = await responses.NextAsync(HttpMethod.Get);
                Assert.IsFalse(item.IsEffectivelyEnabled);
                Assert.AreEqual("fixture-access-900507", current.AccessToken);
                current.Complete(true);
                await UntilAsync(() => item.IsEffectivelyEnabled);
                Assert.IsTrue(model.Avatar is { IsInitialized: true });
                Assert.IsTrue(item.Focus(NavigationMethod.Tab));
                Press(window, Key.Enter, PhysicalKey.Enter);
                var write = await responses.NextAsync(HttpMethod.Post);
                Assert.AreEqual("fixture-access-900507", write.AccessToken);
                Assert.AreEqual("false", write.Body);
                write.Complete(false);
                await model.ToggleAiShowCommand.ExecutionTask!;
                Assert.IsFalse(model.AiShowCache);
                Assert.IsEmpty(responses.Http.UnexpectedRequests);
                Save(window, directory, "account-ready-after-avatar");
            }
            finally
            {
                avatar.TrySetResult(avatarResponse);
                responses.CompletePending();
                window.Close();
            }
        });

    private static async Task ToggleAsync(Window window, TabViewContainer shell, TabViewContainerViewModel model)
    {
        var item = await OpenAiItemAsync(window, shell, model);
        Assert.IsTrue(item.IsEffectivelyEnabled && item.Focus(NavigationMethod.Tab));
        Press(window, Key.Enter, PhysicalKey.Enter);
        await UntilAsync(() => model.ToggleAiShowCommand.IsRunning);
    }

    private static async Task<MenuItem> OpenAiItemAsync(Window window, TabViewContainer shell, TabViewContainerViewModel model)
    {
        await RenderAsync(window);
        var account = shell.GetVisualDescendants().OfType<Button>().Single(button => button.Content is Pixeval.Controls.AvatarImage);
        await ActivateAsync(window, account);
        var flyout = (MenuFlyout) account.Flyout!;
        Assert.IsTrue(flyout.IsOpen);
        return flyout.Items.OfType<MenuItem>().Single(menu => ReferenceEquals(menu.Command, model.ToggleAiShowCommand));
    }

    private sealed class SettingsResponses : IDisposable
    {
        private readonly Channel<Pending> _requests = Channel.CreateUnbounded<Pending>();
        private readonly ConcurrentBag<Pending> _pending = [];
        private long _userId;
        private int _postCount;
        internal int ExpiresIn { get; set; } = 3600;
        internal string AvatarUri { get; set; } = "";
        internal int PostCount => _postCount;
        internal OfflineSearchFixture Http { get; } = new(App.AppViewModel.MakoClient);

        internal SettingsResponses()
        {
            Http.AuthResponseFactory = (_, _) => Task.FromResult($$"""
                {"access_token":"fixture-access-{{_userId}}", "refresh_token":"fixture-refresh-{{_userId}}",
                 "expires_in":{{ExpiresIn}}, "token_type":"bearer", "scope":"",
                 "user":{"id":{{_userId}}, "name":"Fixture", "account":"fixture", "mail_address":"",
                   "is_premium":false,"x_restrict":0,"is_mail_authorized":true,"require_policy_agreement":false,
                   "profile_image_urls":{"px_16x16":"","px_50x50":"{{AvatarUri}}","px_170x170":""}
                 }
                }
                """);
            Http.AiShowResponseFactory = async (request, token) =>
            {
                var body = request.Content is null ? null : System.Web.HttpUtility.ParseQueryString(await request.Content.ReadAsStringAsync(token))["show_ai"];
                var pending = new Pending(request.Method, body, request.Headers.Authorization?.Parameter, token);
                _pending.Add(pending);
                if (request.Method == HttpMethod.Post)
                    Interlocked.Increment(ref _postCount);
                await _requests.Writer.WriteAsync(pending, token);
                // Intentionally return even after account/window cancellation.
                return await pending.Response.Task;
            };
        }

        internal async Task LoginAsync(long id)
        {
            _userId = id;
            var client = App.AppViewModel.MakoClient;
            await client.SetTokenAsync("fixture-login-" + id);
            await client.ValidateTokenAsync();
        }

        internal async Task<Pending> NextAsync(HttpMethod method)
        {
            var pending = await _requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(method, pending.Method);
            return pending;
        }

        internal void CompletePending()
        {
            foreach (var pending in _pending)
                pending.Complete(false);
        }

        public void Dispose()
        {
            CompletePending();
            Http.Dispose();
        }
    }

    private sealed record Pending(HttpMethod Method, string? Body, string? AccessToken, CancellationToken Token)
    {
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Complete(bool value) => Response.TrySetResult(value ? "{\"show_ai\":true}" : "{\"show_ai\":false}");
    }
}
