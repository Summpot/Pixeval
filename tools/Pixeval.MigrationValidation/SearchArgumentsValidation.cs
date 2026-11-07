using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako;
using Mako.Engine;
using Mako.Global.Enum;
using Pixeval.I18N;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Search;
using Pixeval.Views.Search;

namespace Pixeval.MigrationValidation;

internal static class SearchArgumentsValidation
{
    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        await LoginAsync(false);
        List<string> completed = [];

        var illustration = new IllustrationSearchFormViewModel
        {
            MatchOption = SearchIllustrationTagMatchOption.ExactMatchForTags,
            ContentType = SearchIllustrationContentType.Manga,
            RatioPattern = SearchIllustrationRatioPattern.Portrait,
            Tool = "Fixture tool"
        };
        SetCommon(illustration);
        Set(illustration.WidthMinOption, 320);
        Set(illustration.WidthMaxOption, 1920);
        Set(illustration.HeightMinOption, 480);
        Set(illustration.HeightMaxOption, 2560);
        Require(illustration.TryValidate(out _, out _), "Valid illustration form is accepted");
        var illustrationQuery = await RequestAsync(client.IllustrationSearch(illustration.BuildArguments("固定 条件")));
        AssertCommon(illustrationQuery);
        Expect(illustrationQuery, "search_target", "exact_match_for_tags");
        Expect(illustrationQuery, "content_type", "manga");
        Expect(illustrationQuery, "ratio_pattern", "portrait");
        Expect(illustrationQuery, "width_min", "320");
        Expect(illustrationQuery, "width_max", "1920");
        Expect(illustrationQuery, "height_min", "480");
        Expect(illustrationQuery, "height_max", "2560");
        Expect(illustrationQuery, "tool", "Fixture tool");
        completed.Add("Illustration: real engine request preserves all common flags, dates, keyword, match/content/ratio, dimensions and tool");

