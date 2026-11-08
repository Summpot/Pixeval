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
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Mako;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.Config;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;

namespace Pixeval.Tests.Ui;

internal static class ViewerToolbarValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new OfflineSearchFixture(App.AppViewModel.MakoClient);
        const string imageUri = "https://i.pximg.net/m3e/viewer-toolbar.png";
        using (var source = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png")))
        using (var bytes = new MemoryStream())
        {
            source.CopyTo(bytes);
            fixture.ImageResponses[imageUri] = bytes.ToArray();
        }
        var entry = Illustration.CreateDefault() with
        {
            Id = 900101,
            Title = "离线查看器命令栏 / Offline viewer toolbar",
            IllustType = IllustContentType.Illustration,
            Width = 16,
            Height = 16,
            PageCount = 1,
            Visible = true,
            SanityLevel = 2,
            Series = null,
            ImageUrls = new() { SquareMedium = imageUri, Medium = imageUri, Large = imageUri },
            MetaSinglePage = new() { OriginalImageUrl = imageUri }
        };
        List<string> report = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var mode in new[] { BrowseMode.Swipe, BrowseMode.Continuous })
            {
                App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = mode;
                var item = (entry);
                using var model = UiTestSession.Create<IllustrationViewerPageViewModel>(item, false);
                var image = model.CurrentImage?.CurrentPage
                    ?? throw new InvalidOperationException("The fixed illustration did not create an image model.");
                await image.LoadOriginalImageAsync();
                Require(image.LoadSuccessfully && image.HasDisplayImage, "The fixed image must load through the real image pipeline.");
                var page = new IllustrationViewerPage(model);
                var window = new Window { Width = 1000, Height = 720, RequestedThemeVariant = theme, Content = page };
                try
                {
                    Pixeval.Common.MaterialAnimation.Initialize(window);
                    window.Show();
                    await RenderAsync(window);
                    var bar = page.FindControl<CommandBar>("ZoomPane")!;
                    var pane = page.FindControl<Border>("FloatingToolbarPane")!;
                    window.MouseMove(pane.TranslatePoint(new Rect(pane.Bounds.Size).Center, window)!.Value);
                    await RenderAsync(window);
                    Require(pane.IsEffectivelyVisible, "The real viewer must show its floating toolbar.");
                    Require(bar.PrimaryCommands.OfType<Control>().Any(control => control.IsEffectivelyVisible), "No toolbar commands were visible.");
                    Require(bar.PrimaryCommands.OfType<CommandBarElementContainer>()
                        .Single(control => control.IsVisible).GetVisualDescendants().OfType<TextBlock>().Single().Text?.Contains('%') == true,
                        "The active viewer's zoom percentage must be shown.");
                    Save(window, $"viewer-{mode}-{theme.Key}-wide.png", directory);
                    window.MouseMove(new Point(2, 2));
                    var keyboardTarget = bar.PrimaryCommands.OfType<Button>().First(button => button.IsVisible && button.IsEffectivelyEnabled);
                    Require(keyboardTarget.Focus(), "A visible toolbar action must accept keyboard focus.");
                    await Task.Delay(600);
                    await RenderAsync(window);
                    Require(pane.Opacity > 0.99, $"Keyboard focus must keep the toolbar visible; opacity={pane.Opacity}.");

                    if (mode is BrowseMode.Swipe)
                    {
                        var rotate = FindButton(bar, "EntryViewerPage-RotateClockwise");
                        var mirror = bar.PrimaryCommands.OfType<CommandBarToggleButton>()
                            .Single(button => button.Label == Strings.EntryViewerPageMirror());
                        await ActivateAsync(window, bar, rotate);
                        Require(image.RotationDegree is 90, "Rotate must execute exactly once.");
                        await ActivateAsync(window, bar, mirror);
                        Require(image.IsMirrored && mirror.IsChecked == true, "Mirror must update the existing image model once.");
                        image.IsMirrored = false;
                        Require(mirror.IsChecked == false, "Model changes must update the same toggle.");
                    }

                    window.Width = 420;
                    await RenderAsync(window);
                    Require(pane.Bounds.Width <= window.ClientSize.Width - pane.Margin.Left - pane.Margin.Right + 1,
                        $"The floating pane exceeds its viewport: {pane.Bounds.Width} in {window.ClientSize.Width}.");
                    Require(bar.Bounds.Width <= pane.Bounds.Width, "The toolbar must stay inside its floating pane.");
                    if (mode is BrowseMode.Swipe)
                    {
                        Require(bar.IsOverflowButtonVisible, "The narrow swipe toolbar must expose overflow commands.");
                        bar.IsOpen = true;
                        await RenderAsync(window);
                        var more = bar.GetVisualDescendants().OfType<CommandBarButton>()
                            .Single(button => button.Name is "PART_OverflowButton");
                        Require(AutomationProperties.GetName(more) == Strings.MiscMoreCommands(),
                            "The overflow button must have a localized accessible name.");
                        Save(window, $"viewer-{mode}-{theme.Key}-overflow.png", directory);
                        var reset = FindButton(bar, "EntryViewerPage-ZoomToOriginal");
                        await ActivateAsync(window, bar, reset);
                        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
                        while (Math.Abs(image.ZoomFactor - 1) >= 0.0001 && DateTime.UtcNow < deadline)
                            await RenderAsync(window);
                        Require(Math.Abs(image.ZoomFactor - 1) < 0.0001,
                            $"The overflow zoom command must reach the existing viewer; zoom={image.ZoomFactor}.");
                    }
                    if (bar.IsOpen)
                    {
                        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                        window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                        await RenderAsync(window);
                        Require(!bar.IsOpen, "Escape must close the overflow after repeated viewer commands.");
                    }
                    Save(window, $"viewer-{mode}-{theme.Key}-narrow.png", directory);
                    window.Width = 1000;
                    await RenderAsync(window);
                    Require(!bar.IsOpen, "Resizing must not reopen a dismissed menu.");
                    var autoPlay = bar.PrimaryCommands.OfType<CommandBarToggleButton>()
                        .Single(button => button.Label == Strings.EntryViewerPageAutoPlay());
                    await ShowCommandAsync(window, bar, autoPlay);
                    var autoPlayPoint = autoPlay.TranslatePoint(new Rect(autoPlay.Bounds.Size).Center, window)!.Value;
                    window.MouseMove(autoPlayPoint);
                    window.MouseDown(autoPlayPoint, MouseButton.Right);
                    window.MouseUp(autoPlayPoint, MouseButton.Right);
                    await RenderAsync(window);
                    var menu = autoPlay.ContextMenu!;
                    Require(menu.IsOpen, "The autoplay context menu must remain reachable by right click.");
                    var interval = menu.Items.OfType<MenuItem>().Single(item => item.Tag is 30);
                    Require(interval.Focus(), "The autoplay interval must accept focus.");
                    window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                    window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                    await RenderAsync(window);
                    Require(model.AutoPlayInterval is 30, "The original context-menu handler must update autoplay settings.");
                    await ActivateAsync(window, bar, autoPlay);
                    Require(model.IsAutoPlaying, "Autoplay must preserve its two-way model binding.");
                    model.IsAutoPlaying = false;
                    Require(autoPlay.IsChecked == false, "Stopping autoplay must clear the existing toggle.");
                    report.Add($"PASS {mode}/{theme.Key}: real page, fixed image, commands, zoom status, wide/narrow layout, keyboard visibility, localized overflow and autoplay context menu.");
                }
                finally
                {
                    model.IsAutoPlaying = false;
                    window.Content = null;
                    window.Close();
                }
            }
        File.WriteAllLines(Path.Combine(directory, "offline-requests.txt"), fixture.RequestLog);
        Require(fixture.UnexpectedRequests.IsEmpty, "Unexpected network requests: " + string.Join(", ", fixture.UnexpectedRequests));
        File.WriteAllLines(Path.Combine(directory, "viewer-toolbar-contracts.txt"), report);
        Console.WriteLine($"Passed {report.Count} real viewer toolbar scenarios.");
    }

    private static CommandBarButton FindButton(CommandBar bar, string resource) =>
        bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => button.IsVisible && button.Label == FixtureLocalization.GetResource(resource));

    private static async Task ActivateAsync(Window window, CommandBar bar, Control button)
    {
        await ShowCommandAsync(window, bar, button);
        Require(button.IsEffectivelyVisible && button.IsEffectivelyEnabled, "The command is not reachable.");
        var point = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)
            ?? throw new InvalidOperationException("The command is detached.");
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        await RenderAsync(window);
    }

    private static async Task ShowCommandAsync(Window window, CommandBar bar, Control button)
    {
        bar.IsOpen = button is ICommandBarElement { IsInOverflow: true };
        await RenderAsync(window);
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(150);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static void Save(Window window, string file, string directory)
    {
        using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No viewer frame.");
        bitmap.Save(Path.Combine(directory, file), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
