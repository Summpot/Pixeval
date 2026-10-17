// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;

namespace Pixeval.Views.Home;

[Flags]
public enum HomeCardParameterKinds
{
    None = 0,
    WorkKind = 1 << 0,
    SimpleWorkKind = 1 << 1,
    PrivacyRestrict = 1 << 2,
    RankMode = 1 << 3,
    RankingDate = 1 << 4,
    UserId = 1 << 5,
    EntryId = 1 << 6,
    SearchText = 1 << 7,
    Tag = 1 << 8,
    SeriesId = 1 << 9
}
