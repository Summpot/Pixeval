// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.ObjectModel;
using Pixeval.Native.Config;

namespace Pixeval.AppManagement;

public static class HomePageCardsSettings
{
    public static ObservableCollection<HomePageCardLayout> CreateDefaultCards() =>
        new(new ConfigEngine().CreateDefaultCards());
}
