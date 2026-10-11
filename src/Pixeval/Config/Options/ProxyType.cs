// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Config;

[LocalizationMetadata]
public enum ProxyType
{
    [LocalizedResource(EnumResources.ProxyOption.System)]
    System,

    [LocalizedResource(EnumResources.ProxyOption.None)]
    None,

    [LocalizedResource(EnumResources.ProxyOption.Custom)]
    Custom
}
