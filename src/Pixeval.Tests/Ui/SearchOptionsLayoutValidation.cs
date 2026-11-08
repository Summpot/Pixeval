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
using Mako.Global.Enum;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.ViewModels.Search;
using Pixeval.Views.Search;

namespace Pixeval.Tests.Ui;

internal static class SearchOptionsLayoutValidation
{
    internal static async Task RunAsync(string directory)
    {
        List<string> report = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 360, 1000 })
                foreach (var novel in new[] { false, true })
                {
                    SearchArgumentsFormViewModelBase model = novel ? new NovelSearchFormViewModel() : new IllustrationSearchFormViewModel();
                    ContentPage page = novel ? new NovelSearchOptionsPage() : new IllustrationSearchOptionsPage();
                    page.DataContext = model;
                    var scroll = new ScrollViewer { Content = page, Margin = new Thickness(20), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
                    var window = new Window { Width = width, Height = 600, RequestedThemeVariant = theme, Content = scroll };
                    var scenario = $"{theme.Key}-{width}-{(novel ? "Novel" : "Illustration")}";
                    try
                    {
                        window.Show();
                        await RenderAsync(window);
                        var rows = page.GetVisualDescendants().OfType<Grid>().Where(grid => grid.Children.FirstOrDefault() is TextBlock).ToArray();
                        Require(rows.Length >= 14, "The full form is present, including its common and type-specific fields");
                        Save(window, directory, "search-options-top-" + scenario);
                        foreach (var row in rows)
                        {
                            var label = (TextBlock) row.Children[0];
                            HorizontalBounds(window, row);
                            Require(label.TextLayout.Width <= label.Bounds.Width + 1 && label.TextLayout.Height <= label.Bounds.Height + 1
                                && label.TextLayout.TextLines.All(line => !line.HasCollapsed), $"The complete localized label is readable: {label.Text}");
                            foreach (var control in row.Children.Skip(1))
                            {
                                HorizontalBounds(window, control);
                                Require(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)), $"The field has an accessible name: {label.Text}");
                                if (control is SymbolComboBox symbol)
                                    Require(AutomationProperties.GetName(symbol.GetVisualDescendants().OfType<ComboBox>().Single()) == label.Text,
                                        "The focusable inner selector carries the field name, not only its selected value");
                            }
                        }

                        var dates = page.GetVisualDescendants().OfType<CalendarDatePicker>().ToArray();
                        var startRow = (Grid) dates[0].Parent!;
                        var startToggle = startRow.Children.OfType<ToggleSwitch>().Single();
                        model.StartDateOption.Value = new DateTime(2020, 2, 3);
                        model.StartDateOption.IsEnabled = false;
                        await RevealAsync(window, startToggle);
                        Require(!dates[0].IsEffectivelyEnabled, "Disabled date preserves its value without enabling its editor");
                        Activate(window, startToggle);
                        Require(model.StartDateOption.IsEnabled && dates[0].IsEffectivelyEnabled && dates[0].SelectedDate == model.StartDateOption.Value,
                            "Keyboard date enable updates the form and restores its original date");
                        var dateEditor = dates[0].GetVisualDescendants().OfType<TextBox>().Single();
                        Require(dateEditor.Focus(NavigationMethod.Tab), "The enabled date is editable from the keyboard");
                        dateEditor.SelectAll();
                        window.KeyTextInput(new DateTime(2021, 4, 5).ToString("d", System.Globalization.CultureInfo.CurrentCulture));
                        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
                        await RenderAsync(window);
                        Require(model.StartDateOption.Value == new DateTime(2021, 4, 5), "Localized date text commits through the original date picker binding");
                        Activate(window, startToggle);
                        Require(!model.StartDateOption.IsEnabled && model.StartDateOption.Value == new DateTime(2021, 4, 5), "Disabling a date keeps its editable value");

                        await ToggleAsync(SearchResources.Common.ShowAiWorks, () => model.AiType);
                        await ToggleAsync(SearchResources.Common.MergePlainKeywordResults, () => model.MergePlainKeywordResults);
                        await ToggleAsync(SearchResources.Common.IncludeTranslatedTagResults, () => model.IncludeTranslatedTagResults);
                        await ToggleAsync(SearchResources.Common.IncludePotentialViolationWorks, () => model.IncludePotentialViolationWorks);
                        await SelectSymbolAsync(SearchResources.Common.SortOption, () => model.SortOption);

                        if (model is IllustrationSearchFormViewModel illustration)
                        {
                            await SelectSymbolAsync(SearchResources.Illustration.MatchOption, () => illustration.MatchOption);
                            await SelectSymbolAsync(SearchResources.Illustration.ContentType, () => illustration.ContentType);
                            await SelectSymbolAsync(SearchResources.Illustration.RatioPattern, () => illustration.RatioPattern);
                            var numbers = page.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
                            var options = new[] { illustration.WidthMinOption, illustration.WidthMaxOption, illustration.HeightMinOption, illustration.HeightMaxOption };
                            Require(numbers.Length == options.Length, "All illustration dimension fields are retained");
                            for (var index = 0; index < numbers.Length; index++)
                            {
                                var toggle = ((Grid) numbers[index].Parent!).Children.OfType<ToggleSwitch>().Single();
                                await RevealAsync(window, toggle);
                                Activate(window, toggle);
                                Require(options[index].IsEnabled && numbers[index].IsEffectivelyEnabled, "Dimension enable maps to the corresponding model option");
                                await EditNumberAsync(window, numbers[index], 320 + index * 100);
                                Require(options[index].Value == 320 + index * 100, "Dimension keyboard input updates only its intended option");
                                Activate(window, toggle);
                                Require(!options[index].IsEnabled && options[index].Value == 320 + index * 100, "Dimension disable retains the entered number");
                            }
                        }
                        else if (model is NovelSearchFormViewModel fiction)
                        {
                            await SelectSymbolAsync(SearchResources.Novel.MatchOption, () => fiction.MatchOption);
                            await SelectSymbolAsync(SearchResources.Novel.ContentLengthOption, () => fiction.ContentLengthOption);
                            await ToggleAsync(SearchResources.Novel.IsOriginalOnly, () => fiction.IsOriginalOnly);
                            await ToggleAsync(SearchResources.Novel.IsReplaceableOnly, () => fiction.IsReplaceableOnly);
                            fiction.ContentLengthOption = SearchNovelContentLengthOption.WordCount;
                            var numbers = page.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
                            var options = new[] { fiction.ContentLengthMinOption, fiction.ContentLengthMaxOption };
                            for (var index = 0; index < numbers.Length; index++)
                            {
                                var toggle = ((Grid) numbers[index].Parent!).Children.OfType<ToggleSwitch>().Single();
                                await RevealAsync(window, toggle);
                                Activate(window, toggle);
                                await EditNumberAsync(window, numbers[index], 5000 + index * 14999);
                                Require(options[index].IsEnabled && options[index].Value == 5000 + index * 14999, "Novel length edit preserves its range and unit mapping");
                            }
                            fiction.ContentLengthOption = SearchNovelContentLengthOption.None;
                            await RenderAsync(window);
                            Require(numbers.All(control => !control.IsEffectivelyEnabled) && fiction.ContentLengthMinOption.Value == 5000,
                                "Removing length filtering disables its fields and keeps their values");
                            fiction.ContentLengthOption = SearchNovelContentLengthOption.WordCount;
                        }

                        var last = rows[^1].Children[^1];
                        await RevealAsync(window, last);
                        Save(window, directory, "search-options-bottom-" + scenario);
                        var originalRows = rows;
                        window.Width = width == 360 ? 1000 : 360;
                        await RenderAsync(window);
                        Require(originalRows.SequenceEqual(page.GetVisualDescendants().OfType<Grid>().Where(grid => grid.Children.FirstOrDefault() is TextBlock)),
                            "Resizing keeps the same fields, bindings and editor instances");
                        foreach (var row in originalRows)
                            foreach (var control in row.Children)
                                HorizontalBounds(window, control);
                        report.Add($"PASS {scenario}: complete labels and field bounds/names, date enable/value, all numeric ranges, keyboard scroll and resize preservation");

                        async Task ToggleAsync(string resource, Func<bool> value)
                        {
                            var toggle = rows.SelectMany(row => row.Children).OfType<ToggleSwitch>()
                                .Single(control => AutomationProperties.GetName(control) == I18NManager.GetResource(resource));
                            await RevealAsync(window, toggle);
                            var before = value();
                            Activate(window, toggle);
                            Require(value() != before && value() == toggle.IsChecked, "Keyboard option toggle writes the corresponding form flag");
                            Activate(window, toggle);
                            Require(value() == before, "Toggling back restores the original flag");
                        }

                        async Task SelectSymbolAsync(string resource, Func<object> value)
                        {
                            var selector = page.GetVisualDescendants().OfType<SymbolComboBox>()
                                .Single(control => AutomationProperties.GetName(control) == I18NManager.GetResource(resource));
                            await RevealAsync(window, selector);
                            var inner = selector.GetVisualDescendants().OfType<ComboBox>().Single();
                            var next = selector.SelectedIndex == 0 ? 1 : selector.SelectedIndex - 1;
                            var key = selector.SelectedIndex == 0 ? Key.Down : Key.Up;
                            Require(inner.Focus(NavigationMethod.Tab), "The named option selector is keyboard reachable");
                            window.KeyPress(key, RawInputModifiers.None, key == Key.Down ? PhysicalKey.ArrowDown : PhysicalKey.ArrowUp, "");
                            window.KeyRelease(key, RawInputModifiers.None, key == Key.Down ? PhysicalKey.ArrowDown : PhysicalKey.ArrowUp, "");
                            await RenderAsync(window);
                            Require(Equals(value(), selector.ItemsSource![next].Value), "Keyboard selection writes the actual option value, not its localized label");
                        }
                    }
                    finally { window.Content = null; window.Close(); }
                }
        File.WriteAllLines(Path.Combine(directory, "search-options-layout-contracts.txt"), report);
    }

    private static async Task EditNumberAsync(Window window, NumericUpDown number, int value)
    {
        await RevealAsync(window, number);
        var editor = number.GetVisualDescendants().OfType<TextBox>().First();
        Require(editor.Focus(NavigationMethod.Tab), "Numeric editor remains keyboard reachable");
        editor.SelectAll();
        window.KeyTextInput(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
        await RenderAsync(window);
    }

    private static void Activate(Window window, Control control)
    {
        Require(control.Focus(NavigationMethod.Tab), "Field toggle remains keyboard reachable");
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
    }

    private static async Task RevealAsync(Window window, Control control)
    {
        control.BringIntoView();
        await Task.Delay(500); // Use the production smooth-scroll duration.
        await RenderAsync(window);
        HorizontalBounds(window, control);
        var point = control.TranslatePoint(default, window)!.Value;
        Require(point.Y >= -1 && point.Y + control.Bounds.Height <= window.Bounds.Height + 1, "The scrolled field is completely visible");
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(60);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static void HorizontalBounds(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window);
        Require(point is { } p && p.X >= -1 && p.X + control.Bounds.Width <= window.Bounds.Width + 1 && control.Bounds.Width > 0,
            $"{control.GetType().Name} remains inside the {window.Width} DIP form (position={point}, width={control.Bounds.Width})");
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No search form frame.");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }
}
