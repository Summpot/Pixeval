// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Misaki;
using Pixeval.I18N;
using Pixeval.Models.Blocking;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Capability;
using Pixeval.Views.Work;

namespace Pixeval.Views.Viewers;

[PseudoClasses(":docked", ":locked")]
public partial class IllustrationViewerInfoPane : UserControl
{
    private IllustrationViewerPageViewModel? ViewModel => DataContext as IllustrationViewerPageViewModel;

    public static readonly StyledProperty<bool> IsDockedProperty = AvaloniaProperty.Register<IllustrationViewerInfoPane, bool>(
        nameof(IsDocked));

    public static readonly StyledProperty<bool> IsLockedProperty = AvaloniaProperty.Register<IllustrationViewerInfoPane, bool>(
        nameof(IsLocked));

    public bool IsDocked
    {
        get => GetValue(IsDockedProperty);
        set => SetValue(IsDockedProperty, value);
    }

    public bool IsLocked
    {
        get => GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }
    
    public IllustrationViewerInfoPane()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
            UpdatePanePages(ViewModel.CurrentIllustration?.Entry);
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IllustrationViewerPageViewModel.CurrentIllustration))
        {
            UpdatePanePages(ViewModel?.CurrentIllustration?.Entry);
        }
    }

    private void UpdatePanePages(IArtworkInfo? entry)
    {
        if (entry is not Illustration { Id: var id } illustration)
        {
            TabbedPageSection.Pages = [];
            return;
        }

        var pages = new List<Page>
        {
            new WorkInfoPage(illustration)
            {
                ActionZone = new Border
                {
                    Width = 32,
                    Height = 32,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    IsHitTestVisible = false
                }
            }
        };

        if (!BlockedContentHelper.IsBlockedPlaceholder(illustration))
        {
            pages.Add(new CommentsPage(new CommentsViewViewModel(SimpleWorkType.Illustration, id)));
            pages.Add(new WorkRelatedPage(illustration.Id, SimpleWorkType.Illustration) { IsCommandBarCollapsed = true });
        }

        TabbedPageSection.Pages = pages;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if(change.Property == IsDockedProperty)
            PseudoClasses.Set(":docked", IsDocked);
        if(change.Property == IsLockedProperty)
            PseudoClasses.Set(":locked", IsLocked);
    }

    private async void AddToBookmarkButton_OnRightClick(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is Control c && ViewModel?.CurrentIllustration is { Id: { } idStr } && long.TryParse(idStr, out var id))
            await BookmarkTagSelectorFlyoutHelper.ShowAsync(
                c,
                SimpleWorkType.Illustration,
                id,
                AddToBookmarkAsync,
                PlacementMode.TopEdgeAlignedRight);
    }

    private async Task AddToBookmarkAsync((bool IsPrivate, IReadOnlyList<string>? Tags) e)
    {
        if (ViewModel?.CurrentIllustration is not IWorkViewModel current)
            return;

        await current.AddToBookmarkCommand.ExecuteAsync((e.Tags, e.IsPrivate, current));
        TopLevel.GetTopLevel(this)?.ViewContainer?.ShowSuccess(
            I18NManager.GetResource(MiscResources.AddedToBookmark));
    }
    
    private void ChevronButtonClicked(object? sender, RoutedEventArgs e)
    {
        IsDocked = !IsDocked;
    }
}
