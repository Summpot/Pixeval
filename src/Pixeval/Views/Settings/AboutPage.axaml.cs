// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;

namespace Pixeval.Views.Settings;

public partial class AboutPage : IconContentPage
{
    public AboutPage()
    {
        InitializeComponent();
        LoadData();
    }

    public IReadOnlyList<Supporter> Supporters => Supporter.Supporters;

    public string CurrentVersionText => AppInfo.AppVersion.CurrentVersionFullText;

    private void LoadData() =>
        this.RunInteraction(() => LoadDataAsync());

    private async Task LoadDataAsync()
    {
        LicenseTextBlock.Markdown = await AppInfo.GetAssetStringAsync("GPLv3.md");
        await Supporter.GetSupportersAsync();
    }

    private void SupporterCard_OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { Tag: Uri uri })
            _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(uri);
    }
}
