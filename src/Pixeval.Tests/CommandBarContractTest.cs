using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material3Expressive.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Localization;
using Pixeval.Utilities;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CommandBarContractTest
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        var project = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Pixeval"));

        AppLocalization.Instance.SetLanguage("zh-Hans");
    }

    [TestMethod]
    [DataRow(false, CommandBarDefaultLabelPosition.Right)]
    [DataRow(true, CommandBarDefaultLabelPosition.Right)]
    [DataRow(false, CommandBarDefaultLabelPosition.Bottom)]
    [DataRow(true, CommandBarDefaultLabelPosition.Bottom)]
    [DataRow(false, CommandBarDefaultLabelPosition.Collapsed)]
    [DataRow(true, CommandBarDefaultLabelPosition.Collapsed)]
    public async Task OrdinaryBarKeepsCommandsReachableAcrossOverflow(bool dark, CommandBarDefaultLabelPosition position)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            var calls = 0;
            var enabled = true;
            var parameter = new object();
            var command = new RelayCommand<object>(value =>
            {
                Assert.AreSame(parameter, value);
                calls++;
            }, _ => enabled);
            var model = new ToggleState();
            var action = Action("Save", command, parameter);
            var toggle = new CommandBarToggleButton
            {
                Label = "Selection 中文",
                Icon = Icon(),
                [!CommandBarToggleButton.IsCheckedProperty] = new Binding(nameof(ToggleState.IsChecked)) { Source = model, Mode = BindingMode.TwoWay }
            };
            var secondary = Action("Open the original artwork in the default browser 打开原作品并保留完整的本地化命令名称", command, parameter);
            var bar = new CommandBar
            {
                DefaultLabelPosition = position,
                PrimaryCommands = { action, new CommandBarSeparator(), toggle },
                SecondaryCommands = { secondary }
            };
            var window = new Window
            {
                Width = 900,
                Height = 700,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
                Content = new StackPanel { Margin = new Thickness(20), Children = { bar } }
            };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                Render(window);
                Assert.IsTrue(action.GetVisualDescendants().OfType<MaterialSurface>().Any());
                Assert.IsTrue(action.Focus());
                Press(window, Key.Enter);
                Assert.AreEqual(1, calls);
                Assert.IsTrue(toggle.Focus());
                Press(window, Key.Space);
                Assert.IsTrue(model.IsChecked);
                model.IsChecked = false;
                Assert.IsFalse(toggle.IsChecked);
                model.IsChecked = true;

                bar.Width = 120;
                Render(window);
                Assert.IsTrue(bar.IsOverflowButtonVisible);
                Assert.IsTrue(toggle.IsInOverflow, "The trailing toggle must move into the narrow bar's overflow.");
                var more = bar.GetVisualDescendants().OfType<Button>().Single(button => button.Name is "PART_OverflowButton");
                Assert.AreEqual(Strings.MiscMoreCommands(), AutomationProperties.GetName(more));
                Assert.AreEqual(Strings.MiscMoreCommands(), ToolTip.GetTip(more));
                Assert.IsTrue(more.Focus());
                Press(window, Key.Enter);
                Render(window);
                Assert.IsTrue(bar.IsOpen);
                Assert.IsTrue(secondary.IsInOverflow && secondary.IsEffectivelyVisible);
                AssertCompleteLabel(secondary);
                Assert.IsTrue(toggle.IsChecked);
                enabled = false;
                command.NotifyCanExecuteChanged();
                Render(window);
                Assert.IsFalse(action.IsEffectivelyEnabled);
                Assert.IsFalse(secondary.IsEffectivelyEnabled);
                Click(window, secondary);
                Assert.AreEqual(1, calls, "A disabled overflow action cannot execute.");
                enabled = true;
                command.NotifyCanExecuteChanged();
                Render(window);
                Click(window, secondary);
                Assert.AreEqual(2, calls, "The overflow action executes exactly once with its original parameter.");
                bar.IsOpen = true;
                Render(window);
                Click(window, toggle);
                Assert.IsFalse(model.IsChecked);
                Capture(window, $"ordinary-{position}-{dark}-overflow");
                Press(window, Key.Escape);
                Render(window);
                Assert.IsFalse(bar.IsOpen);

                bar.ClearValue(Control.WidthProperty);
                Render(window);
                Assert.IsFalse(toggle.IsInOverflow);
                Assert.AreSame(toggle, bar.PrimaryCommands[2]);
                Assert.IsFalse(toggle.IsChecked);
                action.IsVisible = false;
                Render(window);
                Assert.IsFalse(action.IsEffectivelyVisible);
                action.IsVisible = true;
                secondary.Label += " 动态更新的长命令名称";
                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
                bar.IsOpen = true;
                Render(window);
                AssertCompleteLabel(secondary);
                var late = Action("Added after opening", command, parameter);
                bar.SecondaryCommands.Add(late);
                Render(window);
                Assert.IsTrue(late.IsInOverflow && late.IsEffectivelyVisible);
                Click(window, late);
                Assert.AreEqual(3, calls);
                bar.SecondaryCommands.Remove(late);
                bar.IsOpen = false;
                Render(window);
                Capture(window, $"ordinary-{position}-{dark}-wide");
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
        }, CancellationToken.None);
    }

    private static void AssertCompleteLabel(CommandBarButton button)
    {
        var label = button.GetVisualDescendants().OfType<ContentPresenter>().Single(p => p.Name is "PART_Label");
        var text = label.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.IsTrue(text.Bounds.Width > 0 && text.Bounds.Height > 0);
        Assert.IsTrue(text.TextLayout.Width <= text.Bounds.Width + 1, $"Long overflow label is clipped horizontally: {text.TextLayout.Width} > {text.Bounds.Width}.");
        Assert.IsTrue(text.TextLayout.Height <= text.Bounds.Height + 1, "Wrapped label loses its last line.");
    }

    private static Border Icon() => new() { Width = 20, Height = 20, Background = Brushes.Gray };
    private static CommandBarButton Action(string label, System.Windows.Input.ICommand command, object parameter) =>
        new() { Label = label, Icon = Icon(), Command = command, CommandParameter = parameter };

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)
            ?? throw new AssertFailedException("Command is detached.");
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Render(window);
    }

    private static void Press(Window window, Key key)
    {
        var physical = key switch { Key.Enter => PhysicalKey.Enter, Key.Space => PhysicalKey.Space, _ => PhysicalKey.Escape };
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
    }

    private static void Render(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        using var frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
    }

    private static void Capture(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("PIXEVAL_TOOLBAR_CAPTURE") is not { Length: > 0 } directory)
            return;
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private sealed class ToggleState : ObservableObject
    {
        public bool IsChecked
        {
            get;
            set => SetProperty(ref field, value);
        }
    }
}
