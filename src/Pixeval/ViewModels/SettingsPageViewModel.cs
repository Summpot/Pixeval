// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoSettingsPage;
using AutoSettingsPage.Models;
using Avalonia;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Extensions;
using Pixeval.Services;
using Pixeval.Models.Options;
using Pixeval.Models.Settings;
using Pixeval.Models.Subscriptions;
using Pixeval.Utilities;
using Pixeval.Utilities.IO.Caching;

namespace Pixeval.ViewModels;

public class SettingsPageViewModel : ViewModelBase
{
    private readonly AppSettings _appSettings;
    private readonly ExtensionService _extensionService;
    private readonly DownloadManager _downloadManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly INetworkRuntime _networkRuntime;

    public string CurrentVersion => AppInfo.AppVersion.CurrentVersionShortText;

    public DateTime LastCheckedUpdate
    {
        get => _appSettings.ApplicationSettings.LastCheckedUpdate;
        set => SetProperty(_appSettings.ApplicationSettings.LastCheckedUpdate, value, _appSettings.ApplicationSettings, (setting, v) => setting.LastCheckedUpdate = v);
    }

    public void RefreshLastCheckedUpdate() => OnPropertyChanged(nameof(LastCheckedUpdate));

    public AppSettings AppSettings => _appSettings;

    public IEnumerable<ISettingsGroup> Groups => LocalGroups.Concat(ExtensionGroups);

    public IReadOnlyList<ISettingsGroup> LocalGroups { get; }

    public IReadOnlyList<ExtensionSettingsGroup> ExtensionGroups => _extensionService.SettingsGroups;

    public SettingsPageViewModel() : this(
        App.Services!.GetRequiredService<AppSettings>(),
        App.Services!.GetRequiredService<ExtensionService>(),
        App.Services!.GetRequiredService<DownloadManager>(),
        App.Services!,
        App.Services!.GetRequiredService<INetworkRuntime>())
    {
    }

    public SettingsPageViewModel(
        AppSettings appSettings,
        ExtensionService extensionService,
        DownloadManager downloadManager,
        IServiceProvider serviceProvider,
        INetworkRuntime networkRuntime)
    {
        _appSettings = appSettings;
        _extensionService = extensionService;
        _downloadManager = downloadManager;
        _serviceProvider = serviceProvider;
        _networkRuntime = networkRuntime;
        LocalGroups = BuildLocalGroups();
    }

