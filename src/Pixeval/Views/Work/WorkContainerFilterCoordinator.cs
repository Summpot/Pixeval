// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Pixeval.Filters;
using Pixeval.I18N;
using Pixeval.Models.Filters;
using Pixeval.Native.Filters;
using Pixeval.ViewModels;
using Pixeval.Views.ViewContainers;

namespace Pixeval.Views.Work;

public class WorkContainerFilterCoordinator
{
    private const string FilterDiagnosticResourcePrefix = "Filter.Diagnostics.";

    private IReadOnlyCollection<object>? _filterCompletionSource;
    private int _filterCompletionSourceCount = -1;

    public FilterAnalysisResult AnalyzeFilter(IOperableViewViewModel? viewModel, string? text, int caret)
    {
        if (viewModel is { Source.Count: > 0 })
            EnsureFilterValueCompletions(viewModel.Source);

        return WorkFilterLanguage.CompletionEngine.Analyze(text ?? string.Empty, caret);
    }

    public void PerformSearch(
        IOperableViewViewModel? viewModel,
        WorkFilterAutoSuggestBox suggestBox,
        ViewContainerBase? viewContainer,
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
                viewContainer?.ShowError(
                    I18NManager.GetResource(FilterResources.FilterQueryError),
                    FormatDiagnosticMessage(analysis));
            }

            return;
        }

        viewModel.UserFilter = query.HasPredicates()
            ? o => query.MatchesArtwork(o.ToArtworkMetadata())
            : null;
        suggestBox.ClearSelection();
    }

    public void EnsureFilterValueCompletions(IReadOnlyCollection<object> source)
    {
        if (ReferenceEquals(_filterCompletionSource, source) && _filterCompletionSourceCount == source.Count)
            return;

        _filterCompletionSource = source;
        _filterCompletionSourceCount = source.Count;
        WorkFilterLanguage.CompletionEngine.SetSessionArtworks(source.Select(w => w.ToArtworkMetadata()).ToList());
    }

    public void ResetFilterValueCompletions()
    {
        _filterCompletionSource = null;
        _filterCompletionSourceCount = -1;
        WorkFilterLanguage.CompletionEngine.ClearSessionCandidates();
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
}
