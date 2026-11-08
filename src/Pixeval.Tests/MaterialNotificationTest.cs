using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Avalonia;
using Material3Expressive.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Utilities;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MaterialNotificationTest
{
    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        I18NManager.CandidatePaths.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Pixeval")));
        I18NManager.Register(new JsonMarkdownLangPlugin(), LanguageHelper.DefaultLanguage);
    }

    [TestMethod]
    [DataRow(false, 320)]
    [DataRow(true, 320)]
    [DataRow(false, 640)]
    [DataRow(true, 640)]
    public async Task SeverityAndCloseActionsRemainReadableAtNarrowWidths(bool dark, int width)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            var cards = Enum.GetValues<NotificationType>().Select(type => new NotificationCard
            {
                Content = new Notification("完整的通知标题 / Notification 🌸", "这是一段较长的错误或提示信息，用于确认窄窗口中内容会换行，关闭按钮仍然可操作。", type, TimeSpan.Zero)
            }).ToArray();
            var info = new InfoBar { Mode = InfoBarMode.Error, Text = "固定错误提示 / A persistent validation error" };
            var panel = new StackPanel { Spacing = 8, Children = { info } };
            foreach (var card in cards)
                panel.Children.Add(card);
            var window = new Window { Width = width, Height = 900, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = panel };
            try
            {
                window.Show();
                await SettleAsync(window);
                var symbols = cards.Select(card => card.GetVisualDescendants().OfType<SymbolIcon>().Single(icon => icon.Name is "PART_Icon").Symbol).ToArray();
                Assert.AreEqual(cards.Length, symbols.Distinct().Count(), "Severity must remain distinguishable without color.");
                foreach (var card in cards)
                {
                    Assert.IsTrue(card.GetVisualDescendants().OfType<MaterialSurface>().Any());
                    Assert.IsTrue(card.Bounds.Width <= width, $"Notification is wider than its {width} DIP viewport.");
                    var close = card.GetVisualDescendants().OfType<Button>().Single(button => button.Name is "PART_CloseButton");
                    Assert.IsTrue(close.Bounds.Width >= 48 && close.Bounds.Height >= 48, "Close must retain a usable target.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(AutomationProperties.GetName(close)), "The close icon needs an accessible name.");
                    var title = card.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "完整的通知标题 / Notification 🌸");
                    Assert.IsTrue(title.TextLayout.Height <= title.Bounds.Height + 1, "The complete title must wrap without cropping.");
                    Assert.IsTrue(title.TextLayout.Width <= title.Bounds.Width + 1);
                }
                Assert.IsTrue(info.GetVisualDescendants().OfType<MaterialSurface>().Any());
                var errorColor = ColorOf(info.Background);
                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
                await SettleAsync(window);
                Assert.AreNotEqual(errorColor, ColorOf(info.Background));
                window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
                await SettleAsync(window);
                Capture(window, $"notifications-{width}-{(dark ? "dark" : "light")}");
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ManagerPreservesDismissActionExpirationAndCapacity(bool dark)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            var window = new Window { Width = 640, Height = 720, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            try
            {
                window.Show();
                var manager = new WindowNotificationManager(window) { Position = NotificationPosition.BottomRight, MaxItems = 2 };
                Render(window);
                var clicks = 0;
                var closed = 0;
                var persistent = new Notification("Persistent", "Requires dismissal", NotificationType.Error, TimeSpan.Zero, () => clicks++, () => closed++);
                manager.Show(persistent);
                await SettleAsync(window);
                var card = FindCard(window, persistent);
                Assert.IsFalse(card.IsClosing);
                var close = card.GetVisualDescendants().OfType<Button>().Single(button => button.Name is "PART_CloseButton");
                Assert.IsTrue(close.Focus());
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                await UntilAsync(window, () => closed is 1);
                Assert.AreEqual(0, clicks, "Dismissing a notification must not invoke its action.");
                Assert.IsFalse(window.GetVisualDescendants().OfType<NotificationCard>().Any(candidate => ReferenceEquals(candidate.Content, persistent)));

                var actionable = new Notification("Open details", "Click this content", NotificationType.Information, TimeSpan.Zero, () => clicks++, () => closed++);
                manager.Show(actionable);
                await SettleAsync(window);
                card = FindCard(window, actionable);
                var message = card.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text is "Click this content");
                Click(window, message);
                Click(window, message);
                Assert.AreEqual(1, clicks, "Repeated presses during the exit animation must not execute the notification action again.");
                await UntilAsync(window, () => closed is 2);
                manager.Close(actionable);
                Assert.AreEqual(2, closed);

                var timed = new Notification("Short notice", null, NotificationType.Success, TimeSpan.FromMilliseconds(300), onClose: () => closed++);
                manager.Show(timed);
                // Observe the initial state before rendering pumps timers; a capture can
                // legitimately take longer than the notification's expiration interval.
                Assert.IsFalse(FindCard(window, timed).IsClosing, "Expiration must not be replaced by immediate dismissal.");
                await UntilAsync(window, () => closed is 3);
                Assert.AreEqual(1, clicks);

                var first = new Notification("First", null, expiration: TimeSpan.Zero, onClose: () => closed++);
                var second = new Notification("Second", null, expiration: TimeSpan.Zero, onClose: () => closed++);
                var third = new Notification("Third", null, expiration: TimeSpan.Zero, onClose: () => closed++);
                manager.Show(first);
                manager.Show(second);
                manager.Show(third);
                await UntilAsync(window, () => closed is 4);
                Assert.IsFalse(FindCard(window, second).IsClosing);
                Assert.IsFalse(FindCard(window, third).IsClosing);
                manager.CloseAll();
                await UntilAsync(window, () => closed is 6);
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task CustomContentKeepsCommandParametersAndCloseOptIn()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MaterialThemeIntegrationTest.MaterialTestApplication));
        _ = await session.Dispatch<bool>(async () =>
        {
            var parameter = new object();
            var calls = 0;
            var button = new Button { Content = "Retry", CommandParameter = parameter, Command = new RelayCommand<object>(value => { Assert.AreSame(parameter, value); calls++; }) };
            var card = new NotificationCard { Content = button };
            var window = new Window { Width = 500, Height = 300, Content = card };
            try
            {
                window.Show();
                await SettleAsync(window);
                Click(window, button);
                Assert.AreEqual(1, calls);
                Assert.IsFalse(card.IsClosing);
                NotificationCard.SetCloseOnClick(button, true);
                Click(window, button);
                Assert.AreEqual(2, calls);
                Assert.IsTrue(card.IsClosing);
                await UntilAsync(window, () => card.IsClosed);
            }
            finally
            {
                window.Close();
            }
            return true;
        }, CancellationToken.None);
    }

    private static NotificationCard FindCard(Window window, INotification notification) => window.GetVisualDescendants().OfType<NotificationCard>().Single(card => ReferenceEquals(card.Content, notification));

    private static Color ColorOf(IBrush? brush) => Assert.IsInstanceOfType<ISolidColorBrush>(brush).Color;

    private static async Task SettleAsync(Window window)
    {
        Render(window);
        await Task.Delay(400);
        Render(window);
    }

    private static async Task UntilAsync(Window window, Func<bool> predicate)
    {
        var timer = Stopwatch.StartNew();
        while (!predicate() && timer.Elapsed < TimeSpan.FromSeconds(3))
        {
            await Task.Delay(16);
            Render(window);
        }
        Assert.IsTrue(predicate(), "The notification did not complete its expected lifecycle transition.");
    }

    private static void Render(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        using var frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
    }

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)
            ?? throw new AssertFailedException("The notification content is detached.");
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static void Capture(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("PIXEVAL_NOTIFICATION_CAPTURE") is not { Length: > 0 } directory)
            return;
        _ = Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }
}
