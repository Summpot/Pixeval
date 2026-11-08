// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.Commands.Transformers;
using Pixeval.Utilities;

namespace Pixeval.ViewModels.Viewers;

public sealed partial class NovelViewerPageViewModel
{
    private List<string>? _translatedPageMarkdowns;
    private ITextTransformerCommandExtension? _activeTranslator;
    private CancellationTokenSource? _translationCts;

    public bool HasTranslator => App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>()
        .ActiveTextTransformerCommands.Any();

    public bool IsTranslationAvailable => HasTranslator || _translatedPageMarkdowns is not null;

    public IReadOnlyList<TextTransformerExtensionCommandItem> TransformerExtensionItems =>
    [
        .. App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>()
            .ActiveTextTransformerCommands.Select(extension => new TextTransformerExtensionCommandItem(extension, ToggleTranslationCommand))
    ];

    internal void RefreshTranslationAvailability()
    {
        OnPropertyChanged(nameof(HasTranslator));
        OnPropertyChanged(nameof(IsTranslationAvailable));
        OnPropertyChanged(nameof(TransformerExtensionItems));
        ToggleTranslationCommand.NotifyCanExecuteChanged();
        if (_activeTranslator is { } translator && !IsTranslatorActive(translator))
            _translationCts?.Cancel();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentMarkdown))]
    [NotifyPropertyChangedFor(nameof(TranslationButtonText))]
    public partial bool IsTranslationShown { get; private set; }

    public string TranslationButtonText => IsTranslationShown
        ? Strings.NovelViewerPageShowOriginal()
        : _translatedPageMarkdowns is not null
            ? Strings.NovelViewerPageShowTranslation()
            : Strings.NovelViewerPageTranslateFullText();

    private bool CanTranslate => !_disposed && !IsLoading && LoadErrorMessage is null && PageCount > 0 && IsTranslationAvailable
        && CurrentNovel is { } novel && !BlockedContentHelper.IsBlockedPlaceholder(novel);

    private static bool IsTranslatorActive(ITextTransformerCommandExtension translator) =>
        App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>().ActiveTextTransformerCommands.Contains(translator);

    [RelayCommand(CanExecute = nameof(CanTranslate), IncludeCancelCommand = true)]
    private async Task ToggleTranslationAsync(Control? control, CancellationToken token)
    {
        if (!CanTranslate)
            return;

        var translator = control?.Tag as ITextTransformerCommandExtension;
        if (translator is null && _translatedPageMarkdowns is not null)
        {
            IsTranslationShown = !IsTranslationShown;
            return;
        }

        var translators = App.AppViewModel.AppServiceProvider.GetRequiredService<ExtensionService>()
            .ActiveTextTransformerCommands;
        translator ??= translators.FirstOrDefault();
        if (translator is null || !translators.Contains(translator))
            return;

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, _loadingCts.Token);
        _activeTranslator = translator;
        _translationCts = cancellation;
        token = cancellation.Token;
        var viewContainer = control is null ? null : TopLevel.GetTopLevel(control)?.ViewContainer;
        try
        {
            viewContainer?.ShowInformation(Strings.NovelViewerPageApplyingTransformerExtensions());
            var pages = _pageMarkdowns;
            var translatedPages = new List<string>(pages.Count);
            foreach (var page in pages)
            {
                token.ThrowIfCancellationRequested();
                var translated = string.IsNullOrWhiteSpace(page) ? page :
                    await TextTransformation.RunAsync(translator, page, TextTransformerType.Novel, token);
                token.ThrowIfCancellationRequested();
                // A temporarily hidden reader does not observe host changes until loaded.
                if (!IsTranslatorActive(translator))
                    return;
                if (string.IsNullOrWhiteSpace(translated) && !string.IsNullOrWhiteSpace(page))
                {
                    viewContainer?.ShowError(Strings.NovelViewerPageTransformerExtensionFailed());
                    return;
                }

                translatedPages.Add(translated);
            }

            // 完整翻译成功后再切换显示，取消或异常时仍可继续阅读原文。
            _translatedPageMarkdowns = translatedPages;
            OnPropertyChanged(nameof(IsTranslationAvailable));
            if (IsTranslationShown)
                OnPropertyChanged(nameof(CurrentMarkdown));
            else
                IsTranslationShown = true;
            viewContainer?.ShowSuccess(Strings.NovelViewerPageTransformerExtensionFinishedSuccessfully());
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            if (token.IsCancellationRequested)
                return;

            viewContainer?.ShowError(Strings.NovelViewerPageTransformerExtensionFailed(), e.Message);
            App.AppViewModel.AppServiceProvider.GetRequiredService<FileLogger>()
                .LogError(nameof(NovelViewerPageViewModel), e);
        }
        finally
        {
            _translationCts = null;
            _activeTranslator = null;
        }
    }
}

public sealed class TextTransformerExtensionCommandItem(ITextTransformerCommandExtension extension, IAsyncRelayCommand<Control?> command)
{
    public ITextTransformerCommandExtension Extension { get; } = extension;

    public string Label { get; } = extension.Label;

    public string Description { get; } = extension.Description;

    public Symbol Symbol { get; } = extension.Icon;

    public IAsyncRelayCommand<Control?> Command { get; } = command;
}
