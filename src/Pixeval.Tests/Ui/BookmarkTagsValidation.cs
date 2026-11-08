using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Global.Enum;
using Mako.Model;
using Pixeval.Utilities;
using Pixeval.Views.Work;

namespace Pixeval.Tests.Ui;

internal static class BookmarkTagsValidation
{
    internal static async Task RunAsync(string directory)
    {
        var client = App.AppViewModel.MakoClient;
        using var fixture = new OfflineSearchFixture(client);
        await client.SetTokenAsync("fixture-bookmark-tags");
        await client.ValidateTokenAsync();
        var response = JsonSerializer.Serialize(new
        {
            bookmark_detail = new
            {
                is_bookmarked = true,
                restrict = "private",
                tags = Enumerable.Range(0, 12).Select(index => new { name = $"Tag {index} 中文", is_registered = index == 0 }).ToArray()
            }
        });
        fixture.BookmarkResponseFactory = (_, _) => Task.FromResult(response);
        List<string> report = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 360, 700 })
                foreach (var type in new[] { SimpleWorkType.Illustration, SimpleWorkType.Novel })
                {
                    var scenario = $"{theme.Key}-{width}-{type}";
                    var selector = new TagSelector { WorkType = type, WorkId = 901001 };
                    var window = new Window { Width = width, Height = 700, RequestedThemeVariant = theme, Content = new Border { Padding = new Thickness(16), Child = selector } };
                    try
                    {
                        window.Show();
                        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                        fixture.BookmarkResponseFactory = (_, _) => pending.Task;
                        var loading = selector.ResetSourceAsync();
                        await RenderAsync(window);
                        var confirm = selector.FindControl<Button>("ConfirmButton")!;
                        Require(selector.IsLoading && !confirm.IsEffectivelyEnabled, "Loading disables bookmark confirmation");
                        pending.SetResult(response);
                        await loading.WaitAsync(TimeSpan.FromSeconds(5));
                        fixture.BookmarkResponseFactory = (_, _) => Task.FromResult(response);
                        await RenderAsync(window);
                        Require(selector.IsPrivate && selector.SelectedTags.Single().Name == "Tag 0 中文", "Existing privacy and registered tag load through the real API model");
                        var list = selector.GetVisualDescendants().OfType<ListBox>().Single();
                        list.Selection.SelectRange(0, 11);
                        await RenderAsync(window);
                        Require(selector.SelectedTags.Count == 10 && selector.SelectedTags.All(tag => tag is not AddNewBookmarkTag), "Selection keeps the ten-tag limit and excludes the add sentinel");
                        var add = selector.GetVisualDescendants().OfType<AddNewBookmarkTagItem>().Single();
                        var addButton = Part(add, "PART_AddButton");
                        add.BringIntoView();
                        await RenderAsync(window);
                        await ActivateAsync(window, addButton);
                        var input = add.GetVisualDescendants().OfType<TextBox>().Single();
                        Require(add.IsExpanded && input.IsFocused, "Adding a tag places the caret in the real text editor");
                        window.KeyTextInput("  新标签 with a very long name that must stay inside the popup  ");
                        await RenderAsync(window);
                        InWindow(window, input);
                        InWindow(window, Part(add, "PART_ConfirmButton"));
                        InWindow(window, Part(add, "PART_CancelButton"));
                        Save(window, directory, "bookmark-tags-edit-" + scenario);
                        await ActivateAsync(window, Part(add, "PART_ConfirmButton"));
                        Require(!add.IsExpanded && addButton.IsFocused, "Confirming a new tag returns keyboard focus to the add button");
                        var newName = "新标签 with a very long name that must stay inside the popup";
                        Require(selector.SelectedTags.Count == 10 && selector.SelectedTags.Any(tag => tag.Name == newName), "A new tag is trimmed, added and selected within the existing limit");
                        var count = selector.TagsSource.Count;
                        await ActivateAsync(window, addButton);
                        window.KeyTextInput(newName);
                        await ActivateAsync(window, Part(add, "PART_ConfirmButton"));
                        Require(selector.TagsSource.Count == count, "A duplicate tag does not create another entry");
                        await ActivateAsync(window, addButton);
                        window.KeyTextInput("Discard me");
                        await ActivateAsync(window, Part(add, "PART_CancelButton"));
                        Require(!add.IsExpanded && addButton.IsFocused && string.IsNullOrEmpty(input.Text) && selector.TagsSource.Count == count,
                            "Cancel clears only the draft and restores the add action");
                        Require(new[] { addButton, Part(add, "PART_ConfirmButton"), Part(add, "PART_CancelButton"), confirm }
                            .All(button => !string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))), "Every icon action has a localized accessible name");
                        var privacy = selector.GetVisualDescendants().OfType<CheckBox>().Single();
                        Require(privacy.Focus(), "Privacy remains keyboard reachable");
                        Press(window, Key.Space, PhysicalKey.Space);
                        var submissions = 0;
                        selector.TagsSelected += (_, selection) =>
                        {
                            submissions++;
                            Require(!selection.isPrivate && selection.tags.SequenceEqual(selector.SelectedTags.Select(tag => tag.Name)), "Confirmation carries the selected tags and changed privacy exactly");
                        };
                        await ActivateAsync(window, confirm);
                        Require(submissions == 1, "One confirmation produces one selection event");
                        Save(window, directory, "bookmark-tags-ready-" + scenario);
                        report.Add($"PASS {scenario}: loading gate, privacy/registered tags, ten-tag limit, add/duplicate/cancel, long editor bounds, restored focus and exact payload");
                    }
                    finally { window.Content = null; window.Close(); }
                }

        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var anchor = new Button { Content = "Bookmark tags", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            var window = new Window { Width = 360, Height = 720, RequestedThemeVariant = theme, Content = anchor };
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource? late = null;
            var submissions = 0;
            try
            {
                window.Show();
                await RenderAsync(window);
                await BookmarkTagSelectorFlyoutHelper.ShowAsync(anchor, SimpleWorkType.Illustration, 901001, async _ => { submissions++; await release.Task; });
                await RenderAsync(window);
                var selector = window.GetVisualDescendants().OfType<TagSelector>().Single();
                InWindow(window, selector);
                window.Width = 300;
                await RenderAsync(window);
                InWindow(window, selector);
                window.Width = 360;
                await RenderAsync(window);
                Save(window, directory, $"bookmark-tags-flyout-{theme.Key}");
                var confirm = selector.FindControl<Button>("ConfirmButton")!;
                await ActivateAsync(window, confirm);
                await ActivateAsync(window, confirm);
                Require(submissions == 1 && !confirm.IsEffectivelyEnabled, "An outstanding async bookmark action cannot be submitted twice");
                release.SetResult();
                await RenderAsync(window);
                Require(TopLevel.GetTopLevel(selector) is null, "Successful async confirmation detaches the real flyout content");
                late = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await BookmarkTagSelectorFlyoutHelper.ShowAsync(anchor, SimpleWorkType.Novel, 901001, async _ => { submissions++; await late.Task; });
                await RenderAsync(window);
                var closing = window.GetVisualDescendants().OfType<TagSelector>().Single();
                await ActivateAsync(window, closing.FindControl<Button>("ConfirmButton")!);
                Press(window, Key.Escape, PhysicalKey.Escape);
                await RenderAsync(window);
                Require(TopLevel.GetTopLevel(closing) is null, "Escape can dismiss the flyout while an async action is outstanding");
                late.SetResult();
                await RenderAsync(window);
                Require(submissions == 2 && TopLevel.GetTopLevel(closing) is null, "Late completion does not reopen a closed flyout or submit again");
                var closedContent = (Border) closing.Parent!;
                var closedWidth = closedContent.MaxWidth;
                window.Width = 500;
                await RenderAsync(window);
                Require(closedContent.MaxWidth == closedWidth, "A closed flyout no longer subscribes to window resizing");
                window.Width = 360;
                await RenderAsync(window);
                var expectedFailure = new InvalidOperationException("offline bookmark callback failure");
                var observedFailures = 0;
                var attempts = 0;
                void ObserveFailure(object sender, DispatcherUnhandledExceptionEventArgs args)
                {
                    if (ReferenceEquals(args.Exception, expectedFailure))
                        observedFailures++;
                }
                Dispatcher.UIThread.UnhandledException += ObserveFailure;
                try
                {
                    await BookmarkTagSelectorFlyoutHelper.ShowAsync(anchor, SimpleWorkType.Illustration, 901001,
                        _ => ++attempts == 1 ? Task.FromException(expectedFailure) : Task.CompletedTask);
                    await RenderAsync(window);
                    var retry = window.GetVisualDescendants().OfType<TagSelector>().Single();
                    var retryButton = retry.FindControl<Button>("ConfirmButton")!;
                    await ActivateAsync(window, retryButton);
                    Require(observedFailures == 1 && retry.IsEnabled && retryButton.IsFocused && TopLevel.GetTopLevel(retry) is not null,
                        "Callback failure reaches the existing error boundary and restores a focused retry action");
                    await ActivateAsync(window, retryButton);
                    Require(attempts == 2 && TopLevel.GetTopLevel(retry) is null, "Retry completes once and closes the flyout");
                }
                finally { Dispatcher.UIThread.UnhandledException -= ObserveFailure; }
                report.Add($"PASS flyout-{theme.Key}: compact placement/resize, one pending async submission, Escape/late completion and error/retry");
            }
            finally { release.TrySetResult(); late?.TrySetResult(); window.Content = null; window.Close(); }
        }
        File.WriteAllLines(Path.Combine(directory, "bookmark-tag-contracts.txt"), report);
        File.WriteAllLines(Path.Combine(directory, "offline-requests.txt"), fixture.RequestLog);
        Require(fixture.UnexpectedRequests.IsEmpty, "No request escapes the offline bookmark fixture");
        Console.WriteLine($"Bookmark tag contracts passed: {report.Count} scenarios.");
    }

    private static Button Part(Control control, string name) => control.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);
    private static async Task ActivateAsync(Window window, Button button)
    {
        button.BringIntoView();
        await RenderAsync(window);
        InWindow(window, button);
        var point = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)!.Value;
        window.MouseMove(point); window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
        await RenderAsync(window);
    }
    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
    }
    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(100);
        window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }
    private static void InWindow(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window)!.Value;
        Require(control.Bounds.Width > 0 && control.Bounds.Height > 0 && point.X >= -1 && point.Y >= -1
            && point.X + control.Bounds.Width <= window.ClientSize.Width + 1 && point.Y + control.Bounds.Height <= window.ClientSize.Height + 1,
            $"{control.Name ?? control.GetType().Name} fits inside its window: {point}, {control.Bounds.Size}");
    }
    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
    private static void Require(bool condition, string contract)
    {
        if (!condition) throw new InvalidOperationException("Bookmark tag contract failed: " + contract);
    }
}
