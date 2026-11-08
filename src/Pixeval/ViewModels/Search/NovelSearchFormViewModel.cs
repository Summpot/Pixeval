// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Localization;
using Pixeval.Mako;

namespace Pixeval.ViewModels.Search;

public partial class NovelSearchFormViewModel : SearchArgumentsFormViewModelBase
{
    public NovelSearchFormViewModel(IUserSessionService? userSession = null) : base(userSession)
    {
    }

    [ObservableProperty]
    public partial NovelSearchTarget MatchOption { get; set; } = NovelSearchTarget.PartialMatchForTags;

    [ObservableProperty]
    public partial IReadOnlyList<SearchOptionsLanguage> LanguageItems { get; set; } =
    [
        new("", CommonUnspecified)
    ];

    [ObservableProperty]
    public partial string Language { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContentLengthEnabled))]
    public partial SearchNovelContentLengthOption ContentLengthOption { get; set; }

    [ObservableProperty]
    public partial bool IsOriginalOnly { get; set; }

    [ObservableProperty]
    public partial bool IsReplaceableOnly { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<SearchOptionsGenre> GenreItems { get; set; } =
    [
        new(0, CommonUnspecified)
    ];

    [ObservableProperty]
    public partial int GenreId { get; set; } = 0;

    public OptionalIntSearchOptionViewModel ContentLengthMinOption { get; } = new();

    public OptionalIntSearchOptionViewModel ContentLengthMaxOption { get; } = new();

    public bool IsContentLengthEnabled => ContentLengthOption is not SearchNovelContentLengthOption.None;

    public override bool TryValidate(out string title, out string content)
    {
        if (!base.TryValidate(out title, out content))
            return false;

        if (IsContentLengthEnabled && HasInvalidRange(ContentLengthMinOption.NullableInt, ContentLengthMaxOption.NullableInt))
        {
            title = Strings.SearchValidationInvalidNumericRangeTitle();
            content = Strings.SearchValidationInvalidNumericRangeContent();
            return false;
        }

        if (!(App.AppViewModel?.MakoClient.GetUser()?.IsPremium ?? false))
        {
            var range = (ContentLengthMinOption.NullableInt, ContentLengthMaxOption.NullableInt);
            // Mako otherwise clears unsupported free-account ranges during engine construction.
            var isAllowed = ContentLengthOption switch
            {
                SearchNovelContentLengthOption.None => true,
                SearchNovelContentLengthOption.TextLength or SearchNovelContentLengthOption.WordCount =>
                    range is (null, null) or (null, 4999) or (5000, 19999) or (20000, 79999) or (80000, null),
                SearchNovelContentLengthOption.ReadingTime =>
                    range is (null, null) or (null, 9) or (10, 59) or (60, 179) or (180, null),
                _ => false
            };
            if (!isAllowed)
            {
                title = Strings.SearchValidationPremiumLengthTitle();
                content = ContentLengthOption is SearchNovelContentLengthOption.ReadingTime
                    ? Strings.SearchValidationPremiumLengthReadingTimeContent()
                    : Strings.SearchValidationPremiumLengthTextContent();
                return false;
            }
        }

        title = content = "";
        return true;
    }

    public NovelSearchParams ToSearchParams(string searchText)
    {
        var hasContentLength = IsContentLengthEnabled;
        return new(
            Word: searchText,
            SearchTarget: MatchOption,
            Sort: SortOption,
            SearchAiType: AiType ? 1 : 0,
            Lang: Language is "" ? null : Language,
            ContentLengthOption: ContentLengthOption.ToNovelContentLength(),
            ContentLengthMin: hasContentLength ? ContentLengthMinOption.NullableInt : null,
            ContentLengthMax: hasContentLength ? ContentLengthMaxOption.NullableInt : null,
            IsOriginalOnly: IsOriginalOnly ? true : null,
            Genre: IsOriginalOnly ? GenreId is 0 ? null : GenreId : null,
            IsReplaceableOnly: IsReplaceableOnly ? true : null,
            MergePlainKeywordResults: MergePlainKeywordResults,
            IncludeTranslatedTagResults: IncludeTranslatedTagResults,
            IncludePotentialViolationWorks: IncludePotentialViolationWorks,
            StartDate: StartDateOption.ToNullableDateTimeOffset().ToQueryDate(),
            EndDate: EndDateOption.ToNullableDateTimeOffset().ToQueryDate());
    }
}
