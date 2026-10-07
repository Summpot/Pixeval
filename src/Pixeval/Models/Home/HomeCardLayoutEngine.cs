// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Pixeval.Native.Config;

namespace Pixeval.Models.Home;

public static class HomeCardLayoutEngine
{
    private static readonly ConfigEngine Engine = new();

    public static bool CanPlace(
        IReadOnlyCollection<HomePageCardLayout> cards,
        HomePageCardLayout? movingCard,
        HomeCardBounds bounds,
        int rowCount,
        int columnCount)
    {
        var movingIndex = -1;
        var boundsList = new List<HomeCardBounds>(cards.Count);
        var i = 0;
        foreach (var c in cards)
        {
            if (movingCard is not null && ReferenceEquals(c, movingCard))
                movingIndex = i;
            boundsList.Add(HomeCardBounds.From(c));
            i++;
        }

        return Engine.LayoutCanPlace(
            boundsList,
            movingIndex >= 0 ? (uint)movingIndex : null,
            bounds,
            rowCount,
            columnCount);
    }

    public static bool TryFindFreePosition(
        IReadOnlyCollection<HomePageCardLayout> cards,
        int columnSpan,
        int rowSpan,
        int rowCount,
        int columnCount,
        out int column,
        out int row)
    {
        var boundsList = cards.Select(HomeCardBounds.From).ToList();
        var pos = Engine.LayoutTryFindFreePosition(boundsList, columnSpan, rowSpan, rowCount, columnCount);
        if (pos is not null)
        {
            column = pos.Column;
            row = pos.Row;
            return true;
        }

        column = row = 0;
        return false;
    }

    public static HomeCardBounds Clamp(HomeCardBounds bounds, int rowCount, int columnCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rowCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnCount, 1);
        return Engine.LayoutClamp(bounds, rowCount, columnCount);
    }

    public static bool CanResizeGrid(IReadOnlyCollection<HomePageCardLayout> cards, int rowCount, int columnCount) =>
        Engine.LayoutCanResizeGrid(cards.Select(HomeCardBounds.From).ToList(), rowCount, columnCount);

    public static bool IsWithinGrid(HomeCardBounds bounds, int rowCount, int columnCount) =>
        Engine.LayoutIsWithinGrid(bounds, rowCount, columnCount);

    public static bool Overlaps(HomeCardBounds first, HomeCardBounds second) =>
        Engine.LayoutOverlaps(first, second);
}
