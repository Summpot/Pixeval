using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.Config;
using Pixeval.Storage;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Search;
using Pixeval.Views.Viewers;
using Pixeval.Views.Works;

namespace Pixeval.Tests.Ui;

internal static class WorkCardsValidation
{
    private const string Title = "长作品标题 / A long artwork title ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string Author = "很长的作者名称 / A long author name";
    private const string TagName = "LongTagABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    internal static async Task RunAsync(string directory)
    {
        using var fixture = new OfflineSearchFixture(App.AppViewModel.MakoClient);
        App.AppViewModel.MakoClient.SetRefreshToken("fixture-work-cards");
        await FixtureAuthentication.IdentifyAsync(App.AppViewModel.MakoClient);
        fixture.BookmarkResponseFactory = (request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/bookmark/detail", StringComparison.Ordinal)
            ? "{\"bookmark_detail\":{\"is_bookmarked\":false,\"restrict\":\"public\",\"tags\":[]}}"
            : "{\"bookmark_tags\":[],\"next_url\":null}");
        var bookmarks = Channel.CreateUnbounded<(Uri Uri, string Body, TaskCompletionSource<string> Response)>();
        fixture.WorkCardResponseFactory = async (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/webview/v2/novel")
                throw new HttpRequestException("Fixed unavailable novel content in the real destination viewer.");
            var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            await bookmarks.Writer.WriteAsync((request.RequestUri, await request.Content!.ReadAsStringAsync(), response));
            return await response.Task;
        };
        fixture.NovelTextResponseFactory = (_, _) => Task.FromException<string>(
            new HttpRequestException("Fixed unavailable fallback content in the real destination viewer."));
        fixture.SeriesResponseFactory = async (_, token) =>
        {
            await Task.Delay(-1, token);
            return "{}";
        };
        const string imageUri = "https://i.pximg.net/m3e/work-cards.png";
        using (var stream = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer);
            fixture.ImageResponses[imageUri] = buffer.ToArray();
        }
        List<string> report = [];
        foreach (var type in new[] { SimpleWorkKind.Illustration, SimpleWorkKind.Novel })
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                foreach (var width in new[] { 360, 1000 })
                {
                    var scenario = $"{type}-{theme.Key}-{width}";
                    IWorkEntry entry = type == SimpleWorkKind.Novel
                        ? Mako.Novel.CreateDefault() with
                        {
                            Id = 950001,
                            Title = Title,
                            CreateDate = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero).ToString("O"),
                            User = User.CreateDefault() with { Id = 900007, Name = Author },
                            Caption = "Description / 小说简介",
                            TextLength = 12345,
                            TotalBookmarks = 1200,
                            TotalView = 98765,
                            ImageUrls = new() { Medium = imageUri, SquareMedium = imageUri, Large = imageUri },
                            Tags = Enumerable.Range(0, 8).Select(index => new Tag { Name = TagName + index, TranslatedName = "长标签翻译 / A long translated tag ABCDEFGHIJKLMNOPQRSTUVWXYZ " + index }).ToList()
                        }
                        : Illustration.CreateDefault() with
                        {
                            Id = 950001,
                            Title = Title,
                            CreateDate = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero).ToString("O"),
                            IllustType = IllustContentType.Illustration,
                            Width = 600,
                            Height = 400,
                            PageCount = 1,
                            Visible = true,
                            SanityLevel = 2,
                            Series = null,
                            User = User.CreateDefault() with { Id = 900007, Name = Author },
                            ImageUrls = new() { Medium = imageUri, SquareMedium = imageUri, Large = imageUri },
                            MetaSinglePage = new() { OriginalImageUrl = imageUri }
                        };
                    // Each layout variation starts with the same server bookmark state.
                    ArtworkUiStateStore.SetBookmarkState(entry, false);
                    var work = new WorkContainer();
                    var view = work.FindControl<WorkView>("WorkView")!;
                    view.LayoutType = ThumbnailLayoutType.Grid;
                    view.SetSource([entry], type);
                    var model = (IOperableViewViewModel) view.DataContext!;
                    List<(Type Page, string? Id)> routes = [];
                    var container = new PageTestHost
                    {
                        Content = work,
                        NavigationObserver = page => routes.Add((page.GetType(), page.DataContext switch
                        {
                            IllustrationViewerPageViewModel vm => (vm.CurrentIllustration as IWorkEntry)?.Id.ToString(),
                            NovelViewerPageViewModel vm => vm.NovelId.ToString(),
                            UserViewerPageViewModel vm => vm.Id.ToString(),
                            _ => null
                        }))
                    };
                    var window = new Window { Width = width, Height = 720, Content = container.Shell, RequestedThemeVariant = theme };
                    try
                    {
                        Pixeval.Common.MaterialAnimation.Initialize(window);
                        window.Show();
                        await RenderAsync(window);
                        var card = view.GetVisualDescendants().OfType<WorkItem>().Single();
                        var row = (IWorkEntry) card.DataContext!;
                        var list = view.FindControl<WorkListBox>("WorkListBox")!;
                        Save(window, directory, "card-before-" + scenario);
                        await ActivateAsync(window, card, Key.Enter, PhysicalKey.Enter);
                        Require(routes.Count == 1 && routes[0] == (type == SimpleWorkKind.Novel ? typeof(NovelViewerPage) : typeof(IllustrationViewerPage), "950001"),
                            "Enter on a work card opens exactly its work viewer");
                        container.CloseDestinationAndReturn();
                        await ActivateAsync(window, card, Key.Space, PhysicalKey.Space);
                        container.CloseDestinationAndReturn();
                        await ClickAsync(window, card, new Point(card.Bounds.Width / 2, 12));
                        Require(routes.Count == 3 && routes.All(route => route == routes[0]), "Space and pointer open the same work exactly once");
                        container.CloseDestinationAndReturn();
                        Require(AutomationProperties.GetName(card) == Title, "The full work title names its keyboard entry");
                        card.Focus(NavigationMethod.Tab);
                        await RenderAsync(window);
                        Require(window.GetVisualDescendants().OfType<Border>().Any(border => border.Name == "WorkCardFocusBorder" && border.IsEffectivelyVisible),
                            "Keyboard card activation displays a focus ring above its opaque thumbnail");
                        var heart = card.GetVisualDescendants().OfType<HeartButton>().Single();
                        Require(AutomationProperties.GetName(heart) == Text("EntryItem-AddToBookmark"), "The bookmark icon has its localized action name");
                        Require(heart.Command == App.AppViewModel.AppServiceProvider.GetRequiredService<WorkCommands>().BookmarkCommand && Equals(heart.CommandParameter, entry), "The bookmark command and its owning control remain unchanged");
                        card.Focus(NavigationMethod.Tab);
                        Press(window, Key.Tab, PhysicalKey.Tab);
                        if (card is NovelItem)
                        {
                            var description = card.FindControl<MarkdownBox>("DescriptionBlock")!;
                            Require(description.IsKeyboardFocusWithin, "The novel description remains reachable before its other actions");
                            await RenderAsync(window);
                            Require(description.Opacity > 0, "A keyboard-focused novel description is visible");
                            Press(window, Key.Tab, PhysicalKey.Tab);
                        }
                        Require(heart.IsFocused, "Tab from a work card reaches the bookmark button; actual focus=" + window.FocusManager?.GetFocusedElement());
                        var routeCountBeforeBookmark = routes.Count;
                        await ActivateAsync(window, heart, Key.Space, PhysicalKey.Space);
                        var bookmark = await bookmarks.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                        Require(bookmark.Uri.AbsolutePath == (type == SimpleWorkKind.Novel ? "/v2/novel/bookmark/add" : "/v2/illust/bookmark/add")
                            && System.Web.HttpUtility.ParseQueryString(bookmark.Body)[type == SimpleWorkKind.Novel ? "novel_id" : "illust_id"] == "950001",
                            "The bookmark button keeps the type-specific POST and work ID");
                        Require((heart.State & HeartButtonState.Pending) != 0, "Pending bookmarks retain their progress state");
                        await ClickAsync(window, heart);
                        Require(!bookmarks.Reader.TryPeek(out _) && routes.Count == routeCountBeforeBookmark, "Repeated pending bookmark input neither duplicates the request nor opens the work; routes=" + routes.Count + "; before=" + routeCountBeforeBookmark);
                        bookmark.Response.SetResult("{}");
                        await UntilAsync(() => !App.AppViewModel.AppServiceProvider.GetRequiredService<WorkCommands>().BookmarkCommand.IsRunning);
                        Require(heart.State == HeartButtonState.Checked && (ArtworkUiStateStore.GetOrCreate(entry).BookmarkState == HeartButtonState.Checked), "Successful bookmark completion updates both model and heart");
                        await ClickAsync(window, heart);
                        var removal = await bookmarks.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                        Require(removal.Uri.AbsolutePath.EndsWith("/bookmark/delete", StringComparison.Ordinal), "Pointer activation removes an existing bookmark");
                        removal.Response.SetResult("{}");
                        await UntilAsync(() => !App.AppViewModel.AppServiceProvider.GetRequiredService<WorkCommands>().BookmarkCommand.IsRunning);
                        Require(heart.State == HeartButtonState.Unchecked && !(ArtworkUiStateStore.GetOrCreate(entry).BookmarkState == HeartButtonState.Checked), "Bookmark removal restores the unchecked state");
                        var menu = card.ContextMenu ?? card.GetLogicalDescendants().OfType<Control>().Select(control => control.ContextMenu).First(value => value is not null)!;
                        card.Focus(NavigationMethod.Tab);
                        Press(window, Key.Apps, PhysicalKey.ContextMenu);
                        await RenderAsync(window);
                        Require(menu.IsOpen, "The platform context-menu key reaches the focused work card actions");
                        var items = menu.Items.OfType<MenuItem>().ToArray();
                        Require(items.Single(item => Equals(item.Header, Text("EntryItem-Save"))).Command == App.AppViewModel.AppServiceProvider.GetRequiredService<WorkCommands>().SaveCommand,
                            "The save action retains its original command without starting a download");
                        if (card is IllustrationItem)
                        {
                            var thumbnail = card.FindControl<Image>("ThumbnailImage")!;
                            var copy = items.Single(item => Equals(item.Header, Text("EntryItem-Copy")));
                            Require(copy.IsEnabled && ReferenceEquals(copy.CommandParameter, thumbnail), "Image copy still targets the loaded cached thumbnail");
                        }
                        menu.Close();
                        foreach (var resource in new[] { "EntryItem-GenerateWebLink", "EntryItem-GenerateLink" })
                        {
                            await OpenMenuAsync();
                            var action = items.Single(item => Equals(item.Header, Text(resource)));
                            await ActivateAsync(window, action, Key.Enter, PhysicalKey.Enter);
                            Require(await window.Clipboard!.TryGetTextAsync() == action.Tag!.ToString(), "Copying a work link preserves its full target in the headless clipboard");
                        }
                        await OpenMenuAsync();
                        var bookmarkOptions = items.Single(item => Equals(item.Header, Text("EntryItem-AddToBookmark")));
                        bookmarkOptions.Focus(NavigationMethod.Tab);
                        Press(window, Key.Apps, PhysicalKey.ContextMenu);
                        await RenderAsync(window);
                        Require(window.GetVisualDescendants().OfType<TagSelector>().Any(), "Bookmark popup opens after the context action");
                        var tagSelector = window.GetVisualDescendants().OfType<TagSelector>().Single();
                        Require(tagSelector.WorkId == 950001 && tagSelector.WorkKind == type,
                            "The bookmark context action preserves its work ID/type and reaches the existing tag/privacy editor");
                        Require(!menu.IsOpen, "Bookmark options replace the source menu instead of leaving two competing popup layers");
                        Press(window, Key.Escape, PhysicalKey.Escape);
                        await RenderAsync(window);
                        Require(!window.GetVisualDescendants().OfType<TagSelector>().Any(), "Escape dismisses bookmark options without submitting a write");
                        menu.Close();
                        await OpenMenuAsync();
                        var watch = items.Single(item => Equals(item.Header, Text("EntryItem-AddToWatchLater")));
                        await ActivateAsync(window, watch, Key.Enter, PhysicalKey.Enter);
                        Require(App.AppViewModel.StorageEngine.WatchLaterRepository.ContainsWatchLater(entry), "The menu persists watch-later in the isolated database");
                        await OpenMenuAsync();
                        await ActivateAsync(window, watch, Key.Enter, PhysicalKey.Enter);
                        Require(!App.AppViewModel.StorageEngine.WatchLaterRepository.ContainsWatchLater(entry), "The same menu action removes watch-later without changing work identity");
                        var routeCount = routes.Count;
                        await OpenMenuAsync();
                        await ActivateAsync(window, items.Single(item => Equals(item.Header, Text("EntryItem-OpenUserInfoPage"))), Key.Enter, PhysicalKey.Enter);
                        Require(routes.Count == routeCount + 1 && routes[^1] == (typeof(UserViewerPage), "900007"), "The menu author action opens only the author; before=" + routeCount + "; routes=" + string.Join(", ", routes));
                        container.CloseDestinationAndReturn();
                        if (card is NovelItem novel)
                        {
                            var author = novel.GetVisualDescendants().OfType<Button>().Single(button => button.Content is TextBlock { Name: "AuthorTextBlock" });
                            var tag = novel.FindControl<ItemsControl>("TagsList")!.GetVisualDescendants().OfType<Button>().First();
                            Bounds(author, card);
                            Bounds(tag, card);
                            foreach (var text in novel.FindControl<Panel>("StatisticsPanel")!.GetVisualDescendants().OfType<TextBlock>())
                                Require(text.TextLayout.TextLines.All(line => !line.HasCollapsed) && text.TextLayout.Width <= text.Bounds.Width + 1,
                                    "Mako.Novel statistics fit their available width without losing their formatted values");
                            routeCount = routes.Count;
                            await ClickAsync(window, author);
                            Require(routes.Count == routeCount + 1 && routes[^1] == (typeof(UserViewerPage), "900007"), "Clicking the novel author does not also open its work viewer");
                            container.CloseDestinationAndReturn();
                            await ClickAsync(window, tag);
                            Require(routes.Count == routeCount + 2 && routes[^1].Page == typeof(WorkSearchResultPage), "Clicking a novel tag opens only the tag search");
                            container.CloseDestinationAndReturn();
                            var tags = novel.FindControl<ItemsControl>("TagsList")!.GetVisualDescendants().OfType<Button>().ToArray();
                            tag.Focus(NavigationMethod.Tab);
                            for (var index = 1; index < tags.Length; index++)
                                Press(window, Key.Tab, PhysicalKey.Tab);
                            await RenderAsync(window);
                            Require(tags[^1].IsFocused, "Tab reaches every novel tag, including those outside the initial preview");
                            var scroll = novel.FindControl<ScrollViewer>("TagsScrollViewer")!;
                            Require(scroll.Offset.Y > 0 && scroll.Extent.Height > scroll.Viewport.Height,
                                "Long tag lists scroll their focused action into view inside the fixed-height card");
                        }
                        model.IsSelecting = true;
                        list.UnselectAll();
                        await RenderAsync(window);
                        routeCount = routes.Count;
                        await ActivateAsync(window, card, Key.Enter, PhysicalKey.Enter);
                        Require(routes.Count == routeCount && model.SelectedEntries.Count == 1, "Enter in selection mode selects the work without opening it");
                        await ActivateAsync(window, card, Key.Space, PhysicalKey.Space);
                        Require(routes.Count == routeCount && model.SelectedEntries.Count == 0, "Space toggles the same selection without navigation");
                        await ClickAsync(window, card, new Point(card.Bounds.Width / 2, 12));
                        Require(routes.Count == routeCount && model.SelectedEntries.Count == 1, "A pointer click in selection mode toggles exactly once");
                        var itemContainer = (ListBoxItem) list.ContainerFromIndex(0)!;
                        await ActivateAsync(window, itemContainer, Key.Enter, PhysicalKey.Enter);
                        Require(routes.Count == routeCount && model.SelectedEntries.Count == 0, "Enter after list arrow navigation preserves the selection contract");
                        Save(window, directory, "card-final-" + scenario);
                        model.IsSelecting = false;
                        await ActivateAsync(window, heart, Key.Enter, PhysicalKey.Enter);
                        var obsoleteBookmark = await bookmarks.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                        await OpenMenuAsync();
                        IWorkEntry replacement = entry switch
                        {
                            Mako.Novel replacementNovel => replacementNovel with { Id = 950099, Title = "Replacement novel" },
                            Illustration illustration => illustration with { Id = 950099, Title = "Replacement illustration" },
                            _ => throw new InvalidOperationException("Unexpected card type")
                        };
                        view.SetSource([replacement], type);
                        await RenderAsync(window);
                        Require(!menu.IsOpen, "Replacing a source closes the old card's root context menu");
                        obsoleteBookmark.Response.SetResult("{}");
                        await UntilAsync(() => !App.AppViewModel.AppServiceProvider.GetRequiredService<WorkCommands>().BookmarkCommand.IsRunning);
                        var newCard = view.GetVisualDescendants().OfType<WorkItem>().Single();
                        Require(AutomationProperties.GetName(newCard) == replacement.Title
                            && newCard.GetVisualDescendants().OfType<HeartButton>().Single().State == HeartButtonState.Unchecked,
                            "Recycled cards bind the replacement title and ignore the previous model's late bookmark completion");
                        if (newCard is NovelItem)
                            Require(newCard.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == replacement.Title), "A recycled novel's one-time title binding follows the new entry");
                        var blockedUsers = App.AppViewModel.StorageEngine;
                        try
                        {
                            newCard.Focus(NavigationMethod.Tab);
                            Press(window, Key.Apps, PhysicalKey.ContextMenu);
                            await RenderAsync(window);
                            var block = newCard.ContextMenu!.Items.OfType<MenuItem>().Single(item => Equals(item.Header, Text("BlockedContent-BlockAuthor")));
                            await ActivateAsync(window, block, Key.Enter, PhysicalKey.Enter);
                            var blocked = (IWorkEntry) view.GetVisualDescendants().OfType<WorkItem>().Single().DataContext!;
                            Require(blockedUsers.GetAllBlockedUsers().Single(record => record.Id == 900007).UserName == Author && App.AppViewModel.StorageEngine.IsUserBlocked(blocked.User.Id),
                                "Blocking the author retains the original identity and replaces the visible work with its blocked placeholder");
                            Require(!view.GetVisualDescendants().OfType<HeartButton>().Single().IsEffectivelyVisible,
                                "Blocked illustrations and novels do not expose an unusable bookmark icon");
                        }
                        finally
                        {
                            _ = blockedUsers.TryDeleteBlockedUser(900007);
                        }
                        await ValidateRangeAndDoubleClickAsync(window, view, container, entry, type);
                        view.Dispose();
                        await RenderAsync(window);
                        Require(card.DataContext is null && !menu.IsOpen && list.Items.Count == 0, "Disposal clears card state, context menu and source");
                        report.Add($"PASS {scenario}: single navigation, named actions, Tab/context/copy, watch-later, child actions, selection and disposal");

                        async Task OpenMenuAsync()
                        {
                            await ClickAsync(window, card, new Point(card.Bounds.Width / 2, 12), MouseButton.Right);
                            var point = card.TranslatePoint(new Point(card.Bounds.Width / 2, 12), window)!.Value;
                            Require(menu.IsOpen, "Right click still opens the work menu; hit=" + window.InputHitTest(point));
                        }
                    }
                    catch
                    {
                        Save(window, directory, "card-failure-" + scenario);
                        throw;
                    }
                    finally
                    {
                        container.Shell.CloseAllPages();
                        window.Close();
                    }
                }
        Require(fixture.UnexpectedRequests.IsEmpty, "Card requests stay inside the fixed service fixture: " + string.Join(", ", fixture.UnexpectedRequests));
        File.WriteAllLines(Path.Combine(directory, "work-card-contracts.txt"), report);
        File.WriteAllLines(Path.Combine(directory, "work-card-requests.txt"), fixture.RequestLog);
        Console.WriteLine($"Validated {report.Count} work-card groups.");
    }

    private static string Text(string key) => FixtureLocalization.GetResource(key);

    private static async Task ValidateRangeAndDoubleClickAsync(Window window, WorkView view,
        PageTestHost container, IWorkEntry template, SimpleWorkKind type)
    {
        var entries = Enumerable.Range(0, 3).Select(index => template switch
        {
            Mako.Novel novel => (IWorkEntry) (novel with { Id = 950100 + index, Title = "Range novel " + index, IsBookmarked = false }),
            Illustration illustration => illustration with { Id = 950100 + index, Title = "Range illustration " + index, IsBookmarked = false },
            _ => throw new InvalidOperationException("Unexpected selection sample")
        }).ToArray();
        await DismissNotificationsAsync(window);
        view.SetSource(entries, type);
        var model = (IOperableViewViewModel) view.DataContext!;
        model.IsSelecting = true;
        var list = view.FindControl<WorkListBox>("WorkListBox")!;
        await RenderAsync(window);
        list.Selection.Select(0);
        list.ScrollIntoView(2);
        await RenderAsync(window);
        var card = (WorkItem) ((ListBoxItem) list.ContainerFromIndex(2)!).Content!;
        var point = card.TranslatePoint(new Point(card.Bounds.Width / 2, 12), window)!.Value;
        var before = container.Navigations;
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.Shift);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.Shift);
        await RenderAsync(window);
        Require(model.SelectedEntries.Select(item => ((IWorkEntry)item).Id.ToString()).Order().SequenceEqual(new[] { "950100", "950101", "950102" })
            && container.Navigations == before, "Shift-click preserves inclusive range selection across real card containers");
        point = card.TranslatePoint(new Point(card.Bounds.Width - 60, 12), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        await RenderAsync(window);
        Require(container.Navigations == before + 1, "Double-click in selection mode opens the artwork exactly once");
        container.CloseDestinationAndReturn();
    }

    private static async Task ActivateAsync(Window window, Control control, Key key, PhysicalKey physical)
    {
        control.BringIntoView();
        await RenderAsync(window);
        Require(control.Focus(NavigationMethod.Tab), "The action is keyboard focusable");
        Press(window, key, physical);
        await RenderAsync(window);
    }

    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
    }

    private static async Task ClickAsync(Window window, Control control, Point? offset = null, MouseButton button = MouseButton.Left)
    {
        await DismissNotificationsAsync(window);
        control.BringIntoView();
        await RenderAsync(window);
        var point = control.TranslatePoint(offset ?? new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, button);
        window.MouseUp(point, button);
        await RenderAsync(window);
    }

    private static async Task DismissNotificationsAsync(Window window)
    {
        // Narrow-window success toasts can overlap the artwork. Dismiss them
        // through their real keyboard action before sending the next pointer input.
        foreach (var notification in window.GetVisualDescendants().OfType<NotificationCard>().Where(card => !card.IsClosing).ToArray())
        {
            var close = notification.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_CloseButton");
            close.Focus(NavigationMethod.Tab);
            Press(window, Key.Space, PhysicalKey.Space);
        }
        await UntilAsync(() => !window.GetVisualDescendants().OfType<NotificationCard>().Any());
    }

    private static void Bounds(Control control, Control card)
    {
        var point = control.TranslatePoint(default, card)!.Value;
        Require(control.Bounds.Width > 0 && control.Bounds.Height > 0 && point.X >= -1 && point.Y >= -1
            && point.X + control.Bounds.Width <= card.Bounds.Width + 1 && point.Y + control.Bounds.Height <= card.Bounds.Height + 1,
            "Mako.Novel actions fit inside their card: " + control + " " + point + " " + control.Bounds + "; card=" + card.Bounds);
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(150);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("A work-card command did not complete.");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        }
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No work-card frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }
}
