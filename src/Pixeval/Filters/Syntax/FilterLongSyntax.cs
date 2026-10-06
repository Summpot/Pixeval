namespace Pixeval.Filters.Syntax;

public abstract class FilterLongSyntax<TContext> : FilterSyntax<TContext>
{
    public sealed override FilterValueKind ValueKind => FilterValueKind.Long;
}
