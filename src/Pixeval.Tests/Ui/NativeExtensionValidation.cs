using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoSettingsPage;
using AutoSettingsPage.Avalonia;
using AutoSettingsPage.Models;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.Commands.Transformers;
using Pixeval.Models.Extensions;
using Pixeval.Utilities;
using Pixeval.Views;
using Pixeval.Views.Settings;

namespace Pixeval.Tests.Ui;

internal static class NativeExtensionValidation
{
    internal static async Task RunAsync(string directory)
    {
        var service = App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();
        Require(service.HostModels.Count is 1, "Exactly one isolated fixture host loads");
        var host = service.HostModels.Single();
        Require(host.Name is "Pixeval C++ SDK Demo", "The pinned official C++ fixture is loaded");
        Require(host.Host.SdkVersion == ExtensionService.CurrentVersion, "The fixture matches the application's SDK ABI");
        Require(host.Extensions.Count is 15, "All 15 fixture extensions are exposed");
        var group = service.SettingsGroups.Single();
        Require(group.Count is 8, "All eight generated extension settings are registered");
        var icon = host.Host.GetIcon(out var iconCount);
        Require(icon is { Length: > 0 } && icon.Length == iconCount, "Native icon buffer length matches its contents");
        using var iconStream = new MemoryStream(icon!);
        using (var bitmap = new Bitmap(iconStream))
            Require(bitmap.PixelSize.Width > 0 && bitmap.PixelSize.Height > 0, "The native icon is decodable");
        Console.WriteLine("Loaded native host, 15 extension interfaces and eight settings.");

        var text = service.ActiveTextTransformerCommands.Single();
        var transformed = await TextTransformation.RunAsync(text, "fixture 中文 🌸", TextTransformerType.Novel);
        Require(transformed is "C++ transformed: fixture 中文 🌸", "Text transformer preserves Unicode across the native boundary");
        using (var source = new MemoryStream([0, 1, 2, 127, 128, 255]))
        using (var destination = new MemoryStream())
        {
            await service.ActiveImageTransformerCommands.Single().TransformAsync(source, destination);
            Require(destination.ToArray().SequenceEqual(source.ToArray()), "Image transformer copies stream bytes without corruption");
        }
        host.IsActive = false;
        Require(!service.ActiveExtensions.Any() && !service.ActiveTextTransformerCommands.Any(), "Disabled hosts expose no active commands");
        host.IsActive = true;
        Require(service.ActiveExtensions.Count() is 15, "Re-enabling restores the same extension interfaces");

        var stringEntry = group.OfType<ISingleValueSettingsEntry<string>>().Single();
        var boolEntry = group.OfType<ISingleValueSettingsEntry<bool>>().Single();
        var intEntry = group.OfType<INumberSettingsEntry<int>>().Single();
        var enumEntry = group.OfType<IEnumSettingsEntry<object>>().Single();
        var originalEnum = enumEntry.Value;
        var colorEntry = group.OfType<ISingleValueSettingsEntry<uint>>().Single();
        var scaleEntry = group.OfType<ISingleValueSettingsEntry<double>>().Single();
        var dateEntry = group.OfType<ISingleValueSettingsEntry<DateTimeOffset>>().Single();
        var tagsEntry = group.OfType<ISingleValueSettingsEntry<ObservableCollection<string>>>().Single();
        Require(intEntry.Min is 0 && intEntry.Max is 10, "Native integer range is preserved");
        stringEntry.Value = "fixture 设置 ✓";
        boolEntry.Value = false;
        intEntry.Value = 7;
        enumEntry.Value = enumEntry.EnumItems.Last().Value;
        scaleEntry.Value = 1.25;
        colorEntry.Value = 0x80224466;
        dateEntry.Value = new DateTimeOffset(2026, 11, 7, 13, 24, 35, TimeSpan.FromMinutes(330));
        tagsEntry.Value = ["fixture 中文", "second"];
        foreach (var entry in group)
            entry.ValueSaving(host.Values);
        Require(Equals(host.Values[stringEntry.Token], "fixture 设置 ✓")
                && Equals(host.Values[boolEntry.Token], false)
                && Equals(host.Values[intEntry.Token], 7), "Setting values reach the host's persisted dictionary");
        AppInfo.SaveAppSettings(App.AppViewModel.AppSettings);
        var logger = App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>();
        var restored = AppInfo.LoadAppSettings(logger) ?? throw new InvalidOperationException("Settings reload failed.");
        File.WriteAllLines(Path.Combine(directory, "persisted-setting-types.txt"), restored.ExtensionSettings[host.Name]
            .Select(static pair => $"{pair.Key}: {pair.Value?.GetType().FullName} = {pair.Value}"));
        using (var reloaded = new ExtensionService(logger, restored))
        {
            var entries = reloaded.SettingsGroups.Single();
            Require(entries.OfType<ISingleValueSettingsEntry<string>>().Single().Value == stringEntry.Value, "Unicode string survives disk reload");
            Require(entries.OfType<ISingleValueSettingsEntry<bool>>().Single().Value == boolEntry.Value, "Boolean survives disk reload");
            Require(entries.OfType<ISingleValueSettingsEntry<int>>().Single().Value == intEntry.Value, "Integer survives disk reload");
            Require(Equals(entries.OfType<IEnumSettingsEntry<object>>().Single().Value, enumEntry.Value), "Native enum key survives disk reload without becoming a display label");
            Require(entries.OfType<ISingleValueSettingsEntry<double>>().Single().Value == scaleEntry.Value, "Fractional value survives disk reload");
            Require(entries.OfType<ISingleValueSettingsEntry<uint>>().Single().Value == colorEntry.Value, "ARGB uint survives disk reload");
            Require(entries.OfType<ISingleValueSettingsEntry<DateTimeOffset>>().Single().Value.EqualsExact(dateEntry.Value), "Date, time and offset survive disk reload");
            Require(entries.OfType<ISingleValueSettingsEntry<ObservableCollection<string>>>().Single().Value.SequenceEqual(tagsEntry.Value), "String collection survives disk reload");
        }

        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            await CaptureAsync(new ExtensionsPage(), $"extensions-{theme.Key}.png", theme);
            await CaptureAsync(new SettingsSubView(group), $"extension-settings-{theme.Key}.png", theme);
        }
        foreach (var entry in group)
            entry.ValueReset();
        Require(stringEntry.Value is "Pixeval C++ SDK Demo" && boolEntry.Value && intEntry.Value is 3,
            "Reset restores native defaults through the same settings entries");
        Require(Equals(enumEntry.Value, originalEnum), "Reset restores the native enum key");

