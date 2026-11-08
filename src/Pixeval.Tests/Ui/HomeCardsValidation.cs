using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Global.Enum;
using Mako.Model;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement;
using Pixeval.I18N;
using Pixeval.Models.Home;
using Pixeval.Models.Options;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views;
using Pixeval.Views.Capability;
using Pixeval.Views.Home;
using Pixeval.Views.Viewers;

namespace Pixeval.Tests.Ui;

internal static class HomeCardsValidation
{
    private const string LongTitle = "长首页预览名称 / A long home preview name ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string ImageUri = "https://i.pximg.net/m3e/home-preview.png";

    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        await client.SetTokenAsync("fixture-home-cards");
        await client.ValidateTokenAsync();
        fixture.RelatedResponseFactory = (request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.Contains("/novel/", StringComparison.Ordinal)
            ? JsonSerializer.Serialize(new { novels = new[] { NovelSample(991001), NovelSample(991002) }, next_url = (string?) null })
            : JsonSerializer.Serialize(new { illusts = new[] { IllustrationSample(991001), IllustrationSample(991002) }, next_url = (string?) null }));
        fixture.UserResponseFactory = async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/user/detail")
            {
                await Task.Delay(-1, token);
                return "{}";
            }
            return JsonSerializer.Serialize(new { user_previews = new[] { UserSample(991001), UserSample(991002) }, next_url = (string?) null });
        };
        fixture.SpotlightResponseFactory = (_, _) => Task.FromResult(JsonSerializer.Serialize(new
        {
            spotlight_articles = new[] { SpotlightSample(991001), SpotlightSample(991002) },
            next_url = (string?) null
        }));
        fixture.WorkCardResponseFactory = (_, _) => throw new HttpRequestException("Fixed unavailable novel body; only destination identity is inspected.");
        var details = Channel.CreateUnbounded<TaskCompletionSource<string>>();
        List<TaskCompletionSource<string>> pending = [];
        fixture.ArtworkDetailResponseFactory = async (_, _) =>
        {
            var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(response);
            await details.Writer.WriteAsync(response);
            return await response.Task;
        };
        using (var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var buffer = new MemoryStream())
        {
            await asset.CopyToAsync(buffer);
            fixture.ImageResponses[ImageUri] = buffer.ToArray();
        }
        List<string> report = [];
        try
        {
            foreach (var kind in new[] { "illustration", "novel", "user", "spotlight" })
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                    foreach (var width in new[] { 360, 1000 })
                    {
                        var key = $"{kind}-{theme.Key}-{width}";
                        var layout = new HomePageCardLayout(kind switch
                        {
                            "user" => HomePageCardSourceKind.UserRecommended,
                            "spotlight" => HomePageCardSourceKind.Spotlight,
                            _ => HomePageCardSourceKind.WorkRelated
                        }, 0, 0)
                        { EntryId = 991007, SimpleWorkType = kind == "novel" ? SimpleWorkType.Novel : SimpleWorkType.Illustration };
                        var savedLayout = JsonSerializer.Serialize(layout);
                        var card = new HomePageCardControl(layout, 1, 1);
                        List<(Type Page, string? Id)> routes = [];
                        var host = new PageTestHost
                        {
                            Content = new Grid { Margin = new Thickness(8), Children = { card } },
                            NavigationObserver = page =>
                            {
                                routes.Add((page.GetType(), page.DataContext switch
                                {
                                    IllustrationViewerPageViewModel illustration => illustration.CurrentIllustration?.Entry.Id,
                                    NovelViewerPageViewModel novel => novel.NovelId.ToString(),
                                    UserViewerPageViewModel user => user.Id.ToString(),
                                    _ => null
                                }));
                            }
                        };
                        var window = new Window { Width = width, Height = 480, RequestedThemeVariant = theme, Content = host.Shell };
                        try
                        {
                            window.Show();
                            await card.PreviewViewModel.LoadAsync();
                            await UntilAsync(() => card.PreviewViewModel.Items.Count == 2);
                            await RenderAsync(window);
                            var buttons = card.FindControl<ItemsControl>("PreviewItems")!.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("preview-item")).ToArray();
                            Require(buttons.Length == 2 && buttons.All(button => AutomationProperties.GetName(button) == LongTitle),
                                $"All four home preview templates expose their full work/user/article names: {key}; rendered={buttons.Length}; {string.Join(" | ", buttons.Select(AutomationProperties.GetName))}");
                            var open = card.FindControl<Button>("OpenCardButton")!;
                            Require(AutomationProperties.GetName(open) == Text(HomePageResources.OpenCardPageButton.ToolTip)
                                && open.Bounds.Width >= 40 && open.Bounds.Height >= 40, "The Material open action has a localized name and usable target size");
                            var source = card.PreviewViewModel.ViewModel!.Source;
                            Require(buttons[0].Focus(NavigationMethod.Tab), "The first preview receives keyboard focus");
                            Press(window, Key.Tab, PhysicalKey.Tab);
                            Require(buttons[1].IsFocused, "Tab reaches the next preview item");
                            Press(window, Key.Tab, PhysicalKey.Tab, RawInputModifiers.Shift);
                            Require(buttons[0].IsFocused, "Shift+Tab returns to the previous preview item");
                            await RenderAsync(window);
                            Require(window.GetVisualDescendants().OfType<Border>().Any(border => border.Name == "WorkCardFocusBorder" && border.IsEffectivelyVisible),
                                "Preview keyboard focus remains visible over opaque thumbnails");
                            var clicks = 0;
                            buttons[0].Click += (_, _) => clicks++;
                            Press(window, Key.Enter, PhysicalKey.Enter);
                            await RenderAsync(window);
                            if (kind != "spotlight")
                                host.CloseDestinationAndReturn();
                            var previousButton = buttons[0];
                            buttons = card.FindControl<ItemsControl>("PreviewItems")!.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("preview-item")).ToArray();
                            if (!ReferenceEquals(previousButton, buttons[0]))
                                buttons[0].Click += (_, _) => clicks++;
                            Require(buttons[0].Focus(NavigationMethod.Tab), "The returned preview can receive keyboard focus again");
                            Press(window, Key.Space, PhysicalKey.Space);
                            await RenderAsync(window);
                            if (kind != "spotlight")
                                host.CloseDestinationAndReturn();
                            Require(clicks == 2, "Each preview keyboard activation raises one Click");
                            if (kind == "spotlight")
                                Require(routes.Count == 0 && window.Launcher.GetType().Name == "NoopLauncher"
                                    && ((SpotlightItemViewModel) buttons[0].DataContext!).Entry.ArticleUrl == SpotlightSample(991001).ArticleUrl,
                                    "Spotlight previews retain their full article target and isolated launcher");
                            else
                                Require(routes.Count == 2 && routes.All(route => route == (kind switch
                                {
                                    "novel" => typeof(NovelViewerPage),
                                    "user" => typeof(UserViewerPage),
                                    _ => typeof(IllustrationViewerPage)
                                }, "991001")), "Preview activation opens only its original work or user identity");
                            var before = routes.Count;
                            Require(open.Focus(NavigationMethod.Tab), "The full-source open button receives keyboard focus");
                            Press(window, Key.Enter, PhysicalKey.Enter);
                            await UntilAsync(() => !card.OpenCardPageCommand.IsRunning);
                            host.CloseDestinationAndReturn();
                            Require(routes.Count == before + 1 && routes[^1].Page == (kind switch
                            {
                                "user" => typeof(UserRecommendedPage),
                                "spotlight" => typeof(SpotlightPage),
                                _ => typeof(WorkRelatedPage)
                            }) && source.Count == 2, "Opening and disposing the full source preserves the preview's shared owner");
                            Save(window, directory, "home-ready-" + key);
                            PixevalSettings.Instance.HideHomePageCardTitle = true;
                            await RenderAsync(window);
                            Require(!card.FindControl<Border>("TitleBar")!.IsEffectivelyVisible && buttons[0].IsEffectivelyVisible,
                                "The existing title preference hides title/open together while retaining preview actions");
                            PixevalSettings.Instance.HideHomePageCardTitle = false;
                            var selections = 0;
                            var deletes = 0;
                            card.CardSelected += (_, e) => { Require(ReferenceEquals(e.Card, layout), "Keyboard selection retains the layout identity"); selections++; };
                            card.DeleteRequested += (_, e) => { Require(ReferenceEquals(e.Card, layout), "Quick delete retains the layout identity"); deletes++; };
                            before = routes.Count;
                            card.IsEditing = true;
                            await RenderAsync(window);
                            Require(!card.OpenCardPageCommand.CanExecute(null) && !card.OpenPreviewItemCommand.CanExecute(buttons[0])
                                && !open.IsEffectivelyEnabled && buttons.All(button => !button.IsEffectivelyEnabled),
                                "Editing disables both pointer and keyboard preview/open actions");
                            Require(card.Focus(NavigationMethod.Tab), "An editing card is a named keyboard selection target");
                            Press(window, Key.Enter, PhysicalKey.Enter);
                            Press(window, Key.Space, PhysicalKey.Space);
                            Require(selections == 2 && routes.Count == before && AutomationProperties.GetName(card) == card.CardTitle,
                                "Enter/Space select the editable card exactly once without opening its data");
                            var delete = card.FindControl<Button>("QuickDeleteButton")!;
                            Require(AutomationProperties.GetName(delete) == Text(HomePageResources.DeleteSelectedCardButton.Content) && delete.Focus(NavigationMethod.Tab),
                                "The visible quick-delete action is named and keyboard reachable");
                            Press(window, Key.Enter, PhysicalKey.Enter);
                            Require(deletes == 1 && selections == 2 && routes.Count == before, "Quick delete raises only its existing owner event");
                            Save(window, directory, "home-editing-" + key);
                            card.IsEditing = false;
                            await RenderAsync(window);
                            Require(card.OpenCardPageCommand.CanExecute(null) && buttons[0].IsEffectivelyEnabled && !card.Focusable,
                                "Leaving edit mode restores preview actions and removes the editing-only Tab stop");
                            Require(JsonSerializer.Serialize(layout) == savedLayout, "Display, selection and title preferences never rewrite layout or source parameters");
                            layout.BackgroundColor = 0xFF19324B;
                            card.UpdateBackground();
                            window.RequestedThemeVariant = theme == ThemeVariant.Light ? ThemeVariant.Dark : ThemeVariant.Light;
                            await RenderAsync(window);
                            Require(card.FindControl<Border>("CardBorder")!.Background is ISolidColorBrush brush && brush.Color.ToUInt32() == layout.BackgroundColor,
                                "Theme changes preserve the explicitly configured card color");
                            var titleSurface = card.FindControl<Border>("TitleBar")!;
                            var title = titleSurface.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == card.CardTitle);
                            Require(Contrast(title.Foreground, titleSurface.Background) >= 4.5,
                                "The title's Material surface retains readable contrast over a custom card color in either theme");
                            card.Dispose();
                            Require(source.Count == 0 && card.PreviewViewModel.Items.Count == 0 && !card.OpenCardPageCommand.CanExecute(null),
                                "Closing the final home owner releases preview data and disables actions");
                            report.Add($"PASS {key}: full names/focus, preview/open routes, shared ownership, title preference, edit selection/delete, custom color and disposal");
                        }
                        catch
                        {
                            Save(window, directory, "home-failure-" + key);
                            throw;
                        }
                        finally
                        {
                            PixevalSettings.Instance.HideHomePageCardTitle = false;
                            card.Dispose();
                            host.Content = null;
                            window.Close();
                        }
                    }

            foreach (var mode in new[] { "edit", "detach", "close", "failure", "novel-failure" })
            {
                var card = new HomePageCardControl(new(mode == "novel-failure" ? HomePageCardSourceKind.SingleNovel : HomePageCardSourceKind.SingleImage, 0, 0) { EntryId = 991050, BackgroundColor = 0xFF19324B }, 1, 1);
                var host = new PageTestHost { Content = card };
                var window = new Window { Width = 360, Height = 320, Content = host.Shell };
                try
                {
                    window.Show();
                    var response = await details.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
                    await RenderAsync(window);
                    Require(card.PreviewViewModel.PlaceholderText == Text(HomePageResources.CardPreview.LoadingTextBlock.Text), "Pending details show the existing localized loading state");
                    var open = card.FindControl<Button>("OpenCardButton")!;
                    open.Focus(NavigationMethod.Tab);
                    Press(window, Key.Enter, PhysicalKey.Enter);
                    Require(card.OpenCardPageCommand.IsRunning, "An explicit open waits for its pending preview");
                    if (mode == "edit")
                    {
                        card.IsEditing = true;
                        card.IsEditing = false; // Leaving edit again must not resurrect the old open intent.
                    }
                    else if (mode == "detach")
                    {
                        host.Content = null;
                        await RenderAsync(window);
                        host.Content = card;
                    }
                    else if (mode == "close")
                        card.Dispose();
                    if (mode is "failure" or "novel-failure")
                        response.SetException(new HttpRequestException("Fixed single-image detail failure"));
                    else
                        response.SetResult(JsonSerializer.Serialize(new { illust = IllustrationSample(991050) }));
                    await UntilAsync(() => !card.OpenCardPageCommand.IsRunning);
                    await RenderAsync(window);
                    Require(host.Navigations == 0, "Editing, detach/reattach, closing and failure never complete a stale open intent");
                    if (mode is "failure" or "novel-failure")
                    {
                        Require(card.PreviewViewModel.PlaceholderText == Text(HomePageResources.CardPreview.FailedTextBlock.Text), "Failed details retain the existing localized error placeholder");
                        var text = card.FindControl<TextBlock>("PlaceholderTextBlock")!;
                        Require(Contrast(text.Foreground, card.FindControl<Border>("PlaceholderSurface")!.Background) >= 4.5
                            && text.TextLayout.TextLines.All(line => !line.HasCollapsed),
                            "The full failure message stays readable over a custom background");
                    }
                    else if (mode == "close")
                        Require(card.PreviewViewModel.ViewModel is null && card.PreviewViewModel.Items.Count == 0, "Closed cards reject a late preview source");
                    else
                    {
                        Require(card.PreviewViewModel.Items.Count == 1, "A still-owned card can finish its preview without navigating");
                        open.Focus(NavigationMethod.Tab);
                        Press(window, Key.Enter, PhysicalKey.Enter);
                        await UntilAsync(() => !card.OpenCardPageCommand.IsRunning);
                        Require(host.Navigations == 1 && host.LastPageType == typeof(IllustrationViewerPage), "A new explicit open remains available after the stale request was rejected");
                    }
                    Save(window, directory, "home-lifetime-" + mode);
                    report.Add($"PASS lifetime-{mode}: pending open ownership and original placeholder");
                }
                finally
                {
                    card.Dispose();
                    host.Content = null;
                    window.Close();
                }
            }
            await ValidateHomeEditorAsync(directory, report);
            Require(fixture.UnexpectedRequests.IsEmpty, "Home previews stay inside the fixed service matrix: " + string.Join(", ", fixture.UnexpectedRequests));
            File.WriteAllLines(Path.Combine(directory, "home-card-contracts.txt"), report);
            File.WriteAllLines(Path.Combine(directory, "home-card-requests.txt"), fixture.RequestLog);
            Console.WriteLine($"Validated {report.Count} home-card groups.");
        }
        finally
        {
            foreach (var response in pending)
                response.TrySetResult(JsonSerializer.Serialize(new { illust = IllustrationSample(991050) }));
        }
    }

    private static Illustration IllustrationSample(long id) => Illustration.CreateDefault() with { Id = id, Title = LongTitle, User = UserInfo.CreateDefault() with { Id = 900007, Name = "Fixture author" }, Width = 640, Height = 480, PageCount = 1, ThumbnailUrls = new() { Medium = ImageUri, SquareMedium = ImageUri, Large = ImageUri }, MetaSinglePage = new() { OriginalImageUrl = ImageUri } };
    private static Novel NovelSample(long id) => Novel.CreateDefault() with { Id = id, Title = LongTitle, User = UserInfo.CreateDefault() with { Id = 900007, Name = "Fixture author" }, ThumbnailUrls = new() { Medium = ImageUri, SquareMedium = ImageUri, Large = ImageUri } };
    private static User UserSample(long id) => User.CreateDefault() with { UserInfo = UserInfo.CreateDefault() with { Id = id, Name = LongTitle, ProfileImageUrls = new() { Medium = ImageUri } }, Illustrations = [] };
    private static Spotlight SpotlightSample(long id) => new() { Id = id, Title = LongTitle, PureTitle = LongTitle, Thumbnail = ImageUri, ArticleUrl = $"https://www.pixivision.net/zh/a/{id}?from=fixture", Category = SpotlightCategory.Spotlight, SubcategoryLabel = "Fixture spotlight", PublishDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) };
    private static async Task ValidateHomeEditorAsync(string directory, List<string> report)
    {
        var app = App.AppViewModel;
        var logger = app.AppServiceProvider.GetRequiredService<FileLogger>();
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            app.HomePageCards.Clear();
            var layout = new HomePageCardLayout(HomePageCardSourceKind.WorkRelated, 0, 0) { EntryId = 991007, BackgroundColor = 0xFF19324B };
            app.HomePageCards.Add(layout);
            app.AppSettings.ApplicationSettings.HomePage.HomePageRows = 2;
            app.AppSettings.ApplicationSettings.HomePage.HomePageColumns = 2;
            var page = new HomePage();
            var host = new PageTestHost { Content = page };
            var window = new Window { Width = 1000, Height = 800, RequestedThemeVariant = theme, Content = host.Shell };
            try
            {
                window.Show();
                var card = page.FindControl<Grid>("HomeGrid")!.Children.OfType<HomePageCardControl>().Single();
                await card.PreviewViewModel.LoadAsync();
                await RenderAsync(window);
                var source = card.PreviewViewModel.ViewModel!.Source;
                var edit = page.FindControl<CommandBarToggleButton>("EditHomeButton")!;
                edit.FindAncestorOfType<CommandBar>()!.IsOpen = true;
                await RenderAsync(window);
                Require(edit.Focus(NavigationMethod.Tab), "The real home edit toolbar action remains keyboard reachable");
                Press(window, Key.Space, PhysicalKey.Space);
                await RenderAsync(window);
                Require(page.ViewModel.IsEditMode && card.IsEditing && card.Focus(NavigationMethod.Tab), "The actual home edit toggle exposes the new card selection Tab stop");
                Press(window, Key.Enter, PhysicalKey.Enter);
                await RenderAsync(window);
                Require(page.ViewModel.HasSelectedCard && card.IsSelected && page.ViewModel.SelectedCardDescription == card.CardTitle,
                    "Keyboard card selection populates the existing home editor with the same layout");
                var columns = page.FindControl<NumericUpDown>("CardColumnEditor")!;
                Require(AutomationProperties.GetName(columns) == Text(HomePageResources.PositionXTextBlock.Text), "The position editor exposes its localized purpose");
                columns.BringIntoView();
                await RenderAsync(window);
                var input = columns.GetVisualDescendants().OfType<TextBox>().Single();
                Require(input.Bounds.Width >= 40 && !string.IsNullOrWhiteSpace(input.Text),
                    "The position field leaves visible space for its numeric value between the step buttons");
                Require(input.Focus(NavigationMethod.Tab), "The selected position can be edited by keyboard");
                input.SelectAll();
                window.KeyTextInput("2");
                Press(window, Key.Enter, PhysicalKey.Enter);
                await RenderAsync(window);
                Require(layout.Column == 1 && Grid.GetColumn(card) == 1 && source.Count == 2 && host.Navigations == 0,
                    "Editing the selected column moves the same card without reloading or navigating its preview");
                Require(input.Text == "2" && input.Bounds.Width >= 40, "The edited position remains visibly readable");
                var restored = AppInfo.LoadHomePageCards(logger)!.Single();
                Require(restored.Column == 1 && restored.Row == 0 && restored.ColumnSpan == 1 && restored.RowSpan == 1
                    && restored.EntryId == 991007 && restored.SourceKind == HomePageCardSourceKind.WorkRelated && restored.BackgroundColor == 0xFF19324B,
                    "The existing YAML serializer retains layout, source parameters and custom color after keyboard editing");
                var heading = page.FindControl<TextBlock>("EditPaneTitle")!;
                Require(heading.TextLayout.TextLines.All(line => !line.HasCollapsed)
                    && heading.TextLayout.Width <= heading.Bounds.Width + 1,
                    "The complete localized home editor heading fits its drawer without clipping");
                Save(window, directory, "home-editor-" + theme.Key);
                var delete = card.FindControl<Button>("QuickDeleteButton")!;
                Require(delete.Focus(NavigationMethod.Tab), "The selected card's quick delete remains keyboard reachable in the real home page");
                Press(window, Key.Enter, PhysicalKey.Enter);
                await RenderAsync(window);
                Require(app.HomePageCards.Count == 0 && page.FindControl<Grid>("HomeGrid")!.Children.Count == 0
                    && !page.ViewModel.HasSelectedCard && source.Count == 0 && AppInfo.LoadHomePageCards(logger)!.Count == 0,
                    "Quick delete removes and disposes the actual card, clears selection and persists the empty layout");
                report.Add($"PASS home-editor-{theme.Key}: real edit toggle, keyboard selection/position, YAML reload and owner delete");
            }
            catch
            {
                Save(window, directory, "home-editor-failure-" + theme.Key);
                throw;
            }
            finally
            {
                page.Dispose();
                host.Content = null;
                window.Close();
            }
        }
    }

    private static double Contrast(IBrush? foreground, IBrush? background)
    {
        Require(foreground is ISolidColorBrush && background is ISolidColorBrush, "Text and surface use concrete theme colors");
        var first = Luminance(((ISolidColorBrush) foreground!).Color);
        var second = Luminance(((ISolidColorBrush) background!).Color);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);

        static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        static double Linear(byte value) => value / 255d <= 0.04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + 0.055) / 1.055, 2.4);
    }

    private static string Text(string key) => I18NManager.GetResource(key);

    private static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(200);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The home preview did not reach its expected state.");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No home-card frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }
}
