// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Config;

namespace Pixeval.Config;

public static class NavigationYamlFormatter
{
    private static readonly ConfigEngine Engine = new();

    public static string Format(NavigationConfiguration configuration) =>
        Engine.FormatNavigationYaml(configuration.ToYamlSettings()).ReplaceLineEndings(Environment.NewLine);
}
