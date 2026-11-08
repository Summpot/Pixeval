using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ColorTextBlock.Avalonia;
using Pixeval.Mako;
using Pixeval.Controls;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class NovelMarkdownValidation
{
    private const string LongCode = "var text = \"A long unwrapped code line ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 中文 🌸\";";
    private static readonly string RichText = "[chapter:长篇小说 Chapter]\n[[rb:漢字 > かんじ]] [[emphasismark:重要 > ・]] [b:Bold] [i:Italic]\n"
        + "    Indented prose stays prose.\n[jump:2]\n[uploadedimage:11]\n[pixivimage:994101-1]\n"
        + "```csharp\n" + LongCode + "\n```\n" + string.Join('\n', Enumerable.Repeat("Long paragraph 中文 🌸\n    Indented continuation", 70))
        + "[newpage][chapter:Second chapter]\n[jump:1]\n[Invalid page](#page999)\n[Local anchor](#ending)\n"
        + string.Join('\n', Enumerable.Repeat("Second page paragraph", 70)) + "\n<div id=\"ending\" />\nEnding anchor";

    internal static async Task RunAsync(string directory)
    {
        using var fixture = new NovelReaderFixture();
        App.AppViewModel.MakoClient.SetRefreshToken("fixture-novel-markdown");
        await FixtureAuthentication.IdentifyAsync(App.AppViewModel.MakoClient);
        var illustration = Illustration.CreateDefault() with
        {
            Id = 994101,
            Title = "Embedded illustration",
            Visible = true,
            Width = 256,
            Height = 256,
            PageCount = 1,
            User = User.CreateDefault() with { Id = 900001, Name = "Fixture author", ProfileImageUrls = new() { Medium = NovelReaderFixture.ImageUri } },
            ImageUrls = new() { Medium = NovelReaderFixture.ImageUri, Large = NovelReaderFixture.ImageUri, SquareMedium = NovelReaderFixture.ImageUri },
            MetaSinglePage = new() { OriginalImageUrl = NovelReaderFixture.ImageUri }
        };
        fixture.Http.ArtworkDetailResponseFactory = (_, _) => Task.FromResult(NativeFixtureData.Serialize(new { illust = illustration }));
        List<string> report = [];
        long id = 994001;
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 1100, 360 })
            {
                var model = UiTestSession.Create<NovelViewerPageViewModel>(NovelReaderFixture.Sample(id++), false);
                var page = new NovelViewerPage(model);
                var host = new PageTestHost { Content = page };
                var window = new Window { Width = width, Height = 850, RequestedThemeVariant = theme, Content = host.Shell };
                var key = theme.Key + "-" + width;
                try
                {
                    Pixeval.Common.MaterialAnimation.Initialize(window);
                    window.Show();
                    (await fixture.NextAsync()).Complete(RichText, withImages: true);
                    await UntilAsync(() => !model.IsLoading);
                    Require(model.LoadErrorMessage is null && model.PageCount == 2, "Rich novel parses through the production content loader");
                    await RenderAsync(window);
                    var markdown = page.FindControl<NovelMarkdownBox>("ReaderMarkdown")!;
                    var inlines = Inlines(markdown).ToArray();
                    var ruby = inlines.OfType<RubyInline>().ToArray();
                    Require(ruby.Any(item => item.Body == "漢字" && item.Annotation == "かんじ" && !item.Below)
                        && ruby.Count(item => item.Below && item.Annotation == "・") == 2,
                        "Native novel ruby and below-emphasis marks reach rendered inline objects");
                    Require(inlines.Any(item => item.FontWeight == FontWeight.Bold)
                        && inlines.Any(item => item.FontStyle == FontStyle.Italic), "Bold and italic novel tokens keep their typography");
                    var pictures = inlines.OfType<CImage>().ToArray();
                    Require(pictures.Length == 2, "Uploaded and pixiv image syntax both produce actual inline images");
                    await UntilAsync(() => pictures.All(item => item.Image is not null));
                    Require(pictures.All(item => item.Image!.Size.Width > 0 && item.Image.Size.Height > 0), "Both inline images decode the offline asset");
                    var scrolls = markdown.GetVisualDescendants().OfType<ScrollViewer>().ToArray();
                    var document = scrolls.First();
                    var code = scrolls.Single(viewer => viewer.Classes.Contains("CodeBlock"));
                    Require(markdown.GetVisualDescendants().OfType<CTextBlock>().Any(block => block.Text.Contains("Indented prose stays prose.")),
                        "Mako.Novel paragraph indentation is not misclassified as a code block");
                    foreach (var scroll in scrolls) scroll.IsScrollInertiaEnabled = false;
                    Save(window, directory, "novel-markdown-top-" + key);

                    var copy = markdown.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("CopyButton"));
                    copy.BringIntoView();
                    await RenderAsync(window);
                    Require(copy.Focus(), "The embedded code copy action is keyboard reachable");
                    Press(window, Key.Enter, PhysicalKey.Enter);
                    await RenderAsync(window);
                    Require((await window.Clipboard!.TryGetTextAsync())!.Contains(LongCode), "Code copy uses the headless clipboard and preserves Unicode");
                    code.BringIntoView();
                    await RenderAsync(window);
                    Require(code.Extent.Width > code.Viewport.Width, "Long code scrolls horizontally inside the reading width");
                    var point = code.TranslatePoint(new Point(20, 10), window)!.Value;
                    var beforeY = document.Offset.Y;
                    window.MouseWheel(point, new Vector(-2, 0));
                    await RenderAsync(window);
                    Require(code.Offset.X > 0 && Math.Abs(document.Offset.Y - beforeY) < 1, "Horizontal code wheel stays inside the code block");
                    code.Offset = new Vector(code.Extent.Width - code.Viewport.Width, 0);
                    await RenderAsync(window);
                    window.MouseWheel(point, new Vector(0, -2));
                    await RenderAsync(window);
                    Require(document.Offset.Y > beforeY, "At the code edge vertical wheel chains to the novel");

                    await ClickLinkAsync(window, markdown, "page1");
                    Require(model.CurrentPageIndex == 1 && markdown.Markdown == model.CurrentMarkdown, "A rendered page link updates the reader page and Markdown together");
                    Require(markdown.ScrollValue.Y < 1, "Changing novel pages starts at the new page's top");
                    await ClickLinkAsync(window, markdown, "#page999");
                    Require(model.CurrentPageIndex == 1 && host.Navigations == 0, "Out-of-range page links are handled without navigation");
                    await ClickLinkAsync(window, markdown, "#ending");
                    Require(markdown.ScrollValue.Y > 0 && model.CurrentPageIndex == 1, "An ordinary in-page anchor scrolls the current page without changing works");
                    var anchorScroll = markdown.ScrollValue.Y;
                    window.RequestedThemeVariant = theme == ThemeVariant.Light ? ThemeVariant.Dark : ThemeVariant.Light;
                    await RenderAsync(window);
                    Require(Math.Abs(markdown.ScrollValue.Y - anchorScroll) < 1, "Changing the app theme preserves the reading position");
                    window.RequestedThemeVariant = theme;
                    await ClickLinkAsync(window, markdown, "page0");
                    Require(model.CurrentPageIndex == 0, "The return page link uses the same reader navigation path");
                    var departureScroll = 0d;
                    markdown.HyperlinkClicked += (_, e) =>
                    {
                        if (e.Url == "pixeval://illust/994101") departureScroll = markdown.ScrollValue.Y;
                    };
                    await ClickLinkAsync(window, markdown, "pixeval://illust/994101");
                    await UntilAsync(() => host.Navigations == 1);
                    Require(host.Shell.SelectedPage is IllustrationViewerPage && host.LastPageType == typeof(IllustrationViewerPage),
                        "Embedded illustration links leave the actual viewer destination selected and alive");
                    await RenderAsync(window);
                    host.CloseDestinationAndReturn();
                    await RenderAsync(window);
                    Require(ReferenceEquals(host.Shell.SelectedPage, page) && model.CurrentPageIndex == 0 && !model.IsLoading,
                        "Closing the actual image destination resumes the same novel page");
                    Require(Math.Abs(markdown.ScrollValue.Y - departureScroll) < 1, "Temporary navigation retains the novel scroll position");
                    Save(window, directory, "novel-markdown-return-" + key);
                    host.Shell.ClosePage(page);
                    report.Add($"PASS {key}: native ruby/emphasis, bold/italic, indented prose, uploaded/pixiv images, code copy and nested wheel, page/anchor links, real illustration navigation and return");
                }
                finally { window.Close(); }
            }
        File.WriteAllLines(Path.Combine(directory, "novel-markdown-contracts.txt"), report);
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Rich novel content only uses the installed offline boundaries");
    }

    private static IEnumerable<CInline> Inlines(Control root) => root.GetVisualDescendants().OfType<CTextBlock>()
        .SelectMany(block => block.Content).SelectMany(Flatten);

    private static IEnumerable<CInline> Flatten(CInline inline)
    {
        yield return inline;
        if (inline is CSpan span)
            foreach (var child in span.Content.SelectMany(Flatten)) yield return child;
    }

    private static async Task ClickLinkAsync(Window window, NovelMarkdownBox markdown, string target)
    {
        var block = markdown.GetVisualDescendants().OfType<CTextBlock>()
            .Single(candidate => candidate.Content.SelectMany(Flatten).OfType<CHyperlink>().Any(link => link.CommandParameter == target));
        block.BringIntoView();
        await RenderAsync(window);
        Point point = default;
        await UntilAsync(() =>
        {
            point = block.TranslatePoint(new Point(8, block.Bounds.Height / 2), window)!.Value;
            return window.InputHitTest(point) is Visual hit && (ReferenceEquals(hit, block) || hit.GetVisualAncestors().Contains(block));
        });
        // BringIntoView animates the production smooth scroller. Wait for its
        // geometry, so a pointer sequence targets the same glyph throughout.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            await RenderAsync(window);
            var current = block.TranslatePoint(new Point(8, block.Bounds.Height / 2), window)!.Value;
            if (Math.Abs(current.Y - point.Y) < 0.1) { point = current; break; }
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Mako.Novel link scroll did not settle");
            point = current;
        }
        string? clicked = null;
        var clicks = 0;
        void OnClicked(object? sender, MarkdownHyperlinkClickedEventArgs e) { clicked = e.Url; clicks++; }
        markdown.HyperlinkClicked += OnClicked;
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        await RenderAsync(window);
        markdown.HyperlinkClicked -= OnClicked;
        Require(clicked == target && clicks == 1, $"Pointer activation dispatches the rendered link once: expected={target}, actual={clicked}, count={clicks}, point={point}, block={block.Bounds}");
    }
}
