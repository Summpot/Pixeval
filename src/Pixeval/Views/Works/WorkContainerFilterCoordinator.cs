// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Pixeval.Booru;
using Pixeval.Filters;
using Pixeval.Localization;
using Pixeval.Mako;
using Pixeval.SauceNao;
using Pixeval.ViewModels;
using Pixeval.Views.Shell;

namespace Pixeval.Views.Works;

public class WorkContainerFilterCoordinator(LocalizedFilterSession engines)
{

    private FilterSession CompletionEngine => engines.Current;
    private IReadOnlyCollection<object>? _filterCompletionSource;
    private int _filterCompletionSourceCount = -1;
    private string? _completionLocale;

    public FilterAnalysisResult AnalyzeFilter(IOperableViewViewModel? viewModel, string? text, int caret)
    {
        if (viewModel is { Source.Count: > 0 })
            EnsureFilterValueCompletions(viewModel.Source);

        return CompletionEngine.Analyze(text ?? string.Empty, caret);
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
                    Strings.FilterFilterQueryError(),
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
        if (_completionLocale == Strings.CurrentLocale && ReferenceEquals(_filterCompletionSource, source) && _filterCompletionSourceCount == source.Count)
            return;

        _completionLocale = Strings.CurrentLocale;
        _filterCompletionSource = source;
        _filterCompletionSourceCount = source.Count;
        CompletionEngine.SetSessionArtworks(source.Select(ToArtworkMetadata).ToList());
    }

    public void ResetFilterValueCompletions()
    {
        _filterCompletionSource = null;
        _filterCompletionSourceCount = -1;
        CompletionEngine.ClearSessionCandidates();
    }

    public static string FormatDiagnosticMessage(FilterAnalysisResult analysis)
    {
        var diagnostic = analysis.Diagnostics[0];
        var completionSuffix = analysis.Completions.Count > 0
            ? Strings.FilterDiagnosticsCompletionSuffixFormatted(completions: string.Join(", ", analysis.Completions.Select(t => t.DisplayText).Distinct(StringComparer.OrdinalIgnoreCase).Take(6)))
            : "";
        return Strings.FilterDiagnosticsMessageWithPositionFormatted(message: FormatDiagnostic(diagnostic), position: diagnostic.Span.Start + 1, completionSuffix: completionSuffix);
    }

    public static string FormatDiagnostic(FilterDiagnostic diagnostic)
    {
        var message = diagnostic.Kind switch
        {
            FilterDiagnosticKind.UnexpectedToken => Strings.FilterDiagnosticsUnexpectedToken(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.MissingPredicateAfterNegation => Strings.FilterDiagnosticsMissingPredicateAfterNegation(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.MissingTextValue => Strings.FilterDiagnosticsMissingTextValue(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.MissingLongValue => Strings.FilterDiagnosticsMissingLongValue(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.MissingDoubleValue => Strings.FilterDiagnosticsMissingDoubleValue(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.MissingRangeValue => Strings.FilterDiagnosticsMissingRangeValue(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.MissingDateValue => Strings.FilterDiagnosticsMissingDateValue(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.MissingGroupOperator => Strings.FilterDiagnosticsMissingGroupOperator(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.MissingRightParenthesis => Strings.FilterDiagnosticsMissingRightParenthesis(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.InvalidValue => Strings.FilterDiagnosticsInvalidValue(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.MissingStringQuote => Strings.FilterDiagnosticsMissingStringQuote(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.InvalidLongRangeFormat => Strings.FilterDiagnosticsInvalidLongRangeFormat(predicate: diagnostic.Arguments[0], value: diagnostic.Arguments[1]),
            FilterDiagnosticKind.InvalidDoubleRangeFormat => Strings.FilterDiagnosticsInvalidDoubleRangeFormat(predicate: diagnostic.Arguments[0], value: diagnostic.Arguments[1]),
            FilterDiagnosticKind.DateRequiresMonthAndDay => Strings.FilterDiagnosticsDateRequiresMonthAndDay(predicate: diagnostic.Arguments[0], value: diagnostic.Arguments[1]),
            FilterDiagnosticKind.ExpectedInteger => Strings.FilterDiagnosticsExpectedInteger(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.IntegerOutOfRange => Strings.FilterDiagnosticsIntegerOutOfRange(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.DenominatorCannotBeZero => Strings.FilterDiagnosticsDenominatorCannotBeZero(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.MissingFractionalPart => Strings.FilterDiagnosticsMissingFractionalPart(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.InvalidDoubleValue => Strings.FilterDiagnosticsInvalidDoubleValue(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.DateValueTooLarge => Strings.FilterDiagnosticsDateValueTooLarge(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.NegativeRangeUnsupported => Strings.FilterDiagnosticsNegativeRangeUnsupported(predicate: diagnostic.Arguments[0], value: diagnostic.Arguments[1]),
            FilterDiagnosticKind.RangeMinimumGreaterThanMaximum => Strings.FilterDiagnosticsRangeMinimumGreaterThanMaximum(predicate: diagnostic.Arguments[0], minimum: diagnostic.Arguments[1], maximum: diagnostic.Arguments[2]),
            FilterDiagnosticKind.DoubleRangeOpenIntervalUnsupported => Strings.FilterDiagnosticsDoubleRangeOpenIntervalUnsupported(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.InvalidDate => Strings.FilterDiagnosticsInvalidDate(predicate: diagnostic.Arguments[0], value: diagnostic.Arguments[1]),
            FilterDiagnosticKind.UnsupportedValueKind => Strings.FilterDiagnosticsUnsupportedValueKind(predicate: diagnostic.Arguments[0], value: diagnostic.Arguments[1]),
            FilterDiagnosticKind.InternalExpectedTextValue => Strings.FilterDiagnosticsInternalExpectedTextValue(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.InternalExpectedLongValue => Strings.FilterDiagnosticsInternalExpectedLongValue(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.InternalExpectedDoubleValue => Strings.FilterDiagnosticsInternalExpectedDoubleValue(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.InternalExpectedLongRangeValue => Strings.FilterDiagnosticsInternalExpectedLongRangeValue(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.InternalExpectedDoubleRangeValue => Strings.FilterDiagnosticsInternalExpectedDoubleRangeValue(value: diagnostic.Arguments[0]),
            FilterDiagnosticKind.InternalExpectedDateValue => Strings.FilterDiagnosticsInternalExpectedDateValue(value: diagnostic.Arguments[0]),
            _ => diagnostic.Kind.ToString()
        };
        return message;
    }

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
