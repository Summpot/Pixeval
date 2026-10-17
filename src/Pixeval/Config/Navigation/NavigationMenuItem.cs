// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentIcons.Common;
using Pixeval.Utilities;

namespace Pixeval.Config;

public abstract record NavigationMenuItem(
    string InitialHeader,
    string? HeaderSource,
    Symbol Icon,
    bool NeedLogin,
    IReadOnlyList<NavigationMenuItem> Children) : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string Header
    {
        get
        {
            if (HeaderSource is { } source)
            {
                if (source.StartsWith("$$", StringComparison.Ordinal)) return source[1..];
                if (source.StartsWith('$') && MessageKeys.TryGet(source[1..].Trim(), out var translated)) return translated;
                return InitialHeader;
            }
            return this is NavigationPageItem page && NavigationPageRegistry.TryGetPage(page.PageKey, out var definition)
                ? definition.Header : InitialHeader;
        }
    }
    public void RefreshLocalization()
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Header)));
        foreach (var child in Children) child.RefreshLocalization();
    }

    public NavigationYamlItem ToYamlItem()
    {
        return this switch
        {
            NavigationPageItem page => ToYamlPageItem(page),
            NavigationFolderItem folder => new(
                null,
                folder.HeaderSource ?? folder.Header,
                null,
                folder.Icon.ToString(),
                folder.Children.Select(child => child.ToYamlItem()).ToList()),
            _ => throw new InvalidOperationException("Unknown navigation menu item type")
        };

        static NavigationYamlItem ToYamlPageItem(NavigationPageItem item)
        {
            if (!NavigationPageRegistry.TryGetPage(item.PageKey, out var definition))
                return new(item.PageKey, null, null, null, null);

            string? title = null;
            if (item.HeaderSource is { } headerSource)
                title = headerSource;
            else if (item.Header != definition.Header)
                title = item.Header;

            string? icon = null;
            if (item.Icon != definition.Icon)
                icon = item.Icon.ToString();

            return new(item.PageKey, null, title, icon, null);
        }
    }
}

public sealed record NavigationPageItem(
    Type PageType,
    string PageKey,
    string InitialHeader,
    string? HeaderSource,
    Symbol Icon,
    bool NeedLogin) : NavigationMenuItem(InitialHeader, HeaderSource, Icon, NeedLogin, []);

public sealed record NavigationFolderItem(
    string InitialHeader,
    string? HeaderSource,
    Symbol Icon,
    bool NeedLogin,
    IReadOnlyList<NavigationMenuItem> Children) : NavigationMenuItem(InitialHeader, HeaderSource, Icon, NeedLogin, Children);
