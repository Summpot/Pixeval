// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;

namespace Pixeval.Filters.Syntax;

/// <summary>
/// 表示一个可由外部注册的过滤语法定义。
/// </summary>
public abstract class FilterSyntax
{
    public abstract string Key { get; }

    public abstract FilterValueKind ValueKind { get; }

    public abstract IReadOnlyList<FilterSyntaxPattern> Patterns { get; }

    public virtual string? ExampleValue => null;
}

/// <summary>
/// 表示作用于 <typeparamref name="TContext" /> 的过滤语法定义基类。
/// </summary>
public abstract class FilterSyntax<TContext> : FilterSyntax;
