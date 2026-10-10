// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Pixeval.Models;
using Pixeval.Native.Booru;
using Pixeval.Native.Mako;

namespace Pixeval.ViewModels.Viewers;

public sealed partial class ImageViewerViewModel : ViewModelBase, IDisposable
{
    private bool _isDisposed;

    public ImageViewerViewModel(object thumbnailViewModel)
    {
        ThumbnailViewModel = thumbnailViewModel;
        var entry = thumbnailViewModel;
        var platform = entry switch
        {
            Illustration => PlatformConstants.Pixiv,
            BooruPost bp => bp.PlatformName,
            _ => PlatformConstants.Pixiv
        };

        Images = entry switch
        {
            Illustration { Pages.Count: > 1 } ill => ill.Pages.Select((t, i) => new SingleViewerViewModel(platform, t, i, (ctrl, idx) => WorkCommands.SaveImageAsync(entry, ctrl, idx))).ToArray(),
            _ => [new(platform, entry, 0, (ctrl, idx) => WorkCommands.SaveImageAsync(entry, ctrl, idx))]
        };

        PageCount = Images.Count;

        App.AppViewModel.AddBrowseHistory(thumbnailViewModel);
    }

    public object ThumbnailViewModel { get; set; }

    public IReadOnlyList<SingleViewerViewModel> Images { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    public partial int SelectedPageIndex { get; set; }

    public SingleViewerViewModel? CurrentPage =>
        Images.Count is 0 ? null : Images[int.Clamp(SelectedPageIndex, 0, Images.Count - 1)];

    public int PageCount { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        foreach (var loadableBitmap in Images)
            loadableBitmap.Dispose();
    }
}
