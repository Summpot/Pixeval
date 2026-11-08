using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Platform;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Pixeval.I18N;
using Pixeval.Models.Options;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class IllustrationCopyValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new IllustrationReaderFixture();
        using var clipboard = new Clipboard();
        AvaloniaLocator.CurrentMutable.Bind<IClipboard>().ToConstant(clipboard);
        var storage = new IllustrationSaveFixture();
        AvaloniaLocator.CurrentMutable.Bind<IStorageProviderFactory>().ToConstant(storage);
        var continuous = System.Globalization.CultureInfo.CurrentUICulture.Name is "en-US" or "ru-RU";
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = continuous ? BrowseMode.Continuous : BrowseMode.Swipe;
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(IllustrationReaderFixture.Sample(996420, 1)), false);
        var page = new IllustrationViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 700, Height = 760, Content = host.Shell };
        List<string> report = [];
        try
        {
            window.Show();
            await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            await RenderAsync(window);
            var image = model.CurrentImage!.CurrentPage!;
            var viewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, image));
            var copy = viewer.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == I18NManager.GetResource(EntryViewerPageResources.Copy));
            var expected = Encode(image.DisplaySource.Frames[0]);
            await BeginAsync();
            clipboard.CompleteSet();
            await image.CopyCommand.ExecutionTask!;
            await RenderAsync(window);
            File.WriteAllBytes(Path.Combine(directory, "clipboard-expected.png"), expected);
            if (clipboard.Bytes is { } copied) File.WriteAllBytes(Path.Combine(directory, "clipboard-actual.png"), copied);
            Require(SameImage(clipboard.Bytes!, expected) && clipboard.Flushes == 1 && host.Notifications.Any(n => n.Type == NotificationType.Success),
                $"The actual copy menu writes the displayed PNG and flushes before showing success: bytes={SameImage(clipboard.Bytes!, expected)}, flushes={clipboard.Flushes}, readError={clipboard.ReadError}, notifications={string.Join(',', host.Notifications.Select(n => n.Type))}");
            report.Add("PASS copy menu, displayed bitmap and successful clipboard flush");
            await IllustrationTransformValidation.DismissAsync(window);

            foreach (var failSet in new[] { true, false })
            {
                clipboard.FailSet = failSet;
                clipboard.FailFlush = !failSet;
                await BeginAsync();
                clipboard.CompleteSet();
                await image.CopyCommand.ExecutionTask!;
                await RenderAsync(window);
                Require(host.Notifications.Any(n => n.Type == NotificationType.Error)
                    && !host.Notifications.Any(n => n.Type == NotificationType.Success), "Clipboard set/flush failures are visible and retryable");
                await IllustrationTransformValidation.DismissAsync(window);
            }
            clipboard.FailSet = clipboard.FailFlush = false;
            report.Add("PASS set/flush error feedback and retryable image action");

            window.Width = 360;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            await RenderAsync(window);
            var wrapper = page.FindControl<ImageViewerWrapper>("I")!;
            var scroller = wrapper.GetVisualDescendants().OfType<SmoothScroll.Avalonia.Controls.ScrollView>().First();
            Require(scroller.Focus(NavigationMethod.Tab), "The real image viewport accepts keyboard focus");
            var sets = clipboard.Sets;
            Press(window, Key.C, PhysicalKey.C, RawInputModifiers.Control);
            await UntilAsync(() => clipboard.Sets == sets + 1);
            Require(image.CopyCommand.IsRunning && !wrapper.ImageViewer.CopyCommand.CanExecute(null), "Keyboard and menu copy share the same busy image command");
            Press(window, Key.C, PhysicalKey.C, RawInputModifiers.Control);
            Require(clipboard.Sets == sets + 1, "Repeated copy shortcut cannot start a duplicate request");
            clipboard.CompleteSet();
            await image.CopyCommand.ExecutionTask!;
            Require(SameImage(clipboard.Bytes!, expected), "Keyboard copy uses the same displayed image snapshot");
            await IllustrationTransformValidation.DismissAsync(window);

            var manager = App.AppViewModel.HistoryPersistHelper.DownloadManager;
            await App.AppViewModel.HistoryPersistHelper.RestoreTask;
            Require(scroller.Focus(NavigationMethod.Tab), "The download shortcut retains a viewport focus route");
            var queued = manager.QueuedTasks.Count;
            sets = clipboard.Sets;
            Press(window, Key.S, PhysicalKey.S, RawInputModifiers.Control);
            await UntilAsync(() => manager.QueuedTasks.Count == queued + 1);
            Require(clipboard.Sets == sets, "The save shortcut queues the image download without copying to the clipboard");
            report.Add("PASS dark/narrow actual Ctrl+C busy gate and Ctrl+S download route");
            await IllustrationTransformValidation.DismissAsync(window);

            Require(scroller.Focus(NavigationMethod.Tab), "The save-as shortcut retains a viewport focus route");
            Press(window, Key.S, PhysicalKey.S, RawInputModifiers.Control | RawInputModifiers.Shift);
            await UntilAsync(() => storage.Calls == 1);
            Require(image.SaveAsCommand.IsRunning && !wrapper.ImageViewer.SaveAsCommand.CanExecute(null)
                && clipboard.Sets == sets, "Ctrl+Shift+S shares the image save-as picker and its busy gate");
            Press(window, Key.S, PhysicalKey.S, RawInputModifiers.Control | RawInputModifiers.Shift);
            Require(storage.Calls == 1, "A pending save-as shortcut cannot duplicate the picker");
            var output = new IllustrationSaveFixture.File();
            storage.Complete(output);
            await image.SaveAsCommand.ExecutionTask!;
            Require(output.Output.ToArray().SequenceEqual(expected) && output.Disposed && output.Output.Disposed,
                "The save-as shortcut writes and releases the displayed PNG");
            report.Add("PASS actual Ctrl+Shift+S displayed PNG, picker busy gate and output ownership");
            await IllustrationTransformValidation.DismissAsync(window);

            await BeginAsync();
            var pending = image.CopyCommand.ExecutionTask!;
            host.Shell.ClosePage(page);
            var notifications = host.Notifications.ToArray();
            var flushes = clipboard.Flushes;
            clipboard.CompleteSet();
            await pending;
            Require(clipboard.ReadError is null && SameImage(clipboard.Bytes!, expected), "Closing the reader cannot dispose the pending clipboard image");
            Require(clipboard.Flushes == flushes && host.Notifications.SequenceEqual(notifications), "A late copy does not flush or notify after production close");
            Require(!image.CopyCommand.CanExecute(copy), "A closed image cannot be copied again");
            // Lazy clipboard providers can request the image even after SetDataAsync has returned.
            Require(SameImage(await clipboard.ReadPngAsync(), expected), "Clipboard data remains usable independently of viewer lifetime");
            report.Add("PASS pending copy across real close, no late flush/notification and independent clipboard image ownership");

            var snapshot = await (await clipboard.TryGetInProcessDataAsync())!.TryGetBitmapAsync();
            await clipboard.ClearAsync();
            var disposed = false;
            try { _ = Encode(snapshot!); }
            catch (Exception) { disposed = true; }
            Require(disposed, "Releasing the clipboard transfer disposes its owned bitmap");

            model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(IllustrationReaderFixture.Sample(996421, 1)), false);
            page = new IllustrationViewerPage(model);
            host.Shell.NavigateTo(page);
            await UntilAsync(() => model.CurrentImage?.CurrentPage?.LoadSuccessfully == true);
            await RenderAsync(window);
            image = model.CurrentImage!.CurrentPage!;
            viewer = page.GetVisualDescendants().OfType<SingleImageViewer>().Single(control => ReferenceEquals(control.DataContext, image));
            copy = viewer.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == I18NManager.GetResource(EntryViewerPageResources.Copy));
            clipboard.HoldFlush = true;
            await BeginAsync();
            clipboard.CompleteSet();
            await UntilAsync(() => clipboard.IsFlushing);
            pending = image.CopyCommand.ExecutionTask!;
            host.Shell.ClosePage(page);
            notifications = host.Notifications.ToArray();
            clipboard.CompleteFlush();
            await pending;
            Require(host.Notifications.SequenceEqual(notifications) && SameImage(await clipboard.ReadPngAsync(), expected),
                "Closing during flush suppresses late feedback and retains the provider-owned image");
            report.Add("PASS clipboard snapshot disposal and real close during pending flush");

            async Task BeginAsync()
            {
                await RenderAsync(window);
                var imageWrapper = page.FindControl<ImageViewerWrapper>("I")!;
                var position = new Rect(viewer.TranslatePoint(default, window)!.Value, viewer.Bounds.Size)
                    .Intersect(new Rect(imageWrapper.TranslatePoint(default, window)!.Value, imageWrapper.Bounds.Size)).Center;
                window.MouseDown(position, MouseButton.Right);
                window.MouseUp(position, MouseButton.Right);
                await RenderAsync(window);
                Require(viewer.ContextMenu.IsOpen && copy.IsEffectivelyEnabled && copy.Focus(NavigationMethod.Tab), "The real copy menu is keyboard reachable");
                var calls = clipboard.Sets;
                Press(window, Key.Enter, PhysicalKey.Enter);
                await UntilAsync(() => clipboard.Sets == calls + 1);
                Require(!image.CopyCommand.CanExecute(copy), "A pending copy prevents duplicate clipboard requests");
            }
        }
        finally
        {
            clipboard.CompleteSet();
            clipboard.CompleteFlush();
            File.WriteAllLines(Path.Combine(directory, "illustration-copy-contracts.txt"), report);
            window.Close();
        }
    }

    private static bool SameImage(byte[] actual, byte[] expected)
    {
        using var actualStream = new MemoryStream(actual);
        using var expectedStream = new MemoryStream(expected);
        using var a = new Bitmap(actualStream);
        using var b = new Bitmap(expectedStream);
        return a.PixelSize == b.PixelSize && Pixels(a).SequenceEqual(Pixels(b));

        static byte[] Pixels(Bitmap bitmap)
        {
            var stride = bitmap.PixelSize.Width * 4;
            var bytes = new byte[stride * bitmap.PixelSize.Height];
            var address = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), address, bytes.Length, stride);
                Marshal.Copy(address, bytes, 0, bytes.Length);
                return bytes;
            }
            finally { Marshal.FreeHGlobal(address); }
        }
    }

    private static byte[] Encode(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());
        return stream.ToArray();
    }

    private sealed class Clipboard : IClipboard, IDisposable
    {
        private IAsyncDataTransfer? _data;
        private TaskCompletionSource? _pending;
        private TaskCompletionSource? _flush;
        internal bool FailSet { get; set; }
        internal bool FailFlush { get; set; }
        internal bool HoldFlush { get; set; }
        internal bool IsFlushing => _flush is not null;
        internal int Sets { get; private set; }
        internal int Flushes { get; private set; }
        internal byte[]? Bytes { get; private set; }
        internal Exception? ReadError { get; private set; }
        public async Task SetDataAsync(IAsyncDataTransfer? dataTransfer)
        {
            Sets++;
            _data?.Dispose();
            _data = dataTransfer;
            _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await _pending.Task;
            if (FailSet)
            {
                Dispose();
                throw new IOException("Fixed clipboard set failure");
            }
            try { Bytes = await ReadPngAsync(); }
            catch (Exception error) { ReadError = error; }
        }
        internal async Task<byte[]> ReadPngAsync() => Encode(await _data!.TryGetBitmapAsync() ?? throw new InvalidOperationException("No clipboard bitmap"));
        internal void CompleteSet() { _pending?.TrySetResult(); _pending = null; }
        public async Task FlushAsync()
        {
            Flushes++;
            if (HoldFlush)
            {
                _flush = new(TaskCreationOptions.RunContinuationsAsynchronously);
                await _flush.Task;
            }
            if (FailFlush)
                throw new IOException("Fixed clipboard flush failure");
        }
        internal void CompleteFlush() { _flush?.TrySetResult(); _flush = null; }
        public Task ClearAsync() { Dispose(); return Task.CompletedTask; }
        public Task<IAsyncDataTransfer?> TryGetDataAsync() => throw new NotSupportedException();
        public Task<IAsyncDataTransfer?> TryGetInProcessDataAsync() => Task.FromResult(_data);
        public void Dispose() { _data?.Dispose(); _data = null; }
    }
}
