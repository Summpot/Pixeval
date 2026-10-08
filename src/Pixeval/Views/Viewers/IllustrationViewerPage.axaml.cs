// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Pixeval.Models.Options;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Views.Viewers;

public partial class IllustrationViewerPage : ContentPage
{
    private readonly DispatcherTimer _autoPlayTimer = new();

    private IllustrationViewerPageViewModel ViewModel => (IllustrationViewerPageViewModel) DataContext!;

    public IllustrationViewerPage() : this(null)
    {
    }

    public IllustrationViewerPage(IllustrationViewerPageViewModel? viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        _autoPlayTimer.Tick += AutoPlayTimerOnTick;
        if (viewModel is not null)
        {
            viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
            UpdateAutoPlayTimer();
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IllustrationViewerPageViewModel.IsAutoPlaying) or nameof(IllustrationViewerPageViewModel.AutoPlayInterval))
        {
            UpdateAutoPlayTimer();
        }
    }

    private void UpdateAutoPlayTimer()
    {
        if (ViewModel?.IsAutoPlaying == true)
        {
            _autoPlayTimer.Interval = TimeSpan.FromSeconds(ViewModel.AutoPlayInterval);
            _autoPlayTimer.Start();
        }
        else
        {
            _autoPlayTimer.Stop();
        }
    }

    private void AutoPlayTimerOnTick(object? sender, EventArgs e)
    {
        ViewModel?.MoveAutoPlayNext();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _autoPlayTimer.Stop();
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        _ = KeyboardShortcut.TryExecute(e, Key.Left, ViewModel.PrevCommand)
            || KeyboardShortcut.TryExecute(e, Key.Right, ViewModel.NextCommand)
            || KeyboardShortcut.TryExecute(e, Key.Up, ViewModel.PrevWorkCommand)
            || KeyboardShortcut.TryExecute(e, Key.Down, ViewModel.NextWorkCommand);
    }

    private void PrevButton_OnRightClick(object? sender, ContextRequestedEventArgs e) => ViewModel.PrevWorkCommand.Execute(null);

    private void NextButton_OnRightClick(object? sender, ContextRequestedEventArgs e) => ViewModel.NextWorkCommand.Execute(null);

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

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        RaiseEvent(new ViewModelDisposalEventArgs(ViewModelDisposal.ViewModelDisposalEvent, ViewModel));
    }

    #endregion
}
