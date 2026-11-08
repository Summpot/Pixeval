using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.I18N;
using Pixeval.Views.Search;
using Pixeval.Views.ViewContainers;
using Pixeval.Views.Work;

namespace Pixeval.Tests.Ui;

internal static class SearchCompletionValidation
{
    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        await client.SetTokenAsync("fixture-refresh");
        await client.ValidateTokenAsync();
        Require(client.Me?.Id is 900001, "Only the isolated fixture account is active");
        List<string> completed = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var page = new SearchPage();
            var navigation = new NavigationPage();
            await navigation.PushAsync(page);
            var container = new PageTestHost { Content = navigation };
            var window = new Window { Width = 1000, Height = 720, RequestedThemeVariant = theme, Content = container.Shell };
            try
            {
                window.Show();
                Render(window);
                var box = page.FindControl<AutoCompleteBox>("SearchAutoCompleteBox")
                    ?? throw new InvalidOperationException("Missing search input.");
                var text = box.GetVisualDescendants().OfType<TextBox>().First();
                Type("prefix old");
                var old = await fixture.NextCompletionAsync("old");
                Type("prefix new");
                var current = await fixture.NextCompletionAsync("new");
                Require(old.Token.IsCancellationRequested, "Changing text cancels the previous request");
                current.Complete("new-tag 中文");
                await UntilAsync(box, () => Items(box).Any(item => item.Text is "new-tag 中文"));
                old.Complete("stale-tag");
                await DrainAsync();
                Require(Items(box).All(item => item.Text is not "stale-tag"), "Late response cannot replace current suggestions");
                completed.Add($"{theme.Key}: last-keyword request, cancellation and reversed response order preserve current suggestions");

                var selected = Items(box).Single(item => item.Kind is SearchCompletionKind.Tag);
                box.IsDropDownOpen = true;
                _ = text.Focus();
                for (var index = 0; index < 3 && !Equals(box.SelectedItem, selected); index++)
                {
                    window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, "");
                    window.KeyRelease(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, "");
                }
                Require(Equals(box.SelectedItem, selected), "Arrow keys select the tag suggestion");
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                Render(window);
                Require(box.Text is "prefix new-tag 中文", "Enter replaces only the last keyword");
                Require(text.CaretIndex == box.Text?.Length && text.SelectionStart == text.SelectionEnd, "Tag completion restores the caret without selection");
                Require(container.Navigations is 0, "Selecting a tag does not also execute search");
                completed.Add($"{theme.Key}: real Enter selects one tag, preserves prefix and caret, and does not navigate");

                Type("123456");
                Require(Items(box).Select(item => item.Kind).SequenceEqual(new[]
                {
                    SearchCompletionKind.OpenIllustration, SearchCompletionKind.OpenNovel,
                    SearchCompletionKind.OpenUser, SearchCompletionKind.SearchUser
                }), "Numeric input preserves all four immediate entry kinds");
                Type("clear");
                var clearing = await fixture.NextCompletionAsync("clear");
                Type("");
                Require(clearing.Token.IsCancellationRequested && !Items(box).Any() && !box.IsDropDownOpen,
                    "Empty input cancels and clears suggestions");
                clearing.Complete("cleared-tag");
                await DrainAsync();
                Require(!Items(box).Any() && box.Text is "", "Cleared input is not repopulated by a late response");
                completed.Add($"{theme.Key}: numeric immediate entries and empty-input cancellation remain available");

