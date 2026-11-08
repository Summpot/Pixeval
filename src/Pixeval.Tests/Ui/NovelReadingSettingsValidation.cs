using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoSettingsPage.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ColorTextBlock.Avalonia;
using CommunityToolkit.Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement;
using Pixeval.AppManagement.Settings;
using Pixeval.Controls;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Settings;
using Pixeval.Views.Viewers;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class NovelReadingSettingsValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new NovelReaderFixture();
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-reading-settings");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        using var source = new SimpleOperableSourceView<NovelItemViewModel>([
            NovelReaderFixture.Sample(996001), NovelReaderFixture.Sample(996002)]);
        var model = new NovelViewerPageViewModel(source.CloneSourceView(), 0, false);
        var page = new NovelViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 1200, Height = 900, Content = host.Shell };
        List<string> report = [];
        try
        {
            window.Show();
            (await fixture.NextAsync()).Complete("Reading presentation\n\n[[rb:漢字 > かんじ]]\n\n" + string.Join('\n', Enumerable.Repeat("A long reading paragraph 中文 🌸", 80)));
            await UntilAsync(() => !model.IsLoading);
            await RenderAsync(window);
            var settings = App.AppViewModel.AppSettings.NovelSettings;
            var changes = 0;
            model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(model.NovelFontFamilyObject)) changes++; };
            settings.NovelFontFamily.Add("Arial");
            Require(changes == 1, $"A single font collection edit updates the reader exactly once, even after content loads: {changes}");

            var split = page.FindControl<SplitView>("InfoSplitView")!;
            await ActivateAsync(window, page.FindControl<CommandBarToggleButton>("InformationButton")!);
            var tabs = page.GetVisualDescendants().OfType<TabbedPage>().Single();
            var settingsPage = tabs.Pages!.OfType<SettingsSubView>().Single();
            tabs.SelectedIndex = 3;
            await RenderAsync(window);
            Require(settingsPage.IsEffectivelyVisible, "The settings page is reached through the actual reader information pane");
            var entries = settingsPage.FindControl<ItemsControl>("ItemsControl")!.ItemsSource!.Cast<ISettingsEntry>().ToArray();
            Require(entries.Length == 7, "The existing seven reading settings are retained");
            var markdown = page.FindControl<NovelMarkdownBox>("ReaderMarkdown")!;

            foreach (var (token, value, min, max, step) in new[]
            {
                (nameof(settings.NovelFontSize), 24m, 5m, 100m, 1m),
                (nameof(settings.NovelLineHeight), 36m, 0m, 150m, 1m),
                (nameof(settings.NovelMaxWidth), 600m, 50m, 10000m, 50m)
            })
            {
                var number = settingsPage.GetVisualDescendants().OfType<NumericUpDown>()
                    .Single(control => control.DataContext is ISettingsEntry entry && entry.Token == token);
                number.BringIntoView();
                await RenderAsync(window);
                Require(number.Minimum == min && number.Maximum == max && number.Increment == step, "Reading setting retains its original range/step: " + token);
                var editor = number.GetVisualDescendants().OfType<TextBox>().Single();
                Require(editor.Focus(), "Reading numeric input accepts keyboard focus");
                editor.SelectAll(); window.KeyTextInput(value.ToString()); Press(window, Key.Enter, PhysicalKey.Enter);
                await RenderAsync(window);
                Require(number.Value == value && ((ISingleValueSettingsEntry<int>) entries.Single(entry => entry.Token == token)).Value == value,
                    "Keyboard editing updates the original reading value: " + token);
                Press(window, Key.Up, PhysicalKey.ArrowUp);
                await RenderAsync(window);
                Require(number.Value == value + step && model.CurrentWorkIndex == 0, "Numeric arrows edit the setting without switching novels");
                number.Value = value;
            }

            var colors = settingsPage.GetVisualDescendants().OfType<ColorPicker>().ToArray();
            var foreground = colors.Single(control => ((ISettingsEntry) control.DataContext!).Token == nameof(settings.NovelFontColor));
            var background = colors.Single(control => ((ISettingsEntry) control.DataContext!).Token == nameof(settings.NovelBackground));
            foreground.Color = Color.FromUInt32(0xFF234567);
            background.Color = Color.FromUInt32(0xFFF0E0C0);
            var weight = (ISingleValueSettingsEntry<object>) entries.Single(entry => entry.Token == nameof(settings.NovelFontWeight));
            weight.Value = FontWeight.SemiBold;
            await RenderAsync(window);
            Require(markdown.MarkdownFontSize == 24 && markdown.MarkdownLineHeight == 36 && markdown.MaxWidth == 600,
                "Font size, line height and maximum width reach the live Markdown control");
            Require(markdown.MarkdownFontWeight == FontWeight.SemiBold && markdown.MarkdownForeground is ISolidColorBrush { Color: var color } && color.ToUInt32() == settings.NovelFontColor,
                "Weight and custom foreground reach the live reader");
            Require(markdown.GetVisualAncestors().OfType<Border>().First().Background is ISolidColorBrush brush && brush.Color.ToUInt32() == settings.NovelBackground,
                "Custom background reaches the reading surface");
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = theme;
                await RenderAsync(window);
                var paragraph = markdown.GetVisualDescendants().OfType<CTextBlock>().First(text => text.Text.Contains("Reading presentation"));
                Require(paragraph.FontSize == 24 && paragraph.FontWeight == FontWeight.SemiBold
                    && paragraph.Foreground is ISolidColorBrush textBrush && textBrush.Color.ToUInt32() == 0xFF234567,
                    "Explicit reading presentation takes priority over the app theme");
                Save(window, directory, "reading-settings-" + theme.Key + "-wide");
            }
            report.Add("PASS generated controls: seven settings, numeric keyboard/ranges, color/weight and custom presentation across light/dark themes");

            var fontEntry = (ISingleValueSettingsEntry<ObservableCollection<string>>) entries.Single(entry => entry.Token == nameof(settings.NovelFontFamily));
            var oldFonts = fontEntry.Value;
            changes = 0;
            fontEntry.Value = ["Times New Roman"];
            Require(changes == 1 && model.NovelFontFamilyObject?.Name == "Times New Roman", "Replacing the font collection updates the reader once");
            changes = 0;
            oldFonts.Add("Courier New");
            Require(changes == 0, "The reader detaches from a replaced font collection");
            fontEntry.Value.Add("Arial");
            Require(changes == 1, "The replacement collection remains live");

            model.NextWorkCommand.Execute(null);
            (await fixture.NextAsync()).Complete("Another reading presentation");
            await UntilAsync(() => !model.IsLoading);
            await RenderAsync(window);
            var currentSettings = tabs.Pages!.OfType<SettingsSubView>().Single();
            Require(ReferenceEquals(settingsPage, currentSettings), "Changing novels retains the same settings editor and its input state");
            Require(ReferenceEquals(tabs.SelectedPage, settingsPage), "The selected reading-settings tab remains selected after switching works");
            await RenderAsync(window);
            changes = 0;
            fontEntry.Value.Add("Georgia");
            Require(changes == 1, "Changing novels does not multiply font subscriptions");
            report.Add("PASS lifetime: font replacement detaches old collection, reuses editors and preserves a single active subscription across works");

            window.Width = 360;
            await RenderAsync(window);
            await UntilAsync(() => currentSettings.Bounds.Width <= page.Bounds.Width);
            Save(window, directory, "reading-settings-narrow-before-scroll");
            Require(split.DisplayMode == SplitViewDisplayMode.Overlay && currentSettings.Bounds.Width <= page.Bounds.Width,
                $"The complete settings editor stays within the narrow overlay: settings={currentSettings.Bounds}, page={page.Bounds}, pane={split.OpenPaneLength}, mode={split.DisplayMode}");
            var scroller = currentSettings.GetVisualDescendants().OfType<ScrollViewer>().First();
            scroller.Offset = new(0, scroller.Extent.Height);
            await RenderAsync(window);
            Require(scroller.Offset.Y > 0, "The last reading settings remain reachable in the narrow pane");
            Save(window, directory, "reading-settings-narrow-bottom");
            File.WriteAllLines(Path.Combine(directory, "reading-settings-layout.txt"), currentSettings.GetVisualDescendants()
                .OfType<Control>().Where(control => control is NumericUpDown or ComboBox)
                .SelectMany(control => control.GetVisualAncestors().Prepend(control).OfType<Control>().Take(6)
                    .Select(ancestor => $"{control}: {ancestor} bounds={ancestor.Bounds} width={ancestor.Width} min={ancestor.MinWidth} max={ancestor.MaxWidth}")));
            foreach (var control in currentSettings.GetVisualDescendants().OfType<Control>().Where(control => control is NumericUpDown or ComboBox))
            {
                var position = control.TranslatePoint(default, currentSettings)!.Value;
                Require(position.X >= 0 && position.X + control.Bounds.Width <= currentSettings.Bounds.Width,
                    $"Generated reading value controls fit the narrow pane: {control} at {position}, {control.Bounds}, pane={currentSettings.Bounds}");
                var card = control.GetVisualAncestors().OfType<SettingsCard>().First();
                var cardPosition = control.TranslatePoint(default, card)!.Value;
                Require(cardPosition.X >= card.Padding.Left && cardPosition.X + control.Bounds.Width <= card.Bounds.Width - card.Padding.Right,
                    $"Reading value controls stay inside their card padding: {control} at {cardPosition}, card={card.Bounds}");
            }

            foreground.Color = Color.FromUInt32(0);
            background.Color = Color.FromUInt32(0);
            fontEntry.Value.Clear();
            await RenderAsync(window);
            Require(markdown.MarkdownForeground is null && markdown.MarkdownFontFamily is null,
                "Clearing optional colors/fonts restores theme and font fallback");
            AppInfo.SaveAppSettings(App.AppViewModel.AppSettings);
            var restored = AppInfo.LoadAppSettings(App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>())!.NovelSettings;
            Require(restored.NovelFontSize == settings.NovelFontSize && restored.NovelLineHeight == settings.NovelLineHeight
                && restored.NovelMaxWidth == settings.NovelMaxWidth && restored.NovelFontWeight == settings.NovelFontWeight
                && restored.NovelFontColor == 0 && restored.NovelBackground == 0 && restored.NovelFontFamily.Count == 0,
                "The original settings YAML round-trips all reading values without changing fields");
            host.Shell.ClosePage(page);
            changes = 0;
            fontEntry.Value.Add("Arial");
            fontEntry.Value = ["Georgia"];
            Require(changes == 0, "Closing the actual tab detaches collection and settings-entry notifications");
            report.Add("PASS narrow/fallback/persistence/close: bottom setting reachable, optional values reset, original YAML round-trip and no post-close callbacks");
            File.WriteAllLines(Path.Combine(directory, "novel-reading-settings-contracts.txt"), report);
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Reading settings make no unexpected service requests");
        }
        finally { window.Close(); }
    }
}
