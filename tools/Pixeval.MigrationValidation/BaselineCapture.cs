using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Avalonia.Controls;
using Pixeval.ViewModels;
using Pixeval.Views;
using Pixeval.Views.Login;
using Pixeval.Views.Settings;

namespace Pixeval.MigrationValidation;

internal static class BaselineCapture
{
    internal static async Task ExportAsync(string directory)
    {
        var settings = new SettingsPageViewModel();
        List<CaptureRecord> captures = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            await CaptureAsync("settings-main", () => new SettingsMainView { DataContext = settings }, theme);
            foreach (var group in settings.LocalGroups)
                await CaptureAsync("settings-" + group.Token, () => new SettingsSubView(group), theme);
            await CaptureAsync("navigation", () => new NavigationSettingsPage(), theme);
            await CaptureAsync("help", () => new HelpPage(), theme);
            await CaptureAsync("extensions-empty", () => new ExtensionsPage(), theme);
            await CaptureAsync("login-empty", () => new LoginPage(), theme);
        }

        File.WriteAllText(Path.Combine(directory, "captures.json"),
            JsonSerializer.Serialize(captures, CaptureJsonContext.Default.ListCaptureRecord) + Environment.NewLine);
        Console.WriteLine($"Captured {captures.Count} isolated headless frames. Native input and GPU performance are not covered.");
        return;

        async Task CaptureAsync(string name, Func<Page> create, ThemeVariant theme)
        {
            var stopwatch = Stopwatch.StartNew();
            var page = create();
            var navigation = new NavigationPage();
            await navigation.PushAsync(page);
            var window = new Window
            {
                Width = 1000,
                Height = 720,
                RequestedThemeVariant = theme,
                Content = navigation
            };
            try
            {
                window.Show();
                await RenderAsync(window);
                Save("top");
                // Capture the end of each scrollable page without invoking commands or
                // editing settings. Large nested editors keep their independent scroll.
                var scroller = window.GetVisualDescendants().OfType<ScrollViewer>()
                    .FirstOrDefault(static viewer => viewer.Extent.Height > viewer.Viewport.Height + 1);
                if (scroller is not null)
                {
                    scroller.Offset = new(0, scroller.Extent.Height - scroller.Viewport.Height);
                    await RenderAsync(window);
                    Save("bottom");
                }
                var expanders = page.GetVisualDescendants().OfType<SettingsExpander>()
                    .Where(static expander => expander.IsEffectivelyEnabled).ToArray();
                if (expanders.Length > 0)
                {
                    foreach (var expander in expanders)
                        expander.IsExpanded = true;
                    await RenderAsync(window);
                    if (scroller is not null)
                        scroller.Offset = default;
                    await RenderAsync(window);
                    Save("expanded-top");
                }
            }
            finally
            {
                window.Content = null;
                window.Close();
            }

            void Save(string position)
            {
                var fileName = $"{name}-{theme.Key}-{position}.png";
                using var bitmap = window.CaptureRenderedFrame()
                    ?? throw new InvalidOperationException($"No rendered frame for {fileName}.");
                if (bitmap.PixelSize.Width is not 1000 || bitmap.PixelSize.Height is not 720)
                    throw new InvalidOperationException($"Unexpected capture size: {bitmap.PixelSize}.");
                bitmap.Save(Path.Combine(directory, fileName), new PngBitmapEncoderOptions());
                var controls = window.GetVisualDescendants().OfType<Control>().ToArray();
                if (controls.Any(static control => !double.IsFinite(control.Bounds.Width) || !double.IsFinite(control.Bounds.Height)))
                    throw new InvalidOperationException($"Non-finite layout in {fileName}.");
                var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, fileName))));
                captures.Add(new(fileName, theme.Key.ToString()!, bitmap.PixelSize.Width, bitmap.PixelSize.Height,
                    controls.Length, stopwatch.Elapsed.TotalMilliseconds, hash));
            }
        }
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        // Let the real initial/expansion transitions settle before recording pixels.
        // This is capture settling time, never a rendering-performance measurement.
        await Task.Delay(500);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }
}

internal sealed record CaptureRecord(string File, string Theme, int Width, int Height, int VisualControls,
    double ElapsedMilliseconds, string Sha256);

[JsonSerializable(typeof(List<CaptureRecord>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class CaptureJsonContext : JsonSerializerContext;
