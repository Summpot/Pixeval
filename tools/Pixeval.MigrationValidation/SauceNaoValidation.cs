using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Material3Expressive.Components;
using Mako.Global.Enum;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.ViewModels.Search;
using Pixeval.ViewModels;
using Pixeval.Views.Search;
using Pixeval.Views.Work;

namespace Pixeval.MigrationValidation;

internal static class SauceNaoValidation
{
    internal static async Task RunAsync(string directory)
    {
        var provider = new ImageStorageProvider();
        using var scope = AvaloniaLocator.EnterScope();
        AvaloniaLocator.CurrentMutable.Bind<IStorageProviderFactory>().ToConstant(provider);
        using var asset = AssetLoader.Open(new Uri("avares://Pixeval/Assets/Platforms/pixiv.png"));
        using var buffer = new MemoryStream();
        await asset.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        List<string> report = [];
        using var fixture = new OfflineSearchFixture(App.AppViewModel.MakoClient);
        await SauceNaoResultsValidation.RunAsync(directory, bytes, report, fixture);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var width in new[] { 360, 1000 })
            {
                App.AppViewModel.AppSettings.SearchSettings.SauceNaoApiKey = "";
                var page = new SauceNaoSearchPage();
                using var model = (SauceNaoSearchPageViewModel) page.DataContext!;
                var navigation = new NavigationPage();
                await navigation.PushAsync(page);
                var container = new SearchCompletionValidation.FixtureViewContainer { Content = navigation };
                var window = new Window { Width = width, Height = 720, RequestedThemeVariant = theme, Content = container };
                var scenario = $"{theme.Key}-{width}";
                try
                {
                    window.Show();
                    await RenderAsync(window);
                    var picker = page.FindControl<Card>("ImagePicker")!;
                    var paste = page.FindControl<Button>("PasteButton")!;
                    var search = page.FindControl<Button>("SearchButton")!;
                    Require(!picker.IsEffectivelyEnabled && !paste.IsEffectivelyEnabled && !search.IsEffectivelyEnabled,
                        "No API key keeps all image-search actions disabled");
                    Require(page.GetVisualDescendants().OfType<InfoBar>().Any(bar => bar.IsVisible
                        && bar.Text == I18NManager.GetResource(SearchResources.SauceNAO.ProvideApiKey) && bar.IsEffectivelyEnabled),
                        "The missing-key explanation remains readable instead of inheriting disabled styling");
                    Save(window, directory, "saucenao-no-key-" + scenario);

                    App.AppViewModel.AppSettings.SearchSettings.SauceNaoApiKey = "fixture-sauce-key";
                    container.Content = null;
                    await RenderAsync(window);
                    container.Content = navigation;
                    await RenderAsync(window);
                    Require(picker.IsEffectivelyEnabled && paste.IsEffectivelyEnabled && !search.IsEffectivelyEnabled,
                        "Returning after API-key settings change enables image input but not an empty search");
                    Require(AutomationProperties.GetName(picker) == I18NManager.GetResource(SearchResources.SauceNAO.ProvideImage),
                        "The clickable preview exposes a localized image-selection name");
                    Bounds(window, picker);
                    await ActivateAsync(window, picker);
                    Require(provider.Calls == provider.Completed + 1, "Keyboard activation opens exactly one file request");
                    Require(provider.Options is { AllowMultiple: false, FileTypeFilter.Count: 2 }, "The picker accepts one image and preserves the all-files fallback");
                    // A second key activation while the native picker is pending must not open another picker.
                    Press(window, Key.Space, PhysicalKey.Space);
                    Require(provider.Calls == provider.Completed + 1, "Repeated activation cannot open overlapping file pickers");
                    provider.Complete([]);
                    await RenderAsync(window);
                    Require(!model.FileSelected && model.ErrorMessage is null, "Canceling the file picker leaves the empty selection unchanged");

                    await window.Clipboard!.ClearAsync();
                    await ActivateAsync(window, paste);
                    await UntilAsync(() => !model.IsLoading);
                    Require(model.ErrorMessage == I18NManager.GetResource(SearchResources.SauceNAO.ClipboardHasNoImage)
                        && !model.FileSelected, "An empty clipboard reports a recoverable localized error");

                    var selected = new ImageFile(bytes);
                    await ActivateAsync(window, picker);
                    provider.Complete([selected]);
                    await UntilAsync(() => model.FileSelected && selected.Disposed);
                    await RenderAsync(window);
                    Require(model.Image is not null && model.File.Span.SequenceEqual(bytes) && model.ErrorMessage is null
                        && selected.ReadStreamDisposed, "File selection publishes matching bytes/preview and closes file/stream handles");
                    Require(search.IsEffectivelyEnabled, "A valid selected image enables search");
                    var preview = model.Image;
                    var corrupt = new ImageFile([1, 2, 3]);
                    await ActivateAsync(window, picker);
                    provider.Complete([corrupt]);
                    await UntilAsync(() => corrupt.Disposed);
                    Require(ReferenceEquals(preview, model.Image) && model.File.Span.SequenceEqual(bytes)
                        && model.FileSelected && !string.IsNullOrWhiteSpace(model.ErrorMessage),
                        "Invalid selected bytes preserve the last usable image and show a retryable error");
                    await RenderAsync(window);
                    Save(window, directory, "saucenao-invalid-image-" + scenario);
                    var readFailure = new ImageFile(bytes) { FailOpen = true };
                    await ActivateAsync(window, picker);
                    provider.Complete([readFailure]);
                    await UntilAsync(() => readFailure.Disposed);
                    Require(ReferenceEquals(preview, model.Image) && !model.IsLoading, "File-open failure also preserves the selected preview and ends loading");

                    using var clipboardBitmap = new Bitmap(new MemoryStream(bytes));
                    await window.Clipboard.SetBitmapAsync(clipboardBitmap);
                    await ActivateAsync(window, paste);
                    await UntilAsync(() => !model.IsLoading);
                    Require(model.FileSelected && model.Image is not null && !ReferenceEquals(preview, model.Image)
                        && model.ErrorMessage is null, "Clipboard image retries successfully after file errors");
                    using (var pasted = new Bitmap(new MemoryStream(model.File.ToArray())))
                        Require(pasted.PixelSize == model.Image!.PixelSize, "Clipboard payload is a readable PNG matching the preview dimensions");
                    await ActivateAsync(window, search);
                    Require(container.Navigations == 1 && container.LastPageType == typeof(ArtworkSauceNaoSearchResultPage),
                        "Search keyboard activation opens one result page");
                    await RenderAsync(window);
                    Save(window, directory, "saucenao-selected-" + scenario);

                    window.Height = 340;
                    window.Width = width == 360 ? 1000 : 360;
                    await RenderAsync(window);
                    await RevealAsync(window, paste);
                    Bounds(window, paste);
                    var pasteOrigin = paste.TranslatePoint(default, window)!.Value;
                    Save(window, directory, "saucenao-short-before-check-" + scenario);
                    File.WriteAllLines(Path.Combine(directory, "short-layout-" + scenario + ".txt"),
                        new[] { $"Paste: {pasteOrigin} {paste.Bounds}; client: {window.ClientSize}" }
                            .Concat(page.GetVisualDescendants().OfType<ScrollViewer>().Select(scroll =>
                                $"Scroll: {scroll.GetType().Name} offset={scroll.Offset} extent={scroll.Extent} viewport={scroll.Viewport} bounds={scroll.Bounds}")));
                    Require(pasteOrigin.Y >= 0 && pasteOrigin.Y + paste.Bounds.Height <= window.ClientSize.Height + 1,
                        "The paste action can be fully scrolled into a short window");
                    foreach (var text in paste.GetVisualDescendants().OfType<TextBlock>())
                        Require(text.TextLayout.Width <= text.Bounds.Width + 1 && text.TextLayout.TextLines.All(line => !line.HasCollapsed),
                            "Long translated paste text remains readable in compact layouts");
                    Require(ReferenceEquals(model.Image, page.GetVisualDescendants().OfType<Image>().Single().Source),
                        "Resizing preserves the selected preview instance");
                    Save(window, directory, "saucenao-short-window-" + scenario);

                    await ActivateAsync(window, picker);
                    var late = new ImageFile(bytes);
                    container.Content = null;
                    await RenderAsync(window);
                    provider.Complete([late]);
                    await UntilAsync(() => late.Disposed);
                    Require(!late.ReadOpened, "A file-picker completion after leaving the page never opens the selected file");
                    container.Content = navigation;
                    await RenderAsync(window);
                    Require(model.FileSelected, "Returning to the page preserves its prior valid selection");
                    report.Add($"PASS input {scenario}: API-key gate, keyboard file/cancel, empty/image clipboard, decoding/open errors, handles, search route, compact scrolling, late picker");
                }
                finally
                {
                    container.Content = null;
                    window.Close();
                }
            }
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            App.AppViewModel.AppSettings.SearchSettings.SauceNaoApiKey = "fixture-sauce-key";
            var searchPage = new SearchPage();
            var tabs = searchPage.FindControl<TabbedPage>("AdvancedOptionsTabControl")!;
            var expander = searchPage.FindControl<Expander>("AdvancedExpander")!;
            expander.IsExpanded = true;
            tabs.SelectedIndex = 2;
            var window = new Window { Width = 360, Height = 520, RequestedThemeVariant = theme, Content = searchPage };
            SauceNaoSearchPageViewModel? model = null;
            try
            {
                window.Show();
                await RenderAsync(window);
                Require(tabs.CurrentPage is SauceNaoSearchPage, "The real advanced-options tab selects SauceNao");
                var page = (SauceNaoSearchPage) tabs.CurrentPage!;
                model = (SauceNaoSearchPageViewModel) page.DataContext!;
                var picker = page.FindControl<Card>("ImagePicker")!;
                using var bitmap = new Bitmap(new MemoryStream(bytes));
                await window.Clipboard!.SetBitmapAsync(bitmap);
                await RevealAsync(window, picker);
                Require(picker.Focus(NavigationMethod.Tab), "The embedded image picker can receive keyboard focus");
                window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, null);
                window.KeyRelease(Key.V, RawInputModifiers.Control, PhysicalKey.V, null);
                await UntilAsync(() => model.FileSelected && !model.IsLoading);
                var selected = model.Image;
                Require(selected is not null, "The search page's existing Ctrl+V route pastes into its SauceNao model");
                await window.Clipboard!.ClearAsync();
                window.KeyPress(Key.V, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.V, null);
                window.KeyRelease(Key.V, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.V, null);
                await RenderAsync(window);
                Require(ReferenceEquals(selected, model.Image) && model.ErrorMessage is null, "An additional shortcut modifier does not trigger image paste");
                tabs.SelectedIndex = 0;
                await RenderAsync(window);
                tabs.SelectedIndex = 2;
                await RenderAsync(window);
                Require(ReferenceEquals(page, tabs.CurrentPage) && ReferenceEquals(selected, model.Image), "Changing advanced tabs preserves the selected image and page instance");
                var paste = page.FindControl<Button>("PasteButton")!;
                await RevealAsync(window, paste);
                Bounds(window, paste);
                Save(window, directory, "saucenao-embedded-" + theme.Key);
                report.Add($"PASS embedded {theme.Key}: existing advanced tab, Ctrl+V, exact modifiers, selected-image retention and compact layout");
            }
            finally
            {
                window.Content = null;
                window.Close();
                model?.Dispose();
                SearchPageViewModel.Instance.SelectedAdvancedOptionsType = SimpleWorkType.Illustration;
            }
        }
        Require(fixture.UnexpectedRequests.IsEmpty, "No SauceNao scenario escaped the fixed HTTP fixture");
        File.WriteAllLines(Path.Combine(directory, "saucenao-contracts.txt"), report);
        Console.WriteLine($"Validated {report.Count} SauceNao input/result/embedded groups.");
    }

    private static async Task ActivateAsync(Window window, Control control)
    {
        await RevealAsync(window, control);
        Require(control.Focus(NavigationMethod.Tab), "The image-search action is keyboard reachable");
        Press(window, Key.Space, PhysicalKey.Space);
        await RenderAsync(window);
    }

    private static async Task RevealAsync(Window window, Control control)
    {
        control.BringIntoView();
        await RenderAsync(window);
        Bounds(window, control);
    }

    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
    }

    private static void Bounds(Window window, Control control)
    {
        var origin = control.TranslatePoint(default, window) ?? throw new InvalidOperationException("Detached image-search control");
        Require(control.Bounds.Width > 0 && origin.X >= -1 && origin.X + control.Bounds.Width <= window.ClientSize.Width + 1,
            "Image-search controls stay inside the horizontal viewport");
    }

    private static async Task RenderAsync(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        await Task.Delay(500);
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The image-search operation did not complete.");
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Save(Window window, string directory, string name)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No SauceNao frame.");
        frame.Save(Path.Combine(directory, name + ".png"), new PngBitmapEncoderOptions());
    }

    private static void Require(bool condition, string contract)
    {
        if (!condition)
            throw new InvalidOperationException(contract);
    }

    private sealed class ImageStorageProvider : IStorageProvider, IStorageProviderFactory
    {
        private TaskCompletionSource<IReadOnlyList<IStorageFile>>? _pending;
        public IStorageProvider CreateProvider(TopLevel topLevel) => this;
        internal int Calls { get; private set; }
        internal int Completed { get; private set; }
        internal FilePickerOpenOptions? Options { get; private set; }
        public bool CanOpen => true;
        public bool CanSave => false;
        public bool CanPickFolder => false;
        public Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options)
        {
            Require(_pending is null, "Only one storage picker may be pending");
            Calls++;
            Options = options;
            _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return _pending.Task;
        }
        internal void Complete(IReadOnlyList<IStorageFile> files)
        {
            var pending = _pending ?? throw new InvalidOperationException("No pending file picker");
            _pending = null;
            Completed++;
            pending.SetResult(files);
        }
        public Task<IStorageFile?> SaveFilePickerAsync(FilePickerSaveOptions options) => throw new NotSupportedException();
        public async Task<OpenFilePickerResult> OpenFilePickerWithResultAsync(FilePickerOpenOptions options) =>
            new() { Files = await OpenFilePickerAsync(options) };
        public Task<SaveFilePickerResult> SaveFilePickerWithResultAsync(FilePickerSaveOptions options) => throw new NotSupportedException();
        public Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options) => throw new NotSupportedException();
        public Task<IStorageBookmarkFile?> OpenFileBookmarkAsync(string bookmark) => throw new NotSupportedException();
        public Task<IStorageBookmarkFolder?> OpenFolderBookmarkAsync(string bookmark) => throw new NotSupportedException();
        public Task<IStorageFile?> TryGetFileFromPathAsync(Uri filePath) => throw new NotSupportedException();
        public Task<IStorageFolder?> TryGetFolderFromPathAsync(Uri folderPath) => throw new NotSupportedException();
        public Task<IStorageFolder?> TryGetWellKnownFolderAsync(WellKnownFolder wellKnownFolder) => throw new NotSupportedException();
    }

    private sealed class ImageFile(byte[] bytes) : IStorageFile
    {
        internal bool Disposed { get; private set; }
        internal bool ReadOpened { get; private set; }
        internal bool ReadStreamDisposed { get; private set; }
        internal bool FailOpen { get; init; }
        public string Name => "fixture.png";
        public Uri Path => new("file:///m3e-fixture/fixture.png");
        public bool CanBookmark => false;
        public Task<Stream> OpenReadAsync()
        {
            ReadOpened = true;
            if (FailOpen)
                throw new IOException("Fixed image access failure");
            return Task.FromResult<Stream>(new OwnedStream(bytes, () => ReadStreamDisposed = true));
        }
        public void Dispose() => Disposed = true;
        public Task<Stream> OpenWriteAsync() => throw new NotSupportedException();
        public Task<StorageItemProperties> GetBasicPropertiesAsync() => throw new NotSupportedException();
        public Task<string?> SaveBookmarkAsync() => throw new NotSupportedException();
        public Task<IStorageFolder?> GetParentAsync() => throw new NotSupportedException();
        public Task DeleteAsync() => throw new NotSupportedException();
        public Task<IStorageItem?> MoveAsync(IStorageFolder destination) => throw new NotSupportedException();
    }

    private sealed class OwnedStream(byte[] bytes, Action disposed) : MemoryStream(bytes)
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                disposed();
            base.Dispose(disposing);
        }
    }
}
