// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;

namespace Pixeval.Filters.Syntax;

/// <summary>
/// 用于标记过滤语法实现类的特性，供源生成器发现并聚合。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class FilterSyntaxAttribute<TContext> : Attribute;
