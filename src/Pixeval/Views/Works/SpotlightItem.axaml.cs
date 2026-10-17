// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia.Data.Converters;
using Avalonia.Media;
using Pixeval.Pixiv;

namespace Pixeval.Views.Works;

public partial class SpotlightItem : EntryItem
{
    public SpotlightItem() => InitializeComponent();

    public static readonly FuncValueConverter<object?, SolidColorBrush> BackgroundBrushConverter =
        new(category =>
        {
            var cat = category switch
            {
                Mako.SpotlightCategory native => native,
                _ => Mako.SpotlightCategory.All
            };
            return new(cat switch
            {
                Mako.SpotlightCategory.Spotlight => Color.FromArgb(0xFF, 0x00, 0x96, 0xFA),
                Mako.SpotlightCategory.Tutorial => Color.FromArgb(0xFF, 0x00, 0xD7, 0xA7),
                _ => Color.FromArgb(0xFF, 0xFF, 0x59, 0x00)
            });
        });
}
