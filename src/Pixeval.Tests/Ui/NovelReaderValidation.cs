using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Controls;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;

namespace Pixeval.Tests.Ui;

internal static class NovelReaderValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new NovelReaderFixture();
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-novel-reader");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        List<string> report = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var firstId = theme == ThemeVariant.Light ? 997001 : 997101;
            using var source = new SimpleOperableSourceView<NovelItemViewModel>([
                NovelReaderFixture.Sample(firstId), NovelReaderFixture.Sample(firstId + 1), NovelReaderFixture.Sample(firstId + 2)]);
            var model = new NovelViewerPageViewModel(source.CloneSourceView(), 0, false);
            var page = new NovelViewerPage(model);
            var host = new PageTestHost { Content = page };
            var window = new Window { Width = 1000, Height = 760, Content = host.Shell, RequestedThemeVariant = theme };
            try
            {
                window.Show();
                var first = await fixture.NextAsync();
                Require(first.Id == firstId && model.IsLoading, "The selected novel loads through the real content endpoint");
                first.Complete("[chapter:First]\nFirst page\n[jump:2][newpage]Second page[newpage]Third page");
                await UntilAsync(() => !model.IsLoading);
                Require(model.LoadErrorMessage is null && model.PageCount == 3 && model.CurrentMarkdown.Contains("First page"),
                    "The fixed webview response reaches the product parser: " + model.LoadErrorMessage);
                await RenderAsync(window);
                var next = page.GetVisualDescendants().OfType<PageButton>().Single(button => button.Symbol == FluentIcons.Common.Symbol.ChevronRight);
                Require(next.Focus(NavigationMethod.Tab), "Next page accepts keyboard focus");
                Press(window, Key.Enter, PhysicalKey.Enter);
                await RenderAsync(window);
                Require(model.CurrentPageIndex == 1 && model.CurrentMarkdown.Contains("Second page"), "Enter advances exactly one page");
                Press(window, Key.Left, PhysicalKey.ArrowLeft);
                await RenderAsync(window);
                Require(model.CurrentPageIndex == 0, "Left returns to the previous page");
                model.CurrentPageIndex = 2;
                model.NextWorkCommand.Execute(null);
                var second = await fixture.NextAsync();
                Require(second.Id == firstId + 1 && model.IsLoading, "Next work selects the next source item");
                Require(model.PageCount == 0 && model.CurrentMarkdown.Length == 0 && model.CurrentPageIndex == 0,
                    "Switching works clears the previous text and page count before new content arrives");
                model.NextWorkCommand.Execute(null);
                var third = await fixture.NextAsync();
                third.Complete("Current third novel[newpage]Another current page");
                await UntilAsync(() => !model.IsLoading);
                second.Complete("Stale second novel");
                await RenderAsync(window);
                Require(model.NovelId == firstId + 2 && model.CurrentMarkdown.Contains("Current third novel") && model.PageCount == 2,
                    "A late old response cannot replace the current novel");
                report.Add($"PASS {theme.Key}: parsed pages, keyboard navigation and out-of-order content isolation");

                window.Width = 360;
                await RenderAsync(window);
                var bottom = page.FindControl<Border>("ReaderStatusBar")
                    ?? throw new InvalidOperationException("The reader exposes its responsive status surface");
                foreach (var control in bottom.GetVisualDescendants().OfType<Control>().Where(c => c is CommandBar or TextBlock && c.IsEffectivelyVisible))
                {
                    var origin = control.TranslatePoint(default, bottom)!.Value;
                    Require(origin.X >= -1 && origin.X + control.Bounds.Width <= bottom.Bounds.Width + 1,
                        "Reader status controls fit a narrow window: " + control);
                }
                Save(window, directory, "reader-" + theme.Key + "-narrow");
                report.Add($"PASS {theme.Key}: narrow status and title layout");

                var split = page.FindControl<SplitView>("InfoSplitView")!;
                var info = page.FindControl<CommandBarToggleButton>("InformationButton")!;
                await ActivateAsync(window, info);
                Require(split.IsPaneOpen && split.DisplayMode == SplitViewDisplayMode.Overlay
                    && split.OpenPaneLength <= page.Bounds.Width, "The information pane overlays narrow content within its width");
                Save(window, directory, "reader-" + theme.Key + "-information");
                await ActivateAsync(window, info);
                Require(!split.IsPaneOpen, "The same information toggle closes the pane");
                window.Width = 1000;
                await RenderAsync(window);
                Require(split.DisplayMode == SplitViewDisplayMode.Inline, "The wide reader restores inline information without recreating the page");

                await ActivateAsync(window, page.FindControl<CommandBarToggleButton>("BottomListButton")!);
                var list = page.FindControl<ListBox>("NovelList")!;
                Require(list.IsEffectivelyVisible && list.SelectedIndex == 2, "The bottom list exposes the current source selection");
                var firstItem = (ListBoxItem) list.ContainerFromIndex(0)!;
                Require(firstItem.Focus(NavigationMethod.Tab), "Source thumbnails remain keyboard reachable");
                Press(window, Key.Space, PhysicalKey.Space);
                await UntilAsync(() => model.CurrentWorkIndex == 0 && !model.IsLoading);
                Require(model.PageCount == 3 && model.CurrentPageIndex == 0, "Selecting a cached source novel restores its first page without another request");
                Require(fixture.ContentRequests.Reader.Count == 0, "Successful novel content stays cached");
                report.Add($"PASS {theme.Key}: information toggle, responsive pane, bottom-list selection and content cache");

                host.Shell.ClosePage(page);
                Require(!model.ToggleTranslationCommand.CanExecute(null), "Closing the production tab disposes the reader");
            }
            finally { window.Close(); }
        }
        await FailuresAsync(fixture, directory, report);
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "All reader requests stay offline: " + string.Join(",", fixture.Http.UnexpectedRequests));
        File.WriteAllLines(Path.Combine(directory, "novel-reader-contracts.txt"), report);
    }

    private static async Task FailuresAsync(NovelReaderFixture fixture, string directory, List<string> report)
    {
        var item = new NovelItemViewModel(NovelReaderFixture.Sample(997010));
        var model = new NovelViewerPageViewModel(item, false);
        var page = new NovelViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 360, Height = 760, Content = host.Shell };
        try
        {
            window.Show();
            var failed = await fixture.NextAsync();
            failed.Fail();
            await UntilAsync(() => !model.IsLoading);
            Require(model.LoadErrorMessage is not null && model.PageCount == 0,
                $"A failed content request presents an error without stale pages: id={failed.Id}, current={model.NovelId}, index={model.CurrentWorkIndex}, error={model.LoadErrorMessage}, count={model.PageCount}");
            await RenderAsync(window);
            Save(window, directory, "reader-content-error");
            var retry = page.FindControl<CommandBarButton>("ReloadButton")!;
            await ActivateAsync(window, retry);
            var repeated = await fixture.NextAsync();
            Require(repeated.Id == 997010 && model.IsLoading && !model.RetryCommand.CanExecute(null), "Refresh starts one new request for the same novel");
            Require(ReferenceEquals(item.ContentAsync, item.ContentAsync), "An in-flight retry is shared with other consumers");
            repeated.Complete("Recovered first[newpage]Recovered second");
            await UntilAsync(() => !model.IsLoading);
            Require(model.LoadErrorMessage is null && model.PageCount == 2 && model.CurrentMarkdown.Contains("Recovered first"), "The failed content cache recovers through the real refresh button");
            model.CurrentPageIndex = -1;
            model.CurrentPageIndex = 99;
            model.CurrentWorkIndex = -2;
            model.CurrentWorkIndex = 99;
            Require(model.CurrentPageIndex == 0 && model.CurrentWorkIndex == 0, "Invalid selection indices do not leave the readable range");
            await RenderAsync(window);
            var next = page.FindControl<PageButton>("NextPageButton")!;
            var point = next.TranslatePoint(new Rect(next.Bounds.Size).Center, window)!.Value;
            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);
            await RenderAsync(window);
            Require(model.CurrentWorkIndex == 0 && model.CurrentPageIndex == 0, "Right click without a next work cannot move outside the single-novel source");
            report.Add("PASS content failure: visible error, UI retry, shared in-flight/success cache and bounded navigation");

            var closedModel = new NovelViewerPageViewModel(new NovelItemViewModel(NovelReaderFixture.Sample(997011)), false);
            var closedPage = new NovelViewerPage(closedModel);
            host.Shell.NavigateTo(closedPage);
            await RenderAsync(window);
            var late = await fixture.NextAsync();
            host.Shell.ClosePage(closedPage);
            var notifications = 0;
            closedModel.PropertyChanged += (_, _) => notifications++;
            late.Complete("Closed late content");
            await RenderAsync(window);
            Require(!closedModel.IsLoading && closedModel.PageCount == 0 && notifications == 0,
                "Closing a real tab prevents late content and property notifications");
            report.Add("PASS close: pending shared content may finish without rewriting the closed reader");

            var attempts = 0;
            fixture.Http.ArtworkDetailResponseFactory = (_, _) =>
            {
                if (++attempts == 1) throw new HttpRequestException("Fixed unavailable novel detail");
                return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(new { novel = NovelReaderFixture.Sample(997012) }));
            };
            var detailModel = new NovelViewerPageViewModel(997012);
            var detailPage = new NovelViewerPage(detailModel);
            host.Shell.NavigateTo(detailPage);
            await UntilAsync(() => !detailModel.IsLoading);
            Require(detailModel.LoadErrorMessage is not null && detailModel.CurrentNovel is null, "ID-only detail failures remain visible");
            await RenderAsync(window);
            await ActivateAsync(window, detailPage.FindControl<CommandBarButton>("ReloadButton")!);
            var detailContent = await fixture.NextAsync();
            Require(detailContent.Id == 997012 && attempts == 2, "ID-only refresh preserves the original requested ID");
            detailContent.Complete("");
            await UntilAsync(() => !detailModel.IsLoading);
            Require(detailModel.LoadErrorMessage is null && detailModel.PageCount == 1 && detailModel.CurrentMarkdown.Length == 0,
                "An empty novel is a successful single blank page, not an error");
            host.Shell.ClosePage(detailPage);
            report.Add("PASS ID-only failure: same-ID detail retry and empty content");
        }
        finally { window.Close(); }
    }

    internal static async Task ActivateAsync(Window window, Button button)
    {
        if (button is CommandBarButton { IsInOverflow: true } or CommandBarToggleButton { IsInOverflow: true })
        {
            var bar = window.GetVisualDescendants().OfType<CommandBar>()
                .Single(candidate => candidate.PrimaryCommands.Any(command => ReferenceEquals(command, button))
                    || candidate.SecondaryCommands.Any(command => ReferenceEquals(command, button)));
            await OpenOverflowAsync(window, bar);
        }
        Require(button.IsEffectivelyVisible && button.IsEffectivelyEnabled && button.Focus(NavigationMethod.Tab), "Reader command is keyboard reachable: " + button.Name);
        Press(window, Key.Enter, PhysicalKey.Enter);
        await RenderAsync(window);
    }

    internal static async Task OpenOverflowAsync(Window window, CommandBar bar)
    {
        if (bar.IsOpen)
            return;
        var overflow = bar.GetVisualDescendants().OfType<Button>().Single(control => control.Name is "PART_OverflowButton");
        Require(overflow.Focus(NavigationMethod.Tab), "Reader overflow is keyboard reachable");
        Press(window, Key.Enter, PhysicalKey.Enter);
        await RenderAsync(window);
        Require(bar.IsOpen, "Enter opens the reader overflow menu");
    }

    internal static void Press(Window window, Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, physical, null);
        window.KeyRelease(key, modifiers, physical, null);
    }
    internal static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Novel state did not settle");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }
    internal static async Task RenderAsync(Window window)
    {
        window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(120);
        window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }
    internal static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No reader frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
    internal static void Require(bool condition, string contract)
    {
        if (!condition) throw new InvalidOperationException(contract);
    }
}
