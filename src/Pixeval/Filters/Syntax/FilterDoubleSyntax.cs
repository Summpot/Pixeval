namespace Pixeval.Filters.Syntax;

public abstract class FilterDoubleSyntax<TContext> : FilterSyntax<TContext>
{
    public sealed override FilterValueKind ValueKind => FilterValueKind.Double;
}
