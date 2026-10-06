// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.Models.Home;

namespace Pixeval.Native.Config;

public partial record HomeCardBounds
{
    public static HomeCardBounds From(HomePageCardLayout card) => new(card.Column, card.Row, card.ColumnSpan, card.RowSpan);

    public void ApplyTo(HomePageCardLayout card)
    {
        card.Column = Column;
        card.Row = Row;
        card.ColumnSpan = ColumnSpan;
        card.RowSpan = RowSpan;
    }
}
