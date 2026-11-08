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
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material3Expressive.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.Utilities;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class FloatingCommandBarTest
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Pixeval"));

        AppLocalization.Instance.SetLanguage("zh-Hans");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MaterialToolbarPreservesCommandsToggleAndDisabledState(bool dark)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            var model = new ToggleState();
            var calls = 0;
            var enabled = true;
            var parameter = new object();
            var command = new RelayCommand<object>(value =>
            {
                Assert.AreSame(parameter, value);
                calls++;
            }, _ => enabled);
            var action = Button("Rotate clockwise", command, parameter);
            var toggle = new CommandBarToggleButton
            {
                Label = "Mirror",
                Icon = Icon(),
                [!CommandBarToggleButton.IsCheckedProperty] = new Binding(nameof(ToggleState.IsChecked)) { Source = model, Mode = BindingMode.TwoWay }
            };
            var bar = CreateBar(action, toggle);
            var window = CreateWindow(bar, dark);
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                Render(window);
                Assert.IsTrue(action.GetVisualDescendants().OfType<MaterialSurface>().Any(), "Floating actions must use the Material interaction surface.");
                Assert.IsTrue(toggle.GetVisualDescendants().OfType<MaterialSurface>().Any());
                Assert.AreEqual(action.Label, ToolTip.GetTip(action));
                Assert.IsTrue(action.Focus());
                Press(window, Key.Enter);
                Assert.AreEqual(1, calls);
                Assert.IsTrue(toggle.Focus());
                Press(window, Key.Space);
                Assert.IsTrue(model.IsChecked);
                model.IsChecked = false;
                Assert.IsFalse(toggle.IsChecked);

                enabled = false;
                command.NotifyCanExecuteChanged();
                Render(window);
                Assert.IsFalse(action.IsEffectivelyEnabled);
                Click(window, action);
                Assert.AreEqual(1, calls, "Disabled pointer activation must not execute the command.");
                enabled = true;
                command.NotifyCanExecuteChanged();
                Render(window);
                Click(window, action);
                Assert.AreEqual(2, calls);
                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
                Render(window);
                Assert.IsTrue(action.Bounds.Height >= 40);
                Capture(window, dark ? "toolbar-dark-to-light" : "toolbar-light-to-dark");
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NarrowToolbarKeepsCommandsReachableAndUsesSameToggle(bool dark)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        await session.Dispatch(() =>
        {
            var calls = 0;
            var enabled = true;
            var command = new RelayCommand(() => calls++, () => enabled);
            var action = Button("Rotate a very long localized illustration title 中文", command);
            var toggle = new CommandBarToggleButton { Label = "Mirror illustration", Icon = Icon(), IsChecked = true };
            var zoomText = new TextBlock { Text = "125%" };
            var zoom = new CommandBarElementContainer { Content = zoomText };
            var bar = CreateBar(Button("Zoom out", new RelayCommand(() => { })), zoom,
                Button("Zoom in", new RelayCommand(() => { })), new CommandBarSeparator(), toggle, action);
            bar.Width = 180;
            var window = CreateWindow(bar, dark);
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show();
                Render(window);
                Assert.IsTrue(bar.IsOverflowButtonVisible, "Narrow toolbars must offer the commands that no longer fit.");
                Assert.IsTrue(action.IsInOverflow);
                Assert.IsTrue(toggle.IsInOverflow);
                var more = bar.GetVisualDescendants().OfType<Button>().Single(button => button.Name is "PART_OverflowButton");
                Assert.AreEqual(Strings.MiscMoreCommands(), ToolTip.GetTip(more));
                Assert.AreEqual(Strings.MiscMoreCommands(), AutomationProperties.GetName(more));
                Click(window, more);
                Render(window);
                Assert.IsTrue(bar.IsOpen);
                Assert.IsTrue(action.IsEffectivelyVisible);
                var label = action.GetVisualDescendants().OfType<ContentPresenter>().Single(presenter => presenter.Name is "PART_Label");
                Assert.IsTrue(label.Bounds.Width > 0 && label.Bounds.Height > 0);
                var text = label.GetVisualDescendants().OfType<TextBlock>().Single();
                Assert.IsTrue(text.TextLayout.Width <= text.Bounds.Width + 1, "The full localized label must fit or wrap in overflow.");
                Assert.IsTrue(text.TextLayout.Height <= text.Bounds.Height + 1, "Wrapped command labels must not lose their last line.");
                Capture(window, dark ? "toolbar-overflow-dark" : "toolbar-overflow-light");
                Assert.IsTrue(toggle.IsChecked);
                enabled = false;
                command.NotifyCanExecuteChanged();
                Render(window);
                Assert.IsFalse(action.IsEffectivelyEnabled);
                Click(window, action);
                Assert.AreEqual(0, calls);
                enabled = true;
                command.NotifyCanExecuteChanged();
                Render(window);
                Click(window, action);
                Assert.AreEqual(1, calls);

                bar.IsOpen = true;
                Render(window);
                Click(window, toggle);
                Assert.IsFalse(toggle.IsChecked);
                var outside = new Point(window.ClientSize.Width - 10, window.ClientSize.Height - 10);
                window.MouseMove(outside);
                window.MouseDown(outside, MouseButton.Left);
                window.MouseUp(outside, MouseButton.Left);
                Render(window);
                Assert.IsFalse(bar.IsOpen, "Clicking outside the overflow must dismiss it.");
                bar.Width = 600;
                Render(window);
                Assert.IsFalse(action.IsInOverflow);
                Assert.IsFalse(toggle.IsInOverflow);
                Assert.IsFalse(toggle.IsChecked, "Returning to the main strip must preserve the same toggle state.");
                Assert.AreSame(toggle, bar.PrimaryCommands[4]);
                Assert.IsTrue(zoomText.IsEffectivelyVisible && zoomText.Bounds.Width > 0);
                Assert.IsFalse(zoom.Focusable);
                action.IsVisible = false;
                Render(window);
                Assert.IsFalse(action.IsEffectivelyVisible);
                action.IsVisible = true;
                Render(window);
                Assert.IsTrue(action.IsEffectivelyVisible);
                Capture(window, dark ? "toolbar-wide-dark" : "toolbar-wide-light");
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    private static CommandBar CreateBar(params ICommandBarElement[] commands)
    {
        var bar = new CommandBar
        {
            Classes = { "floating-command-bar" },
            HorizontalAlignment = HorizontalAlignment.Left,
            Theme = (ControlTheme) Application.Current!.FindResource("FloatingCommandBarTheme")!,
            PrimaryCommands = [.. commands]
        };
        return bar;
    }

    private static Window CreateWindow(CommandBar bar, bool dark) => new()
    {
        Width = 800,
        Height = 600,
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        Content = new StackPanel { Margin = new Thickness(24), Children = { bar } }
    };

    private static Border Icon() => new() { Width = 20, Height = 20, Background = Brushes.Gray };

    private static CommandBarButton Button(string label, System.Windows.Input.ICommand command, object? parameter = null) => new()
    {
        Label = label,
        Icon = Icon(),
        Command = command,
        CommandParameter = parameter
    };

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)
            ?? throw new AssertFailedException("The command is detached from its window.");
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static void Press(Window window, Key key)
    {
        var physical = key is Key.Enter ? PhysicalKey.Enter : PhysicalKey.Space;
        var text = key is Key.Enter ? "\r" : " ";
        window.KeyPress(key, RawInputModifiers.None, physical, text);
        window.KeyRelease(key, RawInputModifiers.None, physical, text);
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
        _ = Directory.CreateDirectory(directory);
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
