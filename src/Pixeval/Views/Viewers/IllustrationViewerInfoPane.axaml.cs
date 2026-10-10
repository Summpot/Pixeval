// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pixeval.I18N;
using Pixeval.Models.Pixiv;
using Pixeval.Native.Mako;
using Pixeval.Services;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
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
        if (sender is Control c && ViewModel?.CurrentIllustration is Illustration ill)
            await BookmarkTagSelectorFlyoutHelper.ShowAsync(
                c,
                SimpleWorkType.Illustration,
                ill.Id,
                AddToBookmarkAsync,
                PlacementMode.TopEdgeAlignedRight);
    }

    private async Task AddToBookmarkAsync((bool IsPrivate, IReadOnlyList<string>? Tags) e)
    {
        if (ViewModel?.CurrentIllustration is not IWorkEntry current)
            return;

        await WorkCommands.AddToBookmarkCommand.ExecuteAsync(new BookmarkRequest(current, e.IsPrivate, e.Tags));
        TopLevel.GetTopLevel(this)?.ViewContainer?.ShowSuccess(
            I18NManager.GetResource(MiscResources.AddedToBookmark));
    }
    
    private void ChevronButtonClicked(object? sender, RoutedEventArgs e)
    {
        IsDocked = !IsDocked;
    }
}
