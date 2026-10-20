// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Attributes;

namespace Pixeval.Config;

public enum ProxyType
{
    [LocalizedResource(nameof(Strings.EnumProxyOptionSystem))]
    System,

    [LocalizedResource(nameof(Strings.EnumProxyOptionNone))]
    None,

    [LocalizedResource(nameof(Strings.EnumProxyOptionCustom))]
    Custom
}
