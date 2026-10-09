// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Native.Config;

public partial record HomeCardBounds
{
    public static HomeCardBounds From(HomePageCardLayout card) => new(card.Column, card.Row, card.ColumnSpan, card.RowSpan);

    public HomePageCardLayout ApplyTo(HomePageCardLayout card) =>
        card with { Column = Column, Row = Row, ColumnSpan = ColumnSpan, RowSpan = RowSpan };
}
