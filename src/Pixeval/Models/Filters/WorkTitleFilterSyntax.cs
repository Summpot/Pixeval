using System.Collections.Generic;
using Misaki;
using Pixeval.Filters.Syntax;
using Pixeval.I18N;

namespace Pixeval.Models.Filters;

[FilterSyntax<IArtworkInfo>]
internal sealed class WorkTitleFilterSyntax : FilterTextSyntax<IArtworkInfo>
{
    public const string KeyConst = "Title";

    /// <summary>
    /// 标题筛选语法，支持默认文本和 title: 前缀。
    /// </summary>
    public override string Key => KeyConst;

    public override string? ExampleValue => "keyword";

    public override IReadOnlyList<FilterSyntaxPattern> Patterns { get; } =
    [
        FilterSyntaxPattern.Default("keyword", I18NManager.GetResource(FilterResources.Completions.Title)),
        FilterSyntaxPattern.Keyword("title", exampleValue: "keyword", description: I18NManager.GetResource(FilterResources.Completions.Title))
    ];
}