        illustration.WidthMinOption.IsEnabled = false;
        illustration.WidthMaxOption.IsEnabled = false;
        illustration.StartDateOption.IsEnabled = false;
        illustration.EndDateOption.IsEnabled = false;
        var disabled = illustration.BuildArguments("fixed");
        Require(disabled.WidthMin is null && disabled.WidthMax is null && disabled.StartDate is null && disabled.EndDate is null,
            "Disabled options are omitted");
        Require(illustration.WidthMinOption.Value is 320 && illustration.WidthMaxOption.Value is 1920
            && illustration.StartDateOption.Value == new DateTime(2020, 2, 3), "Disabling preserves editable values");
        Set(illustration.WidthMinOption, 2000);
        Set(illustration.WidthMaxOption, 1000);
        Reject(illustration, SearchResources.Validation.InvalidNumericRange.Title);
        illustration.WidthMinOption.IsEnabled = false;
        illustration.WidthMaxOption.IsEnabled = false;
        illustration.StartDateOption.IsEnabled = true;
        illustration.StartDateOption.Value = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).Date.AddDays(1);
        Reject(illustration, SearchResources.Validation.InvalidDateRange.Title);
        illustration.StartDateOption.IsEnabled = false;
        illustration.SortOption = WorkSortOption.PopularityDescending;
        Reject(illustration, SearchResources.Validation.PremiumSort.Title);
        completed.Add("Illustration: disabled fields retain values; reversed ranges, future dates and non-premium popularity are rejected with localized errors");

        var novel = new NovelSearchFormViewModel
        {
            MatchOption = SearchNovelTagMatchOption.Text,
            Language = "ja",
            IsOriginalOnly = true,
            IsReplaceableOnly = true,
            GenreId = 1
        };
        SetCommon(novel);
        foreach (var (option, key) in new[]
                 {
                     (SearchNovelContentLengthOption.TextLength, "text_length"),
                     (SearchNovelContentLengthOption.WordCount, "word_count"),
                     (SearchNovelContentLengthOption.ReadingTime, "reading_time")
                 })
        {
            novel.ContentLengthOption = option;
            (int? Min, int? Max)[] ranges = option is SearchNovelContentLengthOption.ReadingTime
                ? [(null, null), (null, 9), (10, 59), (60, 179), (180, null)]
                : [(null, null), (null, 4999), (5000, 19999), (20000, 79999), (80000, null)];
            foreach (var (min, max) in ranges)
            {
                Set(novel.ContentLengthMinOption, min);
                Set(novel.ContentLengthMaxOption, max);
                Require(novel.TryValidate(out _, out _), $"Allowed free-account {key} range is accepted");
                var arguments = novel.BuildArguments("固定 条件");
                var query = await RequestAsync(client.NovelSearch(arguments));
                AssertCommon(query);
                Expect(query, "search_target", "text");
                Expect(query, "lang", "ja");
                Expect(query, "is_original_only", "true");
                Expect(query, "is_replaceable_only", "true");
                Expect(query, "genre", "1");
                Expect(query, key + "_min", min?.ToString(CultureInfo.InvariantCulture));
                Expect(query, key + "_max", max?.ToString(CultureInfo.InvariantCulture));
                Require(arguments.ContentLengthMin == min && arguments.ContentLengthMax == max,
                    "Engine construction must not erase a valid selected range");
            }

            Set(novel.ContentLengthMinOption, 123);
            Set(novel.ContentLengthMaxOption, 456);
            var accepted = novel.TryValidate(out var title, out var message);
            if (accepted)
            {
                var unsupported = novel.BuildArguments("fixed");
                var engine = client.NovelSearch(unsupported);
                Console.WriteLine($"Baseline {option}: form accepted 123..456, engine produced {unsupported.ContentLengthMin?.ToString() ?? "null"}..{unsupported.ContentLengthMax?.ToString() ?? "null"}");
                engine.EngineHandle.Cancel();
            }
            Require(!accepted && !string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(message),
                "Unsupported free-account ranges must be rejected before the engine silently removes them");
            Require(novel.ContentLengthMinOption.Value is 123 && novel.ContentLengthMaxOption.Value is 456,
                "Validation keeps the user's unsupported range available for correction");
            completed.Add($"Novel {key}: five allowed free-account ranges reach HTTP unchanged; custom range is visibly rejected without losing input");
        }

        novel.ContentLengthOption = SearchNovelContentLengthOption.None;
        novel.IsOriginalOnly = false;
        novel.Language = "";
        Require(novel.TryValidate(out _, out _), "Disabled length restriction ignores retained custom values");
        var inactive = novel.BuildArguments("fixed");
        Require(inactive.ContentLengthMin is null && inactive.ContentLengthMax is null && inactive.GenreId is null && inactive.LangCode is null,
            "Inactive length/genre/language are omitted");
        Require(novel.GenreId is 1 && novel.ContentLengthMinOption.Value is 123, "Inactive fields retain their editable values");
        completed.Add("Novel: disabling length/original-only/language omits request fields and retains editable range/genre");

        await LoginAsync(true);
        novel.SortOption = WorkSortOption.PopularityDescending;
        foreach (var (option, key) in new[]
                 {
                     (SearchNovelContentLengthOption.TextLength, "text_length"),
                     (SearchNovelContentLengthOption.WordCount, "word_count"),
                     (SearchNovelContentLengthOption.ReadingTime, "reading_time")
                 })
        {
            novel.ContentLengthOption = option;
            Require(novel.TryValidate(out _, out _), "Premium custom range and popularity remain available");
            var query = await RequestAsync(client.NovelSearch(novel.BuildArguments("fixed")));
            Expect(query, key + "_min", "123");
            Expect(query, key + "_max", "456");
            Expect(query, "sort", "popular_desc");
        }
        completed.Add("Premium account: all three custom length dimensions and popularity sorting remain available");
        await LoginAsync(false);
        await ValidatePageAsync(directory, completed);
        Require(fixture.UnexpectedRequests.IsEmpty, "Only fixed authentication and search endpoints were used");
        File.WriteAllLines(Path.Combine(directory, "search-arguments.txt"), completed);
        File.WriteAllLines(Path.Combine(directory, "search-requests.txt"), fixture.RequestLog);
        Console.WriteLine($"Validated {completed.Count} advanced search groups and {fixture.SearchRequests.Count} actual engine requests.");
        return;

        async Task LoginAsync(bool premium)
        {
            fixture.IsPremium = premium;
            await client.SetTokenAsync(premium ? "fixture-premium" : "fixture-free");
            await client.ValidateTokenAsync();
            Require(client.Me?.Id is 900001 && client.Me.IsPremium == premium, "Isolated account has the requested capability");
        }

        async Task<NameValueCollection> RequestAsync<T>(IFetchEngine<T> engine)
        {
            var count = fixture.SearchRequests.Count;
            await using var enumerator = engine.GetAsyncEnumerator();
            Require(!await enumerator.MoveNextAsync(), "Fixed search response is empty");
            Require(fixture.SearchRequests.Count == count + 1, "Exactly one search request reaches the offline handler");
            return HttpUtility.ParseQueryString(fixture.SearchRequests.Last().Query);
        }
    }

    private static async Task ValidatePageAsync(string directory, List<string> completed)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var model = SearchPageViewModel.Instance;
            model.SearchText = "fixture-novel";
            model.SelectedAdvancedOptionsType = SimpleWorkType.Novel;
            model.NovelForm.ContentLengthOption = SearchNovelContentLengthOption.TextLength;
            Set(model.NovelForm.ContentLengthMinOption, 123);
            Set(model.NovelForm.ContentLengthMaxOption, 456);
            var page = new SearchPage();
            var navigation = new NavigationPage();
            await navigation.PushAsync(page);
            var container = new SearchCompletionValidation.FixtureViewContainer { Content = navigation };
            var window = new Window { Width = 1000, Height = 720, RequestedThemeVariant = theme, Content = container };
            try
            {
                window.Show();
                var expander = page.FindControl<Expander>("AdvancedExpander")
                    ?? throw new InvalidOperationException("Missing advanced search options.");
                expander.IsExpanded = true;
                await Task.Delay(300);
                Render();
                var minimum = page.GetVisualDescendants().OfType<NumericUpDown>().Single(control => control.Value is 123);
                minimum.BringIntoView();
                Render();
                var input = minimum.GetVisualDescendants().OfType<TextBox>().First();
                Require(input.Focus(), "Novel minimum editor can receive focus");
                input.SelectAll();
                window.KeyTextInput("321");
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                Require(model.NovelForm.ContentLengthMinOption.Value is 321, "Keyboard numeric edit updates the actual novel form");

                var search = page.FindControl<AutoCompleteBox>("SearchAutoCompleteBox")
                    ?? throw new InvalidOperationException("Missing search input.");
                var button = search.InnerRightContent as Button ?? throw new InvalidOperationException("Missing search action.");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Render();
                Require(container.Navigations is 0, "Unsupported free-account range does not navigate");
                Require(container.Notifications.Any(notification => notification.Type is NotificationType.Warning
                    && notification.Title == I18NManager.GetResource(SearchResources.Validation.PremiumLength.Title)),
                    "Actual search action displays the range validation error");
                var hint = I18NManager.GetResource(SearchResources.Validation.PremiumLength.TextContent);
                var hintBlock = window.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == hint);
                Console.WriteLine($"{theme.Key} range hint: width={hintBlock.Bounds.Width}, maxLines={hintBlock.MaxLines}, collapsed={hintBlock.TextLayout.TextLines.Any(line => line.HasCollapsed)}");
                Require(hintBlock.TextLayout.TextLines.All(line => !line.HasCollapsed), "All allowed ranges are visible without ellipsis");
                Require(model.SearchText is "fixture-novel" && model.NovelForm.ContentLengthMinOption.Value is 321
                    && model.NovelForm.ContentLengthMaxOption.Value is 456, "Validation leaves the complete form available to correct");
                using (var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No advanced search frame."))
                    frame.Save(Path.Combine(directory, $"search-range-error-{theme.Key}.png"), new PngBitmapEncoderOptions());

                model.NovelForm.ContentLengthOption = SearchNovelContentLengthOption.ReadingTime;
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Render();
                var timeHint = I18NManager.GetResource(SearchResources.Validation.PremiumLength.ReadingTimeContent);
                var timeBlock = window.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == timeHint);
                Require(timeBlock.TextLayout.TextLines.All(line => !line.HasCollapsed), "All allowed reading-time ranges are visible without ellipsis");
                Require(container.Navigations is 0, "Unsupported reading-time range also stays on the form");

                model.NovelForm.ContentLengthOption = SearchNovelContentLengthOption.TextLength;
                Set(model.NovelForm.ContentLengthMinOption, 5000);
                Set(model.NovelForm.ContentLengthMaxOption, 19999);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(container.Navigations is 1 && container.LastPageType == typeof(WorkSearchResultPage),
                    "Corrected allowed range uses the existing search route exactly once");
                completed.Add($"{theme.Key}: actual numeric keyboard edit, search warning, preserved form and corrected search route");
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
            continue;

            void Render()
            {
                window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                Dispatcher.UIThread.RunJobs();
            }
        }
    }

    private static void SetCommon(SearchArgumentsFormViewModelBase form)
    {
        form.SortOption = WorkSortOption.PublishDateAscending;
        form.AiType = true;
        form.MergePlainKeywordResults = false;
        form.IncludeTranslatedTagResults = false;
        form.IncludePotentialViolationWorks = true;
        form.StartDateOption.IsEnabled = true;
        form.StartDateOption.Value = new(2020, 2, 3);
        form.EndDateOption.IsEnabled = true;
        form.EndDateOption.Value = new(2020, 4, 5);
    }

    private static void AssertCommon(NameValueCollection query)
    {
        Expect(query, "word", "固定 条件");
        Expect(query, "filter", "for_android");
        Expect(query, "sort", "date_asc");
        Expect(query, "search_ai_type", "1");
        Expect(query, "merge_plain_keyword_results", "false");
        Expect(query, "include_translated_tag_results", "false");
        Expect(query, "include_potential_violation_works", "true");
        Expect(query, "start_date", "2020-02-03");
        Expect(query, "end_date", "2020-04-05");
    }

    private static void Set(OptionalIntSearchOptionViewModel option, int? value)
    {
        option.IsEnabled = value.HasValue;
        if (value is { } number)
            option.Value = number;
    }

    private static void Reject(SearchArgumentsFormViewModelBase form, string titleKey)
    {
        Require(!form.TryValidate(out var title, out var message)
            && title == I18NManager.GetResource(titleKey) && !string.IsNullOrWhiteSpace(message),
            "Invalid form must preserve a specific localized validation error");
    }

    private static void Expect(NameValueCollection query, string key, string? expected) =>
        Require(query[key] == expected, $"Request parameter {key}: expected {expected ?? "omitted"}, actual {query[key] ?? "omitted"}");

    private static void Require(bool condition, string contract)
    {
        if (!condition)
            throw new InvalidOperationException(contract);
    }
}
