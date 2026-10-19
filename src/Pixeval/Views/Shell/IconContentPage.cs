// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia.Controls;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using Pixeval.Utilities;

namespace Pixeval.Views.Shell;

public class IconContentPage : ContentPage
{
    protected IconContentPage()
    {
        var tuple = AvaloniaHelper.GetPageHeader(GetType());
        Header = tuple.Header;
        var previousHeader = tuple.Header;
        var localization = AppLocalization.Instance.Subscribe(() =>
        {
            if (!CheckAccess()) return;
            var translated = AvaloniaHelper.GetPageHeader(GetType()).Header;
            if (Equals(Header, previousHeader)) Header = translated;
            previousHeader = translated;
        });
        Pixeval.ViewModels.ViewModelDisposal.Register(this, localization);
        Icon = new SymbolIcon
        {
            Symbol = tuple.Symbol,
            FontSize = 16,
            IconVariant = IconVariant.Color
        };
    }
}

public class IconNavigationPage : NavigationPage
{
    protected IconNavigationPage()
    {
        var tuple = AvaloniaHelper.GetPageHeader(GetType());
        Header = tuple.Header;
        var previousHeader = tuple.Header;
        var localization = AppLocalization.Instance.Subscribe(() =>
        {
            if (!CheckAccess()) return;
            var translated = AvaloniaHelper.GetPageHeader(GetType()).Header;
            if (Equals(Header, previousHeader)) Header = translated;
            previousHeader = translated;
        });
        Pixeval.ViewModels.ViewModelDisposal.Register(this, localization);
        Icon = new SymbolIcon
        {
            Symbol = tuple.Symbol,
            FontSize = 16,
            IconVariant = IconVariant.Color
        };
    }
}

public class IconTabbedPage : TabbedPage
{
    protected IconTabbedPage()
    {
        var tuple = AvaloniaHelper.GetPageHeader(GetType());
        Header = tuple.Header;
        var previousHeader = tuple.Header;
        var localization = AppLocalization.Instance.Subscribe(() =>
        {
            if (!CheckAccess()) return;
            var translated = AvaloniaHelper.GetPageHeader(GetType()).Header;
            if (Equals(Header, previousHeader)) Header = translated;
            previousHeader = translated;
        });
        Pixeval.ViewModels.ViewModelDisposal.Register(this, localization);
        Icon = new SymbolIcon
        {
            Symbol = tuple.Symbol,
            FontSize = 16,
            IconVariant = IconVariant.Color
        };
    }
}
