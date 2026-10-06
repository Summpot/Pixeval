namespace Pixeval.Filters.Syntax;

public abstract class FilterTextSyntax<TContext> : FilterSyntax<TContext>
{
    public sealed override FilterValueKind ValueKind => FilterValueKind.Text;
}
