// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Diagnostics.CodeAnalysis;
using FluentIcons.Common;

namespace Pixeval.Config;

public sealed record NavigationPageDefinition(
    string Key,
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    [param: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    Type PageType,
    Symbol Icon,
    string HeaderResource,
    string InitialHeader,
    bool NeedLogin)
{
    public string Header => Strings.GetString(HeaderResource);
}
