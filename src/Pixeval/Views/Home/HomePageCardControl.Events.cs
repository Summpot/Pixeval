// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Pixeval.Native.Config;

namespace Pixeval.Views.Home;

public sealed class HomeCardSelectedEventArgs(HomePageCardLayout card) : EventArgs
{
    public HomePageCardLayout Card { get; } = card;
}

public sealed class HomeCardEditPreviewEventArgs(HomePageCardLayout card, HomeCardBounds bounds) : EventArgs
{
    public HomePageCardLayout Card { get; } = card;

    public HomeCardBounds Bounds { get; } = bounds;

    public bool Accepted { get; set; }
}

public sealed class HomeCardEditCompletedEventArgs(HomePageCardLayout card, HomePageCardLayout originalCard, bool hasChanged) : EventArgs
{
    public HomePageCardLayout Card { get; } = card;

    public HomePageCardLayout OriginalCard { get; } = originalCard;

    public bool HasChanged { get; } = hasChanged;
}

public sealed class HomeCardDeleteRequestedEventArgs(HomePageCardLayout card) : EventArgs
{
    public HomePageCardLayout Card { get; } = card;
}
