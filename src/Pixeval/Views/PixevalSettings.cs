// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Pixeval.AppManagement;
using Pixeval.AppManagement.Settings;
using Pixeval.ViewModels;

namespace Pixeval.Views;

public class PixevalSettings : ViewModelBase
{
    private readonly AppSettings _settings;

    public PixevalSettings(AppSettings settings) => _settings = settings;

    public bool OpenWorkInfo
    {
        get => _settings.BrowsingExperienceSettings.OpenWorkInfoByDefault;
        set
        {
            _settings.BrowsingExperienceSettings.OpenWorkInfoByDefault = value;
            AppInfo.SaveAppSettings(_settings);
        }
    }

    public bool OpenUserInfo
    {
        get => _settings.BrowsingExperienceSettings.OpenUserInfoByDefault;
        set
        {
            _settings.BrowsingExperienceSettings.OpenUserInfoByDefault = value;
            AppInfo.SaveAppSettings(_settings);
        }
    }

    public bool HideHomePageCardTitle
    {
        get => _settings.ApplicationSettings.HomePage.HideHomePageCardTitle;
        set => SetProperty(_settings.ApplicationSettings.HomePage.HideHomePageCardTitle, value, _settings.ApplicationSettings.HomePage, (setting, v) =>
        {
            setting.HideHomePageCardTitle = v;
            AppInfo.SaveAppSettings(_settings);
        });
    }

    public bool HideHomePageToolbar
    {
        get => _settings.ApplicationSettings.HomePage.HideHomePageToolbar;
        set => SetProperty(_settings.ApplicationSettings.HomePage.HideHomePageToolbar, value, _settings.ApplicationSettings.HomePage, (setting, v) =>
        {
            setting.HideHomePageToolbar = v;
            AppInfo.SaveAppSettings(_settings);
        });
    }
}
