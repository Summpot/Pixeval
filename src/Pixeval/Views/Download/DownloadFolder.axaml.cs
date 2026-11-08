// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.ViewModels;

namespace Pixeval.Views.Download;

public partial class DownloadFolder : UserControl
{
    public event Action<DownloadFolder, DownloadFolderViewModel>? OpenRequested;

    public DownloadFolder() => InitializeComponent();

    private void FolderLayout_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < 280;
        Grid.SetRow(FolderText, narrow ? 1 : 0);
        Grid.SetColumn(FolderText, narrow ? 0 : 1);
        Grid.SetColumnSpan(FolderText, narrow ? 3 : 1);
    }

    private void Button_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DownloadFolderViewModel vm)
            OpenRequested?.Invoke(this, vm);
    }
}
