// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Mako;
using Pixeval.ViewModels;

namespace Pixeval.Views.Works;

public partial class NovelItem : WorkItem
{
    public NovelItem()
    {
        InitializeComponent();
        // Keep the author and tag actions inside a narrow card in the real tab shell.
        SizeChanged += (_, e) => CoverBounds.MaxWidth = e.NewSize.Width < 360 ? 80 : double.PositiveInfinity;
    }

    internal override void Recycle()
    {
        base.Recycle();
        DescriptionBlock.Opacity = 0;
        CoverImage.Opacity = 1;
    }

    private void TagButton_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: Tag tag })
            return;
        this.GetRequiredUiService<SearchHistorySession>().Add(tag.Name, tag.TranslatedName);
        var nav = this.GetUiService<INavigationService>() ?? new NavigationService();
        nav.NavigateToWorkSearch(tag.Name, SimpleWorkType.Novel, this);
    }

    private void InputElement_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        CoverImage.Opacity = 0;
        DescriptionBlock.Opacity = 1;
    }

    private void InputElement_OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (DescriptionBlock.IsKeyboardFocusWithin)
            return;
        DescriptionBlock.Opacity = 0;
        CoverImage.Opacity = 1;
    }

    private void DescriptionBlock_OnGotFocus(object? sender, RoutedEventArgs e)
    {
        CoverImage.Opacity = 0;
        DescriptionBlock.Opacity = 1;
    }

    private void DescriptionBlock_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (CoverBounds.IsPointerOver || DescriptionBlock.IsKeyboardFocusWithin)
            return;
        DescriptionBlock.Opacity = 0;
        CoverImage.Opacity = 1;
    }
}
