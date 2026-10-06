// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Native.Config;

namespace Pixeval.Models.Navigation;

public static class NavigationYamlFormatter
{
    private static readonly ConfigEngine Engine = new();

    public static string Format(NavigationConfiguration configuration) =>
        Engine.FormatNavigationYaml(configuration.ToYamlSettings()).ReplaceLineEndings(Environment.NewLine);
}
