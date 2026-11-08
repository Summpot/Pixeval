// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pixeval.Models.Options;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Views.Viewers;

public partial class IllustrationViewerPage : ContentPage
{
    private IllustrationViewerPageViewModel? _playbackModel;
    private IllustrationViewerPageViewModel ViewModel => (IllustrationViewerPageViewModel) DataContext!;

    public IllustrationViewerPage() : this(null)
    {
    }

    public IllustrationViewerPage(IllustrationViewerPageViewModel? viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        _ = KeyboardShortcut.TryExecute(e, Key.Left, ViewModel.PrevCommand)
            || KeyboardShortcut.TryExecute(e, Key.Right, ViewModel.NextCommand)
            || KeyboardShortcut.TryExecute(e, Key.Up, ViewModel.PrevWorkCommand)
            || KeyboardShortcut.TryExecute(e, Key.Down, ViewModel.NextWorkCommand);
    }

    private void PrevButton_OnRightClick(object? sender, ContextRequestedEventArgs e)
    {
        if (ViewModel.PrevWorkCommand.CanExecute(null))
        {
            ViewModel.PrevWorkCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void NextButton_OnRightClick(object? sender, ContextRequestedEventArgs e)
    {
        if (ViewModel.NextWorkCommand.CanExecute(null))
        {
            ViewModel.NextWorkCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void AutoPlayMenuFlyout_OnOpened(object? sender, RoutedEventArgs e)
    {
        SetMenuItem(AutoPlayInterval1SecondMenuItem, ViewModel.AutoPlayInterval);
        SetMenuItem(AutoPlayInterval3SecondsMenuItem, ViewModel.AutoPlayInterval);
        SetMenuItem(AutoPlayInterval5SecondsMenuItem, ViewModel.AutoPlayInterval);
        SetMenuItem(AutoPlayInterval10SecondsMenuItem, ViewModel.AutoPlayInterval);
        SetMenuItem(AutoPlayInterval15SecondsMenuItem, ViewModel.AutoPlayInterval);
        SetMenuItem(AutoPlayInterval30SecondsMenuItem, ViewModel.AutoPlayInterval);

        SetMenuItem(AutoPlaySequentialModeMenuItem, ViewModel.AutoPlayMode);
        SetMenuItem(AutoPlayLoopPlaybackModeMenuItem, ViewModel.AutoPlayMode);
        SetMenuItem(AutoPlayCurrentWorkScopeMenuItem, ViewModel.AutoPlayScope);
        SetMenuItem(AutoPlayAllWorksScopeMenuItem, ViewModel.AutoPlayScope);
        return;

        static void SetMenuItem(MenuItem menuItem, object value) => menuItem.IsChecked = Equals(value, menuItem.Tag);
    }

    private void AutoPlayIntervalMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: int interval })
            ViewModel.AutoPlayInterval = interval;
    }

    private void AutoPlayModeMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: IllustrationViewerAutoPlayMode mode })
            ViewModel.AutoPlayMode = mode;
    }

    private void AutoPlayScopeMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: IllustrationViewerAutoPlayScope scope })
            ViewModel.AutoPlayScope = scope;
    }

    #region Disposal

    protected override void OnDataContextChanged(EventArgs e)
    {
        _playbackModel?.SetPlaybackActive(false);
        _playbackModel = DataContext as IllustrationViewerPageViewModel;
        if (_playbackModel is { } model)
            ViewModelDisposal.Register(this, model);
        _playbackModel?.SetPlaybackActive(IsLoaded);
        base.OnDataContextChanged(e);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _playbackModel?.SetPlaybackActive(false);
        base.OnUnloaded(e);
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _playbackModel?.SetPlaybackActive(true);

        RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, ViewModel));
    }

    #endregion
}
