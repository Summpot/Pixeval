using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

internal static class IllustrationReaderValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new IllustrationReaderFixture();
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-illustration-reader");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        List<string> report = [];
        foreach (var mode in new[] { BrowseMode.Swipe, BrowseMode.Continuous })
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = mode;
                using var source = new SimpleOperableSourceView<IllustrationItemViewModel>([
                    IllustrationReaderFixture.Sample(996001), IllustrationReaderFixture.Sample(996002, 1)]);
                var model = new IllustrationViewerPageViewModel(source.CloneSourceView(), 0, false);
                var page = new IllustrationViewerPage(model);
                var host = new PageTestHost { Content = page };
                var window = new Window { Width = 1100, Height = 780, RequestedThemeVariant = theme, Content = host.Shell };
                try
                {
                    window.Show();
                    await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
                    await RenderAsync(window);
                    Require(model.PageCount == 3 && model.CurrentWorkIndex == 0 && model.CurrentPageIndex == 0,
                        "The production viewer loads the first work and page through the original image pipeline");
                    var panePages = model.PanePages;
                    Require(ReferenceEquals(panePages, model.PanePages), "Reading the same work's information pages does not recreate their state");
                    var wrapper = page.FindControl<ImageViewerWrapper>("I")!;
                    Require(wrapper.IsSwipeMode == (mode == BrowseMode.Swipe), "The configured viewer kernel is retained");
                    var navigationFocus = page.GetVisualDescendants().OfType<ToggleButton>()
                        .Single(button => Equals(ToolTip.GetTip(button), I18NManager.GetResource(EntryViewerPageResources.Pin)));
                    Require(navigationFocus.Focus(NavigationMethod.Tab), "A real viewer action accepts focus for bubbling navigation shortcuts");
                    Press(window, Key.Right, PhysicalKey.ArrowRight);
                    await RenderAsync(window);
                    Require(model.CurrentWorkIndex == 0 && model.CurrentPageIndex == 1, "Right moves by one image page");
                    Press(window, Key.Down, PhysicalKey.ArrowDown);
                    await UntilAsync(() => model.CurrentWorkIndex == 1 && model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
                    Require(model.PageCount == 1 && model.CurrentPageIndex == 0, "Down changes work and resets the selected image page");
                    Require(!ReferenceEquals(panePages, model.PanePages) && model.CurrentIllustration?.Entry.Id == "996002", "Information pages follow the newly selected work");
                    Press(window, Key.Up, PhysicalKey.ArrowUp);
                    await UntilAsync(() => model.CurrentWorkIndex == 0 && model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
                    Require(navigationFocus.Focus(NavigationMethod.Tab), "Page navigation can resume");
                    Press(window, Key.Right, PhysicalKey.ArrowRight, RawInputModifiers.Control);
                    Require(model.CurrentPageIndex == 0, "Modified arrows do not execute the unmodified page shortcut");
                    var handled = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right, Handled = true };
                    page.RaiseEvent(handled);
                    Require(model.CurrentPageIndex == 0, "A handled descendant key does not navigate twice");

                    var floating = page.FindControl<FloatingPaneView>("EntryViewerFloatingPaneView")!;
                    floating.IsDocked = true;
                    window.Width = 360;
                    await RenderAsync(window);
                    await Task.Delay(350); // The production docking transition must finish before geometry checks.
                    await RenderAsync(window);
                    var pane = page.FindControl<Border>("FloatingPaneBorder")!;
                    var origin = pane.TranslatePoint(default, window)!.Value;
                    Require(pane.Bounds.Width > 0 && origin.X >= 0 && origin.X + pane.Bounds.Width <= window.ClientSize.Width + 1,
                        "The docked information pane stays inside a narrow viewer");
                    Require(wrapper.Bounds.Width > 0 && wrapper.Bounds.Height > 0, "Docking leaves a usable image viewport");
                    var info = page.GetVisualDescendants().OfType<IllustrationViewerInfoPane>().Single();
                    var actions = info.FindControl<CommandBar>("BottomActionBar")!;
                    foreach (var action in actions.PrimaryCommands.OfType<CommandBarButton>().Where(button => button.IsVisible))
                    {
                        if (action.IsInOverflow)
                            await OpenOverflowAsync(window, actions);
                        await RenderAsync(window);
                        Require(action.Focus(NavigationMethod.Tab) && action.IsEffectivelyVisible,
                            "Each narrow information action is keyboard reachable: " + action.Label);
                        var actionOrigin = action.TranslatePoint(default, window)!.Value;
                        Require(actionOrigin.X >= 0 && actionOrigin.X + action.Bounds.Width <= window.ClientSize.Width + 1,
                            "Information actions and overflow remain inside the viewer: " + action.Label);
                        if (action.Command is { } command)
                            Require(command.CanExecute(action.CommandParameter), "Moving an action into overflow preserves its command context: " + action.Label);
                    }
                    if (actions.IsOpen)
                    {
                        Press(window, Key.Escape, PhysicalKey.Escape);
                        await RenderAsync(window);
                        Require(!actions.IsOpen, "Escape dismisses the information action overflow");
                    }
                    Save(window, directory, $"illustration-{mode}-{theme.Key}-narrow");
                    floating.IsDocked = false;
                    window.Width = 1100;
                    await RenderAsync(window);
                    Save(window, directory, $"illustration-{mode}-{theme.Key}-wide");
                    var image = model.CurrentImage;
                    host.Shell.NavigateTo(new Pixeval.Views.Search.SearchPage());
                    await RenderAsync(window);
                    host.CloseDestinationAndReturn();
                    await RenderAsync(window);
                    Require(ReferenceEquals(image, model.CurrentImage), "Temporary navigation preserves the current image model");
                    host.Shell.ClosePage(page);
                    Require(model.CurrentImage is null && !model.IsLoading, "Closing the production tab releases its image models");
                    report.Add($"PASS {mode}/{theme.Key}: original image pipeline, work/page keys, stable information, narrow docking, temporary navigation and production close");
                }
                finally { window.Close(); }
            }

        await FailuresAsync(fixture, directory, report);
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Illustration viewer requests stay offline: " + string.Join(",", fixture.Http.UnexpectedRequests));
        File.WriteAllLines(Path.Combine(directory, "illustration-reader-contracts.txt"), report);
    }

    private static async Task FailuresAsync(IllustrationReaderFixture fixture, string directory, List<string> report)
    {
        var model = new IllustrationViewerPageViewModel(IllustrationReaderFixture.Identity(996010));
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 360, Height = 760, Content = host.Shell };
        try
        {
            window.Show();
            var first = await fixture.NextDetailAsync();
            Require(first.Id == 996010 && model.IsLoading, "ID-only navigation starts the exact detail request");
            first.Fail();
            await UntilAsync(() => !model.IsLoading);
            Require(model.LoadErrorMessage is not null && model.CurrentImage is null && model.PageCount == 0,
                "Failed detail has a visible error and no stale page count");
            await RenderAsync(window);
            Save(window, directory, "illustration-detail-error");
            var retry = page.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_RetryButton");
            await ActivateAsync(window, retry);
            var repeated = await fixture.NextDetailAsync();
            Require(repeated.Id == first.Id && model.IsLoading && !retry.IsEffectivelyEnabled,
                "The retry button preserves the original identity and disables concurrent requests");
            repeated.Complete();
            await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            Require(model.LoadErrorMessage is null && model.PageCount == 3 && model.CurrentWorkIndex == 0, "UI retry recovers the original detail and images");
            Save(window, directory, "illustration-detail-recovered");
            report.Add("PASS ID-only detail error, exact-ID keyboard retry, busy gate and recovered image pages");

            var invalid = new IllustrationViewerPageViewModel(IllustrationReaderFixture.Identity(996012));
            var invalidPage = new IllustrationViewerPage(invalid);
            host.Shell.NavigateTo(invalidPage);
            var invalidResponse = await fixture.NextDetailAsync();
            invalidResponse.Complete(IllustrationReaderFixture.Sample(996999));
            await UntilAsync(() => !invalid.IsLoading);
            Require(invalid.CurrentIllustration is null && invalid.CurrentImage is null && invalid.LoadErrorMessage is not null,
                "An unrelated/default detail response is an error rather than a successful wrong work");
            host.Shell.ClosePage(invalidPage);
            report.Add("PASS invalid detail identity: unrelated response does not become the current illustration");

            host.Shell.CreateIllustrationPage("000996013", "pixiv");
            var normalizedPage = (IllustrationViewerPage) host.Shell.SelectedPage!;
            var normalized = (IllustrationViewerPageViewModel) normalizedPage.DataContext!;
            var normalizedResponse = await fixture.NextDetailAsync();
            Require(normalizedResponse.Id == 996013, "The existing platform service normalizes a numeric app-link identity");
            normalizedResponse.Complete();
            await UntilAsync(() => normalized.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            Require(normalized.CurrentIllustration?.Entry.Id == "996013" && normalized.LoadErrorMessage is null,
                "Leading-zero Pixiv app links still open their canonical artwork after identity validation");
            host.Shell.ClosePage(normalizedPage);
            report.Add("PASS numeric app-link compatibility: leading zeros resolve to the same canonical Pixiv artwork");

            using var source = new SimpleOperableSourceView<IllustrationItemViewModel>([
                IllustrationReaderFixture.Sample(996020), IllustrationReaderFixture.Sample(996021)]);
            var refreshed = new IllustrationViewerPageViewModel(source.CloneSourceView(), 0, true);
            var refreshedPage = new IllustrationViewerPage(refreshed);
            host.Shell.NavigateTo(refreshedPage);
            var obsolete = await fixture.NextDetailAsync();
            refreshed.CurrentWorkIndex = 1;
            var current = await fixture.NextDetailAsync();
            Require(obsolete.Token.IsCancellationRequested && current.Id == 996021 && refreshed.CurrentImage is null,
                "Changing work cancels obsolete details and clears the old image");
            current.Complete();
            await UntilAsync(() => refreshed.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            var activeImage = refreshed.CurrentImage;
            obsolete.Complete();
            await RenderAsync(window);
            Require(ReferenceEquals(activeImage, refreshed.CurrentImage) && refreshed.CurrentIllustration?.Entry.Id == "996021",
                "A late obsolete response cannot replace the selected work");
            refreshed.CurrentWorkIndex = 0;
            var closed = await fixture.NextDetailAsync();
            host.Shell.ClosePage(refreshedPage);
            Require(closed.Token.IsCancellationRequested, "Production tab close cancels the pending detail request");
            var changed = 0;
            refreshed.PropertyChanged += (_, _) => changed++;
            closed.Complete();
            await RenderAsync(window);
            Require(changed == 0 && refreshed.CurrentImage is null && !refreshed.IsLoading,
                "A closed viewer neither publishes late images nor sends property notifications");
            report.Add("PASS refreshed source: cancellation on work switch/close, out-of-order detail isolation and no closed-page notifications");
        }
        finally { window.Close(); }
    }
}
