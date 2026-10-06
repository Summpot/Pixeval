namespace Pixeval.Filters.Syntax;

public abstract class FilterLongRangeSyntax<TContext> : FilterSyntax<TContext>
{
    public sealed override FilterValueKind ValueKind => FilterValueKind.LongRange;
}
