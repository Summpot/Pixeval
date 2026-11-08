using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Mako.Model;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class IllustrationAnimationValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new IllustrationReaderFixture();
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-animation");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        const string zipUri = "https://i.pximg.net/m3e/animation-1920x1080.zip";
        var png = fixture.Http.ImageResponses[IllustrationReaderFixture.ImageUri];
        using (var buffer = new MemoryStream())
        {
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
                for (var i = 0; i < 2; i++)
                {
                    using var frame = zip.CreateEntry($"{i:D6}.png").Open();
                    frame.Write(png);
                }
            fixture.Http.ImageResponses[zipUri] = buffer.ToArray();
            fixture.Http.ImageResponses[zipUri.Replace("1920x1080", "600x600")] = buffer.ToArray();
        }
        fixture.Http.ImageResponses["https://i.pximg.net/m3e/animation-ugoira0.png"] = png;
        fixture.Http.ImageResponses["https://i.pximg.net/m3e/animation-ugoira1.png"] = png;
        fixture.Http.AnimationResponseFactory = (_, _) => Task.FromResult("""
            {"ugoira_metadata":{"zip_urls":{"medium":"https://i.pximg.net/m3e/animation-600x600.zip"},
            "frames":[{"file":"000000.png","delay":100},{"file":"000001.png","delay":200}]}}
            """);
        var continuous = System.Globalization.CultureInfo.CurrentUICulture.Name is "en-US" or "ru-RU";
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = continuous ? BrowseMode.Continuous : BrowseMode.Swipe;
        var work = IllustrationReaderFixture.Sample(996440, 1) with
        {
            Type = IllustrationType.Ugoira,
            MetaSinglePage = new() { OriginalImageUrl = "https://i.pximg.net/m3e/animation-ugoira0.png" }
        };
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(work), false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 700, Height = 760, Content = host.Shell };
        List<string> report = [];
        try
        {
            window.Show();
            await UntilAsync(() => model.CurrentImage?.CurrentPage is { LoadSuccessfully: true } or { LoadErrorMessage: not null });
            await RenderAsync(window);
            var image = model.CurrentImage!.CurrentPage!;
            Require(image.LoadSuccessfully, "The animation fixture must load: " + image.LoadErrorMessage);
            Require(image.IsPicGif && image.DisplaySource.Frames.Count == 2
                && image.DisplaySource.Delays.SequenceEqual(new[] { 100, 200 }), "The real ugoira ZIP retains both decoded frames and frame intervals");
            Require(!image.CopyCommand.CanExecute(null) && !image.SaveAsCommand.CanExecute(null)
                && !image.TransformExtensionCommand.CanExecute(null), "Single-frame actions are unavailable for animation");
            await ToggleMenuAsync();
            Require(!image.IsPlaying && !Animated().IsPlaying, "The actual animation menu pauses the animated image");
            report.Add("PASS ugoira ZIP frames/intervals, animation capability gates and actual menu pause");
            window.Width = 360;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            await RenderAsync(window);
            Save(window, directory, "animation-paused-dark-narrow");
            var wrapper = page.FindControl<ImageViewerWrapper>("I")!;
            Require(wrapper.GetVisualDescendants().OfType<SmoothScroll.Avalonia.Controls.ScrollView>().First().Focus(NavigationMethod.Tab),
                "Animation viewport accepts keyboard focus");
            Press(window, Key.Space, PhysicalKey.Space);
            Require(image.IsPlaying && Animated().IsPlaying, "Space resumes the same animation without changing pages");
            Require(model.CurrentPageIndex == 0, "Animation Space does not navigate image pages");
            if (!continuous)
            {
                var play = page.GetVisualDescendants().OfType<CommandBarToggleButton>().Single(button => ReferenceEquals(button.Command, image.PlayPauseCommand));
                Require(play.Label == I18NManager.GetResource(MiscResources.Pause), "The playing animation exposes a localized pause action");
                await ActivateAsync(window, play);
                Require(!image.IsPlaying && play.Label == I18NManager.GetResource(MiscResources.Play), "The actual floating toolbar pauses and updates its action label");
                await ActivateAsync(window, play);
                Require(image.IsPlaying, "The same floating toolbar resumes animation");
            }
            await ToggleMenuAsync();
            Require(!image.IsPlaying, "The animation can be paused again after keyboard resume");
            host.Shell.NavigateTo(new Pixeval.Views.Search.SearchPage());
            await RenderAsync(window);
            host.CloseDestinationAndReturn();
            await RenderAsync(window);
            Require(!image.IsPlaying && !Animated().IsPlaying, "Temporary navigation retains the user's paused animation");
            await ToggleMenuAsync();
            Require(image.IsPlaying && Animated().IsPlaying, "Remounted animation resumes through the same menu");
            var source = image.OriginalSource!;
            host.Shell.ClosePage(page);
            Require(!image.PlayPauseCommand.CanExecute(null) && model.CurrentImage is null && source.FrameCount == 0,
                "Real close disables animation commands and releases decoded frames");
            report.Add("PASS dark/narrow keyboard resume, temporary shell navigation and real close resource release");

            AnimatedImage Animated() => page.GetVisualDescendants().OfType<SingleImageViewer>()
                .Single(v => ReferenceEquals(v.DataContext, image)).GetVisualDescendants().OfType<AnimatedImage>().Single();
            async Task ToggleMenuAsync()
            {
                var viewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(v => ReferenceEquals(v.DataContext, image));
                var area = page.FindControl<ImageViewerWrapper>("I")!;
                var position = new Rect(viewer.TranslatePoint(default, window)!.Value, viewer.Bounds.Size)
                    .Intersect(new Rect(area.TranslatePoint(default, window)!.Value, area.Bounds.Size)).Center;
                window.MouseDown(position, MouseButton.Right);
                window.MouseUp(position, MouseButton.Right);
                await RenderAsync(window);
                var toggle = viewer.ContextMenu!.Items.OfType<MenuItem>().Single(m => ReferenceEquals(m.Command, image.PlayPauseCommand));
                Require(toggle.IsEffectivelyVisible && toggle.IsEffectivelyEnabled && toggle.Focus(NavigationMethod.Tab), "Animation menu is keyboard reachable");
                Require(toggle.Header?.ToString() == I18NManager.GetResource(image.IsPlaying ? MiscResources.Pause : MiscResources.Play),
                    "Animation menu text describes the next action");
                Press(window, Key.Enter, PhysicalKey.Enter);
                await RenderAsync(window);
            }
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "illustration-animation-contracts.txt"), report);
            window.Close();
        }
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Animation requests stay offline");
    }
}
