using System.Collections.Generic;
using Misaki;
using Pixeval.Filters.Syntax;
using Pixeval.I18N;

namespace Pixeval.Models.Filters;

[FilterSyntax<IArtworkInfo>]
internal sealed class WorkTagFilterSyntax : FilterTextSyntax<IArtworkInfo>
{
    public const string KeyConst = "Tag";

    /// <summary>
    /// 标签筛选语法，支持 #、t: 和 tag: 写法。
    /// </summary>
    public override string Key => KeyConst;

    public override string? ExampleValue => "tag";

    public override IReadOnlyList<FilterSyntaxPattern> Patterns { get; } =
    [
        FilterSyntaxPattern.PrefixOnly("#", "tag", I18NManager.GetResource(FilterResources.Completions.Tag)),
        FilterSyntaxPattern.Keyword("t", exampleValue: "tag", description: I18NManager.GetResource(FilterResources.Completions.Tag)),
        FilterSyntaxPattern.Keyword("tag", exampleValue: "tag", description: I18NManager.GetResource(FilterResources.Completions.Tag))
    ];
}
