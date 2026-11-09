// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.ComponentModel;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Mako.Global.Enum;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.ViewModels.Viewers;
using Pixeval.Views.Work;

namespace Pixeval.Views.Viewers;

[PseudoClasses(":docked", ":locked")]
public partial class IllustrationViewerInfoPane : UserControl
{
    private IllustrationViewerPageViewModel? _observedModel;
    private IllustrationItemViewModel? _bookmarkTarget;
    private CancellationTokenSource? _bookmarkLifetime;

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
        if (change.Property == IsDockedProperty)
            PseudoClasses.Set(":docked", IsDocked);
        if (change.Property == IsLockedProperty)
            PseudoClasses.Set(":locked", IsLocked);
    }

    private async void AddToBookmarkButton_OnRightClick(object? sender, ContextRequestedEventArgs e)
    {
        if (!IsLoaded || sender is not Control anchor
            || _observedModel?.CurrentIllustration is not { IsBookmarkSupported: true } target
            || (target.IsBookmarkedDisplay & HeartButtonState.Pending) != 0
            || !target.AddToBookmarkCommand.CanExecute((null, false, this)) || !long.TryParse(target.Id, out var id))
            return;

        e.Handled = true;
        CancelBookmark();
        _bookmarkTarget = target;
        _bookmarkLifetime = new();
        var token = _bookmarkLifetime.Token;
        await BookmarkTagSelectorFlyoutHelper.ShowAsync(
            anchor, SimpleWorkType.Illustration, id,
            async selection =>
            {
                if (token.IsCancellationRequested)
                    return;
                await target.AddToBookmarkCommand.ExecuteAsync((selection.Tags, selection.IsPrivate, this));
                if (!token.IsCancellationRequested && target.IsFavorite)
                    TopLevel.GetTopLevel(this)?.ViewContainer?.ShowSuccess(I18NManager.GetResource(MiscResources.AddedToBookmark));
            },
            PlacementMode.TopEdgeAlignedRight, token);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ObserveModel();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        StopObservingModel();
        base.OnUnloaded(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        StopObservingModel();
        base.OnDataContextChanged(e);
        if (IsLoaded)
            ObserveModel();
    }

    private void ObserveModel()
    {
        StopObservingModel();
        _observedModel = DataContext as IllustrationViewerPageViewModel;
        _observedModel?.PropertyChanged += OnModelChanged;
    }

    private void StopObservingModel()
    {
        _observedModel?.PropertyChanged -= OnModelChanged;
        _observedModel = null;
        CancelBookmark();
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IllustrationViewerPageViewModel.CurrentIllustration)
            && !ReferenceEquals(_bookmarkTarget, _observedModel?.CurrentIllustration))
            CancelBookmark();
    }

    private void CancelBookmark()
    {
        _bookmarkLifetime?.Cancel();
        _bookmarkLifetime?.Dispose();
        _bookmarkLifetime = null;
        _bookmarkTarget = null;
    }

    private void ChevronButtonClicked(object? sender, RoutedEventArgs e)
    {
        IsDocked = !IsDocked;
    }
}
