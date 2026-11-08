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
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Services.Core;
using Pixeval.Extensions.Common;
using Pixeval.Services.Background;
using Pixeval.Utilities;
using Pixeval.Views;

namespace Pixeval.Tests.Ui;

internal static class ExtensionManagementValidation
{
    internal static async Task RunAsync(string directory)
    {
        var service = App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();
        Require(service.HostModels.Count == 0, "The management fixture starts without user extensions");
        var models = Enumerable.Range(1, 3).Select(index => new ExtensionsHostModel(new FixtureHost(index), [])
        {
            UninstallTargetRelativePath = $"management-fixture-{index}",
            HostLibraryPath = Path.Combine(AppInfo.ExtensionsFolder, $"management-fixture-{index}", "fixture.dll")
        }).ToArray();
        foreach (var model in models)
        {
            App.AppViewModel.AppSettings.ExtensionSettings[model.Name] = model.Values;
            Directory.CreateDirectory(Path.GetDirectoryName(model.HostLibraryPath)!);
            File.WriteAllText(model.HostLibraryPath, "isolated scheduling sentinel; not a native library");
            service.HostModels.Add(model);
        }
        List<string> report = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 360, 1000 })
            {
                var scenario = $"{theme.Key}-{width}";
                var page = UiTestSession.Create<ExtensionsPage>();
                var host = new PageTestHost { Content = page };
                var window = new Window { Width = width, Height = 800, RequestedThemeVariant = theme, Content = host.Shell };
                try
                {
                    Pixeval.Common.MaterialAnimation.Initialize(window);
                    window.Show();
                    await RenderAsync(window);
                    var management = page.FindControl<CommandBar>("ManagementCommandBar")!;
                    foreach (var command in management.PrimaryCommands.OfType<CommandBarButton>())
                    {
                        management.IsOpen = command.IsInOverflow;
                        await RenderAsync(window);
                        InWindow(window, command);
                        var label = command.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == command.Label);
                        Require(!label.TextLayout.TextLines.Any(line => line.HasCollapsed)
                            && label.TextLayout.Width <= label.Bounds.Width + 1,
                            "Management action labels remain readable in the main bar or overflow");
                    }
                    management.IsOpen = false;
                    await RenderAsync(window);
                    Save(window, directory, "extensions-normal-" + scenario);
                    var first = models[0];
                    var card = Card(page, first);
                    var link = card.GetVisualDescendants().OfType<HyperlinkButton>().Single();
                    InWindow(window, link);
                    var name = link.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == first.Name);
                    Require(!name.TextLayout.TextLines.Any(line => line.HasCollapsed), "A long extension name remains readable");
                    Require(name.TextLayout.Width <= name.Bounds.Width + 1, "The full extension name wraps within its card");
                    var author = card.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == first.Author);
                    InWindow(window, author);
                    Require(author.TextLayout.Width <= author.Bounds.Width + 1 && author.TextLayout.Height <= author.Bounds.Height + 1,
                        "Author metadata wraps without clipping");
                    var toggle = card.GetVisualDescendants().OfType<ToggleSwitch>().Single();
                    InWindow(window, toggle);
                    Require(AutomationProperties.GetName(toggle) == first.Name, "The activation switch identifies its host");
                    Require(toggle.Focus(), "The activation switch is keyboard reachable");
                    Press(window, Key.Space, PhysicalKey.Space);
                    await RenderAsync(window);
                    Require(!first.IsActive, "The real switch disables the existing host model");
                    var logger = App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>();
                    AppInfo.SaveAppSettings(App.AppViewModel.AppSettings);
                    var disabledSettings = AppInfo.LoadAppSettings(logger)!;
                    Require(disabledSettings.ExtensionSettings[first.Name][nameof(ExtensionsHostModel.IsActive)] is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.False },
                        "Disabled activation survives settings disk reload");
                    first.IsActive = true;
                    await RenderAsync(window);
                    Require(toggle.IsChecked is true, "Model activation is reflected by the switch");

                    var bar = Bar(page, first);
                    Require(bar.PrimaryCommands.OfType<CommandBarButton>().All(button => !string.IsNullOrWhiteSpace(button.Label)),
                        "Priority commands retain names in overflow and automation");
                    await ActivateAsync(window, bar, bar.PrimaryCommands.OfType<CommandBarButton>().ElementAt(1));
                    Require(service.HostModels.IndexOf(first) == 1, "Move down changes the same host's priority");
                    bar = Bar(page, first);
                    await ActivateAsync(window, bar, bar.PrimaryCommands.OfType<CommandBarButton>().First());
                    Require(service.HostModels.IndexOf(first) == 0, "Move up restores the host");
                    bar = Bar(page, first);
                    await ActivateAsync(window, bar, bar.SecondaryCommands.OfType<CommandBarButton>().ElementAt(2));
                    Require(service.HostModels.Last() == first, "Move bottom reaches the final priority");
                    Card(page, first).BringIntoView();
                    await RenderAsync(window);
                    bar = Bar(page, first);
                    await ActivateAsync(window, bar, bar.SecondaryCommands.OfType<CommandBarButton>().ElementAt(1));
                    Require(service.HostModels.First() == first, "Move top restores the original identity");
                    Card(page, first).BringIntoView();
                    await RenderAsync(window);
                    Require(service.HostModels.Select(model => model.Priority).SequenceEqual(new[] { 0, 1, 2 }), "Persisted priorities match collection order");
                    bar = Bar(page, first);
                    var uninstall = bar.SecondaryCommands.OfType<CommandBarButton>().Last();
                    var originalLabel = first.UninstallCommandLabel;
                    await ActivateAsync(window, bar, uninstall);
                    Require(first.IsPendingUninstall && uninstall.Label != originalLabel, "Scheduling changes the same command into cancel uninstall");
                    Require(App.AppViewModel.AppSettings.PendingExtensionUninstallTargets.Contains(first.UninstallTargetRelativePath), "The pending target is saved in application settings");
                    Require(AppInfo.LoadAppSettings(logger)!.PendingExtensionUninstallTargets.Contains(first.UninstallTargetRelativePath),
                        "Pending uninstall survives settings disk reload");
                    Require(File.Exists(first.HostLibraryPath), "Scheduling does not delete a loaded host's files");
                    bar.IsOpen = true;
                    await RenderAsync(window);
                    Require(bar.SecondaryCommands.OfType<CommandBarButton>().First().IsVisible
                        && first.HelpLink?.AbsoluteUri == "https://example.invalid/help",
                        "A valid help URL keeps its existing command and destination");
                    Save(window, directory, "extensions-pending-" + scenario);
                    bar.IsOpen = false;
                    await RenderAsync(window);
                    await ActivateAsync(window, bar, uninstall);
                    Require(!first.IsPendingUninstall && uninstall.Label == originalLabel, "Cancel restores uninstall without replacing the command");
                    var restored = AppInfo.LoadAppSettings(logger)!;
                    Require(!restored.PendingExtensionUninstallTargets.Contains(first.UninstallTargetRelativePath), "Cancellation survives settings disk reload");
                    Require(models.All(model => ((System.Text.Json.JsonElement)restored.ExtensionSettings[model.Name][nameof(ExtensionsHostModel.Priority)]).GetInt32() == model.Priority),
                        "All priority values survive settings disk reload");
                    foreach (var withoutHelp in models.Skip(1))
                    {
                        var noHelpBar = Bar(page, withoutHelp);
                        noHelpBar.BringIntoView();
                        noHelpBar.IsOpen = true;
                        await RenderAsync(window);
                        Require(!noHelpBar.SecondaryCommands.OfType<CommandBarButton>().First().IsVisible,
                            "A host without a help URL does not expose a help command");
                        noHelpBar.IsOpen = false;
                        await RenderAsync(window);
                    }
                    report.Add($"PASS {scenario}: long metadata, activation keyboard/model, priority up/down/top/bottom, pending/cancel uninstall, persisted values, hidden missing help and unchanged host files");
                }
                finally
                {
                    window.Content = null;
                    window.Close();
                }
            }
        File.WriteAllLines(Path.Combine(directory, "extension-management-contracts.txt"), report);
        Console.WriteLine($"Extension management contracts passed: {report.Count} theme/width scenarios (managed metadata; native ABI is validated by NativeExtension).");
    }

    private static SettingsCard Card(Control page, ExtensionsHostModel model) => page.GetVisualDescendants().OfType<SettingsCard>().Single(card => ReferenceEquals(card.DataContext, model));
    private static CommandBar Bar(Control page, ExtensionsHostModel model) => Card(page, model).GetVisualDescendants().OfType<CommandBar>().Single();

    private static async Task ActivateAsync(Window window, CommandBar bar, CommandBarButton button)
    {
        bar.BringIntoView();
        await RenderAsync(window);
        if (button.IsInOverflow)
        {
            var more = bar.GetVisualDescendants().OfType<Button>().Single(control => control.Name is "PART_OverflowButton");
            Require(more.Focus(), "The overflow button accepts keyboard focus");
            Press(window, Key.Enter, PhysicalKey.Enter);
            await RenderAsync(window);
            Require(bar.IsOpen, "Keyboard opens extension commands");
        }
        InWindow(window, button);
        var point = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)!.Value;
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        await RenderAsync(window);
        if (bar.IsOpen)
        {
            Press(window, Key.Escape, PhysicalKey.Escape);
            await RenderAsync(window);
            Require(!bar.IsOpen, "Escape dismisses extension commands");
        }
    }

    private static void InWindow(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window) ?? throw new InvalidOperationException("Detached extension control");
        Require(control.Bounds.Width > 0 && control.Bounds.Height > 0 && point.X >= -1 && point.Y >= -1
            && point.X + control.Bounds.Width <= window.ClientSize.Width + 1 && point.Y + control.Bounds.Height <= window.ClientSize.Height + 1,
            $"{control.GetType().Name} must fit its window: {point}, {control.Bounds.Size}");
    }

    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(100);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No extension frame");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        if (!condition)
            throw new InvalidOperationException("Extension management contract failed: " + contract);
    }

    private sealed class FixtureHost(int index) : IExtensionsHost
    {
        public string GetExtensionName() => $"扩展 {index} — Long extension name with readable metadata";
        public string GetAuthorName() => "Fixture author · 很长的作者名称";
        public string GetExtensionLink() => "https://example.invalid/extension";
        public string GetHelpLink() => index switch { 1 => "https://example.invalid/help", 2 => "", _ => "   " };
        public string GetDescription() => "固定离线扩展元数据，用于验证窄窗口内的名称、描述、优先级和卸载操作。";
        public string GetSdkVersion() => ExtensionService.CurrentVersion.ToString();
        public string GetVersion() => "1.0.0";
        public IExtension[] GetExtensions(out int returnCount) { returnCount = 0; return []; }
        public byte[]? GetIcon(out int returnCount) { returnCount = 0; return null; }
        public void Initialize(string cultureName, string tempDirectory, string extensionDirectory, ILogger logger) { }
    }
}
