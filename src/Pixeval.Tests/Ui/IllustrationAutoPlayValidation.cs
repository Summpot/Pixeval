using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Search;
using Pixeval.Views.Viewers;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class IllustrationAutoPlayValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new IllustrationReaderFixture();
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-illustration-autoplay");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = BrowseMode.Swipe;
        List<string> report = [];
        using var source = new SimpleOperableSourceView<IllustrationItemViewModel>([
            IllustrationReaderFixture.Sample(996201, 2), IllustrationReaderFixture.Sample(996202, 2)]);
        var model = new IllustrationViewerPageViewModel(source.CloneSourceView(), 0, false);
        List<string> states = [];
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(model.CurrentPageIndex) or nameof(model.CurrentWorkIndex) or nameof(model.IsAutoPlaying))
                states.Add($"{DateTime.UtcNow:HH:mm:ss.fff} {e.PropertyName}: {model.CurrentWorkIndex}/{model.WorkCount} {model.CurrentPageIndex}/{model.PageCount} playing={model.IsAutoPlaying} loading={model.IsLoading} {model.AutoPlayMode}/{model.AutoPlayScope}");
        };
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 1100, Height = 780, Content = host.Shell };
        try
        {
            window.Show();
            await UntilAsync(() => model.CurrentImage?.Images.All(image => image.LoadSuccessfully) == true);
            await RenderAsync(window);
            var bar = page.FindControl<CommandBar>("ZoomPane")!;
            var toggle = bar.PrimaryCommands.OfType<CommandBarToggleButton>().Single(button => button.Label == I18NManager.GetResource(EntryViewerPageResources.AutoPlay));
            await SelectAsync(1);
            await SelectAsync(IllustrationViewerAutoPlayMode.Sequential);
            await SelectAsync(IllustrationViewerAutoPlayScope.CurrentWork);
            await ToggleAsync();
            Require(model.IsAutoPlaying && toggle.IsChecked == true, "The actual autoplay toggle starts the timer");
            await UntilAsync(() => model.CurrentPageIndex == 1);
            await UntilAsync(() => !model.IsAutoPlaying);
            Require(model.CurrentWorkIndex == 0 && model.CurrentPageIndex == 1 && toggle.IsChecked == false,
                "Current-work sequential playback stops on the last image without changing work");
            report.Add("PASS current-work sequential: real context settings, interval, toggle binding and final image stop");

            await SelectAsync(IllustrationViewerAutoPlayMode.Loop);
            await ToggleAsync();
            await UntilAsync(() => model.CurrentPageIndex == 0);
            await ToggleAsync();
            var pausedPage = model.CurrentPageIndex;
            await Task.Delay(1150); // Observe one real timer interval while explicitly paused.
            Require(!model.IsAutoPlaying && model.CurrentPageIndex == pausedPage, "Explicit pause prevents the next scheduled page change");
            report.Add("PASS current-work loop and explicit pause");

            await SelectAsync(IllustrationViewerAutoPlayMode.Sequential);
            await SelectAsync(IllustrationViewerAutoPlayScope.AllWorks);
            model.CurrentPageIndex = 1;
            await RenderAsync(window); // Let changing navigation labels settle the toolbar overflow before focusing it.
            await ToggleAsync();
            Require(model.IsAutoPlaying, "The all-works sequence starts from the actual toggle");
            await UntilAsync(() => model.CurrentWorkIndex == 1);
            await UntilAsync(() => !model.IsAutoPlaying);
            Require(model.CurrentPageIndex == 1 && model.CurrentWorkIndex == 1, "All-work sequential playback visits the next work and stops at the overall end");
            await SelectAsync(IllustrationViewerAutoPlayMode.Loop);
            await ToggleAsync();
            await UntilAsync(() => model.CurrentWorkIndex == 0);
            await ToggleAsync();
            Require(model.CurrentPageIndex == 0, "All-work loop wraps to the first page of the first work");
            report.Add("PASS all-work sequential and loop across real image source boundaries");

            await SelectAsync(IllustrationViewerAutoPlayScope.CurrentWork);
            await ToggleAsync();
            host.Shell.NavigateTo(new SearchPage());
            await RenderAsync(window);
            var hiddenPage = model.CurrentPageIndex;
            await Task.Delay(1150);
            Require(model.IsAutoPlaying && model.CurrentPageIndex == hiddenPage,
                "Switching to another real tab suspends playback while retaining the requested playing state");
            host.CloseDestinationAndReturn();
            await RenderAsync(window);
            await UntilAsync(() => model.CurrentPageIndex != hiddenPage);
            await ToggleAsync();
            report.Add("PASS real tab switch suspends the timer, and returning resumes the same reader");

            window.Width = 420;
            await RenderAsync(window);
            await SelectAsync(30);
            Require(model.AutoPlayInterval == 30, "The autoplay settings remain accessible from a narrow overflow");
            Save(window, directory, "image-autoplay-narrow");
            model.AutoPlayInterval = 0;
            Require(model.AutoPlayInterval == 1, "The original minimum interval remains enforced");
            model.AutoPlayInterval = 120;
            Require(model.AutoPlayInterval == 60, "The original maximum interval remains enforced");
            var saved = AppInfo.LoadAppSettings(App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>())!;
            Require(saved.BrowsingExperienceSettings.AutoPlay.IllustrationViewerAutoPlayInterval == 60
                && saved.BrowsingExperienceSettings.AutoPlay.IllustrationViewerAutoPlayMode == IllustrationViewerAutoPlayMode.Loop
                && saved.BrowsingExperienceSettings.AutoPlay.IllustrationViewerAutoPlayScope == IllustrationViewerAutoPlayScope.CurrentWork,
                "The original autoplay fields and enum values round-trip through isolated settings YAML");
            report.Add("PASS narrow context menu, interval bounds and unchanged settings serialization");

            var single = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(IllustrationReaderFixture.Sample(996203, 2)), false);
            var singlePage = new IllustrationViewerPage(single);
            host.Shell.NavigateTo(singlePage);
            await UntilAsync(() => single.CurrentImage?.Images.All(image => image.LoadSuccessfully) == true);
            single.AutoPlayInterval = 1;
            single.AutoPlayMode = IllustrationViewerAutoPlayMode.Loop;
            single.AutoPlayScope = IllustrationViewerAutoPlayScope.AllWorks;
            single.CurrentPageIndex = 1;
            single.IsAutoPlaying = true;
            await UntilAsync(() => single.CurrentPageIndex == 0);
            Require(single.CurrentWorkIndex == 0, "All-work loop also wraps a viewer with only one work");
            host.Shell.ClosePage(singlePage);
            var closedPageIndex = single.CurrentPageIndex;
            var changed = 0;
            single.PropertyChanged += (_, _) => changed++;
            await Task.Delay(1150);
            Require(!single.IsAutoPlaying && single.CurrentPageIndex == closedPageIndex && changed == 0,
                "Closing the production tab cancels the timer without late notifications");
            report.Add("PASS single-work all-work loop and production close cancellation");

            using var pendingSource = new SimpleOperableSourceView<IllustrationItemViewModel>([
                IllustrationReaderFixture.Sample(996204, 1), IllustrationReaderFixture.Sample(996205, 1)]);
            var pendingModel = new IllustrationViewerPageViewModel(pendingSource.CloneSourceView(), 0, true);
            var pendingPage = new IllustrationViewerPage(pendingModel);
            host.Shell.NavigateTo(pendingPage);
            var firstDetail = await fixture.NextDetailAsync();
            firstDetail.Complete(IllustrationReaderFixture.Sample(firstDetail.Id, 1));
            await UntilAsync(() => pendingModel.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            pendingModel.IsAutoPlaying = true;
            var nextDetail = await fixture.NextDetailAsync();
            await Task.Delay(1150);
            Require(pendingModel.CurrentWorkIndex == 1 && pendingModel.IsLoading && pendingModel.CurrentImage is null
                && pendingModel.IsAutoPlaying && !nextDetail.Token.IsCancellationRequested,
                "Playback does not skip a work or cancel its pending details when there are temporarily no pages");
            host.Shell.ClosePage(pendingPage);
            Require(nextDetail.Token.IsCancellationRequested && !pendingModel.IsAutoPlaying, "Closing a pending autoplay destination cancels both details and playback");
            nextDetail.Complete();
            await RenderAsync(window);
            Require(pendingModel.CurrentImage is null && !pendingModel.IsAutoPlaying, "Late autoplay details cannot restart a closed viewer");
            report.Add("PASS all-work playback waits for pending details and closes without late resurrection");

            async Task ToggleAsync()
            {
                await RenderAsync(window);
                var playing = model.IsAutoPlaying;
                await ActivateAsync(window, toggle);
                Require(model.IsAutoPlaying != playing, "The settled autoplay toggle changes the playing state exactly once");
            }

            async Task SelectAsync(object value)
            {
                await RenderAsync(window);
                if (toggle.IsInOverflow)
                    await OpenOverflowAsync(window, bar);
                await RenderAsync(window);
                var point = toggle.TranslatePoint(new Rect(toggle.Bounds.Size).Center, window)!.Value;
                window.MouseDown(point, MouseButton.Right);
                window.MouseUp(point, MouseButton.Right);
                await RenderAsync(window);
                var menu = toggle.ContextMenu!;
                Require(menu.IsOpen, "The original autoplay context menu opens");
                var option = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Tag, value));
                Require(option.Focus(NavigationMethod.Tab), "Autoplay options are keyboard reachable");
                Press(window, Key.Enter, PhysicalKey.Enter);
                await RenderAsync(window);
                Require(!menu.IsOpen, "Selecting an autoplay option closes its context menu");
                Require(value switch
                {
                    int interval => model.AutoPlayInterval == interval,
                    IllustrationViewerAutoPlayMode mode => model.AutoPlayMode == mode,
                    IllustrationViewerAutoPlayScope scope => model.AutoPlayScope == scope,
                    _ => false
                }, $"The selected autoplay option is applied: {value}; actual {model.AutoPlayInterval}/{model.AutoPlayMode}/{model.AutoPlayScope}");
                if (bar.IsOpen)
                {
                    Require(toggle.Focus(NavigationMethod.Tab), "Focus returns to the overflow command");
                    Press(window, Key.Escape, PhysicalKey.Escape);
                    await RenderAsync(window);
                    Require(!bar.IsOpen, "Escape closes the command overflow");
                }
            }
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "illustration-autoplay-contracts.txt"), report);
            File.WriteAllLines(Path.Combine(directory, "illustration-autoplay-states.txt"), states);
            window.Close();
        }
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Autoplay uses only fixed offline image requests");
        File.WriteAllLines(Path.Combine(directory, "illustration-autoplay-contracts.txt"), report);
    }
}
