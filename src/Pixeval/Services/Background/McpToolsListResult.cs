using System.Collections.Generic;

namespace Pixeval.Services.Background;

internal sealed record McpToolsListResult
{
    public IReadOnlyList<McpToolSchema>? Tools { get; init; }
}
