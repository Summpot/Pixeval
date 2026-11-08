using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mako.Global.Enum;
using Mako.Model;
using Misaki;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.ViewModels;
using Pixeval.Views.Work;

namespace Pixeval.Tests.Ui;

internal static class WorkSelectionValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new OfflineSearchFixture(App.AppViewModel.MakoClient);
        const string imageUri = "https://i.pximg.net/m3e/work-selection.png";
        using (var stream = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var bytes = new MemoryStream())
        {
            stream.CopyTo(bytes);
            fixture.ImageResponses[imageUri] = bytes.ToArray();
        }
        List<string> report = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 360, 1000 })
                foreach (var layout in new[] { ThumbnailLayoutType.LinedFlow, ThumbnailLayoutType.Grid, ThumbnailLayoutType.Masonry })
                {
                    var scenario = $"{theme.Key}-{width}-{layout}";
                    var entries = Enumerable.Range(0, 6).Select(index => (IArtworkInfo) (Illustration.CreateDefault() with
                    {
                        Id = 901001 + index,
                        Title = index < 2 ? $"Keep 中文 {index}" : $"Other 中文 {index}",
                        Type = IllustrationType.Illustration,
                        Width = 120 + 40 * index,
                        Height = 160,
                        PageCount = 1,
                        Visible = true,
                        SanityLevel = 2,
                        Series = null,
                        TotalFavorite = index * 100,
                        CreateDate = new DateTimeOffset(2024, 1, 1 + index, 0, 0, 0, TimeSpan.Zero),
                        ThumbnailUrls = new() { SquareMedium = imageUri, Medium = imageUri, Large = imageUri },
                        MetaSinglePage = new() { OriginalImageUrl = imageUri },
                        User = new() { Id = 900001, Name = "Fixture author", Account = "fixture", ProfileImageUrls = new() { Medium = imageUri } }
                    })).ToArray();
                    var container = new WorkContainer();
                    var view = container.FindControl<WorkView>("WorkView")!;
                    view.LayoutType = layout;
                    view.SetSource(entries, SimpleWorkType.Illustration);
                    var model = (IOperableViewViewModel) view.DataContext!;
                    var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = container };
                    try
                    {
                        window.Show();
                        await RenderAsync(window);
                        var filter = container.FindControl<WorkFilterAutoSuggestBox>("WorkFilterAutoSuggestBox")!;
                        var sort = container.FindControl<SymbolComboBox>("SortOptionComboBox")!;
                        var toggle = container.FindControl<CommandBarToggleButton>("SelectionToggleButton")!;
                        var right = container.FindControl<CommandBar>("RightToolBar")!;
                        var selection = container.FindControl<CommandBar>("SelectionToolBar")!;
                        var list = view.FindControl<WorkListBox>("WorkListBox")!;
                        Require(list.Items.Count == entries.Length, "The real list must contain every fixed artwork");
                        Save(window, directory, "work-normal-" + scenario);
                        InWindow(window, filter);
                        InWindow(window, sort);
                        InWindow(window, right);
                        Require(!new Rect(filter.TranslatePoint(default, window)!.Value, filter.Bounds.Size)
                                .Intersects(new Rect(right.TranslatePoint(default, window)!.Value, right.Bounds.Size)),
                            "The filter must not cover selection and refresh commands in a compact window");
                        Require(!string.IsNullOrWhiteSpace(toggle.Label), "The selection toggle keeps a name when moved into overflow");

                        var editor = filter.GetVisualDescendants().OfType<TextBox>().Single();
                        Require(editor.Focus(), "The real filter accepts keyboard input");
                        window.KeyTextInput("Keep");
                        Press(window, Key.Enter, PhysicalKey.Enter);
                        await RenderAsync(window);
                        Require(model.View.Count == 2 && model.View.All(item => item.Entry.Title.StartsWith("Keep", StringComparison.Ordinal)),
                            "The filter submits through the real container and preserves matching identities");
                        sort.SelectedValue = LocalSortOption.PopularityDescending;
                        await RenderAsync(window);
                        Require(model.View.First().Entry.Id == "901002", "Sort selection applies to the filtered collection");
                        filter.Text = "";
                        Require(editor.Focus(), "Filter clearing retains its editor");
                        Press(window, Key.Enter, PhysicalKey.Enter);
                        await RenderAsync(window);
                        Require(model.View.Count == entries.Length, "Clearing the filter restores the source");
                        foreach (var option in new[] { LocalSortOption.PublishDateAscending, LocalSortOption.PublishDateDescending, LocalSortOption.DoNotSort })
                        {
                            sort.SelectedValue = option;
                            await RenderAsync(window);
                            var expected = option == LocalSortOption.PublishDateDescending ? entries.Reverse() : entries;
                            Require(model.View.Select(item => item.Entry.Id).SequenceEqual(expected.Select(item => item.Id)),
                                $"{option} preserves the expected identity order");
                        }

                        Require(toggle.Focus(), "Selection mode is keyboard reachable");
                        Press(window, Key.Space, PhysicalKey.Space);
                        await RenderAsync(window);
                        Require(model.IsSelecting && selection.IsEffectivelyVisible, "Selection mode displays the existing selection toolbar");
                        Require(list.SelectionMode.HasFlag(SelectionMode.Multiple), "Selection mode still uses the real multiple selection model");
                        selection.IsOpen = true;
                        await RenderAsync(window);
                        Require(model.SelectedEntries.Count == 0, "Entering selection starts with no selected entries");
                        foreach (var button in selection.PrimaryCommands.OfType<CommandBarButton>().Skip(2))
                            Require(!button.IsEffectivelyEnabled, $"Empty selection disables {button.Label} in both main and overflow strips");
                        selection.IsOpen = false;
                        await RenderAsync(window);
                        await ActivateAsync(window, selection, selection.PrimaryCommands.OfType<CommandBarButton>().First());
                        Require(model.SelectedEntries.Count == entries.Length, "Select all selects the real view exactly once");
                        foreach (var button in selection.PrimaryCommands.OfType<CommandBarButton>())
                            Require(button.IsEffectivelyEnabled, "A populated selection restores all existing actions");
                        Save(window, directory, "work-selected-" + scenario);
                        await ActivateAsync(window, selection, selection.PrimaryCommands.OfType<CommandBarButton>().ElementAt(1));
                        Require(model.SelectedEntries.Count == 0, "Invert removes an all-selected set");
                        list.Selection.Select(0);
                        var selectedId = model.SelectedEntries.Single().Entry.Id;
                        await ActivateAsync(window, selection, selection.PrimaryCommands.OfType<CommandBarButton>().ElementAt(1));
                        Require(model.SelectedEntries.Count == entries.Length - 1 && model.SelectedEntries.All(item => item.Entry.Id != selectedId),
                            "Invert preserves entry identity after sorting");
                        var clear = selection.PrimaryCommands.OfType<CommandBarButton>().ElementAt(2);
                        var clearInOverflow = clear.IsInOverflow;
                        await ActivateAsync(window, selection, clear);
                        Require(model.SelectedEntries.Count == 0, "Clear selection removes all selected entries");
                        Require(!selection.IsOpen && (!clearInOverflow || toggle.IsFocused),
                            "Clearing from overflow closes the disabled menu and restores a usable keyboard target");

                        model.IsSelecting = false;
                        await RenderAsync(window);
                        Require(toggle.IsChecked is false && !selection.IsEffectivelyVisible && filter.IsEffectivelyVisible,
                            "A model-driven mode change updates the toggle and toolbar consistently");
                        model.IsSelecting = true;
                        await RenderAsync(window);
                        Require(toggle.IsChecked is true, "The toggle reflects model-driven selection mode");
                        Require(toggle.Focus(), "Mode toggle stays keyboard reachable");
                        Press(window, Key.Space, PhysicalKey.Space);
                        await RenderAsync(window);
                        Require(!model.IsSelecting && filter.IsEffectivelyVisible, "The next user action exits the reflected selection mode");

                        var refreshes = 0;
                        container.RefreshRequested += (_, _) => refreshes++;
                        var refresh = container.FindControl<CommandBarButton>("RefreshButton")!;
                        await ActivateAsync(window, right, refresh);
                        Require(refreshes == 1, "Refresh keeps its existing event and single invocation");
                        container.IsRefreshEnabled = false;
                        await RenderAsync(window);
                        Require(!right.SecondaryCommands.Contains(refresh), "Disabled refresh is removed from overflow");
                        container.IsRefreshEnabled = true;
                        await RenderAsync(window);
                        Require(right.SecondaryCommands.Count(item => ReferenceEquals(item, refresh)) == 1, "Restored refresh is not duplicated");
                        await ActivateAsync(window, right, refresh);
                        Require(refreshes == 2, "Restored refresh invokes exactly once");
                        var extraCalls = 0;
                        var extra = new Button { Content = "Page command 中文", Width = 160 };
                        extra.Click += (_, _) => extraCalls++;
                        container.CommandBarElements.Add(extra);
                        var extraSecondary = new CommandBarButton { Label = "Page overflow command 中文" };
                        extraSecondary.Click += (_, _) => extraCalls++;
                        container.CommandBarSubElements.Add(extraSecondary);
                        await RenderAsync(window);
                        InWindow(window, extra);
                        InWindow(window, filter);
                        Require(extra.Focus(), "A page-supplied toolbar action remains keyboard reachable");
                        Press(window, Key.Enter, PhysicalKey.Enter);
                        await ActivateAsync(window, right, extraSecondary);
                        Require(extraCalls == 2, "Both page-supplied toolbar surfaces preserve their actions");
                        model.IsSelecting = true;
                        view.SetSource(entries.Take(2).ToArray(), SimpleWorkType.Illustration);
                        await RenderAsync(window);
                        var replacement = (IOperableViewViewModel) view.DataContext!;
                        Require(!ReferenceEquals(model, replacement) && !replacement.IsSelecting && toggle.IsChecked is false,
                            "Replacing a source resets the toggle to the new model's selection mode");
                        Require(list.Items.Count == 2 && replacement.SelectedEntries.Count == 0, "The new source has no stale items or selection");
                        report.Add($"PASS {scenario}: full filter/sort, selection/all/invert/clear, disabled actions, model/toggle synchronization, refresh remove/restore");
                    }
                    finally
                    {
                        window.Content = null;
                        view.Dispose();
                        window.Close();
                    }
                }
        File.WriteAllLines(Path.Combine(directory, "work-selection-contracts.txt"), report);
        File.WriteAllLines(Path.Combine(directory, "offline-requests.txt"), fixture.RequestLog);
        Require(fixture.UnexpectedRequests.IsEmpty, "No unexpected HTTP request may escape the fixed fixture: " + string.Join(", ", fixture.UnexpectedRequests));
        Console.WriteLine($"Work selection contracts passed: {report.Count} theme/width/layout scenarios.");
    }

    private static async Task ActivateAsync(Window window, CommandBar bar, CommandBarButton button)
    {
        if (button.IsInOverflow)
        {
            var more = bar.GetVisualDescendants().OfType<Button>().Single(control => control.Name is "PART_OverflowButton");
            Require(more.Focus(), "More commands accepts keyboard focus");
            Press(window, Key.Enter, PhysicalKey.Enter);
            await RenderAsync(window);
            Require(bar.IsOpen, "Keyboard opens the command overflow");
        }
        InWindow(window, button);
        var point = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)!.Value;
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        await RenderAsync(window);
        if (bar.IsOpen)
        {
            Press(window, Key.Escape, PhysicalKey.Escape);
            await RenderAsync(window);
            Require(!bar.IsOpen, "Escape closes overflow after the action");
        }
    }

    private static void InWindow(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window) ?? throw new InvalidOperationException("Detached control");
        Require(control.Bounds.Width > 0 && control.Bounds.Height > 0 && point.X >= -1 && point.Y >= -1
            && point.X + control.Bounds.Width <= window.ClientSize.Width + 1 && point.Y + control.Bounds.Height <= window.ClientSize.Height + 1,
            $"{control.Name ?? control.GetType().Name} must fit its window: {point}, {control.Bounds.Size}, window {window.ClientSize}");
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
        await Task.Delay(100);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No work list frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        if (!condition)
            throw new InvalidOperationException("Work selection contract failed: " + contract);
    }
}
