// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using FluentIcons.Common;
using Pixeval.Localization;
using Pixeval.Config;
using Pixeval.Utilities;
using Pixeval.ViewModels.Home;

namespace Pixeval.Views.Home;

public sealed class HomeCardDefinition(
    HomePageCardSourceKind sourceKind,
    Func<HomePageCardLayout, Task<HomeCardPreviewSource>> previewSourceFactory,
    Action<HomePageCardLayout, HomeCardPreviewSource> pageOpener,
    Func<HomePageCardLayout, IReadOnlyList<string>>? titleParameterFactory = null)
{
    private readonly HomeCardMetadata _metadata = PixevalConfigMethods.GetCardMetadata(sourceKind);

    public HomePageCardSourceKind SourceKind { get; } = sourceKind;

    public Symbol Symbol => AvaloniaHelper.GetHomeCardHeader(SourceKind).Symbol;

    public HomeCardParameterKinds Parameters => (HomeCardParameterKinds)_metadata.ParameterFlags;

    public int DefaultColumnSpan => _metadata.DefaultColumnSpan;

    public int DefaultRowSpan => _metadata.DefaultRowSpan;

    public WorkKind WorkKind => StoredEnum.FromIndex<WorkKind>(_metadata.DefaultWorkType);

    public SimpleWorkKind SimpleWorkKind => StoredEnum.FromIndex<SimpleWorkKind>(_metadata.DefaultSimpleWorkType);

    public PrivacyRestrict PrivacyRestrict => StoredEnum.FromIndex<PrivacyRestrict>(_metadata.DefaultPrivacyPolicy);

    public bool UseCurrentUserAsDefault => _metadata.UseCurrentUserAsDefault;

    public string Title => AvaloniaHelper.GetHomeCardHeader(SourceKind).Header;

    public string Description => SourceKind switch
    {
        HomePageCardSourceKind.WorkRecommended => Strings.EnumHomePageCardSourceKindDescriptionWorkRecommended(),
        HomePageCardSourceKind.WorkBookmarks => Strings.EnumHomePageCardSourceKindDescriptionWorkBookmarks(),
        HomePageCardSourceKind.WorkRanking => Strings.EnumHomePageCardSourceKindDescriptionWorkRanking(),
        HomePageCardSourceKind.WorkNew => Strings.EnumHomePageCardSourceKindDescriptionWorkNew(),
        HomePageCardSourceKind.WorkFollowing => Strings.EnumHomePageCardSourceKindDescriptionWorkFollowing(),
        HomePageCardSourceKind.WorkMyPixiv => Strings.EnumHomePageCardSourceKindDescriptionWorkMyPixiv(),
        HomePageCardSourceKind.WorkRelated => Strings.EnumHomePageCardSourceKindDescriptionWorkRelated(),
        HomePageCardSourceKind.WorkPosts => Strings.EnumHomePageCardSourceKindDescriptionWorkPosts(),
        HomePageCardSourceKind.WorkSearch => Strings.EnumHomePageCardSourceKindDescriptionWorkSearch(),
        HomePageCardSourceKind.UserRecommended => Strings.EnumHomePageCardSourceKindDescriptionUserRecommended(),
        HomePageCardSourceKind.UserSearch => Strings.EnumHomePageCardSourceKindDescriptionUserSearch(),
        HomePageCardSourceKind.UserFollowing => Strings.EnumHomePageCardSourceKindDescriptionUserFollowing(),
        HomePageCardSourceKind.UserFollower => Strings.EnumHomePageCardSourceKindDescriptionUserFollower(),
        HomePageCardSourceKind.UserMyPixiv => Strings.EnumHomePageCardSourceKindDescriptionUserMyPixiv(),
        HomePageCardSourceKind.Spotlight => Strings.EnumHomePageCardSourceKindDescriptionSpotlight(),
        HomePageCardSourceKind.SingleImage => Strings.EnumHomePageCardSourceKindDescriptionSingleImage(),
        HomePageCardSourceKind.SingleNovel => Strings.EnumHomePageCardSourceKindDescriptionSingleNovel(),
        HomePageCardSourceKind.SingleSeries => Strings.EnumHomePageCardSourceKindDescriptionSingleSeries(),
        HomePageCardSourceKind.SingleUser => Strings.EnumHomePageCardSourceKindDescriptionSingleUser(),
        _ => ""
    };

    public bool HasParameter(HomeCardParameterKinds parameter) =>
        (Parameters & parameter) is not HomeCardParameterKinds.None;

    public Task<HomeCardPreviewSource> CreatePreviewSourceAsync(HomePageCardLayout card) =>
        previewSourceFactory(card);

    public void OpenCardPage(HomePageCardLayout card, HomeCardPreviewSource source)
    {
        try
        {
            pageOpener(card, source);
        }
        finally
        {
            source.Dispose();
        }
    }

    public string BuildTitle(HomePageCardLayout card)
    {
        var parts = new List<string> { Title };
        if (titleParameterFactory is not null)
            parts.AddRange(titleParameterFactory(card));

        return string.Join(Strings.HomePageCardTitleParameterSeparator(), parts);
    }
}
