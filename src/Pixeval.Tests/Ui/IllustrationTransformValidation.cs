using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentIcons.Common;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.Commands.Transformers;
using Pixeval.I18N;
using Pixeval.Models.Extensions;
using Pixeval.Models.Options;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class IllustrationTransformValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new IllustrationReaderFixture();
        var extensions = App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();
        var transform = new Transformer("Image transform fixture");
        using var extensionHost = new ExtensionsHostModel(new FixtureHost(transform), []);
        extensions.HostModels.Add(extensionHost);
        var continuous = System.Globalization.CultureInfo.CurrentUICulture.Name is "en-US" or "ru-RU";
        App.AppViewModel.AppSettings.BrowsingExperienceSettings.BrowseMode = continuous ? BrowseMode.Continuous : BrowseMode.Swipe;
        var model = new IllustrationViewerPageViewModel(new IllustrationItemViewModel(IllustrationReaderFixture.Sample(996401, 1)), false);
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
            await BeginAsync();
            Require(image.IsProcessingImage && !image.ViewOriginalCommand.CanExecute(null)
                && !image.TransformExtensionCommand.CanExecute(transform), "Pending transformation gates duplicate image operations");
            transform.Complete();
            await image.PrimaryTransformerExtensionItem!.Command.ExecutionTask!;
            Require(image.TransformedSource is { IsInitialized: true } && image.DisplaySource.Size == image.OriginalSource!.Size,
                "Actual image menu transforms a PNG and publishes the decoded result");
            Require(host.Notifications.Any(n => n.Type == NotificationType.Success), "A valid transform reports success");
            report.Add("PASS real image menu, decoded output, busy gate and success");
            await DismissAsync(window);

            foreach (var empty in new[] { true, false })
            {
                var displayed = image.TransformedSource;
                await BeginAsync();
                transform.Complete(empty ? [] : [1, 2, 3, 4]);
                await image.PrimaryTransformerExtensionItem!.Command.ExecutionTask!;
                Require(ReferenceEquals(displayed, image.TransformedSource) && image.DisplaySource.IsInitialized,
                    "An empty or invalid extension image preserves the previously decoded result");
                Require(host.Notifications.Any(n => n.Type == NotificationType.Error)
                    && !host.Notifications.Any(n => n.Type == NotificationType.Success), "Undecodable output is an error rather than a false success");
                Require(!transform.Source!.GetCanRead() && !transform.Destination!.GetCanWrite(), "Invalid output releases both extension streams");
                await DismissAsync(window);
            }
            transform.Throw = true;
            await BeginAsync();
            await image.PrimaryTransformerExtensionItem!.Command.ExecutionTask!;
            Require(host.Notifications.Any(n => n.Type == NotificationType.Error) && !image.IsProcessingImage, "An extension invocation failure is visible and releases the busy state");
            transform.Throw = false;
            await DismissAsync(window);
            report.Add("PASS empty/invalid output and thrown invocation, previous result retained, stream disposal and retry");

            var second = new Transformer("Second transformer · 图像转换的长名称");
            using var secondHost = new ExtensionsHostModel(new FixtureHost(second), []);
            extensions.HostModels.Insert(0, secondHost);
            Require(ReferenceEquals(image.PrimaryTransformerExtensionItem!.Extension, second), "The first active host determines the primary transform");
            await BeginAsync(transform); // The original extension is now in the secondary menu.
            transform.Complete();
            await image.TransformerExtensionItems.Single(item => ReferenceEquals(item.Extension, transform)).Command.ExecutionTask!;
            await DismissAsync(window);
            secondHost.IsActive = false;
            Require(ReferenceEquals(image.PrimaryTransformerExtensionItem!.Extension, transform), "Disabling the first host exposes the next active transform");
            extensions.HostModels.Remove(secondHost);

            await BeginAsync();
            var disabledTask = image.PrimaryTransformerExtensionItem!.Command.ExecutionTask!;
            var previousSource = image.TransformedSource;
            extensionHost.IsActive = false;
            Require(!image.HasTransformerExtensions, "Disabling the last host removes its image actions");
            extensionHost.IsActive = true;
            transform.Complete();
            await disabledTask;
            Require(ReferenceEquals(previousSource, image.TransformedSource)
                && !host.Notifications.Any(n => n.Type is NotificationType.Success or NotificationType.Error),
                "Disabling then re-enabling an in-flight extension cannot publish its obsolete result");
            await DismissAsync(window);
            report.Add("PASS host priority, secondary menu, disable/re-enable and in-flight result isolation");

            window.Width = 360;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            await BeginAsync();
            var hiddenTask = image.PrimaryTransformerExtensionItem!.Command.ExecutionTask!;
            host.Shell.NavigateTo(new Pixeval.Views.Search.SearchPage());
            await RenderAsync(window);
            Require(!page.IsLoaded, "Temporary navigation really unloads the image page");
            transform.Complete();
            await hiddenTask;
            host.CloseDestinationAndReturn();
            await RenderAsync(window);
            Require(ReferenceEquals(image, model.CurrentImage.CurrentPage) && image.TransformedSource is { IsInitialized: true },
                "Temporary navigation preserves the same image and accepts a still-active extension result");
            Save(window, directory, "transform-dark-narrow-return");
            await DismissAsync(window);
            await BeginAsync();
            hiddenTask = image.PrimaryTransformerExtensionItem!.Command.ExecutionTask!;
            previousSource = image.TransformedSource;
            host.Shell.NavigateTo(new Pixeval.Views.Search.SearchPage());
            await RenderAsync(window);
            Require(!page.IsLoaded, "Disabling while away uses a genuinely unloaded image page");
            extensionHost.IsActive = false;
            extensionHost.IsActive = true;
            transform.Complete();
            await hiddenTask;
            Require(ReferenceEquals(previousSource, image.TransformedSource)
                && !host.Notifications.Any(n => n.Type is NotificationType.Success or NotificationType.Error),
                "An in-flight transform remains invalidated if its host was disabled while the reader was temporarily unloaded");
            host.CloseDestinationAndReturn();
            await RenderAsync(window);
            await DismissAsync(window);
            extensions.HostModels.Remove(extensionHost);
            Require(!image.HasTransformerExtensions, "Removing the host removes its image actions");
            extensions.HostModels.Add(extensionHost);
            Require(image.HasTransformerExtensions, "Adding a host restores the actions without rebuilding the reader");
            report.Add("PASS temporary navigation/return, disable/re-enable while unloaded, dark narrow layout and host removal/addition");

            await BeginAsync();
            var pending = image.PrimaryTransformerExtensionItem!.Command.ExecutionTask!;
            host.Shell.ClosePage(page);
            var changed = 0;
            image.PropertyChanged += (_, _) => changed++;
            var notifications = host.Notifications.ToArray();
            transform.Complete(); // Reads and writes the original streams after production close.
            await pending;
            Require(changed == 0 && host.Notifications.SequenceEqual(notifications) && image.TransformedSource is null,
                "Production close prevents late image state and notifications while keeping extension streams alive until completion");
            Require(!image.TransformExtensionCommand.CanExecute(transform), "Closed image transformation stays disabled");
            Require(!transform.Source!.GetCanRead() && !transform.Destination!.GetCanWrite(), "Completed closed transformation releases both streams");
            report.Add("PASS real close, late result isolation and extension stream lifetime");

            async Task BeginAsync(Transformer? selected = null)
            {
                selected ??= transform;
                await RenderAsync(window);
                var wrapper = page.FindControl<ImageViewerWrapper>("I")!;
                var visible = new Rect(viewer.TranslatePoint(default, window)!.Value, viewer.Bounds.Size)
                    .Intersect(new Rect(wrapper.TranslatePoint(default, window)!.Value, wrapper.Bounds.Size));
                var position = visible.Center;
                window.MouseDown(position, MouseButton.Right);
                window.MouseUp(position, MouseButton.Right);
                await RenderAsync(window);
                MenuItem item;
                if (ReferenceEquals(image.PrimaryTransformerExtensionItem!.Extension, selected))
                    item = viewer.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Header?.ToString() == selected.Label);
                else
                {
                    var submenu = viewer.ContextMenu!.Items.OfType<MenuItem>().Single(item =>
                        item.Header?.ToString() == I18NManager.GetResource(EntryViewerPageResources.OtherImageTransformerExtensions));
                    Require(submenu.Focus(NavigationMethod.Tab), "Other image transformers remain keyboard reachable");
                    Press(window, Key.Right, PhysicalKey.ArrowRight);
                    await RenderAsync(window);
                    Require(submenu.IsSubMenuOpen, "Right opens the real other-transformers submenu");
                    var index = image.TransformerExtensionItems.ToList().FindIndex(command => ReferenceEquals(command.Extension, selected));
                    item = (MenuItem) submenu.ContainerFromIndex(index)!;
                }
                Require(viewer.ContextMenu.IsOpen && item.IsEffectivelyEnabled && item.Focus(NavigationMethod.Tab), "The actual image transform menu is keyboard reachable");
                var calls = selected.Calls;
                Press(window, Key.Enter, PhysicalKey.Enter);
                await RenderAsync(window);
                Require(selected.Calls == calls + 1 && !viewer.ContextMenu.IsOpen, "Menu activation starts exactly one transform and dismisses the menu");
            }
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "illustration-transform-contracts.txt"), report);
            window.Close();
            extensions.HostModels.Remove(extensionHost);
        }
        Require(fixture.Http.UnexpectedRequests.IsEmpty, "Image transformations stay offline");
    }

    internal static async Task DismissAsync(Window window)
    {
        await RenderAsync(window);
        foreach (var card in window.GetVisualDescendants().OfType<NotificationCard>().ToArray())
        {
            if (!card.IsClosing)
            {
                var close = card.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_CloseButton");
                Require(close.Focus(NavigationMethod.Tab), "Feedback can be acknowledged with the keyboard");
                Press(window, Key.Space, PhysicalKey.Space);
            }
            await UntilAsync(() => !window.GetVisualDescendants().Contains(card));
        }
    }

    private sealed class Transformer(string label) : IImageTransformerCommandExtension
    {
        public string Label => label;
        public int Calls { get; private set; }
        public bool Throw { get; set; }
        public IStream? Source { get; private set; }
        public IStream? Destination { get; private set; }
        private ITaskCompletionSource? _pending;
        public void TransformAsync(ITaskCompletionSource task, IStream originalStream, IStream destinationStream)
        {
            Require(_pending is null, "This fixture has only one in-flight transformation");
            Calls++;
            if (Throw)
                throw new InvalidOperationException("Fixed image extension failure");
            Source = originalStream;
            Destination = destinationStream;
            _pending = task;
        }
        public void Complete(byte[]? replacement = null)
        {
            var task = _pending ?? throw new InvalidOperationException("No pending image transform");
            _pending = null;
            Require(Source!.GetCanRead() && Destination!.GetCanWrite(), "The extension retains usable input/output until it completes");
            if (replacement is null)
                Source.CopyTo(Destination!);
            else
                Destination!.Write(replacement, 0, replacement.Length);
            task.SetCompleted();
        }
        public Symbol GetIcon() => Symbol.Image;
        public string GetLabel() => label;
        public string GetDescription() => "Offline delayed image transformer";
        public void OnExtensionLoaded() { }
        public void OnExtensionUnloaded() { }
    }

    private sealed class FixtureHost(params IExtension[] extensions) : IExtensionsHost
    {
        public string GetExtensionName() => "Image transform fixture host";
        public string GetAuthorName() => "Pixeval tests";
        public string GetExtensionLink() => "";
        public string GetHelpLink() => "";
        public string GetDescription() => "Managed offline image boundary";
        public string GetSdkVersion() => ExtensionService.CurrentVersion;
        public string GetVersion() => "1.0";
        public IExtension[] GetExtensions(out int returnCount) { returnCount = extensions.Length; return extensions; }
        public byte[]? GetIcon(out int returnCount) { returnCount = 0; return null; }
        public void Initialize(string cultureName, string tempDirectory, string extensionDirectory, ILogger logger) { }
    }
}
