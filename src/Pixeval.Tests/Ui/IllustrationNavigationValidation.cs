using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class IllustrationNavigationValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new IllustrationReaderFixture();
        List<string> report = [];
        foreach (var mode in new[] { BrowseMode.Swipe, BrowseMode.Continuous })
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = mode;
                using var source = new SimpleOperableSourceView<IllustrationItemViewModel>([
                    IllustrationReaderFixture.Sample(996101), IllustrationReaderFixture.Sample(996102, 2)]);
                var model = new IllustrationViewerPageViewModel(source.CloneSourceView(), 0, false);
                var page = new IllustrationViewerPage(model);
                var host = new PageTestHost { Content = page };
                var window = new Window { Width = 420, Height = 780, RequestedThemeVariant = theme, Content = host.Shell };
                try
                {
                    window.Show();
                    await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
                    await RenderAsync(window);
                    var slider = page.GetVisualDescendants().OfType<PagePreviewSlider>().Single();
                    Require(slider.Focus(NavigationMethod.Tab), "The original image viewer exposes a keyboard page selector");
                    Require(AutomationProperties.GetName(slider) == I18NManager.GetResource(EntryViewerPageResources.PageSelector),
                        "The page selector has a localized accessible name");
                    Press(window, Key.End, PhysicalKey.End);
                    await RenderAsync(window);
                    Require(model.CurrentPageIndex == 2 && model.CurrentWorkIndex == 0 && slider.IndicatorText == "3/3",
                        "End selects the last image exactly once through the two-way binding");
                    Press(window, Key.Right, PhysicalKey.ArrowRight);
                    Require(model.CurrentPageIndex == 2 && model.CurrentWorkIndex == 0,
                        "A selector boundary key does not bubble into the next work");
                    Press(window, Key.Home, PhysicalKey.Home);
                    Require(model.CurrentPageIndex == 0 && slider.IndicatorText == "1/3", "Home returns to the first image");
                    Press(window, Key.Left, PhysicalKey.ArrowLeft);
                    Require(model.CurrentPageIndex == 0 && model.CurrentWorkIndex == 0, "The first-page boundary remains inside the current work");
                    Press(window, Key.Down, PhysicalKey.ArrowDown);
                    await UntilAsync(() => model.CurrentWorkIndex == 1 && model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
                    await RenderAsync(window);
                    Require(slider.IndicatorText == "1/2" && model.CurrentPageIndex == 0, "The existing next-work shortcut rebinds the selector to the new work");

                    var info = page.GetVisualDescendants().OfType<IllustrationViewerInfoPane>().Single();
                    var infoBar = info.FindControl<CommandBar>("BottomActionBar")!;
                    var bookmark = infoBar.PrimaryCommands.OfType<CommandBarButton>().Single(button => button.Label == I18NManager.GetResource(EntryItemResources.AddToBookmark));
                    var save = infoBar.PrimaryCommands.OfType<CommandBarButton>().Single(button => button.Label == I18NManager.GetResource(EntryItemResources.Save));
                    Require(ReferenceEquals(bookmark.Command, model.CurrentIllustration!.BookmarkCommand)
                        && ReferenceEquals(save.Command, model.CurrentIllustration.SaveCommand),
                        "Information commands in overflow follow the new work rather than losing their binding context");

                    var bar = page.FindControl<CommandBar>("ZoomPane")!;
                    var next = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => ReferenceEquals(button.Command, model.NextCommand));
                    await ShowAsync(next);
                    var point = next.TranslatePoint(new Rect(next.Bounds.Size).Center, window)!.Value;
                    window.MouseDown(point, MouseButton.Right);
                    window.MouseUp(point, MouseButton.Right);
                    await RenderAsync(window);
                    Require(model.CurrentWorkIndex == 1 && model.CurrentPageIndex == 0,
                        "Right-clicking next when only another image page remains cannot leave the source");
                    if (bar.IsOpen)
                    {
                        Require(next.Focus(NavigationMethod.Tab), "The next command retains focus in overflow");
                        Press(window, Key.Escape, PhysicalKey.Escape);
                        await RenderAsync(window);
                    }
                    model.CurrentPageIndex = -1;
                    model.CurrentPageIndex = 99;
                    model.CurrentWorkIndex = -2;
                    model.CurrentWorkIndex = 99;
                    Require(model.CurrentWorkIndex == 1 && model.CurrentPageIndex == 0, "Invalid selection requests leave the existing page and work intact");

                    if (mode == BrowseMode.Swipe)
                    {
                        var pageButton = page.GetVisualDescendants().OfType<PageButton>().Single(button => ReferenceEquals(button.Command, model.NextCommand));
                        Require(pageButton.Focus(NavigationMethod.Tab), "The next-image overlay is keyboard reachable");
                        await Task.Delay(250); // Finish the production opacity transition.
                        Require(pageButton.Opacity > 0.99 && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(pageButton)),
                            "A focused page overlay is visible and named");
                        Press(window, Key.Enter, PhysicalKey.Enter);
                        await RenderAsync(window);
                        Require(model.CurrentPageIndex == 1 && !model.NextCommand.CanExecute(null), "Enter selects exactly one image and disables the final boundary");
                    }
                    else
                    {
                        await ShowAsync(next);
                        await ActivateAsync(window, next);
                        Require(model.CurrentPageIndex == 1 && !model.NextCommand.CanExecute(null), "The continuous viewer retains the same final-page command boundary");
                    }
                    if (bar.IsOpen)
                    {
                        var overflow = bar.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_OverflowButton");
                        Require(overflow.Focus(NavigationMethod.Tab), "Overflow remains available after the final next command disables itself");
                        Press(window, Key.Escape, PhysicalKey.Escape);
                        await RenderAsync(window);
                    }
                    Require(slider.Focus(NavigationMethod.Tab), "Focus can return to the selector after reaching the end");
                    Press(window, Key.Up, PhysicalKey.ArrowUp);
                    await UntilAsync(() => model.CurrentWorkIndex == 0 && model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
                    await RenderAsync(window);
                    var pane = page.FindControl<Border>("FloatingToolbarPane")!;
                    Require(slider.Focus(NavigationMethod.Tab), "The re-bound selector remains keyboard reachable");
                    await Task.Delay(250); // Finish the focus-driven toolbar opacity transition.
                    Require(pane.Opacity > 0.99, "Keyboard selection keeps the floating toolbar visible");
                    var location = pane.TranslatePoint(default, window)!.Value;
                    Require(location.X >= 0 && location.X + pane.Bounds.Width <= window.ClientSize.Width + 1,
                        "The page indicator and toolbar still fit a narrow production shell");
                    Save(window, directory, $"image-navigation-{mode}-{theme.Key}");
                    host.Shell.ClosePage(page);
                    Require(model.CurrentImage is null, "The full navigation flow ends through production tab close");
                    report.Add($"PASS {mode}/{theme.Key}: localized selector, one page per key, no boundary bubbling, work shortcuts, final-page/right-click guards, narrow layout and real close");

                    async Task ShowAsync(CommandBarButton button)
                    {
                        if (button.IsInOverflow)
                            await OpenOverflowAsync(window, bar);
                        await RenderAsync(window);
                        Require(button.IsEffectivelyVisible && button.IsEffectivelyEnabled, "The requested navigation action is available");
                    }
                }
                finally { window.Close(); }
            }
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "All image navigation remains offline");
        File.WriteAllLines(Path.Combine(directory, "illustration-navigation-contracts.txt"), report);
    }
}
