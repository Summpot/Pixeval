using System;
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
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Model;
using Mako.Net.Responses;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.I18N;
using Pixeval.Models.Blocking;
using Pixeval.Models.Database.Managers;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views;
using Pixeval.Views.Capability;
using Pixeval.Views.Viewers;

namespace Pixeval.MigrationValidation;

internal static class UserDetailsValidation
{
    private const long UserId = 993007;
    private const string LongName = "长用户名 / A very long creator name ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string ImageUri = "https://i.pximg.net/m3e/user-details.png";
    private static readonly string Description = string.Join('\n', Enumerable.Range(1, 20).Select(index => $"{index}: 用户介绍 / A long creator profile with selectable text."));

    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        await client.SetTokenAsync("fixture-user-details");
        await client.ValidateTokenAsync();
        using (var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var buffer = new MemoryStream())
        {
            await asset.CopyToAsync(buffer);
            fixture.ImageResponses[ImageUri] = buffer.ToArray();
        }
        var follows = Channel.CreateUnbounded<Pending>();
        var details = Channel.CreateUnbounded<Pending>();
        List<Pending> pending = [];
        fixture.UserResponseFactory = async (request, token) =>
        {
            var uri = request.RequestUri!;
            if (uri.AbsolutePath is "/v1/user/detail" or "/v1/user/follow/add" or "/v1/user/follow/delete")
            {
                var operation = new Pending(uri, request.Content is null ? "" : await request.Content.ReadAsStringAsync(token), token);
                pending.Add(operation);
                await (uri.AbsolutePath == "/v1/user/detail" ? details : follows).Writer.WriteAsync(operation);
                return await operation.Response.Task;
            }
            return "{\"user_previews\":[],\"next_url\":null}";
        };
        fixture.WorkFeedResponseFactory = (_, _) => Task.FromResult("{\"illusts\":[],\"novels\":[],\"next_url\":null}");
        fixture.BookmarkWorksResponseFactory = fixture.WorkFeedResponseFactory;
        fixture.BookmarkResponseFactory = (_, _) => Task.FromResult("{\"bookmark_tags\":[],\"next_url\":null}");
        List<string> report = [];
        try
        {
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                foreach (var width in new[] { 360, 1000 })
                {
                    using var model = new UserViewerPageViewModel(Sample());
                    var page = new UserViewerPage(model);
                    var host = new SearchCompletionValidation.FixtureViewContainer { Content = page };
                    var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = host };
                    var key = $"{theme.Key}-{width}";
                    try
                    {
                        window.Show();
                        var expander = page.FindControl<Expander>("UserInfoExpander")!;
                        expander.IsExpanded = true;
                        await RenderAsync(window);
                        Save(window, directory, "user-expanded-" + key);
                        var title = page.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == LongName && text.IsEffectivelyVisible);
                        Require(title.TextLayout.TextLines.All(line => !line.HasCollapsed) && title.TextLayout.Width <= title.Bounds.Width + 1,
                            "The expanded profile retains a readable complete long name");
                        var description = page.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == Description);
                        var scroller = description.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
                        Require(scroller is not null && scroller.Extent.Height > scroller.Viewport.Height,
                            "Long user descriptions remain scrollable rather than clipped");
                        Require(scroller!.Focus(NavigationMethod.Tab), "The profile scroll region is keyboard reachable");
                        for (var step = 0; step <= Math.Ceiling(scroller.Extent.Height / scroller.Viewport.Height); step++)
                            Press(window, Key.PageDown, PhysicalKey.PageDown);
                        await RenderAsync(window);
                        Require(scroller.Offset.Y > 0, "The end of the creator description is reachable");
                        var lastLine = description.TranslatePoint(new Point(0, description.Bounds.Height), scroller)!.Value;
                        Require(lastLine.Y > 0 && lastLine.Y <= scroller.Bounds.Height + 1,
                            "The final description line is actually inside the visible profile viewport");
                        Save(window, directory, "user-description-end-" + key);
                        var idText = page.FindControl<SelectableTextBlock>("ProfileIdText")!;
                        idText.BringIntoView();
                        await RenderAsync(window);
                        Require(idText.Focus(NavigationMethod.Tab), "The original user ID is keyboard selectable");
                        window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, null);
                        window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, null);
                        window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, null);
                        window.KeyRelease(Key.C, RawInputModifiers.Control, PhysicalKey.C, null);
                        await RenderAsync(window);
                        Require(await window.Clipboard!.TryGetTextAsync() == UserId.ToString(), "The complete user ID can be copied through the actual text control");
                        expander.IsExpanded = false;
                        await RenderAsync(window);
                        var bar = page.GetVisualDescendants().OfType<CommandBar>().First();
                        var copyApp = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => button.Label == Text(EntryItemResources.GenerateLink));
                        await ActivateAsync(window, bar, copyApp);
                        Require(await window.Clipboard!.TryGetTextAsync() == Sample().UserEntity.AppUri.ToString(), "Profile copy preserves the original app URI");
                        var copyWeb = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => button.Label == Text(EntryItemResources.GenerateWebLink));
                        await ActivateAsync(window, bar, copyWeb);
                        Require(await window.Clipboard!.TryGetTextAsync() == Sample().UserEntity.WebsiteUri.ToString(), "Profile copy preserves the complete web URI");
                        var follow = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => ReferenceEquals(button.Command, model.FollowCommand));
                        await ActivateAsync(window, bar, follow);
                        var operation = await NextAsync(follows);
                        Require(!model.FollowPrivatelyCommand.CanExecute(null) && !model.UnfollowCommand.CanExecute(null),
                            "Public/private/unfollow share one pending state");
                        await model.FollowPrivatelyCommand.ExecuteAsync(null);
                        await model.UnfollowCommand.ExecuteAsync(null);
                        Require(!follows.Reader.TryPeek(out _), "Direct repeated actions cannot bypass the shared pending guard");
                        Require(HttpUtility.ParseQueryString(operation.Body)["user_id"] == UserId.ToString()
                            && HttpUtility.ParseQueryString(operation.Body)["restrict"] == "public", "The actual follow action preserves ID and public privacy");
                        operation.Response.SetResult("{}");
                        await UntilAsync(() => model.IsFollowed);
                        await RenderAsync(window);
                        Require(!follow.IsVisible && bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => ReferenceEquals(button.Command, model.UnfollowCommand)).IsVisible,
                            "Successful follow switches the visible action");
                        var tabs = page.GetVisualDescendants().OfType<TabbedPage>().Single();
                        var pages = tabs.Pages!.Cast<Page>().ToArray();
                        Require(pages.Select(item => item.GetType()).SequenceEqual(new[] { typeof(WorkPostsPage), typeof(WorkBookmarksPage), typeof(UserFollowingPage), typeof(UserMyPixivPage), typeof(RelatedUsersPage) }),
                            "All five original user list destinations remain available in order");
                        for (var index = 0; index < pages.Length; index++)
                        {
                            tabs.SelectedIndex = index;
                            await RenderAsync(window);
                            Require(ReferenceEquals(tabs.SelectedPage, pages[index]), "Each profile list tab retains its destination");
                        }
                        Save(window, directory, "user-collapsed-" + key);
                        if (theme == ThemeVariant.Dark && width == 1000)
                        {
                            var block = bar.SecondaryCommands.OfType<CommandBarButton>().Single(button => ReferenceEquals(button.Command, model.BlockUserCommand));
                            await ActivateAsync(window, bar, block);
                            Require(BlockedContentHelper.IsBlocked(Sample().UserEntity) && !model.BlockUserCommand.CanExecute(null)
                                && model.Id == UserId, "The existing overflow block action writes the isolated block list and retains the user identity");
                            App.AppViewModel.AppServiceProvider.GetRequiredService<BlockedUserPersistentManager>().TryDeleteByUserId(UserId);
                        }
                        report.Add($"PASS {key}: full profile text/scroll, copy, real follow/pending state and five list destinations");
                    }
                    finally
                    {
                        foreach (var list in page.GetLogicalDescendants().OfType<Control>().Select(control => control.DataContext)
                                     .OfType<ISimpleViewViewModel>().Distinct().OfType<IDisposable>())
                            list.Dispose();
                        host.Content = null;
                        window.Close();
                    }
                }

            using (var model = new UserViewerPageViewModel(Sample()))
            {
                var action = model.FollowPrivatelyCommand.ExecuteAsync(null);
                var operation = await NextAsync(follows);
                Require(HttpUtility.ParseQueryString(operation.Body)["restrict"] == "private", "Private follow retains its exact payload");
                operation.Response.SetException(new HttpRequestException("Fixed follow failure"));
                await action;
                Require(!model.IsFollowed && model.FollowPrivatelyCommand.CanExecute(null), "Failed follow preserves state and permits retry");
                action = model.FollowPrivatelyCommand.ExecuteAsync(null);
                (await NextAsync(follows)).Response.SetResult("{}");
                await action;
                Require(model.IsFollowed, "Private retry succeeds");
                action = model.UnfollowCommand.ExecuteAsync(null);
                operation = await NextAsync(follows);
                Require(operation.Uri.AbsolutePath == "/v1/user/follow/delete", "Unfollow keeps its original endpoint");
                operation.Response.SetResult("{}");
                await action;
                Require(!model.IsFollowed, "Unfollow updates the model");
                action = model.FollowCommand.ExecuteAsync(null);
                operation = await NextAsync(follows);
                model.Dispose();
                Require(operation.Token.IsCancellationRequested, "Closing the profile cancels an active follow request");
                operation.Response.SetResult("{}");
                await action;
                Require(!model.IsFollowed && !model.FollowCommand.CanExecute(null), "Late follow cannot update or re-enable a closed profile");
                report.Add("PASS follow-lifetime: private/failure/retry/unfollow, close cancellation and no late state write");
            }
            using (var model = new UserViewerPageViewModel(UserId))
            {
                var operation = await NextAsync(details);
                Require(model.IsLoading && !model.FollowCommand.CanExecute(null), "Follow is unavailable before user details are ready");
                operation.Response.SetException(new HttpRequestException("Fixed profile failure"));
                await UntilAsync(() => !model.IsLoading);
                Require(model.UserDetail is null && !string.IsNullOrWhiteSpace(model.LoadErrorMessage) && model.Id == UserId,
                    "A failed profile cannot become a successful empty user with ID zero");
                report.Add("PASS detail-failure: original identity, error state and command gating");
            }
            using (var model = new UserViewerPageViewModel(UserId))
            {
                var operation = await NextAsync(details);
                model.Dispose();
                Require(operation.Token.IsCancellationRequested, "Closing cancels the detail request");
                operation.Response.SetResult(JsonSerializer.Serialize(Sample()));
                await Task.Delay(100);
                Dispatcher.UIThread.RunJobs();
                Require(model.UserDetail is null && !model.IsLoading, "Late detail never populates a closed profile");
                report.Add("PASS detail-close: cancellation and late successful response isolation");
            }
            using (var model = new UserViewerPageViewModel(Sample()))
            {
                var action = model.FollowCommand.ExecuteAsync(null);
                var operation = await NextAsync(follows);
                fixture.AuthResponseFactory = (_, _) => Task.FromResult(JsonSerializer.Serialize(TokenResponse.CreateDefault() with
                {
                    AccessToken = "fixture-second-access",
                    RefreshToken = "fixture-second-refresh",
                    ExpiresIn = 3600,
                    User = TokenUser.CreateDefault() with { Id = 900002, Name = "Fixture second account", Account = "fixture-second" }
                }));
                await client.SetTokenAsync("fixture-second-account");
                await client.ValidateTokenAsync();
                Require(client.Me?.Id == 900002, "The account-switch sample actually changes the isolated account");
                operation.Response.SetResult("{}");
                await action;
                Require(!model.IsFollowed && !model.UserDetail!.UserEntity.IsFollowed, "An old account's follow result cannot update the new account's profile state");
                fixture.AuthResponseFactory = null;
                await client.SetTokenAsync("fixture-user-details");
                await client.ValidateTokenAsync();
                report.Add("PASS follow-account-change: late previous-account response cannot publish follow state");
            }
            using (var self = new UserViewerPageViewModel(Sample() with { UserEntity = Sample().UserEntity with { Id = 900001 } }))
                Require(!self.FollowCommand.CanExecute(null) && !self.FollowPrivatelyCommand.CanExecute(null) && !self.UnfollowCommand.CanExecute(null), "Own-account follow actions stay disabled");
            Require(fixture.UnexpectedRequests.IsEmpty, "User details remain inside the offline endpoint matrix");
            File.WriteAllLines(Path.Combine(directory, "user-detail-contracts.txt"), report);
            Console.WriteLine($"Validated {report.Count} user-detail groups.");
        }
        finally
        {
            foreach (var operation in pending)
                operation.Response.TrySetResult(operation.Uri.AbsolutePath == "/v1/user/detail" ? JsonSerializer.Serialize(Sample()) : "{}");
            File.WriteAllLines(Path.Combine(directory, "user-detail-requests.txt"), fixture.RequestLog);
        }
    }

    private static SingleUserResponse Sample() => SingleUserResponse.CreateDefault() with
    {
        UserEntity = SingleUserInfo.CreateDefault() with { Id = UserId, Name = LongName, Description = Description, ProfileImageUrls = new() { Medium = ImageUri } },
        UserProfile = Profile.CreateDefault() with { TotalFollowUsers = 1234567, TotalMyPixivUsers = 12345, TotalIllustrations = 123456, TotalManga = 123456, TotalNovels = 123456, TotalNovelSeries = 12345, BackgroundImageUrl = ImageUri, IsPremium = true }
    };
    private static string Text(string key) => I18NManager.GetResource(key);
    private static Task<Pending> NextAsync(Channel<Pending> channel) => channel.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    private static async Task ActivateAsync(Window window, CommandBar bar, CommandBarButton button)
    {
        if (button.IsInOverflow)
        {
            var more = bar.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "PART_OverflowButton");
            Require(more.Focus(NavigationMethod.Tab), "Profile overflow is keyboard reachable");
            Press(window, Key.Enter, PhysicalKey.Enter);
            await RenderAsync(window);
            Require(bar.IsOpen, "The actual overflow opens");
        }
        var origin = button.TranslatePoint(default, window)!.Value;
        Require(button.Bounds.Width > 0 && origin.X >= -1 && origin.X + button.Bounds.Width <= window.ClientSize.Width + 1,
            "Profile commands remain inside the window");
        Require(button.Focus(NavigationMethod.Tab), "Profile actions remain keyboard reachable");
        Press(window, Key.Enter, PhysicalKey.Enter);
        await RenderAsync(window);
        if (bar.IsOpen)
        {
            Press(window, Key.Escape, PhysicalKey.Escape);
            await RenderAsync(window);
        }
    }
    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
    }
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The user detail operation did not reach its expected state");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }
    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(250);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }
    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No user profile frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
    private static void Require(bool condition, string contract)
    {
        if (!condition)
            throw new InvalidOperationException(contract);
    }
    private sealed record Pending(Uri Uri, string Body, CancellationToken Token)
    {
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
