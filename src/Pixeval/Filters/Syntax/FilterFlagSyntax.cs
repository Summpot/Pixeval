namespace Pixeval.Filters.Syntax;

public abstract class FilterFlagSyntax<TContext> : FilterSyntax<TContext>
{
    public sealed override FilterValueKind ValueKind => FilterValueKind.Flag;
}
