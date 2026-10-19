// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

#pragma warning disable CS0436

using System.Collections.Generic;
using UniFFISharp;

namespace Pixeval.Config;

public static partial class PixevalConfigMethods
{
    [PreferUniFFI]
    public static partial string FormatNavigationYaml(NavigationYamlSettings settings);

    [PreferUniFFI]
    public static partial string FormatHomePageCardsYaml(List<HomePageCardLayout> cards);

    [PreferUniFFI]
    public static partial bool LayoutCanPlace(List<HomeCardBounds> cards, uint? movingIndex, HomeCardBounds bounds, int rowCount, int columnCount);

    [PreferUniFFI]
    public static partial bool LayoutCanResizeGrid(List<HomeCardBounds> cards, int rowCount, int columnCount);

    [PreferUniFFI]
    public static partial CardNormalizationResult LayoutNormalizeCards(List<HomeCardBounds> cards, int rowCount, int columnCount);

    [PreferUniFFI]
    public static partial GridPlacement? LayoutTryFindBestFittingFreePosition(List<HomeCardBounds> cards, int preferredColumnSpan, int preferredRowSpan, int rowCount, int columnCount);

    [PreferUniFFI]
    public static partial GridPosition? LayoutTryFindFreePosition(List<HomeCardBounds> cards, int columnSpan, int rowSpan, int rowCount, int columnCount);

    [PreferUniFFI]
    public static partial NavigationParseResult ParseNavigationYaml(string yaml, List<string> knownPages, List<string> knownIcons, List<string>? knownI18nKeys);

    [PreferUniFFI]
    public static partial HomeCardMetadata GetCardMetadata(HomePageCardSourceKind sourceKind);

    [PreferUniFFI]
    public static partial HomeCardBounds LayoutCalculateEditCandidate(HomeCardEditAction action, HomeCardBounds startBounds, int deltaColumn, int deltaRow, int rowCount, int columnCount);
}
