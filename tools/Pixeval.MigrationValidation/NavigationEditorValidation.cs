using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Pixeval.AppManagement;
using Pixeval.I18N;
using Pixeval.Models.Navigation;
using Pixeval.Utilities;
using Pixeval.Views.Settings;

namespace Pixeval.MigrationValidation;

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
        {
            App.AppViewModel.NavigationMenuYamlText = NavigationMenuYaml.DefaultYaml;
            AppInfo.SaveNavigationMenuYaml(NavigationMenuYaml.DefaultYaml);
            var page = new NavigationSettingsPage();
            var navigation = new NavigationPage();
            await navigation.PushAsync(page);
            var window = new Window { Width = 1000, Height = 720, RequestedThemeVariant = theme, Content = navigation };
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
                var bar = NavigationPage.GetTopCommandBar(page)
                    ?? throw new InvalidOperationException("Missing navigation command bar.");
                Require(bar.IsEffectivelyVisible && bar.Bounds.Height > 0, "Command bar is rendered");
                Require(apply.IsEffectivelyEnabled, "Default YAML can be applied");

                editor.Text = ValidYaml;
                var parsed = NavigationYamlParser.Parse(ValidYaml);
                Require(parsed.Configuration is not null && parsed.IsValid, "Fixed valid YAML parses");
                var expectedFormat = NavigationYamlFormatter.Format(parsed.Configuration!);
                Click(NavigationSettingsPageResources.FormatButton.Content);
                Require(editor.Text == expectedFormat, "Format uses the production formatter");
                Require(App.AppViewModel.NavigationMenuYamlText == NavigationMenuYaml.DefaultYaml, "Format does not apply changes");
                completed.Add($"{theme.Key}: format preserves navigation semantics without applying");

                Require(editor.TextArea.Focus(), "Editor receives focus");
                var command = KeyboardShortcut.CreatePlatformCommandGesture(Key.S).KeyModifiers;
                var rawCommand = command.HasFlag(KeyModifiers.Meta) ? RawInputModifiers.Meta : RawInputModifiers.Control;
                window.KeyPress(Key.S, rawCommand, PhysicalKey.S, "s");
                window.KeyRelease(Key.S, rawCommand, PhysicalKey.S, "s");
                Dispatcher.UIThread.RunJobs();
                Require(App.AppViewModel.NavigationMenuYamlText == expectedFormat, "Platform save shortcut applies current YAML");
                Require(File.ReadAllText(savedPath) == expectedFormat.ReplaceLineEndings(Environment.NewLine), "Apply persists YAML in the isolated directory");
                completed.Add($"{theme.Key}: platform save shortcut persists the exact formatted YAML");

                var saved = File.ReadAllBytes(savedPath);
                editor.Text = "header: [";
                Render(window);
                Require(!apply.IsEffectivelyEnabled && status.IsVisible, "Invalid YAML disables apply and displays diagnostics");
                window.KeyPress(Key.S, rawCommand, PhysicalKey.S, "s");
                window.KeyRelease(Key.S, rawCommand, PhysicalKey.S, "s");
                Require(editor.Text == "header: [", "Invalid editor contents are preserved");
                Require(File.ReadAllBytes(savedPath).SequenceEqual(saved), "Invalid shortcut leaves the saved file unchanged");
                Save(window, Path.Combine(directory, $"navigation-invalid-{theme.Key}.png"));
                completed.Add($"{theme.Key}: invalid YAML and save shortcut preserve the last valid file");

                Click(NavigationSettingsPageResources.ResetButton.Content);
                Require(editor.Text == NavigationMenuYaml.DefaultYaml && apply.IsEffectivelyEnabled, "Reset changes the editor to defaults");
                Require(File.ReadAllBytes(savedPath).SequenceEqual(saved), "Reset waits for explicit apply");
                completed.Add($"{theme.Key}: reset edits defaults without overwriting saved navigation");

                editor.TextArea.Caret.Offset = editor.Text.Length;
                var length = editor.Text.Length;
                Click(NavigationSettingsPageResources.InsertFolderButton.Content);
                Require(editor.Text.Length > length && editor.TextArea.Caret.Offset == editor.Text.Length, "Insert folder keeps the caret after the inserted text");
                Require(editor.Text.Contains("$" + NavigationSettingsPageResources.DefaultFolderTitle, StringComparison.Ordinal), "Inserted folder retains its i18n resource reference");
                Require(File.ReadAllBytes(savedPath).SequenceEqual(saved), "Insert folder waits for explicit apply");
                completed.Add($"{theme.Key}: folder insertion preserves its resource key and pending edit state");
                Console.WriteLine($"Navigation editor contracts passed for {theme.Key}.");

                void Click(string resource)
                {
                    var label = I18NManager.GetResource(resource);
                    var button = bar.GetVisualDescendants().OfType<CommandBarButton>().Single(button => button.Label == label);
                    Require(button.IsEffectivelyVisible && button.IsEffectivelyEnabled, $"Command is available: {label}");
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Render(window);
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
