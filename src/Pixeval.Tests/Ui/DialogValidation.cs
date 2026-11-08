using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pixeval.Controls;
using Pixeval.I18N;

namespace Pixeval.Tests.Ui;

internal static class DialogValidation
{
    internal static async Task RunAsync(string directory)
    {
        List<string> report = [];
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 1000, 360 })
            {
                var origin = new Button { Content = "Open fixture confirmation" };
                // Use the production shell; external update checks are supplied by the session.
                var container = new PageTestHost { Content = origin };
                var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = container.Shell };
                try
                {
                    window.Show();
                    await RenderAsync(window);
                    Require(origin.Focus(), "The confirmation entry must receive focus.");
                    foreach (var confirm in new[] { false, true })
                    {
                        var result = container.Shell.CreateOkCancelAsync("确认操作 / Confirmation", "保留原有确认与取消结果，关闭后返回调用入口。");
                        await RenderAsync(window);
                        var dialog = window.GetVisualDescendants().OfType<ContentDialog>().Single();
                        Require(dialog.PrimaryButtonText == I18NManager.GetResource(ContentDialogResources.OkButtonContent), "Localized confirmation label changed.");
                        Require(dialog.CloseButtonText == I18NManager.GetResource(ContentDialogResources.CancelButtonContent), "Localized cancellation label changed.");
                        if (confirm)
                        {
                            Save(window, directory, $"confirmation-{theme.Key}-{width}");
                            Click(window, dialog.GetVisualDescendants().OfType<Button>().Single(button => button.Name is "PART_PrimaryButton"));
                        }
                        else
                            PressKey(window, Key.Escape, PhysicalKey.Escape);
                        Require(await result.WaitAsync(TimeSpan.FromSeconds(3)) == (confirm ? ContentDialogResult.Primary : ContentDialogResult.Close), "Confirmation result changed.");
                        Require(ReferenceEquals(origin, window.FocusManager.GetFocusedElement()), "Confirmation must restore its entry focus.");
                    }

                    var acknowledgement = container.Shell.CreateAcknowledgementAsync("错误详情 / Error details", "持久错误必须等待用户确认，不能变成自动消失的通知。");
                    await RenderAsync(window);
                    PressKey(window, Key.Escape, PhysicalKey.Escape);
                    Require(!acknowledgement.IsCompleted, "Escape must not invent an unavailable close action.");
                    PressKey(window, Key.Enter, PhysicalKey.Enter);
                    Require(await acknowledgement.WaitAsync(TimeSpan.FromSeconds(3)) is ContentDialogResult.Primary, "Acknowledgement must retain its default action.");

                    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    var pending = container.Shell.CreateAcknowledgementTaskAsync("异步操作 / Async operation", async dialog =>
                    {
                        dialog.IsPrimaryButtonEnabled = false;
                        await release.Task;
                        dialog.Content = "操作完成 / Complete";
                        dialog.IsPrimaryButtonEnabled = true;
                        updated.SetResult();
                    });
                    await RenderAsync(window);
                    var progress = window.GetVisualDescendants().OfType<ContentDialog>().Single();
                    Require(progress.Content is ProgressBar { IsIndeterminate: true }, "Async acknowledgement lost its progress state.");
                    PressKey(window, Key.Enter, PhysicalKey.Enter);
                    Require(!pending.IsCompleted, "A disabled default action must not complete a pending operation.");
                    release.SetResult();
                    await updated.Task.WaitAsync(TimeSpan.FromSeconds(3));
                    await RenderAsync(window);
                    Require(progress.Content is "操作完成 / Complete", "Async content update was lost.");
                    Save(window, directory, $"acknowledgement-{theme.Key}-{width}");
                    PressKey(window, Key.Enter, PhysicalKey.Enter);
                    Require(await pending.WaitAsync(TimeSpan.FromSeconds(3)) is ContentDialogResult.Primary, "Completed acknowledgement result changed.");
                    Require(ReferenceEquals(origin, window.FocusManager.GetFocusedElement()), "Async acknowledgement must restore focus.");
                    Require(!window.GetVisualDescendants().OfType<ContentDialog>().Any(), "Closed dialogs must leave the visual tree.");
                    report.Add($"PASS {theme.Key}/{width}: ViewContainerBase confirmation/cancel, localized labels, pointer hit, persistent acknowledgement, disabled default action, async content and focus restoration");
                }
                finally
                {
                    window.Close();
                }
            }
        report.Add("Only managed focus and headless input are covered; native IME, system focus, touch and DPI remain pending.");
        File.WriteAllLines(Path.Combine(directory, "dialog-contracts.txt"), report);
        Console.WriteLine($"Dialog consumer contracts passed: {report.Count - 1} theme/width scenarios.");
    }

    private static void PressKey(Window window, Key key, PhysicalKey physicalKey)
    {
        window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
        window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
    }

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;
        Require(window.InputHitTest(point) is Visual hit && (ReferenceEquals(hit, control) || hit.GetVisualAncestors().Contains(control)), "The dialog action is not reachable through the rendered overlay.");
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No dialog frame.");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(condition, contract);
    }
}
