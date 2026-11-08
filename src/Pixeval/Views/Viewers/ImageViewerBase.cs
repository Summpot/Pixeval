// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.Utilities;
using Pixeval.ViewModels.Viewers;

namespace Pixeval.Views.Viewers;

public abstract partial class ImageViewerBase : UserControl
{
    public event EventHandler<Control, ImageViewerSelectionChangedEventArgs>? SelectionChanged;

    public static readonly DirectProperty<ImageViewerBase, double> ZoomFactorProperty =
        AvaloniaProperty.RegisterDirect<ImageViewerBase, double>(
            nameof(ZoomFactor),
            o => o.ZoomFactor,
            (o, v) => o.ZoomFactor = v);

    public static readonly DirectProperty<ImageViewerBase, Orientation> BrowseDirectionProperty =
        AvaloniaProperty.RegisterDirect<ImageViewerBase, Orientation>(
            nameof(BrowseDirection),
            o => o.BrowseDirection,
            (o, v) => o.BrowseDirection = v);

    public abstract double ZoomFactor { get; set; }

    public Orientation BrowseDirection
    {
        get;
        set
        {
            var old = field;
            if (old == value)
                return;

            SetAndRaise(BrowseDirectionProperty, ref field, value);
            OnBrowseDirectionChanged(old, value);
        }
    }

    protected SingleViewerViewModel? CurrentPageViewModel { get; private set; }

    protected bool CanManipulateCurrentImage => CurrentPageViewModel?.HasDisplayImage is true;

    private bool CanCopyCurrentImage => CurrentPageViewModel?.CopyCommand.CanExecute(this) is true;

    private bool CanSaveCurrentImage => CurrentPageViewModel?.SaveImageCommand.CanExecute(this) is true;

    private bool CanSaveAsCurrentImage => CurrentPageViewModel?.SaveAsCommand.CanExecute(this) is true;

    protected void SetCurrentPageViewModel(SingleViewerViewModel? currentPage)
    {
        if (ReferenceEquals(CurrentPageViewModel, currentPage))
            return;

        if (CurrentPageViewModel is { } previous)
        {
            previous.PropertyChanged -= CurrentPageOnPropertyChanged;
            previous.CopyCommand.CanExecuteChanged -= CurrentPageCommandOnCanExecuteChanged;
            previous.SaveImageCommand.CanExecuteChanged -= CurrentPageCommandOnCanExecuteChanged;
            previous.SaveAsCommand.CanExecuteChanged -= CurrentPageCommandOnCanExecuteChanged;
        }
        CurrentPageViewModel = currentPage;
        if (currentPage is not null)
        {
            currentPage.PropertyChanged += CurrentPageOnPropertyChanged;
            currentPage.CopyCommand.CanExecuteChanged += CurrentPageCommandOnCanExecuteChanged;
            currentPage.SaveImageCommand.CanExecuteChanged += CurrentPageCommandOnCanExecuteChanged;
            currentPage.SaveAsCommand.CanExecuteChanged += CurrentPageCommandOnCanExecuteChanged;
        }

        NotifyCommandCanExecuteChanged();
    }

    private void CurrentPageCommandOnCanExecuteChanged(object? sender, EventArgs e) => NotifyCommandCanExecuteChanged();

    protected void RaiseSelectionChanged(int newIndex, object? newItem) =>
        SelectionChanged?.Invoke(this, new ImageViewerSelectionChangedEventArgs(newIndex, newItem));

    protected virtual void NotifyCommandCanExecuteChanged()
    {
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        ZoomToOriginalCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
    }

    protected virtual void OnBrowseDirectionChanged(Orientation oldValue, Orientation newValue)
    {
    }

    private void CurrentPageOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SingleViewerViewModel.LoadSuccessfully)
            or nameof(SingleViewerViewModel.OriginalSource)
            or nameof(SingleViewerViewModel.DisplaySource)
            or nameof(SingleViewerViewModel.HasDisplayImage))
            NotifyCommandCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanManipulateCurrentImage))]
    private void ZoomIn() => ZoomFactor *= 1.2;

    [RelayCommand(CanExecute = nameof(CanManipulateCurrentImage))]
    private void ZoomOut() => ZoomFactor /= 1.2;

    [RelayCommand(CanExecute = nameof(CanManipulateCurrentImage))]
    private void ZoomToOriginal() => ZoomFactor = 1;

    [RelayCommand(CanExecute = nameof(CanCopyCurrentImage))]
    private Task CopyAsync() => CurrentPageViewModel?.CopyCommand.ExecuteAsync(this) ?? Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanSaveCurrentImage))]
    private Task SaveAsync() => CurrentPageViewModel?.SaveImageCommand.ExecuteAsync(this) ?? Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanSaveAsCurrentImage))]
    private Task SaveAsAsync() => CurrentPageViewModel?.SaveAsCommand.ExecuteAsync(this) ?? Task.CompletedTask;
}
