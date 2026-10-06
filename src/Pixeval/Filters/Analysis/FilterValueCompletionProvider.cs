// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Pixeval.Native.Filters;

namespace Pixeval.Filters.Analysis;

/// <summary>
/// 为特定语法值提供上下文补全的委托。
/// </summary>
public delegate IReadOnlyList<FilterCompletionDefinition>? FilterValueCompletionProvider(FilterValueCompletionContext context);
