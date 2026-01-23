// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Avalonia.AnimatedImage;
using CommunityToolkit.Mvvm.ComponentModel;
using Mako;
using Mako.Model;
using Pixeval.Utilities.IO.Caching;

namespace Pixeval.ViewModels;

public partial class TabViewContainerViewModel : ObservableObject
{
    public TabViewContainerViewModel()
    {
        App.AppViewModel.MakoClient.TokenRefreshed += OnTokenRefreshed;
    }

    private async void OnTokenRefreshed(MakoClient sender, TokenResponse? e)
    {
        Avatar = e is null ? null : await CacheHelper.GetAnimatedBitmapFromCacheAsync(e.User.ProfileImageUrls.Px50X50);
    }

    [ObservableProperty]
    public partial IAnimatedBitmap? Avatar { get; private set; }
}
