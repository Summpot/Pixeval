// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Config;

[LocalizationMetadata]
public enum ApplicationTheme
{
    [LocalizedResource(EnumResources.ApplicationTheme.Default)]
    Default,

    [LocalizedResource(EnumResources.ApplicationTheme.Light)]
    Light,

    [LocalizedResource(EnumResources.ApplicationTheme.Dark)]
    Dark
}
