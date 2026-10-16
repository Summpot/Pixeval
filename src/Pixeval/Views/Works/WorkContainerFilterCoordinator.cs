// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Pixeval.Booru;
using Pixeval.Filters;
using Pixeval.I18N;
using Pixeval.Mako;
using Pixeval.SauceNao;
using Pixeval.ViewModels;
using Pixeval.Views.Shell;

namespace Pixeval.Views.Works;

public class WorkContainerFilterCoordinator(FilterCompletionEngine completionEngine)
{
    private const string FilterDiagnosticResourcePrefix = "Filter.Diagnostics.";

    private IReadOnlyCollection<object>? _filterCompletionSource;
    private int _filterCompletionSourceCount = -1;

    public FilterAnalysisResult AnalyzeFilter(IOperableViewViewModel? viewModel, string? text, int caret)
    {
        if (viewModel is { Source.Count: > 0 })
            EnsureFilterValueCompletions(viewModel.Source);

        return completionEngine.Analyze(text ?? string.Empty, caret);
    }

    public void PerformSearch(
        IOperableViewViewModel? viewModel,
        WorkFilterAutoSuggestBox suggestBox,
        INotificationService notifications,
        string? text,
        int caret)
    {
        if (viewModel is null)
            return;

        if (string.IsNullOrWhiteSpace(text))
        {
            viewModel.UserFilter = null;
            suggestBox.ClearSelection();
            suggestBox.RefreshCompletions();
            return;
        }

        var analysis = AnalyzeFilter(viewModel, text, caret);
        suggestBox.RefreshCompletions(analysis);
        if (!analysis.IsSuccess || analysis.Query is not { } query)
        {
            if (analysis.Diagnostics.Count > 0)
            {
                suggestBox.HighlightDiagnostic(analysis.Diagnostics[0].Span);
                notifications.ShowError(
                    I18NManager.GetResource(FilterResources.FilterQueryError),
                    FormatDiagnosticMessage(analysis));
            }

            return;
        }

        viewModel.UserFilter = query.HasPredicates()
            ? o => query.MatchesArtwork(ToArtworkMetadata(o))
            : null;
        suggestBox.ClearSelection();
    }

    public void EnsureFilterValueCompletions(IReadOnlyCollection<object> source)
    {
        if (ReferenceEquals(_filterCompletionSource, source) && _filterCompletionSourceCount == source.Count)
            return;

        _filterCompletionSource = source;
        _filterCompletionSourceCount = source.Count;
        completionEngine.SetSessionArtworks(source.Select(ToArtworkMetadata).ToList());
    }

    public void ResetFilterValueCompletions()
    {
        _filterCompletionSource = null;
        _filterCompletionSourceCount = -1;
        completionEngine.ClearSessionCandidates();
    }

    public static string FormatDiagnosticMessage(FilterAnalysisResult analysis)
    {
        var diagnostic = analysis.Diagnostics[0];
        var completionSuffix = analysis.Completions.Count > 0
            ? I18NManager.GetResource(
                FilterResources.Diagnostics.CompletionSuffixFormatted,
                string.Join(", ", analysis.Completions.Select(t => t.DisplayText).Distinct(StringComparer.OrdinalIgnoreCase).Take(6)))
            : "";
        return I18NManager.GetResource(
            FilterResources.Diagnostics.MessageWithPositionFormatted,
            FormatDiagnostic(diagnostic),
            diagnostic.Span.Start + 1,
            completionSuffix);
    }

    public static string FormatDiagnostic(FilterDiagnostic diagnostic)
    {
        var arguments = diagnostic.Arguments;
        return arguments.Count > 0
            ? I18NManager.GetResource(GetFilterDiagnosticResourceKey(diagnostic.Kind), [.. arguments])
            : I18NManager.GetResource(GetFilterDiagnosticResourceKey(diagnostic.Kind));
    }

    public static string GetFilterDiagnosticResourceKey(FilterDiagnosticKind kind) =>
        FilterDiagnosticResourcePrefix + kind;

    private static ArtworkMetadata ToArtworkMetadata(object info) => info switch
    {
        Illustration illust => illust.ToArtworkMetadata(),
        Mako.Novel novel => novel.ToArtworkMetadata(),
        BooruPost booru => booru.ToArtworkMetadata(),
        SauceNaoItem sauce => sauce.ToArtworkMetadata(),
        WorkEntry we => we.ToArtworkMetadata(),
        _ => new ArtworkMetadata(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0,
            0,
            0,
            0,
            0,
            0,
            0)
    };
}
