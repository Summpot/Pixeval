namespace Pixeval.Filters.Syntax;

public abstract class FilterDateSyntax<TContext> : FilterSyntax<TContext>
{
    public sealed override FilterValueKind ValueKind => FilterValueKind.Date;
}
