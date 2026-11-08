using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Global.Enum;
using Mako.Model;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Collections;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Database.Managers;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Capability;
using Pixeval.Views.Entry;
using Pixeval.Views.Viewers;

namespace Pixeval.Tests.Ui;

internal static class UserPagesValidation
{
    private const string LongName = "长用户名 / A very long creator name ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        var requests = Channel.CreateUnbounded<PendingUsers>();
        ConcurrentBag<PendingUsers> pending = [];
        ConcurrentQueue<(string Path, string Body)> followRequests = [];
        ConcurrentQueue<CancellationToken> detailTokens = [];
        fixture.UserResponseFactory = async (request, token) =>
        {
            var uri = request.RequestUri!;
            if (uri.AbsolutePath == "/v1/user/detail")
            {
                detailTokens.Enqueue(token);
                await Task.Delay(Timeout.Infinite, token); // The observed destination is immediately disposed by the fixture container.
                return "{}";
            }
            if (uri.AbsolutePath is "/v1/user/follow/add" or "/v1/user/follow/delete")
            {
                followRequests.Enqueue((uri.AbsolutePath, await request.Content!.ReadAsStringAsync(token)));
                return "{}";
            }
            var response = new PendingUsers(uri);
            pending.Add(response);
            await requests.Writer.WriteAsync(response);
            return await response.Response.Task; // Model ownership must reject results even when a transport finishes late.
        };
        const string imageUri = "https://i.pximg.net/m3e/user.png";
        using (var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var buffer = new MemoryStream())
        {
            await asset.CopyToAsync(buffer);
            fixture.ImageResponses[imageUri] = buffer.ToArray();
        }
        await client.SetTokenAsync("fixture-user-pages");
        await client.ValidateTokenAsync();
        List<string> report = [];
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                foreach (var width in new[] { 360, 1000 })
                {
                    foreach (var scenario in Scenarios())
                    {
                        var page = scenario.Create();
                        var users = page.FindControl<UserContainer>("UserContainer")!;
                        var view = users.FindControl<UserView>("UserView")!;
                        var model = (UserViewViewModel) view.DataContext!;
                        var navigation = new NavigationPage();
                        await navigation.PushAsync(page);
                        List<long> routes = [];
                        var container = new PageTestHost
                        {
                            Content = navigation,
                            NavigationObserver = destination =>
                            {
                                Require(destination is UserViewerPage && destination.DataContext is UserViewerPageViewModel,
                                    "A user card opens the existing user-detail destination");
                                routes.Add(((UserViewerPageViewModel) destination.DataContext!).Id);
                            }
                        };
                        var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = container.Shell };
                        var key = $"{scenario.Name}-{theme.Key}-{width}";
                        try
                        {
                            window.Show();
                            var initial = await NextAsync();
                            ValidateRequest(initial.Uri, scenario);
                            initial.Complete([Sample(920001), Sample(920002)]);
                            await UntilAsync(() => model.Source.Count == 2 && !Source().IsLoading);
                            await RenderAsync(window);
                            var card = Card(920001);
                            Require(card.Focus(NavigationMethod.Tab), "A user card can receive keyboard focus");
                            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                            Require(card.FindControl<Button>("AvatarButton")!.IsFocused, "Tab reaches the user card's avatar menu");
                            window.KeyPress(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
                            window.KeyRelease(Key.Tab, RawInputModifiers.Shift, PhysicalKey.Tab, null);
                            Require(card.IsFocused, "Shift+Tab returns from the avatar to the user card");
                            Save(window, directory, "users-before-" + key);
                            await ActivateAsync(window, card, Key.Enter, PhysicalKey.Enter);
                            Require(routes.SequenceEqual(new long[] { 920001 }), "Enter on a user card navigates exactly once to that user's ID");
                            container.CloseDestinationAndReturn();
                            await ActivateAsync(window, card, Key.Space, PhysicalKey.Space);
                            Require(routes.SequenceEqual(new long[] { 920001, 920001 }), "Space on a user card has the same single navigation behavior");
                            container.CloseDestinationAndReturn();
                            var origin = card.TranslatePoint(new Point(card.Bounds.Width - 20, 25), window)!.Value;
                            window.MouseDown(origin, MouseButton.Left);
                            window.MouseUp(origin, MouseButton.Left);
                            await RenderAsync(window);
                            Require(routes.Count == 3 && routes[^1] == 920001, "Pointer activation does not duplicate the card's keyboard/click route");
                            container.CloseDestinationAndReturn();
                            Require(AutomationProperties.GetName(card) == LongName && Equals(ToolTip.GetTip(card), LongName),
                                "A long truncated user name remains available as the card's name and tooltip");
                            Bounds(window, card);

                            var refresh = users.FindControl<CommandBarButton>("RefreshButton")!;
                            Require(refresh.Label == I18NManager.GetResource(WorkContainerResources.RefreshButton.Label),
                                "The user-list refresh action has its localized command label");
                            await ActivateAsync(window, refresh, Key.Space, PhysicalKey.Space);
                            var refreshed = await NextAsync();
                            ValidateRequest(refreshed.Uri, scenario);
                            refreshed.Complete([Sample(920003)]);
                            await UntilAsync(() => model.Source.Count == 1 && model.Source[0].UserId == 920003 && !Source().IsLoading);
                            await RenderAsync(window);
                            Require(!requests.Reader.TryPeek(out _), "One refresh produces one list request");

                            if (scenario.Name == "following-self")
                            {
                                var privacy = page.FindControl<SymbolComboBox>("PrivacyPolicyComboBox")!;
                                var selector = privacy.GetVisualDescendants().OfType<ComboBox>().Single();
                                Require(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(selector)), "Privacy selection exposes its purpose");
                                await ActivateAsync(window, selector, Key.Down, PhysicalKey.ArrowDown);
                                var privateRequest = await NextAsync();
                                Require(HttpUtility.ParseQueryString(privateRequest.Uri.Query)["restrict"] == "private", "The self-following privacy selector sends the private API value");
                                selector.SelectedIndex = 0;
                                privateRequest.Complete([Sample(929999)], "https://app-api.pixiv.net/m3e/obsolete-user-next");
                                var publicRequest = await NextAsync();
                                Require(HttpUtility.ParseQueryString(publicRequest.Uri.Query)["restrict"] == "public", "Changing privacy restores the public request");
                                publicRequest.Complete([Sample(920004)]);
                                await UntilAsync(() => model.Source.Count == 1 && !Source().IsLoading);
                                Require(model.Source[0].UserId == 920004, "The canceled privacy source cannot publish its old user or continue its next page");
                            }
                            else if (scenario.Name == "following-other")
                                Require(page.FindControl<SymbolComboBox>("PrivacyPolicyComboBox") is { IsVisible: false, IsEnabled: false },
                                    "Another user's following page still hides the private-source selector");

                            if (scenario.Name == "recommended")
                                await CheckActionsAsync();
                            Save(window, directory, "users-ready-" + key);
                            report.Add($"PASS {key}: route parameters, card Enter/Space/pointer, named refresh, source replacement and privacy/menu contracts");

                            async Task CheckActionsAsync()
                            {
                                var item = Card(920003);
                                var avatar = item.FindControl<Button>("AvatarButton")!;
                                var entries = ((MenuFlyout) avatar.Flyout!).Items.OfType<MenuItem>().ToArray();
                                var row = (UserItemViewModel) item.DataContext!;
                                var routeCount = routes.Count;
                                await OpenMenuAsync();
                                Require(routes.Count == routeCount, "Opening avatar actions does not also navigate to the profile");
                                var web = entries.Single(menu => Equals(menu.Header, I18NManager.GetResource(EntryItemResources.OpenInWebBrowser)));
                                Require(Equals(web.Tag, row.WebsiteUri), "The original external user URL is preserved");
                                var copy = entries.Single(menu => Equals(menu.Header, I18NManager.GetResource(EntryItemResources.GenerateWebLink)));
                                await ActivateAsync(window, copy, Key.Enter, PhysicalKey.Enter);
                                Require(await window.Clipboard!.TryGetTextAsync() == row.WebsiteUri.ToString(), "Copy user link keeps the complete URL in the isolated clipboard");
                                await OpenMenuAsync();
                                var appLink = entries.Single(menu => Equals(menu.Header, I18NManager.GetResource(EntryItemResources.GenerateLink)));
                                await ActivateAsync(window, appLink, Key.Enter, PhysicalKey.Enter);
                                Require(await window.Clipboard!.TryGetTextAsync() == row.AppUri.ToString(), "Copy app link preserves its scheme and user ID");
                                await OpenMenuAsync();
                                var follow = entries.Single(menu => ReferenceEquals(menu.Command, row.FollowCommand));
                                Require(Equals(follow.Header, I18NManager.GetResource(UserViewerPageResources.Follow)), "An unfollowed user's action is labeled Follow");
                                var before = followRequests.Count;
                                await ActivateAsync(window, follow, Key.Enter, PhysicalKey.Enter);
                                await UntilAsync(() => row.IsFollowed && !row.FollowCommand.IsRunning);
                                Require(followRequests.Count == before + 1 && followRequests.Last().Path == "/v1/user/follow/add"
                                    && HttpUtility.ParseQueryString(followRequests.Last().Body)["user_id"] == "920003"
                                    && HttpUtility.ParseQueryString(followRequests.Last().Body)["restrict"] == "public",
                                    "Follow invokes one existing public-follow command with the exact user ID");
                                await OpenMenuAsync();
                                Require(Equals(follow.Header, I18NManager.GetResource(UserViewerPageResources.Unfollow)), "The same menu exposes Unfollow after following succeeds");
                                await ActivateAsync(window, follow, Key.Enter, PhysicalKey.Enter);
                                await UntilAsync(() => !row.IsFollowed && !row.FollowCommand.IsRunning);
                                Require(followRequests.Count == before + 2 && followRequests.Last().Path == "/v1/user/follow/delete",
                                    "Unfollow uses the existing remove endpoint exactly once");
                                await OpenMenuAsync();
                                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                                window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                                await RenderAsync(window);
                                Require(!avatar.Flyout.IsOpen && routes.Count == routeCount, "Escape closes avatar actions without navigating");
                                var blockedUsers = App.AppViewModel.AppServiceProvider.GetRequiredService<BlockedUserPersistentManager>();
                                try
                                {
                                    await OpenMenuAsync();
                                    var block = entries.Single(menu => ReferenceEquals(menu.Command, row.BlockUserCommand));
                                    await ActivateAsync(window, block, Key.Enter, PhysicalKey.Enter);
                                    Require(blockedUsers.GetByUserId(row.UserId)?.Name == LongName && !row.BlockUserCommand.CanExecute(null),
                                        "Block user persists the original ID/name in the isolated database and disables duplicate blocking");
                                }
                                finally
                                {
                                    _ = blockedUsers.TryDeleteByUserId(row.UserId);
                                }

                                async Task OpenMenuAsync()
                                {
                                    await ActivateAsync(window, avatar, Key.Space, PhysicalKey.Space);
                                    Require(avatar.Flyout!.IsOpen, "Avatar actions can be opened with the keyboard");
                                    Require(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(avatar)), "Avatar actions have a usable automation name");
                                }
                            }

                            UserItem Card(long id) => view.GetVisualDescendants().OfType<UserItem>().Single(control => control.DataContext is UserItemViewModel row && row.UserId == id);
                            IncrementalLoadingCollection<UserItemViewModel> Source() => (IncrementalLoadingCollection<UserItemViewModel>) model.Source;
                        }
                        finally
                        {
                            container.Shell.CloseAllPages();
                            window.Close();
                        }
                    }
                }
            await SourceContractsAsync();
            Require(fixture.UnexpectedRequests.IsEmpty, "All user-page HTTP requests remained inside the fixed endpoint matrix");
            Require(detailTokens.All(token => token.IsCancellationRequested), "Observed profile destinations released their pending detail queries");
            File.WriteAllLines(Path.Combine(directory, "user-page-contracts.txt"), report);
            File.WriteAllLines(Path.Combine(directory, "user-page-requests.txt"), fixture.RequestLog);
            Console.WriteLine($"Validated {report.Count} user-page groups.");
        }
        finally
        {
            foreach (var item in pending)
                _ = item.Response.TrySetResult("{\"user_previews\":[],\"next_url\":null}");
        }

        Task<PendingUsers> NextAsync() => requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        User Sample(long id) => User.CreateDefault() with
        {
            UserInfo = UserInfo.CreateDefault() with { Id = id, Name = LongName, ProfileImageUrls = new() { Medium = imageUri } },
            Illustrations = [Illustration.CreateDefault() with { Id = id + 1000, ThumbnailUrls = new() { Medium = imageUri, SquareMedium = imageUri, Large = imageUri } }]
        };

        async Task SourceContractsAsync()
        {
            var empty = new UserSearchResultPage(null);
            var container = new PageTestHost { Content = empty };
            var window = new Window { Width = 360, Height = 720, Content = container.Shell };
            var emptyModel = (UserViewViewModel) empty.FindControl<UserContainer>("UserContainer")!.FindControl<UserView>("UserView")!.DataContext!;
            UserViewViewModel? pagedModel = null;
            UserViewViewModel? injected = null;
            UserViewViewModel? replacement = null;
            try
            {
                window.Show();
                await RenderAsync(window);
                Require(emptyModel.Source.Count == 0 && !requests.Reader.TryPeek(out _), "A user search without a keyword keeps its empty computed source and sends no HTTP request");
                Save(window, directory, "users-empty-search");
                var page = new UserRecommendedPage();
                var users = page.FindControl<UserContainer>("UserContainer")!;
                var view = users.FindControl<UserView>("UserView")!;
                pagedModel = (UserViewViewModel) view.DataContext!;
                container.Content = page;
                var first = await NextAsync();
                first.Complete([Sample(920011)], "https://app-api.pixiv.net/m3e/user-next?offset=1");
                var second = await NextAsync();
                Require(second.Uri.AbsolutePath == "/m3e/user-next" && HttpUtility.ParseQueryString(second.Uri.Query)["offset"] == "1",
                    "User pagination follows the exact service next_url");
                second.Complete([Sample(920011), Sample(920012)]);
                await UntilAsync(() => pagedModel.Source.Count == 2 && !((IncrementalLoadingCollection<UserItemViewModel>) pagedModel.Source).IsLoading);
                Require(pagedModel.Source.Select(row => row.UserId).SequenceEqual(new long[] { 920011, 920012 }), "Pagination retains order and de-duplicates repeated user identities");
                await ActivateAsync(window, users.FindControl<CommandBarButton>("RefreshButton")!, Key.Space, PhysicalKey.Space);
                var late = await NextAsync();
                var canceledSource = pagedModel.Source;
                container.Shell.CloseAllPages();
                late.Complete([Sample(929998)], "https://app-api.pixiv.net/m3e/obsolete-user-next");
                await RenderAsync(window);
                Require(canceledSource.Count == 0 && !requests.Reader.TryPeek(out _), "Disposing a loading user source rejects the late user and does not fetch its next page");

                injected = new UserViewViewModel();
                injected.ResetEngine(client.Computed(new[] { Sample(920013) }.ToAsyncEnumerable()), static (user, _) => new(user));
                var injectedPage = new UserRecommendedPage(injected);
                var injectedView = injectedPage.FindControl<UserContainer>("UserContainer")!.FindControl<UserView>("UserView")!;
                container.Content = injectedPage;
                await UntilAsync(() => injected.Source.Count == 1);
                await RenderAsync(window);
                Require(ReferenceEquals(injectedView.DataContext, injected) && !requests.Reader.TryPeek(out _), "A supplied user view model is reused without an unsolicited replacement request");
                replacement = new UserViewViewModel();
                replacement.ResetEngine(client.Computed(new[] { Sample(920014) }.ToAsyncEnumerable()), static (user, _) => new(user));
                var previousSource = injected.Source;
                injectedView.SetViewModel(replacement);
                await UntilAsync(() => replacement.Source.Count == 1);
                await RenderAsync(window);
                Require(previousSource.Count == 0, "Replacing an owned user model releases its old source");
                var count = container.Navigations;
                var item = injectedView.GetVisualDescendants().OfType<UserItem>().Single();
                await ActivateAsync(window, item, Key.Enter, PhysicalKey.Enter);
                Require(container.Navigations == count + 1 && ((UserItemViewModel) item.DataContext!).UserId == 920014,
                    "A rebuilt card keeps one navigation handler bound to the replacement model");
                container.CloseDestinationAndReturn();
                report.Add("PASS source lifetime: empty query, next_url/order/de-duplication, late closed source, supplied model and replacement ownership");
            }
            finally
            {
                container.Shell.CloseAllPages();
                pagedModel?.Dispose();
                injected?.Dispose();
                replacement?.Dispose();
                window.Close();
            }
        }
    }

    private static IEnumerable<Scenario> Scenarios()
    {
        yield return new("recommended", () => new UserRecommendedPage(), "/v1/user/recommended");
        yield return new("search", () => new UserSearchResultPage("画师 fixture"), "/v1/search/user", "word", "画师 fixture");
        yield return new("following-self", () => new UserFollowingPage(900001), "/v1/user/following", "user_id", "900001");
        yield return new("following-other", () => new UserFollowingPage(900007), "/v1/user/following", "user_id", "900007");
        yield return new("follower", () => new UserFollowerPage(), "/v1/user/follower");
        yield return new("mypixiv", () => new UserMyPixivPage(900008), "/v1/user/mypixiv", "user_id", "900008");
        yield return new("related", () => new RelatedUsersPage(900009), "/v1/user/related", "seed_user_id", "900009");
    }

    private static void ValidateRequest(Uri uri, Scenario scenario)
    {
        Require(uri.AbsolutePath == scenario.Path && (scenario.Parameter is null || HttpUtility.ParseQueryString(uri.Query)[scenario.Parameter] == scenario.Value),
            "The user page preserves its original API path and author/search parameters");
    }

    private static async Task ActivateAsync(Window window, Control control, Key key, PhysicalKey physical)
    {
        control.BringIntoView();
        await RenderAsync(window);
        Require(control.Focus(NavigationMethod.Tab), "The user-page action is keyboard reachable");
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
        await RenderAsync(window);
    }

    private static void Bounds(Window window, Control control)
    {
        var origin = control.TranslatePoint(default, window) ?? throw new InvalidOperationException("Detached user control");
        Require(control.Bounds.Width > 0 && origin.X >= -1 && origin.X + control.Bounds.Width <= window.ClientSize.Width + 1,
            "User controls stay within the horizontal viewport");
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(300);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The user-page operation did not reach the expected state.");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No user-page frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }

    private sealed record Scenario(string Name, Func<ContentPage> Create, string Path, string? Parameter = null, string? Value = null);

    private sealed class PendingUsers(Uri uri)
    {
        internal Uri Uri { get; } = uri;
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Complete(IReadOnlyList<User> users, string? next = null) =>
            Response.SetResult(JsonSerializer.Serialize(new { user_previews = users, next_url = next }));
    }
}
