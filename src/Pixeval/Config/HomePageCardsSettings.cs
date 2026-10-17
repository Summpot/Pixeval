// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.ObjectModel;
using Pixeval.Config;

namespace Pixeval.Config;

public static class HomePageCardsSettings
{
    public static ObservableCollection<HomePageCardLayout> CreateDefaultCards() =>
        new(PixevalConfigMethods.CreateDefaultCards());
}
