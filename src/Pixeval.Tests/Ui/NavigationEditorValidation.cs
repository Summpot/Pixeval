using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Notifications;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using CommunityToolkit.Avalonia.Controls;
using FluentIcons.Common;
using Material3Expressive.Components;
using Pixeval.AppManagement;
using Pixeval.I18N;
using Pixeval.Models.Navigation;
using Pixeval.Utilities;
using Pixeval.Views.Settings;

namespace Pixeval.Tests.Ui;

internal static class NavigationEditorValidation
{
    private const string ValidYaml = """
        newTab: Search
        header:
          - folder: Fixture 中文
            icon: Folder
            children:
              - page: Search
              - page: WorkRecommended
        footer:
          - page: Settings
        """;

    internal static async Task RunAsync(string directory)
    {
        List<string> completed = [];
        var savedPath = Path.Combine(AppInfo.SettingsFolder, "navigation_menu.yaml");
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 1000, 420, 360 })
            {
                var scenario = $"{theme.Key}-{width}";
                App.AppViewModel.NavigationMenuYamlText = NavigationMenuYaml.DefaultYaml;
                AppInfo.SaveNavigationMenuYaml(NavigationMenuYaml.DefaultYaml);
                var page = new NavigationSettingsPage();
                var navigation = new NavigationPage();
                await navigation.PushAsync(page);
                var container = new PageTestHost { Content = navigation };
                var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = container.Shell };
                try
                {
                    window.Show();
                    Render(window);
                    var editor = page.FindControl<TextEditor>("YamlEditor")
                        ?? throw new InvalidOperationException("Missing YAML editor.");
                    var apply = page.FindControl<CommandBarButton>("CanApplyButton")
                        ?? throw new InvalidOperationException("Missing apply command.");
                    var status = page.FindControl<Control>("StatusInfoBar")
                        ?? throw new InvalidOperationException("Missing validation status.");
                    var bar = NavigationPage.GetTopCommandBar(page) as CommandBar
                        ?? throw new InvalidOperationException("Missing navigation command bar.");
                    Require(bar.IsEffectivelyVisible && bar.Bounds.Height > 0, "Command bar is rendered");
                    Require(apply.IsEffectivelyEnabled, "Default YAML can be applied");
                    foreach (var primary in bar.PrimaryCommands.OfType<CommandBarButton>().Where(button => !button.IsInOverflow))
                    {
                        var position = primary.TranslatePoint(default, window)!.Value;
                        Require(position.X >= 0 && position.X + primary.Bounds.Width <= window.ClientSize.Width + 1,
                            $"Visible command fits the window: {primary.Label} at {position}, {primary.Bounds.Size}");
                    }

                    editor.Text = ValidYaml;
                    var parsed = NavigationYamlParser.Parse(ValidYaml);
                    Require(parsed.Configuration is not null && parsed.IsValid, "Fixed valid YAML parses");
                    var expectedFormat = NavigationYamlFormatter.Format(parsed.Configuration!);
                    Click(NavigationSettingsPageResources.FormatButton.Content);
                    Require(editor.Text == expectedFormat, "Format uses the production formatter");
                    Require(App.AppViewModel.NavigationMenuYamlText == NavigationMenuYaml.DefaultYaml, "Format does not apply changes");
                    completed.Add($"{scenario}: format preserves navigation semantics without applying");

                    Require(editor.TextArea.Focus(), "Editor receives focus");
                    var command = KeyboardShortcut.CreatePlatformCommandGesture(Key.S).KeyModifiers;
                    var rawCommand = command.HasFlag(KeyModifiers.Meta) ? RawInputModifiers.Meta : RawInputModifiers.Control;
                    window.KeyPress(Key.S, rawCommand, PhysicalKey.S, "s");
                    window.KeyRelease(Key.S, rawCommand, PhysicalKey.S, "s");
                    Dispatcher.UIThread.RunJobs();
                    Require(App.AppViewModel.NavigationMenuYamlText == expectedFormat, "Platform save shortcut applies current YAML");
                    Require(File.ReadAllText(savedPath) == expectedFormat.ReplaceLineEndings(Environment.NewLine), "Apply persists YAML in the isolated directory");
                    completed.Add($"{scenario}: platform save shortcut persists the exact formatted YAML");
                    await DismissNotificationsAsync(window);

                    var saved = File.ReadAllBytes(savedPath);
                    editor.Text = "header: [";
                    Render(window);
                    Require(!apply.IsEffectivelyEnabled && status.IsVisible, "Invalid YAML disables apply and displays diagnostics");
                    window.KeyPress(Key.S, rawCommand, PhysicalKey.S, "s");
                    window.KeyRelease(Key.S, rawCommand, PhysicalKey.S, "s");
                    Require(editor.Text == "header: [", "Invalid editor contents are preserved");
                    Require(File.ReadAllBytes(savedPath).SequenceEqual(saved), "Invalid shortcut leaves the saved file unchanged");
                    Save(window, Path.Combine(directory, $"navigation-invalid-{scenario}.png"));
                    Require(editor.Bounds.Width >= Math.Min(280, page.Bounds.Width - 42) && editor.Bounds.Height >= 180,
                        $"Invalid YAML must leave a usable editing viewport, actual {editor.Bounds.Size}");
                    var diagnostics = page.FindControl<ScrollViewer>("DiagnosticsScroller")!;
                    Require(diagnostics.IsEffectivelyVisible && diagnostics.Bounds.Height <= 160,
                        "Long diagnostics must be bounded independently of the editor");
                    if (diagnostics.Extent.Height > diagnostics.Viewport.Height + 1)
                    {
                        Require(diagnostics.Focus(), "Diagnostics accept keyboard focus");
                        window.KeyPress(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null);
                        window.KeyRelease(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null);
                        await SettleAsync(window);
                        Require(diagnostics.Offset.Y > 0, $"Keyboard input scrolls long diagnostics: extent={diagnostics.Extent}, viewport={diagnostics.Viewport}, offset={diagnostics.Offset}");
                    }
                    completed.Add($"{scenario}: invalid YAML and save shortcut preserve the last valid file");

                    Click(NavigationSettingsPageResources.ResetButton.Content);
                    Require(editor.Text == NavigationMenuYaml.DefaultYaml && apply.IsEffectivelyEnabled, "Reset changes the editor to defaults");
                    Require(File.ReadAllBytes(savedPath).SequenceEqual(saved), "Reset waits for explicit apply");
                    completed.Add($"{scenario}: reset edits defaults without overwriting saved navigation");

                    editor.TextArea.Caret.Offset = editor.Text.Length;
                    var length = editor.Text.Length;
                    Click(NavigationSettingsPageResources.InsertFolderButton.Content);
                    Require(editor.Text.Length > length && editor.TextArea.Caret.Offset == editor.Text.Length, "Insert folder keeps the caret after the inserted text");
                    Require(editor.Text.Contains("$" + NavigationSettingsPageResources.DefaultFolderTitle, StringComparison.Ordinal), "Inserted folder retains its i18n resource reference");
                    Require(File.ReadAllBytes(savedPath).SequenceEqual(saved), "Insert folder waits for explicit apply");
                    completed.Add($"{scenario}: folder insertion preserves its resource key and pending edit state");

                    // Resizing must keep the same editor, text, undo history and pending selection.
                    editor.Text = ValidYaml;
                    Require(editor.TextArea.Focus(), "Editor accepts correction after an invalid document");
                    editor.TextArea.Caret.Offset = editor.Text.Length;
                    window.KeyTextInput("\n# pending 中文");
                    var pendingText = editor.Text;
                    editor.Select(3, 5);
                    var selection = editor.SelectionStart;
                    window.Width = width > 720 ? 360 : 1000;
                    await SettleAsync(window);
                    Require(editor.Text == pendingText && editor.SelectionStart == selection && editor.SelectionLength == 5,
                        "Responsive layout preserves the editor document and selection");
                    Require(ReferenceEquals(editor, page.FindControl<TextEditor>("YamlEditor")), "Resizing does not recreate the editor");
                    window.Width = width;
                    await SettleAsync(window);
                    Require(editor.Bounds.Width >= Math.Min(280, page.Bounds.Width - 42) && editor.Bounds.Height >= 180, "Editor remains usable after resizing back");
                    Require(editor.TextArea.Focus(), "Editor accepts undo after resizing");
                    window.KeyPress(Key.Z, rawCommand, PhysicalKey.Z, "z");
                    window.KeyRelease(Key.Z, rawCommand, PhysicalKey.Z, "z");
                    Render(window);
                    Require(editor.Text == ValidYaml, "Responsive layout preserves undo history");
                    Require(File.ReadAllBytes(savedPath).SequenceEqual(saved), "Typing and resizing do not implicitly save");
                    completed.Add($"{scenario}: narrow/wide round trip preserves pending text, selection and undo");

                    await VerifyReferencesAsync(window, page, directory, scenario);
                    Require(editor.Text == ValidYaml && File.ReadAllBytes(savedPath).SequenceEqual(saved),
                        "Reference copying does not edit or apply navigation");
                    completed.Add($"{scenario}: reference pages and icon names copy through real controls to the headless clipboard");
                    Console.WriteLine($"Navigation editor contracts passed for {scenario}.");

                    void Click(string resource)
                    {
                        var label = I18NManager.GetResource(resource);
                        var button = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => button.Label == label);
                        bar.IsOpen = button.IsInOverflow;
                        Render(window);
                        Require(button.IsEffectivelyVisible && button.IsEffectivelyEnabled, $"Command is available: {label}");
                        var point = button.TranslatePoint(new Rect(button.Bounds.Size).Center, window)
                            ?? throw new InvalidOperationException("Command has no window position.");
                        Require(point.X >= 0 && point.X < window.ClientSize.Width && point.Y >= 0 && point.Y < window.ClientSize.Height, $"Command lies within the window: {label} at {point}");
                        window.MouseMove(point);
                        window.MouseDown(point, MouseButton.Left);
                        window.MouseUp(point, MouseButton.Left);
                        Render(window);
                        if (bar.IsOpen)
                        {
                            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                            Render(window);
                            Require(!bar.IsOpen, "Escape closes the command overflow after activation");
                        }
                    }
                }
                finally
                {
                    window.Content = null;
                    window.Close();
                }
            }
        File.WriteAllLines(Path.Combine(directory, "navigation-contracts.txt"), completed);
        Console.WriteLine($"Verified {completed.Count} isolated navigation editor contracts.");
    }

    private static async Task VerifyReferencesAsync(Window window, NavigationSettingsPage page, string directory, string scenario)
    {
        var references = page.FindControl<ScrollViewer>("ReferenceScroller")!;
        var expanders = references.GetVisualDescendants().OfType<SettingsExpander>().ToArray();
        Require(expanders.Length == 2, "Both page and icon reference groups remain available");
        foreach (var expander in expanders)
        {
            var header = expander.GetVisualDescendants().OfType<ToggleButton>().Single(button => button.Name is "ExpanderHeader");
            header.BringIntoView();
            await SettleAsync(window);
            Require(header.Focus(), "Reference groups are keyboard accessible");
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            await SettleAsync(window);
            Require(expander.IsExpanded, "Enter opens the reference group");
            if (ReferenceEquals(expander, expanders[0]))
            {
                var cards = expander.GetVisualDescendants().OfType<Card>().Where(card => card.DataContext is NavigationPageDefinition).ToArray();
                Require(cards.Length == NavigationPageRegistry.Pages.Count, "Every registered page has a copy entry");
                foreach (var card in new[] { cards[0], cards[^1] })
                {
                    card.BringIntoView();
                    await SettleAsync(window);
                    Require(card.Focus(), "Page definition copy accepts keyboard focus");
                    window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                    window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                    await SettleAsync(window);
                    var definition = (NavigationPageDefinition) card.DataContext!;
                    var copied = await window.Clipboard!.TryGetTextAsync();
                    Require(copied == "- page: " + definition.Key, $"Copied page must be a usable YAML entry for {definition.Key}, actual: {copied}");
                    var parsed = NavigationYamlParser.Parse("header:\n  " + copied
                        + (definition.Key is "Settings" ? "" : "\nfooter:\n  - page: Settings"));
                    Require(parsed.IsValid, "Copied page definition parses using the production YAML reader");
                    await DismissNotificationsAsync(window);
                }
            }
            else
            {
                var icons = expander.GetVisualDescendants().OfType<Button>().Where(button => button.DataContext is Symbol && button.Tag is string).ToArray();
                Require(icons.Length == NavigationPageRegistry.ColorSymbols.Count,
                    $"Every existing color icon is available: {icons.Length}/{NavigationPageRegistry.ColorSymbols.Count}");
                foreach (var icon in new[] { icons[0], icons[^1] })
                {
                    icon.BringIntoView();
                    await SettleAsync(window);
                    Require(icon.Bounds.Width >= 48 && icon.Bounds.Height >= 48, "Icon copy has a usable target");
                    Require(icon.Focus(), "Icon copy accepts keyboard focus");
                    window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                    window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                    await SettleAsync(window);
                    Require(await window.Clipboard!.TryGetTextAsync() == (string) icon.Tag!, "Copied icon keeps its persisted enum name");
                    await DismissNotificationsAsync(window);
                }
            }
            Save(window, Path.Combine(directory, $"navigation-reference-{scenario}-{Array.IndexOf(expanders, expander)}.png"));
            header.BringIntoView();
            await SettleAsync(window);
            Require(header.Focus(), "Focus returns to the reference header");
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            await SettleAsync(window);
            Require(!expander.IsExpanded, "Space closes the reference group");
        }
    }

    private static async Task SettleAsync(Window window)
    {
        Render(window);
        await Task.Delay(250);
        Render(window);
    }

    private static async Task DismissNotificationsAsync(Window window)
    {
        foreach (var card in window.GetVisualDescendants().OfType<NotificationCard>().ToArray())
        {
            var close = card.GetVisualDescendants().OfType<Button>().Single(button => button.Name is "PART_CloseButton");
            Require(close.Focus(), "The real success notification can be dismissed before the next toolbar action");
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        }
        for (var attempt = 0; attempt < 12 && window.GetVisualDescendants().OfType<NotificationCard>().Any(); attempt++)
            await SettleAsync(window);
        Require(!window.GetVisualDescendants().OfType<NotificationCard>().Any(), "Dismissed notifications leave the input layer");
    }

    private static void Require(bool condition, string contract)
    {
        if (!condition)
            throw new InvalidOperationException("Navigation contract failed: " + contract);
    }

    private static void Render(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static void Save(Window window, string path)
    {
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("No navigation editor frame was rendered.");
        frame.Save(path, new PngBitmapEncoderOptions());
    }
}
