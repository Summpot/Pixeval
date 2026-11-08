using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Global.Enum;
using Pixeval.Controls;
using Pixeval.Views.Capability;
using Pixeval.Views.Work;

namespace Pixeval.Tests.Ui;

internal static class RankingPageValidation
{
    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        ConcurrentQueue<Uri> requests = [];
        fixture.RankingResponseFactory = uri =>
        {
            requests.Enqueue(uri);
            return uri.AbsolutePath.Contains("novel", StringComparison.Ordinal)
                ? "{\"novels\":[],\"next_url\":null}" : "{\"illusts\":[],\"next_url\":null}";
        };
        await client.SetTokenAsync("fixture-ranking-page");
        await client.ValidateTokenAsync();
        var settings = App.AppViewModel.AppSettings.SearchSettings;
        settings.RankOptions.IllustrationRankOption = RankOption.DayManga;
        settings.RankOptions.NovelRankOption = RankOption.WeekAi;
        settings.DefaultSimpleWorkType = SimpleWorkType.Novel;
        var defaultPage = new WorkRankingPage();
        Require(defaultPage.FindControl<SymbolComboBox>("RankOptionComboBox")!.GetSelectedValue<RankOption>() == RankOption.WeekAi,
            "The default novel page uses the configured novel ranking, even when the illustration default is unsupported");
        defaultPage.FindControl<WorkContainer>("WorkContainer")!.FindControl<WorkView>("WorkView")!.Dispose();
        var originalDate = new DateTime(2021, 4, 5);
        List<string> report = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 360, 1000 })
            {
                requests.Clear();
                var page = new WorkRankingPage(SimpleWorkType.Illustration, RankOption.Day, originalDate);
                var navigation = new NavigationPage();
                await navigation.PushAsync(page);
                var container = new PageTestHost { Content = navigation };
                var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = container.Shell };
                var scenario = $"{theme.Key}-{width}";
                var works = page.FindControl<WorkContainer>("WorkContainer")!;
                var date = page.FindControl<CalendarDatePicker>("RankDateTimeCalendarDatePicker")!;
                var ranking = page.FindControl<SymbolComboBox>("RankOptionComboBox")!;
                var type = page.FindControl<SymbolComboBox>("SimpleWorkTypeComboBox")!;
                try
                {
                    window.Show();
                    await UntilAsync(() => !requests.IsEmpty);
                    await RenderAsync(window);
                    Save(window, directory, "ranking-ready-" + scenario);
                    HorizontalBounds(window, date);
                    HorizontalBounds(window, ranking);
                    Require(date.Bounds.Width <= date.GetVisualAncestors().OfType<Avalonia.Controls.WrapPanel>().First().Bounds.Width + 1
                        && ranking.Bounds.Width <= ranking.GetVisualAncestors().OfType<Avalonia.Controls.WrapPanel>().First().Bounds.Width + 1,
                        "Ranking fields fit their command-bar column without consuming its adjacent action area");
                    Require(AutomationProperties.GetName(date) is { Length: > 0 }
                        && AutomationProperties.GetName(Inner(ranking)) is { Length: > 0 }
                        && AutomationProperties.GetName(Inner(type)) is { Length: > 0 }, "Ranking selectors expose their localized purposes");
                    Require(requests.Count == 1, "Opening the ranking page issues exactly one initial request");
                    CheckRequest(SimpleWorkType.Illustration, RankOption.Day, originalDate);
                    await CheckOptionsAsync(SimpleWorkType.Illustration);
                    var before = requests.Count;
                    Require(Inner(type).Focus(NavigationMethod.Tab), "Ranking work type is keyboard reachable");
                    Press(window, Key.Down, PhysicalKey.ArrowDown);
                    await UntilAsync(() => requests.Count > before);
                    await RenderAsync(window);
                    Require(requests.Count == before + 1, "Switching work type issues one request without a transient unsupported ranking");
                    CheckRequest(SimpleWorkType.Novel, RankOption.WeekAi, originalDate);
                    await CheckOptionsAsync(SimpleWorkType.Novel);

                    var editor = date.GetVisualDescendants().OfType<TextBox>().Single();
                    var editedDate = new DateTime(2022, 6, 7);
                    before = requests.Count;
                    await EditDateAsync(editedDate.ToString("d", CultureInfo.CurrentCulture));
                    await UntilAsync(() => requests.Count > before);
                    Require(date.SelectedDate == editedDate && requests.Count == before + 1, "Localized date editing commits exactly once");
                    CheckRequest(SimpleWorkType.Novel, ranking.GetSelectedValue<RankOption>(), editedDate);
                    window.KeyPress(Key.Down, RawInputModifiers.Control, PhysicalKey.ArrowDown, "");
                    window.KeyRelease(Key.Down, RawInputModifiers.Control, PhysicalKey.ArrowDown, "");
                    await Task.Delay(300);
                    await RenderAsync(window);
                    Require(date.IsDropDownOpen, "The calendar popup opens from its date editor keyboard shortcut");
                    var calendar = window.GetVisualDescendants().OfType<Avalonia.Controls.Calendar>().Single();
                    HorizontalBounds(window, calendar);
                    Require(calendar.BlackoutDates.Contains(WorkRankingPage.MaxDate.Date.AddDays(1)), "The calendar also disables unavailable future dates");
                    Save(window, directory, "ranking-calendar-" + scenario);
                    var previousDay = calendar.GetVisualDescendants().OfType<CalendarDayButton>()
                        .Single(button => button.DataContext is DateTime day && day.Date == editedDate.AddDays(-1));
                    var point = previousDay.TranslatePoint(new Point(previousDay.Bounds.Width / 2, previousDay.Bounds.Height / 2), window)!.Value;
                    before = requests.Count;
                    window.MouseMove(point);
                    window.MouseDown(point, MouseButton.Left);
                    window.MouseUp(point, MouseButton.Left);
                    await RenderAsync(window);
                    editedDate = editedDate.AddDays(-1);
                    await UntilAsync(() => requests.Count > before);
                    Require(date.SelectedDate == editedDate && !date.IsDropDownOpen && requests.Count == before + 1,
                        "Picking a calendar day closes the popup and updates the source exactly once");
                    CheckRequest(SimpleWorkType.Novel, ranking.GetSelectedValue<RankOption>(), editedDate);
                    before = requests.Count;
                    await EditDateAsync(WorkRankingPage.MaxDate.AddDays(10).ToString("d", CultureInfo.CurrentCulture));
                    File.WriteAllText(Path.Combine(directory, "future-date-" + scenario + ".txt"), $"Selected: {date.SelectedDate:O}; editor: {editor.Text}; requests: {before}->{requests.Count}; latest: {requests.Last()}");
                    Save(window, directory, "ranking-future-" + scenario);
                    Require(date.SelectedDate == editedDate && requests.Count == before
                        && container.Notifications.Any(notification => notification.Type == NotificationType.Warning),
                        "A future date shows a warning, preserves the valid selection and does not dispatch an invalid request");
                    await EditDateAsync("not-a-date");
                    Require(date.SelectedDate == editedDate && requests.Count == before, "Unparseable date text also preserves the valid source");
                    before = requests.Count;
                    Require(editor.Focus(NavigationMethod.Tab), "The date editor remains keyboard reachable after validation");
                    editor.SelectAll();
                    Press(window, Key.Back, PhysicalKey.Backspace);
                    Press(window, Key.Enter, PhysicalKey.Enter);
                    await UntilAsync(() => requests.Count > before);
                    CheckRequest(SimpleWorkType.Novel, ranking.GetSelectedValue<RankOption>(), WorkRankingPage.MaxDate.Date);
                    Require(date.SelectedDate is null && requests.Count == before + 1, "An empty date keeps the existing latest-ranking fallback");

                    var bar = works.FindControl<CommandBar>("RightToolBar")!;
                    bar.IsOpen = true;
                    await RenderAsync(window);
                    before = requests.Count;
                    Require(works.FindControl<CommandBarButton>("RefreshButton")!.Focus(NavigationMethod.Tab), "Ranking refresh remains reachable in overflow");
                    Press(window, Key.Enter, PhysicalKey.Enter);
                    await UntilAsync(() => requests.Count > before);
                    Require(requests.Count == before + 1, "One refresh issues one request with the active source");
                    CheckRequest(SimpleWorkType.Novel, ranking.GetSelectedValue<RankOption>(), WorkRankingPage.MaxDate.Date);
                    settings.RankOptions.IllustrationRankOption = settings.RankOptions.NovelRankOption = RankOption.Day;
                    Require(Inner(ranking).Focus(NavigationMethod.Tab), "Ranking mode remains keyboard reachable after refresh");
                    for (var index = ranking.SelectedIndex; index > 0; index--)
                        Press(window, Key.Up, PhysicalKey.ArrowUp);
                    await RenderAsync(window);
                    before = requests.Count;
                    Require(Inner(type).Focus(NavigationMethod.Tab), "Work type remains reachable with an unchanged ranking mode");
                    Press(window, Key.Up, PhysicalKey.ArrowUp);
                    await UntilAsync(() => requests.Count > before);
                    await RenderAsync(window);
                    Require(requests.Count == before + 1, "Switching type with the same mode still dispatches exactly one request");
                    CheckRequest(SimpleWorkType.Illustration, RankOption.Day, WorkRankingPage.MaxDate.Date);
                    settings.RankOptions.IllustrationRankOption = RankOption.DayManga;
                    settings.RankOptions.NovelRankOption = RankOption.WeekAi;
                    window.Width = width == 360 ? 1000 : 360;
                    await RenderAsync(window);
                    Require(ReferenceEquals(date, page.FindControl<CalendarDatePicker>("RankDateTimeCalendarDatePicker"))
                        && date.SelectedDate is null, "Resizing preserves the ranking editor and its cleared value");
                    HorizontalBounds(window, date);
                    HorizontalBounds(window, ranking);
                    Save(window, directory, "ranking-final-" + scenario);
                    report.Add($"PASS {scenario}: all ranking modes and exact date/type requests, localized editing, future-date rejection, latest-date fallback, single refresh and resize");

                    async Task CheckOptionsAsync(SimpleWorkType workType)
                    {
                        var expected = Enum.GetValues<RankOption>().Where(option => workType == SimpleWorkType.Illustration
                            ? RankOption.IsIllustrationSupport(option) : RankOption.IsNovelSupport(option)).ToArray();
                        Require(ranking.ItemsSource!.Select(item => (RankOption) item.Value).SequenceEqual(expected), "Ranking choices match the engine's work-type support matrix");
                        Require(Inner(ranking).Focus(NavigationMethod.Tab), "Ranking option is keyboard reachable");
                        for (var index = ranking.SelectedIndex; index > 0; index--)
                            Press(window, Key.Up, PhysicalKey.ArrowUp);
                        await RenderAsync(window);
                        CheckRequest(workType, expected[0], originalDate);
                        for (var index = 1; index < expected.Length; index++)
                        {
                            var count = requests.Count;
                            Press(window, Key.Down, PhysicalKey.ArrowDown);
                            await UntilAsync(() => requests.Count > count);
                            Require(requests.Count == count + 1 && Equals(ranking.SelectedValue, expected[index]), "Each ranking keyboard action selects its real value once");
                            CheckRequest(workType, expected[index], originalDate);
                            await RenderAsync(window);
                            foreach (var text in Inner(ranking).GetVisualDescendants().OfType<TextBlock>())
                            {
                                HorizontalBounds(window, text);
                                Require(text.TextLayout.Width <= text.Bounds.Width + 1 && text.TextLayout.Height <= text.Bounds.Height + 1
                                    && text.TextLayout.TextLines.All(line => !line.HasCollapsed), "Every localized ranking label remains completely readable");
                            }
                        }
                    }

                    async Task EditDateAsync(string value)
                    {
                        Require(editor.Focus(NavigationMethod.Tab), "Ranking date is keyboard editable");
                        editor.SelectAll();
                        window.KeyTextInput(value);
                        Press(window, Key.Enter, PhysicalKey.Enter);
                        await RenderAsync(window);
                    }
                }
                finally
                {
                    window.Content = null;
                    window.Close();
                    works.FindControl<WorkView>("WorkView")!.Dispose();
                }
            }

        Require(fixture.UnexpectedRequests.IsEmpty, "No ranking request escaped its fixed offline fixture");
        File.WriteAllLines(Path.Combine(directory, "ranking-page-contracts.txt"), report);
        File.WriteAllLines(Path.Combine(directory, "offline-requests.txt"), fixture.RequestLog);
        Console.WriteLine($"Validated {report.Count} ranking page groups and the default novel route.");
        return;

        void CheckRequest(SimpleWorkType type, RankOption option, DateTime date)
        {
            var uri = requests.Last();
            var query = HttpUtility.ParseQueryString(uri.Query);
            Require(uri.AbsolutePath == (type == SimpleWorkType.Novel ? "/v1/novel/ranking" : "/v1/illust/ranking")
                && query["mode"] == typeof(RankOption).GetField(option.ToString())!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!.Name
                && query["date"] == date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                $"Ranking request preserves {type}/{option}/{date:yyyy-MM-dd}: {uri.PathAndQuery}");
        }
    }

    private static ComboBox Inner(SymbolComboBox control) => control.GetVisualDescendants().OfType<ComboBox>().Single();
    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Require(condition(), "The controlled ranking operation did not complete");
    }
    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(100);
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
            $"{control.GetType().Name} stays inside the {window.Width} DIP ranking page ({point}, {control.Bounds.Size})");
    }
    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No ranking page frame.");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }
}
