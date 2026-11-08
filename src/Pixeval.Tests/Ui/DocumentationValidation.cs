using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ColorTextBlock.Avalonia;
using Material3Expressive.Components;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.Views.Settings;

namespace Pixeval.Tests.Ui;

internal static class DocumentationValidation
{
    internal static async Task RunAsync(string directory)
    {
        // Populate only the isolated AppInfo cache before AboutPage loads. The real
        // supporter loader takes its cached branch and has no network work to do.
        _ = Directory.CreateDirectory(Supporter.BasePath);
        File.WriteAllText(Path.Combine(Supporter.BasePath, "github-supporters.json"),
            "[" + string.Join(',', Enumerable.Repeat("{}", Supporter.Supporters.Length)) + "]");
        using (var source = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var bytes = new MemoryStream())
        {
            source.CopyTo(bytes);
            foreach (var supporter in Supporter.Supporters)
            {
                supporter.ProfileUri = new Uri("https://github.com/" + supporter.Name);
                File.WriteAllBytes(Path.Combine(Supporter.BasePath, supporter.Name + ".png"), bytes.ToArray());
            }
        }

        List<string> report = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 1000, 360 })
            {
                await VerifyAboutAsync(theme, width, directory);
                await VerifyHelpAsync(theme, width, directory);
                report.Add($"PASS {theme.Key}/{width}: supporter keyboard activation and URI, all dependency licenses and links, GPL, help expand/collapse, MCP example copy and disabled-server refresh");
            }
        report.Add("Headless clipboard is in-memory; URI launcher is a no-op. Native browser dispatch, system clipboard, IME and DPI are not claimed.");
        File.WriteAllLines(Path.Combine(directory, "documentation-contracts.txt"), report);
        Console.WriteLine($"Documentation contracts passed: {report.Count - 1} theme/width scenarios.");
    }

    private static async Task VerifyAboutAsync(ThemeVariant theme, int width, string directory)
    {
        var page = UiTestSession.Create<AboutPage>();
        var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = new PageTestHost { Content = page }.Shell };
        try
        {
            Pixeval.Common.MaterialAnimation.Initialize(window);
            window.Show();
            await RenderAsync(window);
            var cards = page.GetVisualDescendants().OfType<Card>().Where(card => card.DataContext is Supporter).ToArray();
            Require(cards.Length == Supporter.Supporters.Length, "All existing supporters must be present.");
            foreach (var card in cards)
            {
                var supporter = (Supporter) card.DataContext!;
                Require(card.Tag is Uri uri && uri.AbsoluteUri == "https://github.com/" + supporter.Name, "Supporter URI binding changed.");
                Require(AutomationProperties.GetName(card) == supporter.Nickname, "Supporter card has no readable name.");
                Require(card.Bounds.Width <= width - 40, "Supporter card exceeds the page viewport.");
            }
            var first = cards[0];
            var clicks = 0;
            first.Click += (_, _) => clicks++;
            Require(first.Focus(NavigationMethod.Tab), "Supporters must be reachable by keyboard.");
            PressKey(window, Key.Enter, PhysicalKey.Enter, "\r");
            Require(clicks is 1, "Enter must activate the supporter once.");
            PressKey(window, Key.Space, PhysicalKey.Space, " ");
            Require(clicks is 2, "Space must activate the supporter once.");
            await RenderAsync(window);
            Save(window, $"about-{theme.Key}-{width}-top.png", directory);

            var dependencies = page.GetVisualDescendants().OfType<Expander>().Where(expander => expander.DataContext is DependencyViewModel).ToArray();
            Require(dependencies.Length == DependencyViewModel.DependencyViewModels.Count(), "Dependency license entries were lost.");
            foreach (var expander in dependencies)
            {
                var model = (DependencyViewModel) expander.DataContext!;
                var header = expander.GetVisualDescendants().OfType<ToggleButton>().Single(button => button.Name is "ExpanderHeader");
                header.BringIntoView();
                await RenderAsync(window);
                Require(header.Focus(), "A dependency header must accept keyboard focus.");
                PressKey(window, Key.Space, PhysicalKey.Space, " ");
                await RenderAsync(window);
                Require(expander.IsExpanded, "Space did not reveal the dependency license.");
                var markdown = expander.GetVisualDescendants().OfType<MarkdownBox>().Single();
                Require(markdown.Markdown == model.License, "A dependency's full license changed.");
                var link = page.GetVisualDescendants().OfType<IconButton>().Single(button => Equals(button.Tag, model.Url));
                Require(link.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(link)), "Dependency external link is missing.");
                var linkClicks = 0;
                link.Click += (_, _) => linkClicks++;
                Require(link.Focus(), "The external link must be focusable independently of expansion.");
                PressKey(window, Key.Enter, PhysicalKey.Enter, "\r");
                Require(linkClicks is 1 && expander.IsExpanded, "External link activation must not collapse the license.");
                expander.IsExpanded = false;
                await RenderAsync(window);
            }
            var license = page.FindControl<MarkdownBox>("LicenseTextBlock")!;
            Require(license.Markdown?.Contains("GNU GENERAL PUBLIC LICENSE", StringComparison.Ordinal) == true, "The bundled GPL must remain readable.");
            license.BringIntoView();
            await RenderAsync(window);
            Save(window, $"about-{theme.Key}-{width}-license.png", directory);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static async Task VerifyHelpAsync(ThemeVariant theme, int width, string directory)
    {
        var page = new HelpPage();
        var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = new PageTestHost { Content = page }.Shell };
        try
        {
            Pixeval.Common.MaterialAnimation.Initialize(window);
            window.Show();
            await RenderAsync(window);
            var expanders = page.GetVisualDescendants().OfType<Expander>().Where(expander => expander.Classes.Contains("documentation-section")).ToArray();
            Require(expanders.Length is 5, "All four general help sections and MCP help must remain available.");
            Save(window, $"help-{theme.Key}-{width}-collapsed.png", directory);
            var copied = 0;
            foreach (var expander in expanders)
            {
                var header = expander.GetVisualDescendants().OfType<ToggleButton>().Single(button => button.Name is "ExpanderHeader");
                header.BringIntoView();
                await RenderAsync(window);
                Require(header.Focus(), "Help headers must accept keyboard focus.");
                PressKey(window, Key.Enter, PhysicalKey.Enter, "\r");
                await RenderAsync(window);
                Require(expander.IsExpanded, "Enter did not expand help.");
                var documents = expander.GetVisualDescendants().OfType<MarkdownBox>().ToArray();
                Require(documents.Length > 0 && documents.All(document => !string.IsNullOrWhiteSpace(document.Markdown)), "Localized help content is empty.");
                foreach (var copy in expander.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("CopyButton")))
                {
                    copy.BringIntoView();
                    await RenderAsync(window);
                    Require(copy.Bounds.Width >= 48 && copy.Bounds.Height >= 48, "A help copy action is too small.");
                    Require(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(copy)), "A help copy action lacks an accessible name.");
                    var root = copy.GetVisualAncestors().OfType<Grid>().First(grid => grid.Classes.Contains("PixevalCodeBlockRoot"));
                    var text = root.GetVisualDescendants().OfType<CTextBlock>().Single(block => block.Classes.Contains("CodeBlock")).Text;
                    Require(copy.Focus(), "Code copy must be keyboard accessible.");
                    PressKey(window, Key.Space, PhysicalKey.Space, " ");
                    await RenderAsync(window);
                    Require(await window.Clipboard!.TryGetTextAsync() == text, "The help copy action changed its code text.");
                    copied++;
                }
                header.BringIntoView();
                await RenderAsync(window);
                Save(window, $"help-{theme.Key}-{width}-section-{Array.IndexOf(expanders, expander)}.png", directory);
                Require(header.Focus(), "Focus could not return to the help header.");
                PressKey(window, Key.Space, PhysicalKey.Space, " ");
                await RenderAsync(window);
                Require(!expander.IsExpanded, "Space did not collapse help.");
                await UntilAsync(window, () => !expander.GetVisualDescendants().OfType<Button>().Any(button => button.Classes.Contains("CopyButton") && button.IsEffectivelyVisible));
            }
            Require(copied > 0, "The real localized help must exercise code-copy examples.");
            var section = page.GetVisualDescendants().OfType<McpHelpSection>().Single();
            var tools = section.FindControl<Expander>("ToolsExpander")!;
            tools.IsExpanded = true;
            var refresh = section.FindControl<IconButton>("RefreshMcpToolsButton")!;
            refresh.BringIntoView();
            await RenderAsync(window);
            Require(refresh.IsEnabled && refresh.Focus(), "MCP refresh must remain enabled and focusable while the server is disabled.");
            var refreshes = 0;
            refresh.Click += (_, _) => refreshes++;
            PressKey(window, Key.Enter, PhysicalKey.Enter, "\r");
            await RenderAsync(window);
            Require(refreshes is 1 && tools.IsExpanded, "Refreshing the tools must not collapse their section.");
            Require(section.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == Strings.HelpPageMcpToolsStatusDisabled()),
                "Disabled-server refresh must preserve the localized disabled status.");
            Save(window, $"help-{theme.Key}-{width}-mcp-tools.png", directory);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static void PressKey(Window window, Key key, PhysicalKey physical, string symbol)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, symbol);
        window.KeyRelease(key, RawInputModifiers.None, physical, symbol);
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(350);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static async Task UntilAsync(Window window, Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(3))
        {
            await Task.Delay(16);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        }
        Require(condition(), "Collapsed help left interactive code buttons visible after its exit animation.");
    }

    private static void Save(Window window, string name, string directory)
    {
        using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No documentation frame.");
        bitmap.Save(Path.Combine(directory, name), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