        List<string> report =
        [
            "SDK source: Pixeval/Pixeval.Extensions@20c900a76cb427ad1dac38ed3f64463be2fd496b (Common 5.0.0 package metadata)",
            $"Host: {host.Name}; version: {host.Version}; SDK: {host.Host.SdkVersion}",
            $"Extensions: {host.Extensions.Count}; settings: {group.Count}; icon bytes: {iconCount}",
            "Passed: native Unicode text transform, stream copy, disable/re-enable, typed settings/range/default reset; both themes captured for inspection.",
            "Passed: color picker displays/edits/resets ARGB including alpha, with uint persistence in both themes; strings/bools/integers/doubles/ARGB/date offsets/string arrays survive disk reload into a second native host.",
            "Passed: date/time text editor preserves ticks and explicit offsets, rejects invalid/incomplete input without overwriting the model, and supports model-to-editor reset in both themes.",
            "Passed: fractional numeric values display without integer rounding and preserve the native range, increment and double persistence. Native platform input/scale/color popup acceptance is still pending.",
            "Passed: string keyboard edit/undo, boolean Space activation, integer keyboard stepping and enum item/key/model binding in both themes; enum keys also survive disk reload and default reset.",
            "Not covered: downloader/format-provider output semantics, async cancellation/failures, native platform UI, unload/reload lifecycle."
        ];
        report.AddRange(group.Cast<IExtensionSettingEntry>().Select(static entry => $"Setting: {entry.Token} / {entry.Header} / {entry.GetType().Name}"));
        File.WriteAllLines(Path.Combine(directory, "extension-contracts.txt"), report);
        Console.WriteLine("Native extension contracts passed; application disposal follows.");
        return;

