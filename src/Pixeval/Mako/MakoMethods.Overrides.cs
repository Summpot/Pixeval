// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#pragma warning disable CS0436

using System.Collections.Generic;
using UniFFISharp;

namespace Pixeval.Mako;

public static partial class PixevalMakoMethods
{
    [PreferUniFFI]
    public static partial SafeRatingKind GetSafeRating(XRestrict xRestrict, List<RestrictionFlag>? attributes, int sanityLevel);

    [PreferUniFFI]
    public static partial List<RankMode> SupportedRankModes(SimpleWorkKind kind);
}
