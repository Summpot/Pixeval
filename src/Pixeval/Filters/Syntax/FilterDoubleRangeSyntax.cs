namespace Pixeval.Filters.Syntax;

public abstract class FilterDoubleRangeSyntax<TContext> : FilterSyntax<TContext>
{
    public sealed override FilterValueKind ValueKind => FilterValueKind.DoubleRange;
}
