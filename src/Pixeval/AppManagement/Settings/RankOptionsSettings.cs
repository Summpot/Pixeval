// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.AppManagement.Settings;

public record RankOptionsSettings
{
    public RankOption IllustrationRankOption { get; set; }

    public RankOption NovelRankOption { get; set; }
}
