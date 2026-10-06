using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Pixeval.AppManagement;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Extensions;
using Pixeval.Models.Subscriptions;
using Pixeval.Native.Mcp;
using Pixeval.Native.Storage;
using Pixeval.Utilities;
using Pixeval.Utilities.IO.Caching;
using NativeMcpServer = Pixeval.Native.Mcp.McpServer;

namespace Pixeval.Models.McpServer;

public sealed class PixevalMcpService : IPixevalMcpService, IMcpSessionBridge
{
    public const string DefaultPath = "/mcp";

    private readonly AppViewModel _appViewModel;
    private readonly FileLogger _logger;
    private readonly SemaphoreSlim _lifetimeLock = new(1, 1);
    private NativeMcpServer? _server;
    private ushort? _serverPort;
    private bool _disposed;

    public PixevalMcpService(AppViewModel appViewModel, FileLogger logger)
    {
        _appViewModel = appViewModel;
        _logger = logger;
    }

    private McpSettingsGroup Settings => _appViewModel.AppSettings.McpSettings;

    public string AppVersion => AppInfo.AppVersion.CurrentVersionShortText;

    public ushort Port => Settings.Port;

    public Uri? Endpoint => _server is not null ? new Uri(_server.Endpoint()) : null;

    public Task StartAsync(CancellationToken token = default) =>
        ApplySettingsAsync(token);

    public async Task ApplySettingsAsync(CancellationToken token = default)
    {
        await _lifetimeLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;

            await ApplySettingsCoreAsync(token).ConfigureAwait(false);
        }
        finally
        {
            _lifetimeLock.Release();
        }
    }

    private async Task ApplySettingsCoreAsync(CancellationToken token)
    {
        var port = Port;
        if (!Settings.EnableServer)
        {
            await StopCoreAsync().ConfigureAwait(false);
            return;
        }

        if (_server is not null && _serverPort == port && _server.IsRunning())
            return;

        if (_server is not null)
            await StopCoreAsync().ConfigureAwait(false);

        await StartCoreAsync(port, token).ConfigureAwait(false);
    }

    private async Task StartCoreAsync(ushort port, CancellationToken token)
    {
        var config = new McpServerConfig(
            port,
            Settings.EnableWriteTools,
            Math.Clamp(Settings.MaxBinaryResourceMegabytes, 1, McpSettingsGroup.MaxBinaryResourceMegabytesLimit),
            AppVersion,
            _appViewModel.AppSettings.BrowsingExperienceSettings.TargetFilter.ToString()
        );

        try
        {
            var storageEngine = _appViewModel.AppServiceProvider.GetRequiredService<StorageEngine>();
            var downloadManager = _appViewModel.HistoryPersistHelper.DownloadManager;
            var syncEngine = _appViewModel.AppServiceProvider.GetRequiredService<WorkSubscriptionDownloadService>().SyncEngine;
            var pluginEngine = _appViewModel.AppServiceProvider.GetService<ExtensionService>()?.PluginEngine;

            var server = new NativeMcpServer(
                config,
                this,
                _appViewModel.MakoClient,
                storageEngine,
                downloadManager,
                CacheHelper.CacheEngine,
                syncEngine,
                pluginEngine
            );

            await server.StartAsync().ConfigureAwait(false);
            _server = server;
            _serverPort = port;
            _logger.LogInformation($"Pixeval native MCP server started at {_server.Endpoint()}", null);
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to start Pixeval native MCP server", e);
        }
    }

    private async Task StopCoreAsync()
    {
        if (_server is { } server)
        {
            _server = null;
            _serverPort = null;
            try
            {
                await server.StopAsync().ConfigureAwait(false);
                server.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogWarning("Error while stopping native MCP server", e);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetimeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            await StopCoreAsync().ConfigureAwait(false);
            _lifetimeLock.Dispose();
        }
        finally
        {
            // Lock released
        }
    }

    #region IMcpSessionBridge Implementation

    public McpSessionUserInfo? GetCurrentUser()
    {
        if (_appViewModel.GetCurrentLoginUser() is { } user)
            return new McpSessionUserInfo(user.Id.ToString(), user.Name, user.Account);
        return null;
    }

    public string GetHelpDocument(string? topic)
    {
        try
        {
            var helpFileName = topic switch
            {
                _ => "McpHelp.md"
            };

            var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
            var path = Path.Combine(AppContext.BaseDirectory, "i18n", culture, helpFileName);
            if (!File.Exists(path))
                path = Path.Combine(AppContext.BaseDirectory, "i18n", "zh-Hans", helpFileName);
            if (!File.Exists(path))
                path = Path.Combine(AppContext.BaseDirectory, "i18n", "en-US", helpFileName);

            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to read help markdown", ex);
        }

        return "# Pixeval MCP Help\n\nRefer to Pixeval documentation for available tools and resources.";
    }

    public void OnDownloadMacroChanged(string macroText)
    {
        _appViewModel.AppSettings.DownloadSettings.DownloadPathMacro = macroText;
        AppInfo.SaveAppSettings(_appViewModel.AppSettings);
    }

    public void LogEvent(string level, string message)
    {
        if (string.Equals(level, "error", StringComparison.OrdinalIgnoreCase))
            _logger.LogError($"[MCP] {message}", null);
        else
            _logger.LogInformation($"[MCP] {message}", null);
    }

    #endregion
}
