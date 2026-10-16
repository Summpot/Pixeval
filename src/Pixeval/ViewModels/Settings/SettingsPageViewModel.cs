// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AutoSettingsPage;
using AutoSettingsPage.Models;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.Controls;
using Pixeval.I18N;
using Pixeval.Config;
using Pixeval.Services.Background;
using Pixeval.Utilities;

namespace Pixeval.ViewModels.Settings;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly AppSettings _appSettings;
    private readonly ExtensionService _extensionService;
    private readonly DownloadManager _downloadManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly INetworkRuntime _networkRuntime;
    private readonly INotificationService _notifications;
    private readonly IDialogService _dialogs;
    private readonly IStoragePickerService _storagePicker;
    private readonly INavigationService _navigation;

    /// <summary>
    /// Mode of the update status <see cref="InfoBar"/>.
    /// </summary>
    [ObservableProperty]
    public partial InfoBarMode UpdateStatusMode { get; set; } = InfoBarMode.Info;

    /// <summary>
    /// Text of the update status <see cref="InfoBar"/>.
    /// </summary>
    [ObservableProperty]
    public partial string UpdateStatusText { get; set; } = "";

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

    public SettingsPageViewModel(
        AppSettings appSettings,
        ExtensionService extensionService,
        DownloadManager downloadManager,
        IServiceProvider serviceProvider,
        INetworkRuntime networkRuntime,
        INotificationService notifications,
        IDialogService dialogs,
        IStoragePickerService storagePicker,
        INavigationService navigation)
    {
        _notifications = notifications;
        _dialogs = dialogs;
        _storagePicker = storagePicker;
        _navigation = navigation;
        _appSettings = appSettings;
        _extensionService = extensionService;
        _downloadManager = downloadManager;
        _serviceProvider = serviceProvider;
        _networkRuntime = networkRuntime;
        LocalGroups = BuildLocalGroups();
        RefreshUpdateStatus();
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
                        t => t.ValueChanged += _value => _ = _serviceProvider.GetRequiredService<IImageProviderService>().EnforceCacheSizeLimitAsync()),
                    t => t.MainValue.ValueChanged += enabled =>
                    {
                        if (enabled)
                            _ = _serviceProvider.GetRequiredService<IImageProviderService>().EnforceCacheSizeLimitAsync();
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
                    entry.IllustrationDownloadFormat(_extensionService, format => format.ValueChanged += _ => PushDownloadPolicy())
                        .UgoiraDownloadFormat(_extensionService, format => format.ValueChanged += _ => PushDownloadPolicy())
                        .NovelDownloadFormat(_extensionService, format => format.ValueChanged += _ => PushDownloadPolicy()))
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

    public void RefreshUpdateStatus()
    {
        RefreshLastCheckedUpdate();
        var updateState = AppInfo.AppVersion.UpdateState;
        var (mode, resource) = updateState switch
        {
            UpdateState.UpToDate => (InfoBarMode.Success, SettingsMainViewResources.IsUpToDate),
            UpdateState.MajorUpdate => (InfoBarMode.Warning, SettingsMainViewResources.UpdateAvailable.Major),
            UpdateState.MinorUpdate => (InfoBarMode.Warning, SettingsMainViewResources.UpdateAvailable.Minor),
            UpdateState.BuildUpdate => (InfoBarMode.Warning, SettingsMainViewResources.UpdateAvailable.Build),
            UpdateState.Insider => (InfoBarMode.Info, SettingsMainViewResources.IsInsider),
            UpdateState.Unknown => (InfoBarMode.Info, SettingsMainViewResources.UpdateAvailable.Unknown),
            _ => throw new ArgumentOutOfRangeException(nameof(updateState), updateState, null)
        };
        UpdateStatusMode = mode;
        UpdateStatusText = I18NManager.GetResource(resource);
    }

    [RelayCommand]
    private void SwitchAccount() => _navigation.NavigateToLogin();

    [RelayCommand]
    private async Task ResetDefaultSettingsAsync()
    {
        if (await _dialogs.CreateOkCancelAsync(
                I18NManager.GetResource(SettingsMainViewResources.ResetSettingConfirmationDialogTitle),
                I18NManager.GetResource(SettingsMainViewResources.ResetSettingConfirmationDialogContent))
            is not ContentDialogResult.Primary)
            return;

        var settings = new AppSettings();
        _serviceProvider.GetRequiredService<NavigationMenuDocument>().Text = NavigationMenuYaml.DefaultYaml;
        _serviceProvider.GetRequiredService<HomePageCardSession>().Reset();
        foreach (var localGroup in LocalGroups)
            foreach (var settingsEntry in localGroup)
                settingsEntry.LocalValueReset(settings);
        foreach (var extensionGroup in ExtensionGroups)
            foreach (var settingsEntry in extensionGroup)
                settingsEntry.ValueReset();

        _navigation.ReloadNavigation();
    }

    [RelayCommand]
    private async Task ShowReleaseNotesAsync() =>
        _ = await _dialogs.CreateAcknowledgementTaskAsync(
            I18NManager.GetResource(SettingsMainViewResources.ReleaseNoteDialog.Title),
            async contentDialog =>
            {
                var control = await Views.Settings.SettingsPage.GetReleaseNotesAsync();
                RefreshUpdateStatus();
                contentDialog.Title = Views.Settings.SettingsPage.ReleaseTitle;
                contentDialog.Content = control;
            });

    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        UpdateStatusMode = InfoBarMode.Info;
        UpdateStatusText = I18NManager.GetResource(SettingsMainViewResources.CheckingForUpdate);
        await AppInfo.AppVersion.CheckForUpdateAsync();
        RefreshUpdateStatus();

        if (AppInfo.AppVersion is not { UpdateAvailable: true, NewestAppReleaseModel: { } release })
            return;

        if (!AppInfo.AppVersion.UsesVelopack)
        {
            _ = await _dialogs.CreateAcknowledgementAsync(
                Views.Settings.SettingsPage.GetReleaseTitle(release.Version),
                Views.Settings.SettingsPage.CreateReleaseNotes(release));
            return;
        }

        ReportUpdateProgress(0);
        try
        {
            IProgress<int> progress = new Progress<int>(value => Dispatcher.UIThread.Post(() => ReportUpdateProgress(value)));
            if (!await AppInfo.AppVersion.DownloadUpdateAsync(progress.Report))
                return;
        }
        catch (Exception exception)
        {
            UpdateStatusMode = InfoBarMode.Error;
            UpdateStatusText = I18NManager.GetResource(SettingsMainViewResources.UpdateDownloadFailed);
            _notifications.ShowError(
                I18NManager.GetResource(SettingsMainViewResources.UpdateDownloadFailed),
                exception.Message);
            return;
        }

        var readyVersion = AppInfo.AppVersion.PendingUpdateVersion?.ToString() ?? release.Version;
        var readyMessage = I18NManager.GetResource(SettingsMainViewResources.UpdateDownloadReadyFormatted, readyVersion);
        UpdateStatusMode = InfoBarMode.Success;
        UpdateStatusText = readyMessage;
        _notifications.ShowSuccess(readyMessage);
    }

    private void ReportUpdateProgress(int value)
    {
        UpdateStatusMode = InfoBarMode.Info;
        UpdateStatusText = I18NManager.GetResource(SettingsMainViewResources.UpdateDownloadProgressFormatted, value);
    }

    [RelayCommand]
    private async Task ExportSettingsAsync()
    {
        try
        {
            if (await _storagePicker.SaveFileAsync(new()
            {
                FileTypeChoices =
                [
                    new("YAML")
                    {
                        Patterns = ["*.yaml"],
                        MimeTypes = ["text/yaml", "application/x-yaml"]
                    }
                ],
                DefaultExtension = "yaml",
                SuggestedFileName = "settings"
            }) is not { } file)
                return;
            await using var stream = await file.OpenWriteAsync();
            var json = JsonSerializer.Serialize(_appSettings, SettingsSerializerContext.Default.AppSettings);
            var yaml = new ConfigEngine().JsonToYaml(json);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(yaml);

            _notifications.ShowSuccess(I18NManager.GetResource(SettingsMainViewResources.ExportSettingsSuccess));
        }
        catch (Exception exception)
        {
            _notifications.ShowError(exception.Message, exception.StackTrace);
        }
    }

    [RelayCommand]
    private async Task ImportSettingsAsync()
    {
        try
        {
            if (await _storagePicker.OpenFilesAsync(new()
            {
                FileTypeFilter = [new("YAML") { Patterns = ["*.yaml"] }],
                AllowMultiple = false
            }) is not [{ } file])
                return;

            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var rawYaml = await reader.ReadToEndAsync();
            var engine = new ConfigEngine();
            var json = engine.YamlToJson(engine.MigrateYaml(rawYaml));

            if (JsonSerializer.Deserialize(json, SettingsSerializerContext.Default.AppSettings) is { } appSettings)
            {
                foreach (var localGroup in LocalGroups)
                    foreach (var settingsEntry in localGroup)
                        settingsEntry.LocalValueReset(appSettings);
                _notifications.ShowSuccess(I18NManager.GetResource(SettingsMainViewResources.ImportSettingsSuccess), file.Name);
            }
        }
        catch (Exception exception)
        {
            _notifications.ShowError(exception.Message, exception.StackTrace);
        }
    }

    [RelayCommand]
    private async Task ClearDataAsync(ClearDataKind kind)
    {
        switch (kind)
        {
            case ClearDataKind.FileCache:
                await _serviceProvider.GetRequiredService<IImageProviderService>().PurgeCacheAsync();
                break;
            case ClearDataKind.SearchHistory:
                _serviceProvider.GetRequiredService<SearchHistorySession>().Clear();
                break;
            case ClearDataKind.BrowseHistory:
                _serviceProvider.GetRequiredService<StorageEngine>().HistoryRepository.Clear();
                break;
            case ClearDataKind.DownloadHistory:
                var storage = _serviceProvider.GetRequiredService<StorageEngine>();
                _downloadManager.ClearTasks();
                storage.ClearDownloadHistory();
                storage.ClearSubscriptionDownloadHistory();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        _notifications.ShowSuccess(I18NManager.GetResource(kind switch
        {
            ClearDataKind.FileCache => EnumResources.ClearDataKind.FileCache,
            ClearDataKind.BrowseHistory => EnumResources.ClearDataKind.BrowseHistory,
            ClearDataKind.SearchHistory => EnumResources.ClearDataKind.SearchHistory,
            _ => EnumResources.ClearDataKind.DownloadHistory
        }));
    }

    /// <summary>
    /// Persists extension settings and the application settings file, then reports success.
    /// </summary>
    public void SaveSettings()
    {
        foreach (var extensionGroup in ExtensionGroups)
            foreach (var settingsEntry in extensionGroup)
                settingsEntry.ValueSaving(extensionGroup.Model.Values);

        AppInfo.SaveSettings(
            _appSettings,
            _serviceProvider.GetService<HomePageCardSession>()?.Cards,
            _serviceProvider.GetService<NavigationMenuDocument>()?.Text);
        _notifications.ShowSuccess(I18NManager.GetResource(SettingsMainViewResources.SettingsSaved));
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
