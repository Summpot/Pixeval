// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;

namespace Pixeval.Native.Filters;

public partial record FilterSyntaxPattern
{
    public FilterSyntaxPattern(
        string Prefix,
        List<string> Aliases,
        string? Metadata = null,
        string? ExampleValue = null,
        string? Description = null)
        : this(Prefix, Aliases, "", Metadata, ExampleValue, Description)
    {
    }

    public static FilterSyntaxPattern Default(string? exampleValue = null, string? description = null)
        => new("", [""], "", null, exampleValue, description);

    public static FilterSyntaxPattern PrefixOnly(string prefix, string? exampleValue = null, string? description = null)
        => new(prefix, [""], "", null, exampleValue, description);

    public static FilterSyntaxPattern Keyword(string keyword, string suffix = ":", string? exampleValue = null, string? description = null)
        => new("", [keyword], suffix, null, exampleValue, description);
}
