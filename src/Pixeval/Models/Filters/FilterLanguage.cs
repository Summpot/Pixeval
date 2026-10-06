// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Pixeval.Filters.Analysis;
using Pixeval.Filters.Syntax;
using Pixeval.Native.Filters;

namespace Pixeval.Models.Filters;

/// <summary>
/// 负责把外部注册的过滤语法组织成可解析、可补全的语言对象。
/// </summary>
public sealed class FilterLanguage : IDisposable
{
    private readonly FilterEngine _engine;

    public FilterLanguage(
        IEnumerable<FilterSyntax> syntaxes,
        IReadOnlyCollection<FilterCompletionDefinition>? intrinsicCompletions = null,
        IReadOnlyCollection<FilterFullCompletionDefinition>? fullCompletions = null,
        IReadOnlyDictionary<FilterValueKind, IReadOnlyCollection<FilterCompletionDefinition>>? valueHintCompletions = null)
    {
        var syntaxList = syntaxes.Select(syntax => new FilterSyntaxDefinition(
            syntax.Key,
            syntax.ValueKind,
            syntax.ExampleValue,
            [.. syntax.Patterns]
        )).ToList();

        var intrinsicList = intrinsicCompletions?.ToList();
        var fullList = fullCompletions?.ToList();

        List<FilterValueHintGroup>? hintGroups = null;
        if (valueHintCompletions != null)
        {
            hintGroups = valueHintCompletions.Select(kv => new FilterValueHintGroup(
                kv.Key,
                kv.Value.ToList()
            )).ToList();
        }

        _engine = new FilterEngine(syntaxList, intrinsicList, fullList, hintGroups);
    }

    public FilterAnalysisResult Analyze(string text) => Analyze(text, text.Length, null);

    public FilterAnalysisResult Analyze(string text, int caretPosition) => Analyze(text, caretPosition, null);

    public FilterAnalysisResult Analyze(
        string text,
        int caretPosition,
        FilterValueCompletionProvider? callback)
    {
        IFilterCompletionCallback? rustCallback = callback is not null ? new CallbackBridge(callback) : null;
        return _engine.Analyze(text, Math.Clamp(caretPosition, 0, text.Length), rustCallback);
    }

    public void Dispose() => _engine.Dispose();

    private sealed class CallbackBridge(FilterValueCompletionProvider provider) : IFilterCompletionCallback
    {
        public List<FilterCompletionDefinition> GetCompletions(FilterValueCompletionContext context)
        {
            var items = provider(context);
            if (items == null || items.Count == 0)
                return [];

            return items.ToList();
        }
    }
}
