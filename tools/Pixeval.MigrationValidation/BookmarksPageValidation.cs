using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Global.Enum;
using Mako.Model;
using Pixeval.Controls;
using Pixeval.Utilities;
using Pixeval.Views.Capability;
using Pixeval.Views.Work;

namespace Pixeval.MigrationValidation;

internal static class BookmarksPageValidation
{
    private const string InitialTag = "指定收藏标签 with a deliberately long name ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        var tags = Channel.CreateUnbounded<PendingTags>();
        ConcurrentBag<PendingTags> pending = [];
        ConcurrentQueue<Uri> workRequests = [];
        fixture.BookmarkResponseFactory = async (request, _) =>
        {
            var item = new PendingTags(request.RequestUri!);
            pending.Add(item);
            await tags.Writer.WriteAsync(item);
            return await item.Response.Task; // Deliberately allow a response after its owner is obsolete.
        };
        fixture.BookmarkWorksResponseFactory = (request, _) =>
        {
            workRequests.Enqueue(request.RequestUri!);
            return Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("novel", StringComparison.Ordinal)
                ? "{\"novels\":[],\"next_url\":null}" : "{\"illusts\":[],\"next_url\":null}");
        };
        await client.SetTokenAsync("fixture-bookmarks-page");
        await client.ValidateTokenAsync();
        List<string> report = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 360, 1000 })
            {
                workRequests.Clear();
                var page = new WorkBookmarksPage(client.Me!, SimpleWorkType.Illustration, PrivacyPolicy.Public, InitialTag);
                var navigation = new NavigationPage();
                await navigation.PushAsync(page);
                var container = new SearchCompletionValidation.FixtureViewContainer { Content = navigation };
                var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = container };
                var scenario = $"{theme.Key}-{width}";
                var works = page.FindControl<WorkContainer>("WorkContainer")!;
                var picker = page.FindControl<ComboBox>("TagComboBox")!;
                try
                {
                    window.Show();
                    await RenderAsync(window);
                    var initial = await NextAsync();
                    await UntilAsync(() => !workRequests.IsEmpty);
                    Require(workRequests.All(uri => HttpUtility.ParseQueryString(uri.Query)["tag"] == InitialTag),
                        "The explicit initial bookmark tag is applied to the very first work request");
                    initial.Complete([InitialTag, "another tag"]);
                    await UntilAsync(() => picker.ItemsSource!.Cast<BookmarkTag>().Any(tag => tag.Name == InitialTag));
                    await RenderAsync(window);
                    Require((picker.SelectedItem as BookmarkTag)?.Name == InitialTag, "Loaded tags preserve the explicit initial selection");
                    HorizontalBounds(window, picker);
                    foreach (var text in picker.GetVisualDescendants().OfType<TextBlock>())
                    {
                        HorizontalBounds(window, text);
                        Require(text.TextLayout.Width <= text.Bounds.Width + 1 && text.TextLayout.Height <= text.Bounds.Height + 1
                            && text.TextLayout.TextLines.All(line => !line.HasCollapsed), "The full selected bookmark tag remains readable");
                    }
                    Save(window, directory, "bookmarks-ready-" + scenario);
                    Require(picker.Focus(NavigationMethod.Tab), "Bookmark tags can be opened from the keyboard");
                    Press(window, Key.Space, PhysicalKey.Space);
                    await RenderAsync(window);
                    Require(picker.IsDropDownOpen, "The tag selector opens its real dropdown");
                    var options = window.GetVisualDescendants().OfType<ComboBoxItem>().Where(item => item.DataContext is BookmarkTag).ToArray();
                    Require(options.Length >= 3, "The dropdown exposes the loaded bookmark tag choices");
                    foreach (var option in options)
                    {
                        HorizontalBounds(window, option);
                        foreach (var text in option.GetVisualDescendants().OfType<TextBlock>())
                        {
                            HorizontalBounds(window, text);
                            Require(text.TextLayout.Width <= text.Bounds.Width + 1 && text.TextLayout.TextLines.All(line => !line.HasCollapsed),
                                "Long bookmark tags remain readable in the dropdown");
                        }
                    }
                    Save(window, directory, "bookmarks-dropdown-" + scenario);
                    Press(window, Key.Escape, PhysicalKey.Escape);
                    await RenderAsync(window);
                    Require(!picker.IsDropDownOpen && (picker.SelectedItem as BookmarkTag)?.Name == InitialTag, "Escape closes the tag dropdown without changing its filter");

                    var type = page.FindControl<SymbolComboBox>("SimpleWorkTypeComboBox")!;
                    var privacy = page.FindControl<SymbolComboBox>("PrivacyPolicyComboBox")!;
                    Require(new Control[] { picker, type.GetVisualDescendants().OfType<ComboBox>().Single(), privacy.GetVisualDescendants().OfType<ComboBox>().Single() }
                        .All(control => !string.IsNullOrWhiteSpace(AutomationProperties.GetName(control))), "Bookmark selectors expose their localized purpose");
                    SelectNext(window, type);
                    var old = await NextAsync();
                    Require(old.Uri.AbsolutePath.EndsWith("novel", StringComparison.Ordinal), "Changing work type requests novel bookmark tags");
                    SelectNext(window, privacy);
                    var current = await NextAsync();
                    Require(HttpUtility.ParseQueryString(current.Uri.Query)["restrict"] == "private", "Privacy selection reaches the actual tag request");
                    current.Complete(["current tag"]);
                    await UntilAsync(() => picker.ItemsSource!.Cast<BookmarkTag>().Any(tag => tag.Name == "current tag"));
                    if (theme == ThemeVariant.Light)
                        old.Complete(["stale tag"], "https://app-api.pixiv.net/m3e/stale-bookmark-next");
                    else
                        old.Response.SetException(new HttpRequestException("Fixed obsolete bookmark tag failure"));
                    await RenderAsync(window);
                    Require(picker.ItemsSource!.Cast<BookmarkTag>().Any(tag => tag.Name == "current tag")
                        && picker.ItemsSource!.Cast<BookmarkTag>().All(tag => tag.Name != "stale tag"), "Late tag results cannot replace the current type/privacy list");
                    Require((picker.SelectedItem as BookmarkTag)?.Name == InitialTag, "Changing type or privacy keeps the explicit active tag");
                    Require(((BookmarkTag) picker.SelectedItem!).Count == 0, "A retained tag absent from the new source does not display the old source's count");
                    Require(container.Notifications.All(notification => notification.Type != NotificationType.Error), "An obsolete tag failure cannot show a current-page error");
                    var count = workRequests.Count;
                    Require(picker.Focus(NavigationMethod.Tab), "Bookmark tag selection remains keyboard reachable");
                    Press(window, Key.Up, PhysicalKey.ArrowUp);
                    await UntilAsync(() => workRequests.Count > count);
                    var query = HttpUtility.ParseQueryString(workRequests.Last().Query);
                    Require(query["tag"] == "current tag" && query["restrict"] == "private" && query["user_id"] == "900001"
                        && workRequests.Last().AbsolutePath.EndsWith("novel", StringComparison.Ordinal), "Tag keyboard selection preserves the exact user/type/privacy/filter request");

                    await RefreshAsync();
                    var failure = await NextAsync();
                    failure.Response.SetException(new HttpRequestException("Fixed bookmark tag failure"));
                    await UntilAsync(() => container.Notifications.Any(notification => notification.Type == NotificationType.Error));
                    Require((picker.SelectedItem as BookmarkTag)?.Name == "current tag", "Current tag failure leaves the user's chosen filter intact");
                    Save(window, directory, "bookmarks-error-" + scenario);
                    await RefreshAsync();
                    var retry = await NextAsync();
                    count = workRequests.Count;
                    Require(picker.Focus(NavigationMethod.Tab), "The active tag can still be edited while refreshing tags");
                    for (var index = picker.SelectedIndex; index > 0; index--)
                        Press(window, Key.Up, PhysicalKey.ArrowUp);
                    await UntilAsync(() => workRequests.Count > count && HttpUtility.ParseQueryString(workRequests.Last().Query)["tag"] is null);
                    Require(HttpUtility.ParseQueryString(workRequests.Last().Query)["tag"] is null, "All bookmarks omits the tag filter");
                    retry.Complete(["current tag", "retry tag"]);
                    await UntilAsync(() => picker.ItemsSource!.Cast<BookmarkTag>().Any(tag => tag.Name == "retry tag"));
                    Require(picker.SelectedItem is AllBookmarkTag, "Retry preserves a filter chosen while the request was pending");
                    count = workRequests.Count;
                    Press(window, Key.Down, PhysicalKey.ArrowDown);
                    await UntilAsync(() => workRequests.Count > count);
                    Require(picker.SelectedItem is UncategorizedBookmarkTag && HttpUtility.ParseQueryString(workRequests.Last().Query)["tag"] == "未分類",
                        "Uncategorized keeps the API sentinel independently of the UI language");
                    await RefreshAsync();
                    var unloaded = await NextAsync();
                    var source = picker.ItemsSource;
                    container.Content = null;
                    await RenderAsync(window);
                    unloaded.Complete(["unloaded tag"], "https://app-api.pixiv.net/m3e/unloaded-bookmark-next");
                    await RenderAsync(window);
                    Require(ReferenceEquals(source, picker.ItemsSource), "Unloaded page rejects the late tag response");
                    Require(fixture.UnexpectedRequests.IsEmpty, "The canceled nonempty tag response does not continue to its next page");
                    container.Content = navigation;
                    await RenderAsync(window);
                    var reloaded = await NextAsync();
                    reloaded.Complete(["reloaded tag"]);
                    await UntilAsync(() => picker.ItemsSource!.Cast<BookmarkTag>().Any(tag => tag.Name == "reloaded tag"));
                    Require(picker.SelectedItem is UncategorizedBookmarkTag, "Reload starts a new tag request and keeps the current filter");
                    window.Width = width == 360 ? 1000 : 360;
                    await RenderAsync(window);
                    HorizontalBounds(window, picker);
                    report.Add($"PASS {scenario}: initial filter, complete compact tag, reverse type/privacy responses and obsolete failure, exact all/uncategorized/tag requests, visible failure/retry, edits during loading, unload/reload and resize");

                    async Task RefreshAsync()
                    {
                        var bar = works.FindControl<CommandBar>("RightToolBar")!;
                        bar.IsOpen = true;
                        await RenderAsync(window);
                        var refresh = works.FindControl<CommandBarButton>("RefreshButton")!;
                        Require(refresh.Focus(NavigationMethod.Tab), "The existing refresh action remains reachable in overflow");
                        Press(window, Key.Enter, PhysicalKey.Enter);
                        await RenderAsync(window);
                    }
                }
                finally
                {
                    window.Content = null;
                    window.Close();
                    works.FindControl<WorkView>("WorkView")!.Dispose();
                    foreach (var request in pending)
                        request.TryComplete();
                }
            }
        File.WriteAllLines(Path.Combine(directory, "bookmark-page-contracts.txt"), report);
        File.WriteAllLines(Path.Combine(directory, "offline-requests.txt"), fixture.RequestLog);
        Console.WriteLine($"Validated {report.Count} bookmark page groups.");
        return;

        Task<PendingTags> NextAsync() => tags.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(8));
    }

    private static void SelectNext(Window window, SymbolComboBox selector)
    {
        var inner = selector.GetVisualDescendants().OfType<ComboBox>().Single();
        Require(inner.Focus(NavigationMethod.Tab), "Bookmark source selector is keyboard reachable");
        Press(window, Key.Down, PhysicalKey.ArrowDown);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Require(condition(), "The controlled bookmark operation did not complete");
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(350);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, "");
        window.KeyRelease(key, RawInputModifiers.None, physical, "");
    }

    private static void HorizontalBounds(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window);
        Require(point is { } p && p.X >= -1 && p.X + control.Bounds.Width <= window.Bounds.Width + 1 && control.Bounds.Width > 0,
            $"{control.GetType().Name} stays inside the {window.Width} DIP bookmarks page ({point}, {control.Bounds.Size})");
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No bookmarks page frame.");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private sealed class PendingTags(Uri uri)
    {
        internal Uri Uri { get; } = uri;
        internal TaskCompletionSource<string> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Complete(string[] names, string? next = null) => Response.SetResult(JsonSerializer.Serialize(new
        {
            bookmark_tags = names.Select(name => new { name, count = 3 }).ToArray(),
            next_url = next
        }));
        internal void TryComplete() => Response.TrySetResult("{\"bookmark_tags\":[],\"next_url\":null}");
    }

    private static void Require(bool condition, string contract)
    {
        if (!condition)
            throw new InvalidOperationException(contract);
    }
}
