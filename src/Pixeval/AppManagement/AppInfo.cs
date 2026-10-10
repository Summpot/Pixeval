// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using Pixeval.AppManagement.Settings;
using Pixeval.Native.Config;
using Pixeval.Utilities;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Pixeval.AppManagement;

/// <summary>
/// Provide miscellaneous information about the app
/// </summary>
public static class AppInfo
{
    public const string AppIdentifier = nameof(Pixeval);

    public const string AppProtocol = "pixeval";

    private static string ClassicApplicationFolderPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppIdentifier);

    public static string ApplicationFolderPath { get; } = AppContext.BaseDirectory.Contains(
            Path.Combine("WindowsApps", "PokerKo.4454907E5DDB5_"),
            StringComparison.OrdinalIgnoreCase)
        && !Directory.Exists(ClassicApplicationFolderPath)
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages",
            "PokerKo.4454907E5DDB5_0wpjzgvbyjvyr",
            "LocalCache",
            "Local",
            AppIdentifier)
        : ClassicApplicationFolderPath;

    public static string SettingsFolder { get; } = Path.Combine(ApplicationFolderPath, "Settings");

    public static string CacheFolder { get; } = Path.Combine(ApplicationFolderPath, "Cache");

    public static string LogsFolder { get; } = Path.Combine(ApplicationFolderPath, "Logs");

    public static string TempFolder { get; } = Path.Combine(ApplicationFolderPath, "Temp");

    public static string ExtensionsFolder { get; } = Path.Combine(ApplicationFolderPath, "Extensions");

    private static string AppSettingsPath { get; } = Path.Combine(SettingsFolder, "settings.yaml");

    private static string LoginContextPath { get; } = Path.Combine(SettingsFolder, "login_context.yaml");

    private static string HomePageCardsPath { get; } = Path.Combine(SettingsFolder, "home_page_cards.yaml");

    private static string NavigationMenuPath { get; } = Path.Combine(SettingsFolder, "navigation_menu.yaml");

    public static string DatabaseFilePath { get; } = Path.Combine(SettingsFolder, "Pixeval5.0.0.sqlite");

    public static readonly Uri IconApplicationUri = new($"avares://{AppIdentifier}/Assets/logo.ico");

    public static readonly Uri SvgIconApplicationUri = new($"avares://{AppIdentifier}/Assets/logo.svg");

    public static Versioning AppVersion { get; } = new();

    static AppInfo()
    {
        _ = FileHelper.TryDeleteDirectory(TempFolder);
        // Ensure directories exist
        _ = FileHelper.TryCreateDirectory(ApplicationFolderPath);
        _ = FileHelper.TryCreateDirectory(SettingsFolder);
        _ = FileHelper.TryCreateDirectory(CacheFolder);
        _ = FileHelper.TryCreateDirectory(LogsFolder);
        _ = FileHelper.TryCreateDirectory(TempFolder);
        _ = FileHelper.TryCreateDirectory(ExtensionsFolder);
    }

    public const string AssetsPathPrefix = $"avares://{AppIdentifier}/Assets/";

    public const string ImageNotAvailablePath = $"{AssetsPathPrefix}image-not-available.png";

    public const string BlockedContentPath = $"{AssetsPathPrefix}blocked-content.png";

    public static Stream GetImageNotAvailableStream() => AssetLoader.Open(new Uri(ImageNotAvailablePath));

    public static async Task<byte[]> GetAssetBytesAsync(string relativeToAssetsFolder)
    {
        await using var stream = GetAssetStream(relativeToAssetsFolder);
        await using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    public static Stream GetAssetStream(string relativeToAssetsFolder)
    {
        var uri = new Uri(AssetsPathPrefix + relativeToAssetsFolder);
        return AssetLoader.Open(uri);
    }

    public static async Task<string> GetAssetStringAsync(string relativeToAssetsFolder, Encoding? encoding = null)
    {
        var reader = new StreamReader(GetAssetStream(relativeToAssetsFolder), encoding);
        return await reader.ReadToEndAsync();
    }

    public static void SaveWindowContext(Window window, AppSettings? settings = null)
    {
        settings ??= App.Services?.GetService<AppSettings>();
        if (settings is null)
            return;
        var applicationSettings = settings.ApplicationSettings;
        var isMaximized = applicationSettings.IsMaximized = window.WindowState is WindowState.Maximized;
        // 在非最大化状态下保存窗口大小，以便从最大化恢复时使用
        if (!isMaximized)
        {
            applicationSettings.WindowWidth = window.Width;
            applicationSettings.WindowHeight = window.Height;
        }
    }

    public static void SaveContext(
        LoginContext? loginContext = null,
        AppSettings? appSettings = null,
        ObservableCollection<HomePageCardLayout>? cards = null,
        string? navYaml = null)
    {
        loginContext ??= App.Services?.GetService<LoginContext>();
        SaveLoginContext(loginContext);
        SaveSettings(appSettings, cards, navYaml);
    }

    public static AppSettings? LoadAppSettings(FileLogger logger)
    {
        if (!File.Exists(AppSettingsPath))
            return null;

        return TryLoad(() =>
        {
            var rawYaml = File.ReadAllText(AppSettingsPath);
            var engine = new ConfigEngine();
            var migratedYaml = engine.MigrateYaml(rawYaml);
            var json = engine.YamlToJson(migratedYaml);
            return JsonSerializer.Deserialize(json, SettingsSerializerContext.Default.AppSettings);
        }, logger);
    }

    public static LoginContext? LoadLoginContext(FileLogger logger)
    {
        if (!File.Exists(LoginContextPath))
            return null;

        return TryLoad(() =>
        {
            var rawYaml = File.ReadAllText(LoginContextPath);
            var json = new ConfigEngine().YamlToJson(rawYaml);
            return JsonSerializer.Deserialize(json, SettingsSerializerContext.Default.LoginContext);
        }, logger);
    }

    public static ObservableCollection<HomePageCardLayout>? LoadHomePageCards(FileLogger logger)
    {
        if (!File.Exists(HomePageCardsPath))
            return null;

        return TryLoad(() =>
        {
            var cards = new ConfigEngine().LoadHomePageCardsFromFile(HomePageCardsPath);
            return new ObservableCollection<HomePageCardLayout>(cards);
        }, logger);
    }

    public static string? LoadNavigationMenuYaml(FileLogger logger)
    {
        if (!File.Exists(NavigationMenuPath))
            return null;

        return TryLoad(() => File.ReadAllText(NavigationMenuPath), logger);
    }

    public static void SaveSettings(
        AppSettings? appSettings = null,
        ObservableCollection<HomePageCardLayout>? cards = null,
        string? navYaml = null)
    {
        appSettings ??= App.Services?.GetService<AppSettings>();
        cards ??= App.Services?.GetService<Services.HomePageCardSession>()?.Cards;
        navYaml ??= App.Services?.GetService<Services.NavigationMenuDocument>()?.Text;

        SaveAppSettings(appSettings);
        if (cards is not null)
            SaveHomePageCards(cards);
        if (navYaml is not null)
            SaveNavigationMenuYaml(navYaml);
    }

    public static void SaveAppSettings(AppSettings? appSettings)
    {
        if (appSettings is null)
            return;

        _ = TrySave(() =>
        {
            var json = JsonSerializer.Serialize(appSettings, SettingsSerializerContext.Default.AppSettings);
            var engine = new ConfigEngine();
            var yaml = engine.JsonToYaml(json);
            engine.SaveToFile(AppSettingsPath, yaml);
        });
    }

    public static void SaveLoginContext(LoginContext? loginContext)
    {
        if (loginContext is null)
            return;

        _ = TrySave(() =>
        {
            var json = JsonSerializer.Serialize(loginContext, SettingsSerializerContext.Default.LoginContext);
            var engine = new ConfigEngine();
            var yaml = engine.JsonToYaml(json);
            engine.SaveToFile(LoginContextPath, yaml);
        });
    }

    public static void SaveHomePageCards(ObservableCollection<HomePageCardLayout>? cards)
    {
        if (cards is null)
            return;

        _ = TrySave(() =>
        {
            new ConfigEngine().SaveHomePageCardsToFile(HomePageCardsPath, [.. cards]);
        });
    }

    public static void SaveNavigationMenuYaml(string? yaml)
    {
        if (yaml is null)
            return;

        _ = TrySave(() =>
            File.WriteAllText(NavigationMenuPath, yaml.ReplaceLineEndings(Environment.NewLine), Encoding.UTF8));
    }

    private static T? TryLoad<T>(Func<T> load, FileLogger logger, [CallerMemberName] string? callerName = null)
    {
        try
        {
            return load();
        }
        catch (Exception e)
        {
            logger.LogError($"Failed to {callerName}", e);
            return default;
        }
    }

    private static bool TrySave(Action save)
    {
        try
        {
            save();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
