using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class IllustrationInfoValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new IllustrationReaderFixture();
        var logger = App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>();
        ConcurrentQueue<string> layoutErrors = [];
        logger.Logging += ObserveErrors;
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-image-info");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = BrowseMode.Swipe;
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(IllustrationReaderFixture.Sample(996430, 1)), false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 1000, Height = 800, RequestedThemeVariant = ThemeVariant.Dark, Content = host.Shell };
        List<string> report = [];
        try
        {
            window.Show();
            await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            await RenderAsync(window);
            var info = page.GetVisualDescendants().OfType<IllustrationViewerInfoPane>().Single();
            var expand = info.GetVisualDescendants().OfType<Button>().Single(button =>
                Equals(ToolTip.GetTip(button), I18NManager.GetResource(EntryViewerPageResources.InfoAndComments)));
            Require(expand.Bounds.Width >= 48 && expand.Bounds.Height >= 48
                && Avalonia.Automation.AutomationProperties.GetName(expand) == I18NManager.GetResource(EntryViewerPageResources.InfoAndComments),
                "The information toggle has a localized accessible name and a 48 DIP target");
            if (!info.IsDocked)
                await ActivateAsync(window, expand);
            var floating = page.FindControl<FloatingPaneView>("EntryViewerFloatingPaneView")!;
            await UntilAsync(() => Math.Abs(floating.DockProgress - 1) < 0.001);
            var tabs = info.FindControl<TabbedPage>("TabbedPageSection")!;
            var pages = model.PanePages;
            Require(tabs.Pages?.Count() == 3 && tabs.SelectedIndex == 0, "The docked pane retains information/comments/related pages");
            foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
                foreach (var width in new[] { 1000, 360 })
                {
                    window.RequestedThemeVariant = theme;
                    window.Width = width;
                    await RenderAsync(window);
                    var control = tabs.GetVisualDescendants().OfType<TabControl>().Single(candidate => candidate.Name == "PART_TabControl");
                    var active = (TabItem) control.ContainerFromIndex(tabs.SelectedIndex)!;
                    Require(active is not null, "The information tab template realizes its headers");
                    Require(active!.Focus(NavigationMethod.Tab), "Information tab accepts keyboard focus");
                    for (var index = tabs.SelectedIndex; index > 0; index--)
                        Press(window, Key.Left, PhysicalKey.ArrowLeft);
                    await RenderAsync(window);
                    Require(tabs.SelectedIndex == 0, "Left returns to the information page through the existing tab control");
                    Save(window, directory, $"information-{theme.Key}-{width}");
                    var background = page.FindControl<Border>("FloatingPaneBorder")!.Background;
                    foreach (var tab in control.GetRealizedContainers().OfType<TabItem>())
                    {
                        var label = tab.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == tab.Header?.ToString());
                        Require(Contrast(label.Foreground, background) >= 4.5,
                            $"Information tab text remains readable in {theme.Key}: {label.Text}, foreground={label.Foreground}, surface={background}");
                    }
                    Press(window, Key.Right, PhysicalKey.ArrowRight);
                    await RenderAsync(window);
                    Require(tabs.SelectedIndex == 1 && ReferenceEquals(tabs.SelectedPage, pages[1]) && pages[1].IsLoaded,
                        "Right selects and loads the real comments page");
                    Press(window, Key.Right, PhysicalKey.ArrowRight);
                    await RenderAsync(window);
                    Require(tabs.SelectedIndex == 2 && ReferenceEquals(tabs.SelectedPage, pages[2]) && pages[2].IsLoaded,
                        "Right selects and loads the real related works page");
                    Require(model.CurrentPageIndex == 0, "Handled information tab arrows do not navigate image pages");
                    var selected = (TabItem) control.ContainerFromIndex(2)!;
                    var origin = selected.TranslatePoint(default, window)!.Value;
                    Require(origin.X >= 0 && origin.X < window.ClientSize.Width, "The selected tab scrolls into the narrow viewport");
                    Save(window, directory, $"related-{theme.Key}-{width}");
                    await ActivateAsync(window, expand);
                    await UntilAsync(() => floating.DockProgress < 0.001);
                    await ActivateAsync(window, expand);
                    await UntilAsync(() => Math.Abs(floating.DockProgress - 1) < 0.001);
                    Require(tabs.SelectedIndex == 2 && ReferenceEquals(model.PanePages, pages), "Collapse/expand retains the selected page and page instances");
                    report.Add($"PASS {theme.Key}/{width}: tab contrast, real keyboard page selection, no image key leakage, collapse/expand state");
                }
            host.Shell.NavigateTo(new Pixeval.Views.Search.SearchPage());
            await RenderAsync(window);
            host.CloseDestinationAndReturn();
            await RenderAsync(window);
            Require(tabs.SelectedIndex == 2 && ReferenceEquals(model.PanePages, pages), "Temporary shell navigation retains information tab state");
            host.Shell.ClosePage(page);
            Require(model.CurrentImage is null && pages.All(inner => !inner.IsLoaded), "Closing the real viewer unloads all information pages");
            await RenderAsync(window);
            Require(layoutErrors.IsEmpty, "Information templates must not raise dispatcher errors: " + string.Join("; ", layoutErrors));
            report.Add("PASS temporary shell navigation and real close of the information pages");
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "illustration-information-contracts.txt"), report);
            window.Close();
            logger.Logging -= ObserveErrors;
        }
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Information page requests stay offline");

        bool ObserveErrors(FileLogger _, LogModel log)
        {
            if (log.Message == "UnhandledException") layoutErrors.Enqueue(log.Exception?.Message ?? log.Message);
            return false;
        }
    }

    private static double Contrast(IBrush? foreground, IBrush? background)
    {
        Require(foreground is ISolidColorBrush && background is ISolidColorBrush, "Information colors are resolved solid brushes");
        var a = Luminance(((ISolidColorBrush) foreground!).Color);
        var b = Luminance(((ISolidColorBrush) background!).Color);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        static double Linear(byte channel) => channel / 255d is var value && value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