    private IReadOnlyList<ISettingsGroup> BuildLocalGroups()
    {
        LocalSettingsEntryHelper.Initialize();

        return SettingsBuilder.CreateGroupList(_appSettings)
            .NewGroup(t => t.ApplicationSettings, group => group
                .Language(t => t.CultureName)
                .Enum(t => t.Theme,
                    entry => entry.ValueChanged += t => Application.Current?.RequestedThemeVariant = t switch
                    {
                        ApplicationTheme.Light => ThemeVariant.Light,
                        ApplicationTheme.Dark => ThemeVariant.Dark,
                        _ => ThemeVariant.Default
                    })
                .Font(t => t.AppFontFamily, entry => entry.ValueChanged += App.ApplyAppFontFamily)
                .MultiValuesWithSwitch(t => t.FileCache, t => t.LimitFileCacheSize,
                    entry => entry.Int(t => t.FileCacheSizeLimitInMegabytes, 1, 0x100000, 0x80,
                        t => t.ValueChanged += _value => _ = CacheHelper.EnforceCacheSizeLimitAsync()),
                    t => t.MainValue.ValueChanged += enabled =>
                    {
                        if (enabled)
                            _ = CacheHelper.EnforceCacheSizeLimitAsync();
                    })
                .MultiValues(t => t.HomePage, entries => entries
                    .Int(t => t.HomePageRows, 1, 12, 1)
                    .Int(t => t.HomePageColumns, 1, 12, 1)
                    .Bool(t => t.HideHomePageToolbar)
                    .Bool(t => t.HideHomePageCardTitle)))
            .NewGroup(t => t.NetworkSettings, group => group
                .Int(t => t.ApiRequestCooldown, 0, 5000, 100, entry => entry.ValueChanged += _ =>
                {
                    _networkRuntime.UpdateNetworkOptions();
                })
                .DomainFronting(t => t.PixivDomainFronting, t => t.EnablePixivDomainFronting, entry =>
                        entry.Enum(t => t.PixivDomainFrontingType)
                            .IPSet(t => t.PixivAppApiNameResolver)
                            .IPSet(t => t.PixivImageNameResolver)
                            .IPSet(t => t.PixivImageNameResolver2)
                            .IPSet(t => t.PixivOAuthNameResolver)
                            .IPSet(t => t.PixivAccountNameResolver)
                            .IPSet(t => t.PixivWebApiNameResolver),
                    entry => entry.MainValue.ValueChanged += t =>
                    {
                        _networkRuntime.AttachNameResolverHooks();
                        _downloadManager.UpdateNetworkOptions();
                        _networkRuntime.UpdateNetworkOptions();
                    })
                .DomainFronting(t => t.GitHubDomainFronting, t => t.EnableGitHubDomainFronting, entry => entry
                    .IPSet(t => t.GitHubNameResolver)
                    .IPSet(t => t.GitHubApiNameResolver)
                    .IPSet(t => t.GitHubAvatarNameResolver)
                    .IPSet(t => t.GitHubUserContentNameResolver)
                    .IPSet(t => t.GitHubAssetsNameResolver)
                    .IPSet(t => t.GitHubCodeloadNameResolver))
                .Proxy(entry => entry.ProxyChanged += t =>
                {
                    _downloadManager.UpdateNetworkOptions();
                    _networkRuntime.UpdateNetworkOptions();
                })
                .String(t => t.MirrorHost)
                .String(t => t.WebCookie))
            .NewGroup(t => t.BrowsingExperienceSettings, group => group
                .MultiValuesWithMainValue(t => t.ThumbnailLayout, t => t.ThumbnailLayoutType, entries => entries
                    .Int(t => t.IllustrationLinedFlowItemHeight, 50, 1000, 10)
                    .Int(t => t.IllustrationGridItemSize, 50, 1000, 10)
                    .Int(t => t.IllustrationGridLineSize, 50, 1000, 10)
                    .Int(t => t.IllustrationMasonryColumnWidth, 50, 1000, 10))
                .Enum(t => t.BrowseMode)
                .Enum(t => t.BrowseDirection)
                .MultiValues(t => t.AutoPlay, entries => entries
                    .Int(t => t.IllustrationViewerAutoPlayInterval, 1, 60, 1)
                    .Enum(t => t.IllustrationViewerAutoPlayMode)
                    .Enum(t => t.IllustrationViewerAutoPlayScope))
                .Enum(t => t.TargetFilter)
                .Collection(t => t.BlockedTags)
                .Collection(t => t.PinnedTags)
                .BlockedUsers(t => t.BlockedUsers)
                .Bool(t => t.OpenWorkInfoByDefault)
                .Bool(t => t.OpenUserInfoByDefault))
            .NewGroup(t => t.SearchSettings, group => group
                .String(t => t.SauceNaoApiKey)
                .Enum(t => t.DefaultSimpleWorkType)
                .MultiValues(t => t.RankOptions, entry =>
                    entry.Enum(
                            WorkTypeEnum.Illustration,
                            t => t.IllustrationRankOption,
                            SimpleWorkType.Illustration)
                        .Enum(
                            WorkTypeEnum.Novel,
                            t => t.NovelRankOption,
                            SimpleWorkType.Novel)))
            .NewGroup(t => t.DownloadSettings, group => group
                .Bool(t => t.OverwriteDownloadedFile, entry => entry.ValueChanged += _ => PushDownloadPolicy())
                .Int(t => t.MaxDownloadTaskConcurrencyLevel, 1, Environment.ProcessorCount, 1,
                    entry => entry.ValueChanged += t => _downloadManager.ConcurrencyDegree = t)
                .DownloadMacro(t => t.DownloadPathMacro)
                .MultiValues(t => t.DownloadFormats, entry =>
                    entry.IllustrationDownloadFormat(format => format.ValueChanged += _ => PushDownloadPolicy())
                        .UgoiraDownloadFormat(format => format.ValueChanged += _ => PushDownloadPolicy())
                        .NovelDownloadFormat(format => format.ValueChanged += _ => PushDownloadPolicy()))
                .Bool(t => t.EnableSubscriptionDaemon, entry => entry.ValueChanged += enabled =>
                {
                    if (_serviceProvider.GetService<IWorkSubscriptionService>() is { } subService)
                    {
                        if (enabled)
                            subService.StartDaemon((ulong)Math.Max(1, _appSettings.DownloadSettings.SubscriptionDaemonIntervalMinutes) * 60);
                        else
                            subService.StopDaemon();
                    }
                })
                .Int(t => t.SubscriptionDaemonIntervalMinutes, 1, 1440, 5, entry => entry.ValueChanged += minutes =>
                {
                    if (_serviceProvider.GetService<IWorkSubscriptionService>() is { } subService)
                    {
                        subService.SetDaemonInterval((ulong)Math.Max(1, minutes) * 60);
                    }
                })
                .WorkSubscriptions(t => t.WorkSubscriptions))
#if PIXEVAL_MCP
            .NewGroup(t => t.McpSettings, group => group
                .Bool(t => t.EnableServer, entry => entry.ValueChanged += value =>
                {
                    _ = ApplyMcpSettingsAsync();
                })
                .Int(t => t.Port, 1, 65535, 1, entry => entry.ValueChanged += value =>
                {
                    _ = ApplyMcpSettingsAsync();
                })
                .Bool(t => t.EnableWriteTools)
                .Int(t => t.MaxBinaryResourceMegabytes, 1, McpSettingsGroup.MaxBinaryResourceMegabytesLimit, 1))
#endif
            .Build();
    }

    private void PushDownloadPolicy() =>
        _downloadManager.SetDownloadPolicy(AppViewModel.CreateDownloadPolicy(_appSettings));

#if PIXEVAL_MCP
    private async Task ApplyMcpSettingsAsync()
    {
        try
        {
            if (_serviceProvider.GetService<IPixevalMcpService>() is { } service)
                await service.ApplySettingsAsync();
        }
        catch (Exception e)
        {
            _serviceProvider.GetService<FileLogger>()?
                .LogError("Failed to apply Pixeval MCP settings", e);
        }
    }
#endif
}
