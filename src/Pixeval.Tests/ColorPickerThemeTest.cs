using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ColorPickerThemeTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ColorModelLabelsFitTheirNativeComponentRow(bool usePicker)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            var view = usePicker ? new ColorPicker() : new ColorView { Width = 352 };
            var window = new Window { Width = 480, Height = 480, Content = view };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                window.UpdateLayout();
                Control root = view;
                if (usePicker)
                {
                    var button = view.GetVisualDescendants().OfType<DropDownButton>().Single();
                    var flyout = Assert.IsInstanceOfType<Flyout>(button.Flyout);
                    flyout.ShowAt(button);
                    root = Assert.IsInstanceOfType<Control>(flyout.Content);
                }
                window.UpdateLayout();
                root.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex = 2;
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                {
                    Application.Current!.RequestedThemeVariant = theme;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var radios = root.GetVisualDescendants().OfType<RadioButton>().ToArray();
                    Assert.HasCount(2, radios);
                    foreach (var radio in radios)
                    {
                        var label = radio.GetVisualDescendants().OfType<TextBlock>().Single();
                        var origin = label.TranslatePoint(default, radio)!.Value;
                        Assert.IsTrue(label.Bounds.Height >= label.TextLayout.Height && origin.Y >= 0
                            && origin.Y + label.Bounds.Height <= radio.Bounds.Height,
                            $"{radio.Content}: label {label.Bounds}, origin {origin}, text {label.TextLayout.Height}, radio {radio.Bounds} ({theme}).");
                        var color = view.Color;
                        Assert.IsTrue(radio.Focus());
                        var topLevel = TopLevel.GetTopLevel(radio)!;
                        topLevel.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                        topLevel.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                        Assert.AreEqual(radio.Name is "RgbRadioButton" ? ColorModel.Rgba : ColorModel.Hsva, view.ColorModel);
                        Assert.AreEqual(color, view.Color, "Changing the component model must not alter the selected color.");
                    }
                }
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task CurrentColorPreviewKeepsItsAreaAcrossColorAndThemeChanges()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            var picker = new ColorPicker { Color = Color.FromArgb(255, 51, 102, 204) };
            var window = new Window { Width = 480, Height = 320, Content = picker };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
                {
                    Application.Current!.RequestedThemeVariant = theme;
                    foreach (var color in new[] { Color.FromArgb(255, 51, 102, 204), Color.FromArgb(128, 17, 34, 51) })
                    {
                        picker.Color = color;
                        window.UpdateLayout();
                        Dispatcher.UIThread.RunJobs();
                        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
                        var swatch = picker.GetVisualDescendants().OfType<Border>()
                            .Single(border => border.Background is ISolidColorBrush brush && brush.Color == color);
                        Assert.IsTrue(swatch.Bounds.Width >= 16 && swatch.Bounds.Height >= 16,
                            $"The current-color preview is collapsed: {swatch.Bounds} ({theme}, {color}).");
                        var button = picker.GetVisualDescendants().OfType<DropDownButton>().Single();
                        Assert.IsNotNull(button.Flyout);
                        Assert.IsTrue(button.Focus());
                        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                        Dispatcher.UIThread.RunJobs();
                        Assert.IsTrue(button.Flyout.IsOpen, "The color dropdown must still open from the keyboard.");
                        button.Flyout.Hide();
                    }
                }
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }
}
