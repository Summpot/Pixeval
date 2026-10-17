// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Config;

namespace Pixeval.Config;

public static class NavigationYamlFormatter
{
    public static string Format(NavigationConfiguration configuration) =>
        PixevalConfigMethods.FormatNavigationYaml(configuration.ToYamlSettings()).ReplaceLineEndings(Environment.NewLine);
}
