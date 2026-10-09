// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Globalization;
using Pixeval.Models.Options;
using Pixeval.Native.Mako;

namespace Pixeval.Native.Config;

public partial record HomePageCardLayout
{
    public HomePageCardLayout()
        : this(HomePageCardSourceKind.WorkRecommended, 0, 0, 1, 1)
    {
    }

    public HomePageCardLayout(
        HomePageCardSourceKind sourceKind,
        int column,
        int row,
        int columnSpan = 1,
        int rowSpan = 1)
        : this(
            sourceKind,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            null,
            null,
            0,
            false,
            null,
            column,
            row,
            columnSpan,
            rowSpan)
    {
    }

    public WorkType WorkType => (WorkType)RawWorkType;

    public SimpleWorkType SimpleWorkType => (SimpleWorkType)RawSimpleWorkType;

    public PrivacyPolicy PrivacyPolicy => (PrivacyPolicy)RawPrivacyPolicy;

    public RankOption RankOption => (RankOption)RawRankOption;

    public DateTimeOffset RankingDate
    {
        get
        {
            if (UseSpecifiedRankingDate && !string.IsNullOrWhiteSpace(RawRankingDate))
            {
                if (DateTimeOffset.TryParse(RawRankingDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                {
                    return dt;
                }
            }
            return default;
        }
    }

    public DateTimeOffset GetRankingDate() =>
        UseSpecifiedRankingDate && RankingDate != default
            ? RankingDate
            : MakoClient.RankingMaxDateTime;
}
