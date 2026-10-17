// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pixeval.Controls;
using Pixeval.Localization;
using Pixeval.Utilities;
using Pixeval.ViewModels;

namespace Pixeval.Views.Settings;

public partial class SettingsPage : NavigationPage
{
    public SettingsPage()
    {
        InitializeComponent();
        _ = PushAsync(new SettingsMainView());
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        ValueSaving(Parent);
    }

    /// <summary>
    /// 保存设置
    /// </summary>
    private void ValueSaving(object? parent) => (DataContext as SettingsPageViewModel)?.SaveSettings();

    public static string ReleaseTitle => GetReleaseTitle(AppInfo.AppVersion.CurrentVersion);

    public static string GetReleaseTitle(Version version) =>
        GetReleaseTitle(version.ToString());

    public static string GetReleaseTitle(string version) =>
        Strings.SettingsMainViewReleaseNoteDialogTitleFormatted(version: version);

    public static Control CreateReleaseNotes(Pixeval.Update.AppRelease? release) =>
        new MarkdownBox { Markdown = release?.ReleaseNotes ?? Strings.SettingsMainViewReleaseNoteDialogEmpty() };

    public static async Task<Control> GetReleaseNotesAsync()
    {
        return CreateReleaseNotes(await AppInfo.AppVersion.GetCurrentAppReleaseModelAsync());
    }
}
