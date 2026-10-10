// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pixeval.Native.Mako;
using Pixeval.ViewModels;

namespace Pixeval.Views.Work;

public partial class NovelItem : WorkItem
{
    public NovelItem() => InitializeComponent();

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
        App.AppViewModel.AddSearchHistory(tag.Name, tag.TranslatedName);
        App.AppViewModel.NavigationService.NavigateToWorkSearch(tag.Name, SimpleWorkType.Novel, this);
    }

    private void AuthorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: Novel vm })
            return;
        App.AppViewModel.NavigationService.NavigateToUser(vm.Entry.User.Id, this);
    }

    private void InputElement_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        CoverImage.Opacity = 0;
        DescriptionBlock.Opacity = 1;
    }

    private void InputElement_OnPointerExited(object? sender, PointerEventArgs e)
    {
        DescriptionBlock.Opacity = 0;
        CoverImage.Opacity = 1;
    }
}
