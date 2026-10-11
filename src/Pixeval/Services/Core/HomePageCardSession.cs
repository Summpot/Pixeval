// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.ObjectModel;
using Pixeval.Config;

namespace Pixeval.Services.Core;

public sealed class HomePageCardSession(ObservableCollection<HomePageCardLayout> cards)
{
    public ObservableCollection<HomePageCardLayout> Cards { get; } = cards;

    public void Reset()
    {
        Cards.Clear();
        foreach (var card in HomePageCardsSettings.CreateDefaultCards())
            Cards.Add(card);
    }
}
