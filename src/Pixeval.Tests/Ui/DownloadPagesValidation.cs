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
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Config;
using Pixeval.Controls;
using Pixeval.Download;
using Pixeval.Views.Download;

namespace Pixeval.Tests.Ui;

internal static class DownloadPagesValidation
{
    internal static async Task RunAsync(string directory)
    {
        // The Rust manager publishes value snapshots; UI models never own backend tasks.
        using var manager = new DownloadManager(1);
        using var model = new DownloadPageViewModel(manager);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        foreach (var width in new[] { 360d, 1000d })
        {
            List<DownloadItemSnapshot> items = [Item(1), Item(2), Item(3), Item(4, "Unrelated")];
            void Publish() => manager.ApplyPageSnapshot(new([.. items], []));
            Publish();
            var listModel = new DownloadItemPageViewModel(model);
            var page = new DownloadItemView(listModel);
            var host = new PageTestHost { Content = page };
            var window = new Window { Width = width, Height = 1000, Content = host.Shell, RequestedThemeVariant = theme };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show(); await RenderAsync(window);
                var filter = page.FindControl<TextBox>("FilterTextBox")!;
                var status = page.FindControl<SymbolComboBox>("StatusFilter")!;
                var list = page.FindControl<ListBox>("ListBox")!;
                var bulk = page.FindControl<CommandBar>("BulkCommands")!;
                foreach (var field in new Control[] { filter, status, page.FindControl<CheckBox>("DeleteLocalFilesCheckBox")! })
                    InWindow(window, field);
                Assert.IsFalse(string.IsNullOrWhiteSpace(AutomationProperties.GetName(filter)));
                Assert.IsFalse(string.IsNullOrWhiteSpace(AutomationProperties.GetName(status)));
                Assert.AreEqual(4, list.Items.Count);
                Assert.IsTrue(filter.Focus());
                filter.Text = "fixed DOWNLOAD"; Press(window, Key.Enter, PhysicalKey.Enter); await RenderAsync(window);
                Assert.AreEqual(3, list.Items.Count);
                items.Add(Item(5)); Publish(); await RenderAsync(window);
                Assert.AreEqual(4, list.Items.Count, "New matching snapshots enter an active search.");
                items.RemoveAt(4); Publish(); await RenderAsync(window);
                Assert.AreEqual(3, list.Items.Count);
                filter.Text = ""; Press(window, Key.Enter, PhysicalKey.Enter); await RenderAsync(window);
                foreach (var state in new[] { DownloadState.Running, DownloadState.Cancelled, DownloadState.Error })
                {
                    listModel.CurrentOption = Enum.Parse<DownloadListOption>(state.ToString());
                    Assert.IsTrue(page.HasNoDownloadItem);
                    items[0] = items[0] with { State = state, ProgressPercentage = 42, ErrorMessage = state == DownloadState.Error ? "完整错误 / Full diagnostic" : null };
                    Publish(); await RenderAsync(window);
                    Assert.AreEqual(1, list.Items.Count);
                    Assert.IsFalse(page.HasNoDownloadItem);
                    var row = page.GetVisualDescendants().OfType<DownloadItem>().First();
                    Assert.AreEqual(items[0].ProgressMessage, row.FindControl<TextBlock>("ProgressText")!.Text);
                    items[0] = items[0] with { State = DownloadState.Completed }; Publish(); await RenderAsync(window);
                    Assert.IsTrue(page.HasNoDownloadItem);
                }
                listModel.CurrentOption = DownloadListOption.AllQueued; await RenderAsync(window);
                await ActivateAsync(window, bulk, (CommandBarButton)bulk.PrimaryCommands[0]);
                Assert.AreEqual(4, page.SelectedCount);
                await ActivateAsync(window, bulk, (CommandBarButton)bulk.PrimaryCommands[1]);
                Assert.AreEqual(0, page.SelectedCount);
                list.SelectedItem = items[0]; await RenderAsync(window);
                items[0] = items[0] with { ProgressPercentage = 73 }; Publish(); await RenderAsync(window);
                Assert.AreEqual(1, page.SelectedCount, "Selection follows the stable task key across value replacements.");
                await ActivateAsync(window, bulk, (CommandBarButton)bulk.PrimaryCommands[2]);
                Assert.AreEqual(0, page.SelectedCount);
                host.Content = null; await RenderAsync(window); host.Content = page; await RenderAsync(window);
                items.Add(Item(6)); Publish(); await RenderAsync(window);
                Assert.AreEqual(5, list.Items.Count);
                page.DataContext = null; await RenderAsync(window);
                Assert.IsTrue(page.HasNoDownloadItem);
                page.DataContext = listModel; await RenderAsync(window);
                Assert.AreEqual(5, list.Items.Count);
                using var frame = window.CaptureRenderedFrame();
                frame?.Save(Path.Combine(directory, $"downloads-{theme}-{width}.png"), new PngBitmapEncoderOptions());
                host.Shell.CloseAllPages();
                Assert.HasCount(5, manager.OrdinaryItems, "Closing the UI cannot clear the backend snapshot store.");
            }
            finally { host.Shell.CloseAllPages(); window.Close(); }
        }
    }

    internal static DownloadItemSnapshot Item(long id, string title = "Fixed download / 很长的作品标题") =>
        new(new DownloadTaskKey("fixture-" + id, 0, id.ToString()), title, "author", "", "", "",
            DownloadState.Completed, 100, 0, 1, 0, null, "fixture-" + id, false, id.ToString(), 0);

    internal static async Task ActivateAsync(Window window, CommandBar bar, CommandBarButton button)
    {
        bar.IsOpen = true; await RenderAsync(window);
        Assert.IsTrue(button.Focus()); Press(window, Key.Enter, PhysicalKey.Enter); await RenderAsync(window);
    }
    internal static void Press(Window window, Key key, PhysicalKey physical)
    { window.KeyPress(key, RawInputModifiers.None, physical, null); window.KeyRelease(key, RawInputModifiers.None, physical, null); }
    internal static async Task RenderAsync(Window window)
    { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(1); await Task.Delay(80); window.UpdateLayout(); }
    internal static void InWindow(Window window, Control control)
    {
        var point = control.TranslatePoint(default, window);
        Assert.IsNotNull(point); Assert.IsTrue(point.Value.X >= -1 && point.Value.X + control.Bounds.Width <= window.Width + 1);
    }
}
