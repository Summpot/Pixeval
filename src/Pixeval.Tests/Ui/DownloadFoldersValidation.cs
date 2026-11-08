using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Config;
using Pixeval.Download;
using Pixeval.Views.Download;

namespace Pixeval.Tests.Ui;

internal static class DownloadFoldersValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var manager = new DownloadManager(1);
        using var model = new DownloadPageViewModel(manager);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        foreach (var width in new[] { 360d, 1000d })
        {
            var first = Folder(1, [DownloadPagesValidation.Item(1), DownloadPagesValidation.Item(2)]);
            var second = Folder(2, []);
            void Publish() => manager.ApplyPageSnapshot(new([], [first, second]));
            Publish();
            var frame = new DownloadFolderView(new DownloadFolderPageViewModel(model));
            var host = new PageTestHost { Content = frame };
            var window = new Window { Width = width, Height = 900, Content = host.Shell, RequestedThemeVariant = theme };
            try
            {
                Pixeval.Common.MaterialAnimation.Initialize(window);
                window.Show(); await DownloadPagesValidation.RenderAsync(window);
                Assert.IsInstanceOfType<DownloadFolderListView>(frame.CurrentPage);
                var list = (DownloadFolderListView)frame.CurrentPage!;
                Assert.IsFalse(list.HasNoFolder);
                var row = list.GetVisualDescendants().OfType<DownloadFolder>().First(control => control.DataContext is DownloadFolderSnapshot value && value.SubscriptionId == first.SubscriptionId);
                var button = row.FindControl<Material3Expressive.Components.Card>("FolderCard")!;
                DownloadPagesValidation.InWindow(window, button);
                first = first with { ProgressPercentage = 30, ActiveCount = 1, CompletedCount = 0, ErrorCount = 1 };
                Publish(); await DownloadPagesValidation.RenderAsync(window);
                row = list.GetVisualDescendants().OfType<DownloadFolder>().First(control => control.DataContext is DownloadFolderSnapshot value && value.SubscriptionId == first.SubscriptionId);
                Assert.AreEqual(first.GroupProgressMessage, row.FindControl<TextBlock>("GroupStatsText")!.Text);
                Assert.AreEqual(30d, row.GetVisualDescendants().OfType<ProgressBar>().Single().Value);
                button = row.FindControl<Material3Expressive.Components.Card>("FolderCard")!;
                Assert.IsTrue(button.Focus()); DownloadPagesValidation.Press(window, Key.Enter, PhysicalKey.Enter);
                await DownloadPagesValidation.RenderAsync(window);
                Assert.IsInstanceOfType<DownloadItemView>(frame.CurrentPage);
                var itemView = (DownloadItemView)frame.CurrentPage!;
                Assert.AreEqual(2, itemView.FindControl<ListBox>("ListBox")!.Items.Count);
                first = first with { Items = [first.Items[1]] }; Publish(); await DownloadPagesValidation.RenderAsync(window);
                Assert.AreEqual(1, itemView.FindControl<ListBox>("ListBox")!.Items.Count, "Open folders observe replacement native snapshots.");
                using var picture = window.CaptureRenderedFrame();
                picture?.Save(Path.Combine(directory, $"folders-{theme}-{width}.png"), new PngBitmapEncoderOptions());
                host.Shell.CloseAllPages();
                Assert.HasCount(2, manager.Folders, "UI closure preserves subscription folders and backend state.");
            }
            finally { host.Shell.CloseAllPages(); window.Close(); }
        }
    }
    private static DownloadFolderSnapshot Folder(long id, List<DownloadItemSnapshot> items) =>
        new(id, "完整订阅标题 / A very long subscription title", "", (uint)WorkSubscriptionType.Posts,
            (uint)WorkSubscriptionWorkKind.Illustration, items, (uint)items.Count, 0, (uint)items.Count, 0,
            100, DownloadState.Completed, false, 0, null);
}