        async Task CaptureAsync(Page page, string name, ThemeVariant theme)
        {
            var navigation = new NavigationPage();
            await navigation.PushAsync(page);
            var window = new Window { Width = 1000, Height = 900, RequestedThemeVariant = theme, Content = navigation };
            try
            {
                window.Show();
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                if (page is SettingsSubView)
                {
                    var stringInput = page.GetVisualDescendants().OfType<TextBox>().Single(input => ReferenceEquals(input.DataContext, stringEntry));
                    var originalText = stringEntry.Value;
                    Require(stringInput.Text == originalText && stringInput.Focus(), "Native string value displays and receives keyboard focus");
                    stringInput.SelectAll();
                    window.KeyTextInput("键盘 fixture 🌸");
                    Require(stringEntry.Value is "键盘 fixture 🌸", "String keyboard edits preserve Unicode in the native entry");
                    window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
                    window.KeyRelease(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
                    Require(stringEntry.Value == originalText, "String undo restores the original native setting");

                    var boolInput = page.GetVisualDescendants().OfType<ToggleSwitch>().Single(input => ReferenceEquals(input.DataContext, boolEntry));
                    var originalBool = boolEntry.Value;
                    Require(boolInput.IsChecked == originalBool && boolInput.Focus(), "Boolean setting displays and receives keyboard focus");
                    window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                    window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                    Require(boolEntry.Value != originalBool, "Space activates the existing boolean setting control");
                    boolEntry.Value = originalBool;
                    Require(boolInput.IsChecked == originalBool, "Boolean model reset updates the control");

                    var intInput = page.GetVisualDescendants().OfType<NumericUpDown>().Single(input => ReferenceEquals(input.DataContext, intEntry));
                    var originalInt = intEntry.Value;
                    Require(intInput.Value == originalInt && intInput.Minimum == intEntry.Min && intInput.Maximum == intEntry.Max && intInput.Increment == intEntry.Step,
                        "Integer editor retains native value, range and step");
                    Require(intInput.GetVisualDescendants().OfType<TextBox>().Single().Focus(), "Integer text field receives keyboard focus");
                    window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
                    window.KeyRelease(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
                    Require(intEntry.Value == originalInt + intEntry.Step, "Integer Up key uses the native step");
                    intEntry.Value = originalInt;
                    Require(intInput.Value == originalInt, "Integer model reset updates the control");

                    var enumInput = page.GetVisualDescendants().OfType<EnumComboBox>().Single(input => ReferenceEquals(input.DataContext, enumEntry));
                    var savedEnum = enumEntry.Value;
                    Require(Equals(enumInput.SelectedEnum, savedEnum) && enumInput.SelectedItem is IReadOnlyStringPair<object> pair && Equals(pair.Value, savedEnum),
                        "Enum editor selects the item matching the native key");
                    enumInput.SelectedIndex = 0;
                    Require(Equals(enumEntry.Value, enumEntry.EnumItems[0].Value), "Enum item selection writes its key to the existing native entry");
                    ((IExtensionSettingEntry) enumEntry).ValueSaving(host.Values);
                    Require(Equals(host.Values[enumEntry.Token], enumEntry.Value), "Enum persistence stores the key rather than the item or label");
                    enumEntry.Value = savedEnum;
                    Require(Equals(enumInput.SelectedEnum, savedEnum), "Enum model reset updates the same editor");

                    var numberInput = page.GetVisualDescendants().OfType<NumericUpDown>().Single(input => ReferenceEquals(input.DataContext, scaleEntry));
                    var numericEntry = (INumberSettingsEntry<double>) scaleEntry;
                    Require(numberInput.Value is { } numberValue && numberValue == (decimal) scaleEntry.Value
                            && numberInput.Text == numberValue.ToString("G", numberInput.NumberFormat), "Fractional native settings display their actual value");
                    Require(numberInput.Minimum == (decimal) numericEntry.Min && numberInput.Maximum == (decimal) numericEntry.Max && numberInput.Increment == (decimal) numericEntry.Step, "Native numeric range and increment are preserved");
                    var originalNumber = scaleEntry.Value;
                    numberInput.Value = 2.75m;
                    Require(scaleEntry.Value is 2.75, "Numeric editor updates the same native double entry");
                    ((IExtensionSettingEntry) scaleEntry).ValueSaving(host.Values);
                    Require(Equals(host.Values[scaleEntry.Token], 2.75), "Numeric saving preserves the original double representation");
                    scaleEntry.Value = originalNumber;
                    Require(numberInput.Value == (decimal) originalNumber, "Resetting the native double updates the same numeric editor");
                    var dateInput = page.GetVisualDescendants().OfType<TextBox>().Single(input => ReferenceEquals(input.DataContext, dateEntry));
                    Require(!string.IsNullOrWhiteSpace(dateInput.Text), "Date/time settings have an editable value control");
                    var originalDate = dateEntry.Value;
                    Require(dateInput.Text == originalDate.ToString("O", CultureInfo.InvariantCulture), "Date editor displays the full native value");
                    var editedDate = new DateTimeOffset(2024, 2, 29, 23, 45, 56, TimeSpan.FromMinutes(-210)).AddTicks(7654321);
                    Require(dateInput.Focus(), "Date editor can receive keyboard focus");
                    dateInput.SelectAll();
                    window.KeyTextInput(editedDate.ToString("O", CultureInfo.InvariantCulture));
                    Require(dateEntry.Value.EqualsExact(editedDate), "Editing preserves time, fractional ticks and non-hour offset");
                    window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
                    window.KeyRelease(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
                    Require(dateEntry.Value.EqualsExact(originalDate), "Undo restores the previous complete date value");
                    dateInput.SelectAll();
                    window.KeyTextInput(editedDate.ToString("O", CultureInfo.InvariantCulture));
                    var shiftedDate = editedDate.ToOffset(TimeSpan.FromMinutes(345));
                    dateInput.Text = shiftedDate.ToString("O", CultureInfo.InvariantCulture);
                    Require(dateEntry.Value.EqualsExact(shiftedDate), "An offset-only edit is retained even for the same UTC instant");
                    foreach (var invalid in new[] { "", "2026-11-07", "2026-02-29T12:00:00+08:00", "2026-11-07T13:24:35" })
                    {
                        dateInput.Text = invalid;
                        Require(dateInput.Text == invalid && dateEntry.Value.EqualsExact(shiftedDate), "Invalid date text remains editable without overwriting the valid value");
                        Require(DataValidationErrors.GetHasErrors(dateInput), "Invalid date input has a visible validation state");
                    }
                    window.UpdateLayout();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                    using (var invalidFrame = window.CaptureRenderedFrame()
                               ?? throw new InvalidOperationException("No invalid date frame was rendered."))
                        invalidFrame.Save(Path.Combine(directory, $"extension-date-invalid-{theme.Key}.png"), new PngBitmapEncoderOptions());
                    dateInput.Text = originalDate.ToString("O", CultureInfo.InvariantCulture);
                    Require(!DataValidationErrors.GetHasErrors(dateInput) && dateEntry.Value.EqualsExact(originalDate), "Correcting invalid input clears the error and restores binding");
                    dateEntry.Value = editedDate;
                    Require(dateInput.Text == editedDate.ToString("O", CultureInfo.InvariantCulture), "Model changes update the same editor");
                    ((IExtensionSettingEntry) dateEntry).ValueSaving(host.Values);
                    Require(host.Values[dateEntry.Token] is DateTimeOffset saved && saved.EqualsExact(editedDate), "Date saving retains the existing DateTimeOffset representation");
                    dateEntry.Value = originalDate;
                    var picker = page.GetVisualDescendants().OfType<ColorPicker>().Single();
                    Require(picker.Color.ToUInt32() == colorEntry.Value, "Color settings display the native ARGB value");
                    var original = colorEntry.Value;
                    picker.Color = Avalonia.Media.Color.FromUInt32(0x40667788);
                    Require(colorEntry.Value is 0x40667788, "Color picker preserves alpha and updates the native uint setting");
                    ((IExtensionSettingEntry) colorEntry).ValueSaving(host.Values);
                    Require(Equals(host.Values[colorEntry.Token], 0x40667788u), "Color persistence keeps the original uint format");
                    colorEntry.Value = original;
                    Require(picker.Color.ToUInt32() == original, "Resetting the setting updates the same color picker");
                }
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                using var frame = window.CaptureRenderedFrame()
                    ?? throw new InvalidOperationException("No native extension page frame was rendered.");
                frame.Save(Path.Combine(directory, name), new PngBitmapEncoderOptions());
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
        }
    }

    private static void Require(bool condition, string contract)
    {
        if (!condition)
            throw new InvalidOperationException("Native extension contract failed: " + contract);
    }
}