                Type("leave");
                var leaving = await fixture.NextCompletionAsync("leave");
                container.Content = null;
                Render(window);
                Require(leaving.Token.IsCancellationRequested, "Unloading the page cancels its pending request");
                leaving.Complete("detached-tag");
                await DrainAsync();
                Require(!Items(box).Any(), "An unloaded page does not receive late suggestions");
                container.Content = navigation;
                Render(window);
                var resumed = await fixture.NextCompletionAsync("leave");
                resumed.Complete("resumed-tag");
                await UntilAsync(box, () => Items(box).Any(item => item.Text is "resumed-tag"));
                Require(box.Text is "leave", "Remount keeps the user's query and restarts completion");
                completed.Add($"{theme.Key}: unload cancels, late response is ignored, remount resumes the same query");
                Save(window, Path.Combine(directory, $"search-completion-{theme.Key}.png"));
                Type("ordinary-search");
                box.IsDropDownOpen = true;
                Require(box.SelectedItem is null, "Ordinary search has no chosen completion");
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                Require(container.Navigations is 1 && container.LastPageType == typeof(WorkSearchResultPage),
                    "Enter without a selection executes ordinary search exactly once");
                box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Handled = true });
                Require(container.Navigations is 1, "Already handled Enter cannot execute a second search");
                container.CloseDestinationAndReturn();
                completed.Add($"{theme.Key}: ordinary Enter navigates once and an already handled event is ignored");
                Type("fail");
                var failing = await fixture.NextCompletionAsync("fail");
                failing.Fail();
                await UntilNotificationAsync();
                Require(box.Text is "fail" && Items(box).Any(item => item.Kind is SearchCompletionKind.SearchUser),
                    "Completion failure preserves the query and immediate search action");
                Save(window, Path.Combine(directory, $"search-error-{theme.Key}.png"));
                Type("recover");
                var recovery = await fixture.NextCompletionAsync("recover");
                recovery.Complete("recovered-tag");
                await UntilAsync(box, () => Items(box).Any(item => item.Text is "recovered-tag"));
                var notifications = container.Notifications.Count();
                Type("obsolete-error");
                var obsolete = await fixture.NextCompletionAsync("obsolete-error");
                Type("current-query");
                obsolete.Fail();
                await DrainAsync();
                Require(container.Notifications.Count() == notifications && box.Text is "current-query",
                    "An obsolete failure does not notify the current query");
                completed.Add($"{theme.Key}: current failure is visible, query/actions survive, recovery works, obsolete failure is silent");
                Type("");

                async Task UntilNotificationAsync()
                {
                    var deadline = DateTime.UtcNow.AddSeconds(5);
                    while (DateTime.UtcNow < deadline)
                    {
                        Render(window);
                        if (container.Notifications.Any(notification => notification.Type is NotificationType.Error
                            && notification.Title == I18NManager.GetResource(SearchResources.Validation.SearchFailed.Title)
                            && !string.IsNullOrWhiteSpace(notification.Message)))
                            return;
                        await Task.Delay(10);
                    }
                    throw new InvalidOperationException("A current autocomplete failure must display an error notification.");
                }

                void Type(string value)
                {
                    Require(text.Focus(), "Search text input receives focus");
                    text.SelectAll();
                    if (value.Length is 0)
                    {
                        window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, "\b");
                        window.KeyRelease(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, "\b");
                    }
                    else
                        window.KeyTextInput(value);
                    Render(window);
                    Require(box.Text == value, "Keyboard text updates the production AutoCompleteBox");
                }
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
        }
        Require(fixture.UnexpectedRequests.IsEmpty, "No request escaped the fixed endpoint matrix");
        File.WriteAllLines(Path.Combine(directory, "search-validation.txt"), completed);
        File.WriteAllLines(Path.Combine(directory, "offline-requests.txt"), fixture.RequestLog);
        Console.WriteLine($"Validated {completed.Count} search interaction groups with offline HTTP responses.");
    }

    private static IEnumerable<SearchCompletionItem> Items(AutoCompleteBox box) =>
        box.ItemsSource?.Cast<SearchCompletionItem>() ?? [];

    private static async Task UntilAsync(AutoCompleteBox box, Func<bool> condition)
    {
        if (condition())
            return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        box.PropertyChanged += Changed;
        try
        {
            if (condition())
                return;
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            box.PropertyChanged -= Changed;
        }
        return;

        void Changed(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs args)
        {
            if (condition())
                _ = completion.TrySetResult();
        }
    }

    private static async Task DrainAsync()
    {
        // Let HTTP deserialization and the UI dispatch complete after a deliberately late response.
        await Task.Delay(50);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    }

    private static void Render(Window window)
    {
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(Window window, string path)
    {
        Render(window);
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No search frame.");
        frame.Save(path, new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }

}
