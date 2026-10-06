using System.Collections.Generic;
using Misaki;
using Pixeval.Filters.Syntax;
using Pixeval.I18N;

namespace Pixeval.Models.Filters;

[FilterSyntax<IArtworkInfo>]
internal sealed class WorkAuthorFilterSyntax : FilterTextSyntax<IArtworkInfo>
{
    public const string KeyConst = "Author";

    /// <summary>
    /// 作者筛选语法，支持 @、a: 和 artist: 写法。
    /// </summary>
    public override string Key => KeyConst;

    public override string? ExampleValue => "artist";

    public override IReadOnlyList<FilterSyntaxPattern> Patterns { get; } =
    [
        FilterSyntaxPattern.PrefixOnly("@", "artist", I18NManager.GetResource(FilterResources.Completions.Author)),
        FilterSyntaxPattern.Keyword("a", exampleValue: "artist", description: I18NManager.GetResource(FilterResources.Completions.Author)),
        FilterSyntaxPattern.Keyword("artist", exampleValue: "artist", description: I18NManager.GetResource(FilterResources.Completions.Author)),
        FilterSyntaxPattern.Keyword("author", exampleValue: "artist", description: I18NManager.GetResource(FilterResources.Completions.Author))
    ];
}
