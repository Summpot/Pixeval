using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentIcons.Common;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.Commands.Transformers;
using Pixeval.I18N;
using Pixeval.Models.Extensions;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Viewers;
using static Pixeval.Tests.Ui.NovelReaderValidation;

namespace Pixeval.Tests.Ui;

internal static class NovelTranslationValidation
{
    internal static async Task RunAsync(string directory)
    {
        using var fixture = new NovelReaderFixture();
        await App.AppViewModel.MakoClient.SetTokenAsync("fixture-translation");
        await App.AppViewModel.MakoClient.ValidateTokenAsync();
        var extensions = App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>();
        var first = new Translator("First translator");
        var second = new Translator("Second translator · 翻译扩展的长名称");
        using var extensionHost = new ExtensionsHostModel(new FixtureHost(first, second), []);
        using var source = new SimpleOperableSourceView<NovelItemViewModel>([
            NovelReaderFixture.Sample(995001), NovelReaderFixture.Sample(995002), NovelReaderFixture.Sample(995003)]);
        var model = new NovelViewerPageViewModel(source.CloneSourceView(), 0, false);
        var page = new NovelViewerPage(model);
        var host = new PageTestHost { Content = page };
        var window = new Window { Width = 1100, Height = 800, Content = host.Shell };
        List<string> report = [];
        try
        {
            window.Show();
            (await fixture.NextAsync()).Complete("First original[newpage]Second original");
            await UntilAsync(() => !model.IsLoading);
            await RenderAsync(window);
            var bar = page.FindControl<CommandBar>("ReaderCommands")!;
            var translate = bar.PrimaryCommands.OfType<CommandBarButton>().Single(button => ReferenceEquals(button.Command, model.ToggleTranslationCommand));
            Require(!translate.IsVisible, "No installed translator leaves no unusable translation action");
            extensions.HostModels.Add(extensionHost);
            await RenderAsync(window);
            Require(translate.IsVisible && translate.IsEnabled, "Installing an active translator makes the existing reader action available");
            Require(translate.Label == I18NManager.GetResource(NovelViewerPageResources.TranslateFullText), "The initial translation label is localized");
            await ActivateAsync(window, translate);
            await UntilAsync(() => !model.ToggleTranslationCommand.IsRunning);
            Require(first.Inputs.Count == 2 && second.Inputs.Count == 0 && model.IsTranslationShown, "The main button translates all pages with the first active translator");
            model.NextCommand.Execute(null);
            Require(model.CurrentMarkdown.StartsWith(first.Label) && model.CurrentMarkdown.Contains("Second original") && model.PageCount == 2, "Translation preserves page index and count");
            extensionHost.IsActive = false;
            await RenderAsync(window);
            Require(translate.IsVisible && translate.IsEnabled, "Disabling the last translator retains the cached original/translation switch");
            await ActivateAsync(window, translate);
            Require(!model.IsTranslationShown && model.CurrentMarkdown.Contains("Second original"), "The reader can return to the original with no active extension");
            await ActivateAsync(window, translate);
            Require(model.IsTranslationShown && first.Inputs.Count == 2, "Showing cached translation needs no extension request");
            extensionHost.IsActive = true;

            await ChooseTranslatorAsync(window, translate, second);
            await UntilAsync(() => !model.ToggleTranslationCommand.IsRunning);
            Require(second.Inputs.Count == 2 && model.CurrentMarkdown.StartsWith(second.Label), "The actual context menu selects a different translator for all pages");
            report.Add("PASS availability/cache/menu: add/disable/enable host, priority, both cached directions and explicit translator selection preserve pages");

            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = theme;
                window.Width = theme == ThemeVariant.Light ? 1100 : 360;
                await RenderAsync(window);
                first.Hold = true;
                await ChooseTranslatorAsync(window, translate, first);
                Require(model.ToggleTranslationCommand.IsRunning && model.CurrentMarkdown.StartsWith(second.Label), "An unfinished replacement leaves the successful translation readable");
                var cancel = page.FindControl<CommandBarButton>("CancelTranslationButton") ?? throw new InvalidOperationException("No cancel button");
                Require(cancel.IsVisible && cancel.IsEnabled, "A running translation exposes a reachable cancel action");
                Require(cancel.Label == I18NManager.GetResource(NovelViewerPageResources.CancelTranslation), "The cancellation label is localized");
                if (cancel.IsInOverflow)
                    await OpenOverflowAsync(window, bar);
                Save(window, directory, "translation-pending-" + theme.Key);
                await ActivateAsync(window, cancel);
                await UntilAsync(() => !model.ToggleTranslationCommand.IsRunning);
                var oldInputCount = first.Inputs.Count;
                await ChooseTranslatorAsync(window, translate, first);
                Require(model.ToggleTranslationCommand.IsRunning && first.Inputs.Count == oldInputCount, "Retry waits for the same extension's uncancellable previous call");
                first.Complete("late cancelled result");
                await UntilAsync(() => first.Inputs.Count == oldInputCount + 1);
                first.Complete("replacement page one");
                await UntilAsync(() => first.Inputs.Count == oldInputCount + 2);
                first.Complete("replacement page two");
                await UntilAsync(() => !model.ToggleTranslationCommand.IsRunning);
                Require(model.CurrentMarkdown == "replacement page two", "A cancelled extension result cannot contaminate the subsequent translation");
                first.Hold = false;
                await ChooseTranslatorAsync(window, translate, second);
                await UntilAsync(() => !model.ToggleTranslationCommand.IsRunning);
            }
            report.Add("PASS cancel/retry: light/wide and dark/narrow actual toolbar cancellation, retained content and serialized late-result isolation");

            first.Hold = true;
            await ChooseTranslatorAsync(window, translate, first);
            extensionHost.IsActive = false;
            await UntilAsync(() => !model.ToggleTranslationCommand.IsRunning);
            first.Complete("disabled late translation");
            await RenderAsync(window);
            Require(model.CurrentMarkdown.StartsWith(second.Label), "Disabling the active extension cancels its wait and preserves the previous completed result");
            extensionHost.IsActive = true;

            await ChooseTranslatorAsync(window, translate, first);
            model.NextWorkCommand.Execute(null);
            (await fixture.NextAsync()).Complete("New novel original");
            await UntilAsync(() => !model.IsLoading && !model.ToggleTranslationCommand.IsRunning);
            var newOriginal = model.CurrentMarkdown;
            Require(newOriginal.Contains("New novel original"), "The second work loaded its expected source content");
            first.Complete("previous novel late translation");
            await RenderAsync(window);
            Require(!model.IsTranslationShown && model.CurrentMarkdown == newOriginal, "Switching works cancels translation and drops the previous work's cache");

            first.Hold = false;
            foreach (var failure in new[] { "empty", "exception" })
            {
                first.Result = failure == "empty" ? "" : null;
                first.Throw = failure == "exception";
                await ActivateAsync(window, translate);
                await UntilAsync(() => !model.ToggleTranslationCommand.IsRunning);
                Require(!model.IsTranslationShown && model.CurrentMarkdown == newOriginal && translate.IsEnabled, "Failed translation leaves the original and permits retry: " + failure);
            }
            first.Result = null;
            first.Throw = false;
            await ActivateAsync(window, translate);
            await UntilAsync(() => !model.ToggleTranslationCommand.IsRunning);
            Require(model.IsTranslationShown, "Retry after both extension failure modes succeeds");

            first.Hold = true;
            await ChooseTranslatorAsync(window, translate, first);
            extensions.HostModels.Remove(extensionHost);
            await UntilAsync(() => !model.ToggleTranslationCommand.IsRunning);
            first.Complete("removed late translation");
            await RenderAsync(window);
            Require(!model.CurrentMarkdown.Contains("removed"), "Removing the active host cancels its pending reader operation");
            extensions.HostModels.Add(extensionHost);
            await ChooseTranslatorAsync(window, translate, first);
            host.Shell.ClosePage(page);
            await UntilAsync(() => !model.ToggleTranslationCommand.IsRunning);
            var notifications = 0;
            model.PropertyChanged += (_, _) => notifications++;
            first.Complete("closed late translation");
            extensionHost.IsActive = false;
            await RenderAsync(window);
            Require(notifications == 0, "Closing the real reader tab detaches host events and prevents late result notifications");
            report.Add("PASS lifetime/failure: disable/remove host, switch works, empty/exception/retry, real tab close and no late writeback");
            File.WriteAllLines(Path.Combine(directory, "novel-translation-contracts.txt"), report);
            Require(fixture.Http.UnexpectedRequests.IsEmpty, "Translation only uses the offline novel fixture");
        }
        finally
        {
            window.Close();
            extensions.HostModels.Remove(extensionHost);
        }
    }

    private static async Task ChooseTranslatorAsync(Window window, CommandBarButton button, Translator translator)
    {
        if (button.IsInOverflow)
        {
            var bar = window.GetVisualDescendants().OfType<CommandBar>()
                .Single(candidate => candidate.PrimaryCommands.Any(command => ReferenceEquals(command, button)));
            await OpenOverflowAsync(window, bar);
        }
        Require(button.Focus(), "The translator button is focusable before opening its menu");
        Press(window, Key.Apps, PhysicalKey.ContextMenu);
        await RenderAsync(window);
        var menu = button.ContextMenu!;
        Require(menu.IsOpen, "The context-menu key opens the actual translator context menu");
        var index = menu.ItemsSource!.Cast<TextTransformerExtensionCommandItem>().ToList().FindIndex(item => ReferenceEquals(item.Extension, translator));
        var item = (MenuItem) menu.ContainerFromIndex(index)!;
        Require(item.IsEffectivelyVisible && item.Focus(), "The selected active translator is reachable in the menu");
        Press(window, Key.Enter, PhysicalKey.Enter);
        await RenderAsync(window);
    }

    private sealed class Translator(string label) : ITextTransformerCommandExtension
    {
        public string Label => label;
        public List<string> Inputs { get; } = [];
        public bool Hold { get; set; }
        public bool Throw { get; set; }
        public string? Result { get; set; }
        private ITaskCompletionSource? _pending;
        private string _currentResult = "";
        public void TransformAsync(ITaskCompletionSource task, string originalString, TextTransformerType type)
        {
            Require(type == TextTransformerType.Novel, "Full text preserves the Novel extension argument");
            Require(_pending is null, "An extension with a shared result slot is never called concurrently");
            Inputs.Add(originalString);
            if (Throw) throw new InvalidOperationException("Fixed extension failure");
            _pending = task;
            if (!Hold) Complete(Result ?? label + ":translated:" + originalString);
        }
        public void Complete(string result)
        {
            var task = _pending ?? throw new InvalidOperationException("No pending transform");
            _pending = null;
            _currentResult = result;
            task.SetCompleted();
        }
        public string GetTransformResult() => _currentResult;
        public Symbol GetIcon() => Symbol.Translate;
        public string GetLabel() => label;
        public string GetDescription() => "Offline translator fixture";
        public void OnExtensionLoaded() { }
        public void OnExtensionUnloaded() { }
    }

    private sealed class FixtureHost(params IExtension[] extensions) : IExtensionsHost
    {
        public string GetExtensionName() => "Reader translation fixture";
        public string GetAuthorName() => "Pixeval tests";
        public string GetExtensionLink() => "";
        public string GetHelpLink() => "";
        public string GetDescription() => "Managed offline translation boundary";
        public string GetSdkVersion() => ExtensionService.CurrentVersion;
        public string GetVersion() => "1.0";
        public IExtension[] GetExtensions(out int returnCount) { returnCount = extensions.Length; return extensions; }
        public byte[]? GetIcon(out int returnCount) { returnCount = 0; return null; }
        public void Initialize(string cultureName, string tempDirectory, string extensionDirectory, ILogger logger) { }
    }
}


