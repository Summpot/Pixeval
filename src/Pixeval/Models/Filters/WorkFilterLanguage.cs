// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Native.Filters;

namespace Pixeval.Models.Filters;

/// <summary>
/// 提供作品列表使用的筛选语言与补全引擎门面。
/// </summary>
public static class WorkFilterLanguage
{
    /// <summary>
    /// 获取全局默认的作品筛选补全引擎。
    /// </summary>
    public static FilterCompletionEngine CompletionEngine => FilterCompletionEngine.Default;

    /// <summary>
    /// 兼容旧代码的筛选引擎实例引用。
    /// </summary>
    public static FilterCompletionEngine Instance => CompletionEngine;
}
