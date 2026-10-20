// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Config;

public enum ApplicationTheme
{
    [LocalizedResource(nameof(Strings.EnumApplicationThemeDefault))]
    Default,

    [LocalizedResource(nameof(Strings.EnumApplicationThemeLight))]
    Light,

    [LocalizedResource(nameof(Strings.EnumApplicationThemeDark))]
    Dark
}
