// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using Pixeval.AppManagement;

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

    private async void LoadData()
    {
        LicenseTextBlock.Markdown = await AppInfo.GetAssetStringAsync("GPLv3.md");
        await Supporter.GetSupportersAsync();
    }
}
