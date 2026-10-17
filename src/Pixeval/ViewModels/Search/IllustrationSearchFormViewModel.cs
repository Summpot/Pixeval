// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Localization;
using Pixeval.Mako;

namespace Pixeval.ViewModels.Search;

public partial class IllustrationSearchFormViewModel : SearchArgumentsFormViewModelBase
{
    public IllustrationSearchFormViewModel(IUserSessionService? userSession = null) : base(userSession)
    {
    }

    [ObservableProperty]
    public partial IllustSearchTarget MatchOption { get; set; } = IllustSearchTarget.PartialMatchForTags;

    [ObservableProperty]
    public partial SearchIllustrationContentType ContentType { get; set; } = SearchIllustrationContentType.IllustrationAndMangaAndUgoira;

    [ObservableProperty]
    public partial SearchIllustrationRatioPattern RatioPattern { get; set; } = SearchIllustrationRatioPattern.All;

    [ObservableProperty]
    public partial IReadOnlyList<string> ToolItems { get; set; } = [CommonUnspecified];

    [ObservableProperty]
    public partial string Tool { get; set; } = CommonUnspecified;

    public OptionalIntSearchOptionViewModel WidthMinOption { get; } = new();

    public OptionalIntSearchOptionViewModel WidthMaxOption { get; } = new();

    public OptionalIntSearchOptionViewModel HeightMinOption { get; } = new();

    public OptionalIntSearchOptionViewModel HeightMaxOption { get; } = new();

    public override bool TryValidate(out string title, out string content)
    {
        if (!base.TryValidate(out title, out content))
            return false;

        if (HasInvalidRange(WidthMinOption.NullableInt, WidthMaxOption.NullableInt)
            || HasInvalidRange(HeightMinOption.NullableInt, HeightMaxOption.NullableInt))
        {
            title = Strings.SearchValidationInvalidNumericRangeTitle();
            content = Strings.SearchValidationInvalidNumericRangeContent();
            return false;
        }

        title = content = "";
        return true;
    }

    public IllustrationSearchParams ToSearchParams(string searchText) =>
        new(
            Word: searchText,
            SearchTarget: MatchOption,
            Sort: SortOption,
            SearchAiType: AiType ? 1 : 0,
            ContentType: ContentType.ToIllustContentType(),
            RatioPattern: RatioPattern.ToRatioPattern(),
            MergePlainKeywordResults: MergePlainKeywordResults,
            IncludeTranslatedTagResults: IncludeTranslatedTagResults,
            IncludePotentialViolationWorks: IncludePotentialViolationWorks,
            StartDate: StartDateOption.ToNullableDateTimeOffset().ToQueryDate(),
            EndDate: EndDateOption.ToNullableDateTimeOffset().ToQueryDate(),
            WidthMin: WidthMinOption.NullableInt,
            WidthMax: WidthMaxOption.NullableInt,
            HeightMin: HeightMinOption.NullableInt,
            HeightMax: HeightMaxOption.NullableInt,
            Tool: Tool == CommonUnspecified ? null : Tool);
}
