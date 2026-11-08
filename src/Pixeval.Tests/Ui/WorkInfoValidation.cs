using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
using Material3Expressive.Components;
using Pixeval.Controls;
using Pixeval.Services.Core;
using Pixeval.Localization;
using Pixeval.Views.Search;
using Pixeval.Views.Viewers;
using Pixeval.Views;
using Pixeval.Views.Works;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Tests.Ui;

internal static class WorkInfoValidation
{
    private const string ImageUri = "https://i.pximg.net/m3e/work-info.png";
    private const string AuthorName = "长作者名字 / A long creator ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string LongTag = "长标签ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string Translation = "标签译文 / " + LongTag;
    private const string Title = "长作品标题 / A complete long work title ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private static readonly string Caption = string.Join('\n', Enumerable.Range(1, 18).Select(index => $"{index}: 作品介绍 / A long work description."));

    internal static async Task RunAsync(string directory)
    {
        using var fixture = new OfflineSearchFixture(App.AppViewModel.MakoClient);
        App.AppViewModel.MakoClient.SetRefreshToken("fixture-work-info");
        await FixtureAuthentication.IdentifyAsync(App.AppViewModel.MakoClient);
        using (var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var buffer = new MemoryStream())
        {
            await asset.CopyToAsync(buffer);
            fixture.ImageResponses[ImageUri] = buffer.ToArray();
        }
        fixture.SearchResponseFactory = (_, _) => Task.FromResult("{\"illusts\":[],\"novels\":[],\"next_url\":null}");
        List<string> report = [];
        foreach (var type in new[] { SimpleWorkKind.Illustration, SimpleWorkKind.Novel })
            foreach (var lite in new[] { false, true })
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                    foreach (var width in new[] { 360, 1000 })
                    {
                        var authorRequest = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var authorResponse = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                        fixture.UserResponseFactory = (request, token) =>
                        {
                            if (request.RequestUri!.AbsolutePath != "/v2/user/detail")
                                return Task.FromResult("{\"user_previews\":[],\"next_url\":null}");
                            authorRequest.TrySetResult(token);
                            return authorResponse.Task;
                        };
                        var pane = new WorkInfoPane { ArtworkInfo = Sample(type) };
                        if (lite)
                            pane.Theme = (ControlTheme) Application.Current!.FindResource("LiteWorkInfoPaneStyle")!;
                        var actionCount = 0;
                        var action = new Button { Content = "⋮", MinWidth = 48, MinHeight = 48 };
                        action.Click += (_, _) => actionCount++;
                        pane.ActionZone = action;
                        var host = new PageTestHost
                        {
                            Content = pane,
                            NavigationObserver = page =>
                            {
                                if (page is UserViewerPage { DataContext: UserViewerPageViewModel user })
                                {
                                    Require(user.Id == 995007, "Author navigation retains the complete original user ID");
                                    return;
                                }
                                Require(page is WorkSearchResultPage search
                                    && Equals(search.FindControl<SymbolComboBox>("SimpleWorkTypeComboBox")!.SelectedValue, type)
                                    && search.Header?.ToString()?.Contains(LongTag, StringComparison.Ordinal) is true,
                                    "A tag opens the existing search page with its full original name and work type");
                            }
                        };
                        var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = host.Shell };
                        var key = $"{type}-{(lite ? "Lite" : "Full")}-{theme.Key}-{width}";
                        try
                        {
                            Pixeval.Common.MaterialAnimation.Initialize(window);
                            window.Show();
                            await RenderAsync(window);
                            Save(window, directory, "work-info-top-" + key);
                            var author = pane.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == AuthorName);
                            Require(author.Bounds.Width > 0 && author.TextLayout.Width <= author.Bounds.Width + 1
                                && author.TextLayout.TextLines.All(line => !line.HasCollapsed), "The complete author name wraps within its measured space");
                            var location = author.TranslatePoint(default, pane)!.Value;
                            Require(location.X >= 0 && location.X + author.Bounds.Width <= pane.Bounds.Width + 1,
                                "Author cards remain inside the information pane");
                            var title = pane.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == Title);
                            Require(title.Bounds.Width >= 160 && title.TextLayout.Width <= title.Bounds.Width + 1,
                                "The action zone leaves readable title space");
                            if (!lite)
                                Require(title.TextLayout.TextLines.All(line => !line.HasCollapsed), "The full pane retains the entire title");
                            foreach (var statistic in pane.GetVisualDescendants().OfType<IconText>())
                            {
                                location = statistic.TranslatePoint(default, pane)!.Value;
                                Require(location.X >= 0 && location.X + statistic.Bounds.Width <= pane.Bounds.Width + 1,
                                    "View, favorite and date statistics stay within the pane");
                            }
                            Require(action.Focus(NavigationMethod.Tab), "The caller-provided action zone remains keyboard reachable");
                            Press(window, Key.Enter, PhysicalKey.Enter);
                            Require(actionCount == 1, "The action zone executes once");
                            var description = pane.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == Caption);
                            var captionScroll = description.GetVisualAncestors().OfType<ScrollViewer>().First();
                            Require(captionScroll.Focus(NavigationMethod.Tab), "The work description can receive keyboard scrolling");
                            for (var step = 0; step <= Math.Ceiling(captionScroll.Extent.Height / captionScroll.Viewport.Height); step++)
                                Press(window, Key.PageDown, PhysicalKey.PageDown);
                            await RenderAsync(window);
                            var last = description.TranslatePoint(new Point(0, description.Bounds.Height), captionScroll)!.Value;
                            Require(last.Y > 0 && last.Y <= captionScroll.Bounds.Height + 1, "The final description line remains reachable");
                            if (!lite)
                            {
                                var copyButtons = pane.GetVisualDescendants().OfType<Button>().Where(button => button.Tag is "995001" or "995007").ToArray();
                                Require(copyButtons.Length == 2, "Both work and author ID copy actions remain available");
                                foreach (var copy in copyButtons)
                                {
                                    Require(AutomationProperties.GetName(copy) == Strings.WorkInfoPageCopyId()
                                        && copy.Bounds.Width >= 40 && copy.Bounds.Height >= 40, "ID copy buttons expose their localized purpose and usable target size");
                                    copy.BringIntoView();
                                    await RenderAsync(window);
                                    var count = host.Navigations;
                                    Require(copy.Focus(NavigationMethod.Tab), "ID copy actions remain keyboard reachable");
                                    Press(window, Key.Enter, PhysicalKey.Enter);
                                    await RenderAsync(window);
                                    Require(await window.Clipboard!.TryGetTextAsync() == (string) copy.Tag!
                                        && host.Navigations == count, "Copying an ID never opens the enclosing author card");
                                }
                                var chip = pane.GetVisualDescendants().OfType<SuggestionChip>().Single(control => control.CommandParameter is WorkInfoTag { Name: LongTag });
                                chip.BringIntoView();
                                await RenderAsync(window);
                                location = chip.TranslatePoint(default, pane)!.Value;
                                var tagText = chip.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == Translation);
                                Require(location.X >= 0 && location.X + chip.Bounds.Width <= pane.Bounds.Width + 1
                                    && tagText.TextLayout.Width <= tagText.Bounds.Width + 1 && tagText.TextLayout.TextLines.All(line => !line.HasCollapsed)
                                    && TranslatableTextBlockBehavior.GetIsTranslated(tagText),
                                    "Long tag chips retain the existing pretranslated display and wrap without losing searchable text");
                                var outerScroll = chip.GetVisualAncestors().OfType<ScrollViewer>().First();
                                Require(outerScroll.Focus(NavigationMethod.Tab), "The outer information pane is keyboard scrollable");
                                for (var step = 0; step <= Math.Ceiling(outerScroll.Extent.Height / outerScroll.Viewport.Height); step++)
                                    Press(window, Key.PageDown, PhysicalKey.PageDown);
                                await RenderAsync(window);
                                var lastChip = pane.GetVisualDescendants().OfType<SuggestionChip>().Last();
                                var lastTag = lastChip.TranslatePoint(new Point(0, lastChip.Bounds.Height), outerScroll)!.Value;
                                Require(lastTag.Y > 0 && lastTag.Y <= outerScroll.Bounds.Height + 1, "The final tag remains inside the keyboard-reachable viewport");
                                Save(window, directory, "work-info-tags-" + key);
                                Require(chip.Focus(NavigationMethod.Tab), "Tag search remains keyboard reachable");
                                Press(window, Key.Enter, PhysicalKey.Enter);
                                await RenderAsync(window);
                                Require(host.Navigations == 1, "A tag activation opens exactly one search destination");
                                host.CloseDestinationAndReturn();
                                var menu = chip.ContextMenu!;
                                menu.Open(chip);
                                await RenderAsync(window);
                                var items = menu.Items.OfType<MenuItem>().ToArray();
                                Require(items.Length == 3 && items[1].IsVisible, "Tag copy and block menu actions remain present");
                                foreach (var item in items.Take(2))
                                {
                                    menu.Open(chip);
                                    await RenderAsync(window);
                                    Require(item.Focus(NavigationMethod.Tab), "Tag copy menu items are keyboard reachable");
                                    Press(window, Key.Enter, PhysicalKey.Enter);
                                    await RenderAsync(window);
                                    Require(await window.Clipboard!.TryGetTextAsync() == (string) item.Tag!, "Tag copy preserves the full original or translated text");
                                }
                                menu.Open(chip);
                                await RenderAsync(window);
                                Require(items[2].Focus(NavigationMethod.Tab), "The tag block action is keyboard reachable");
                                Press(window, Key.Enter, PhysicalKey.Enter);
                                await RenderAsync(window);
                                var blocked = App.AppViewModel.AppSettings.BrowsingExperienceSettings.BlockedTags;
                                Require(blocked.Contains(LongTag), "The existing block action stores the complete original tag in isolated settings");
                                blocked.Remove(LongTag);
                                AppInfo.SaveAppSettings(App.AppViewModel.AppSettings);
                                menu.Close();
                            }
                            var authorButton = author.GetVisualAncestors().OfType<Button>().First();
                            authorButton.BringIntoView();
                            await RenderAsync(window);
                            var previousNavigations = host.Navigations;
                            Require(authorButton.Focus(NavigationMethod.Tab), "The author card is keyboard reachable");
                            Press(window, Key.Enter, PhysicalKey.Enter);
                            await RenderAsync(window);
                            Require(host.Navigations == previousNavigations + 1 && host.LastPageType == typeof(UserViewerPage),
                                "The author card opens exactly one original profile page");
                            var pendingAuthor = await authorRequest.Task.WaitAsync(TimeSpan.FromSeconds(10));
                            host.CloseDestinationAndReturn();
                            await RenderAsync(window);
                            Require(pendingAuthor.IsCancellationRequested,
                                "Closing the observed author destination cancels its detail request");
                            authorResponse.TrySetResult(NativeFixtureData.Serialize(SingleUserResponse.CreateDefault()));
                            report.Add($"PASS {key}: full author/title space, bounded statistics, description keyboard scrolling, action zone" + (lite ? "" : ", named/nested ID copy, long tag/search/context copy/block"));
                        }
                        finally
                        {
                            authorResponse.TrySetCanceled();
                            host.Content = null;
                            window.Close();
                        }
                    }
        await ValidateViewerPaneAsync(directory, report);
        Require(fixture.UnexpectedRequests.IsEmpty, "Work information stays inside the offline request matrix: " + string.Join(", ", fixture.UnexpectedRequests));
        File.WriteAllLines(Path.Combine(directory, "work-info-contracts.txt"), report);
        File.WriteAllLines(Path.Combine(directory, "work-info-requests.txt"), fixture.RequestLog);
        Console.WriteLine($"Validated {report.Count} work-information groups.");
    }

    private static async Task ValidateViewerPaneAsync(string directory, List<string> report)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 360, 1000 })
            {
                var item = ((Illustration) Sample(SimpleWorkKind.Illustration));
                using var model = UiTestSession.Create<IllustrationViewerPageViewModel>(item, false);
                var pane = new IllustrationViewerInfoPane { DataContext = model };
                var host = new PageTestHost { Content = pane };
                var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = host.Shell };
                var key = $"{theme.Key}-{width}";
                try
                {
                    Pixeval.Common.MaterialAnimation.Initialize(window);
                    window.Show();
                    await RenderAsync(window);
                    var lite = pane.FindControl<WorkInfoPane>("InfoPaneSection")!;
                    var tabs = pane.FindControl<TabbedPage>("TabbedPageSection")!;
                    Require(!pane.IsDocked && lite.IsEffectivelyVisible && !tabs.IsEffectivelyVisible
                        && Equals(lite.ArtworkInfo, item), "The floating illustration pane keeps the current work in its compact view");
                    var bar = pane.FindControl<CommandBar>("BottomActionBar")!;
                    var buttons = bar.PrimaryCommands.OfType<CommandBarButton>().ToArray();
                    Require(buttons.Any(button => ReferenceEquals(button.Command, App.AppViewModel.AppServiceProvider.GetRequiredService<WorkCommands>().BookmarkCommand))
                        && buttons.Any(button => ReferenceEquals(button.Command, App.AppViewModel.AppServiceProvider.GetRequiredService<WorkCommands>().SaveCommand))
                        && buttons.Any(button => ReferenceEquals(button.Command, App.AppViewModel.AppServiceProvider.GetRequiredService<WorkCommands>().AddToWatchLaterCommand)),
                        "Information layout preserves the viewer's original bookmark, download and watch-later command instances");
                    var pin = pane.GetVisualDescendants().OfType<ToggleButton>()
                        .Single(button => Equals(ToolTip.GetTip(button), Strings.EntryViewerPagePin()));
                    Require(pin.Focus(NavigationMethod.Tab), "The floating pane's pin action is keyboard reachable");
                    Press(window, Key.Enter, PhysicalKey.Enter);
                    Require(pane.IsLocked, "The original pin action changes the pane lock state");
                    var expand = pane.GetVisualDescendants().OfType<Button>()
                        .Single(button => Equals(ToolTip.GetTip(button), Strings.EntryViewerPageInfoAndComments()));
                    Require(expand.Focus(NavigationMethod.Tab), "The information expansion action is keyboard reachable");
                    Press(window, Key.Enter, PhysicalKey.Enter);
                    await RenderAsync(window);
                    Require(pane.IsDocked && !lite.IsEffectivelyVisible && tabs.IsEffectivelyVisible,
                        "Expanding uses the existing tabbed information view");
                    var pages = tabs.Pages!.Cast<Page>().ToArray();
                    Require(pages.Length == 3 && pages[0] is WorkInfoPage && pages[1] is CommentsPage && pages[2] is WorkRelatedPage,
                        "The info, comments and related-work destinations retain their order");
                    var full = pages[0].GetVisualDescendants().OfType<WorkInfoPane>().Single();
                    Require(Equals(full.ArtworkInfo, item) && full.ActionZone is Border { Width: 32, Height: 32 },
                        "The full page shares the same work and preserves the expansion-button reserved area");
                    Save(window, directory, "viewer-info-expanded-" + key);
                    Require(expand.Focus(NavigationMethod.Tab), "The expanded pane can return by keyboard");
                    Press(window, Key.Enter, PhysicalKey.Enter);
                    await RenderAsync(window);
                    Require(!pane.IsDocked && lite.IsEffectivelyVisible && pane.IsLocked && Equals(lite.ArtworkInfo, item),
                        "Collapsing preserves the current work and pin state");
                    Save(window, directory, "viewer-info-collapsed-" + key);
                    report.Add($"PASS viewer-{key}: compact/full pane, original action bindings, pin and keyboard expand/collapse, shared work and three original destinations");
                }
                finally
                {
                    host.Shell.CloseAllPages();
                    host.Content = null;
                    window.Close();
                }
            }
    }

    private static IWorkEntry Sample(SimpleWorkKind type)
    {
        var user = User.CreateDefault() with { Id = 995007, Name = AuthorName, ProfileImageUrls = new() { Medium = ImageUri } };
        var tags = new[] { Tag.CreateDefault() with { Name = LongTag, TranslatedName = Translation }, Tag.CreateDefault() with { Name = "短标签", TranslatedName = "Short tag" } };
        var image = Illustration.CreateDefault() with { Id = 995001, Title = Title, Caption = Caption, User = user, Tags = [.. tags], TotalView = int.MaxValue, TotalBookmarks = int.MaxValue, CreateDate = new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero).ToString("O"), Width = 640, Height = 480, PageCount = 1, ImageUrls = new() { Medium = ImageUri, SquareMedium = ImageUri, Large = ImageUri }, MetaSinglePage = new() { OriginalImageUrl = ImageUri } };
        return type == SimpleWorkKind.Illustration ? image : Mako.Novel.CreateDefault() with { Id = image.Id, Title = Title, Caption = Caption, User = user, Tags = [.. tags], TotalView = image.TotalView, TotalBookmarks = image.TotalBookmarks, CreateDate = image.CreateDate, ImageUrls = image.ImageUrls };
    }
    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
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
    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No work information frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }
}
