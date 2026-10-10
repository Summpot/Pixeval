// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Pixeval.AppManagement;
using Pixeval.AppManagement.Settings;
using Pixeval.Extensions.Common;
using Pixeval.Native.Plugin;
using Pixeval.Utilities;

namespace Pixeval.Models.Extensions;

public sealed partial class ExtensionService : IDisposable
{
    private static readonly StringComparer _TargetPathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static string CurrentVersion { get; } = ExtensionsHostStatics.CurrentSdkVersion.ToString();
    public PluginHostEngine PluginEngine { get; } = new(CurrentVersion);

    public static string? NativeLibraryExtension =>
        OperatingSystem.IsWindows() ? ".dll"
        : OperatingSystem.IsLinux() || OperatingSystem.IsAndroid() ? ".so"
        : OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst() ? ".dylib"
        : null;

    public ObservableCollection<ExtensionsHostModel> HostModels { get; } = [];
    public IEnumerable<ExtensionsHostModel> ActiveModels => HostModels.Where(t => t.IsActive);
    public IReadOnlyList<ExtensionSettingsGroup> SettingsGroups => _settingsGroups;
    public int OutDateExtensionHostsCount => _outdatedExtensionHostUninstallTargets.Count;

    private readonly List<ExtensionSettingsGroup> _settingsGroups = [];
    private readonly HashSet<string> _outdatedExtensionHostUninstallTargets = new(_TargetPathComparer);
    private readonly HashSet<string> _pendingExtensionUninstallTargets;
    private readonly Dictionary<string, Dictionary<string, object?>> _extensionSettings;
    private readonly AppSettings? _appSettings;

    public ExtensionService(FileLogger logger, AppSettings appSettings, bool loadInstalledHosts = true)
        : this(logger, appSettings, appSettings.ExtensionSettings,
            appSettings.PendingExtensionUninstallTargets = new HashSet<string>(appSettings.PendingExtensionUninstallTargets, _TargetPathComparer),
            loadInstalledHosts) { }

    internal ExtensionService(
        FileLogger logger,
        Dictionary<string, Dictionary<string, object?>> extensionSettings,
        HashSet<string> pendingExtensionUninstallTargets,
        bool loadInstalledHosts)
        : this(logger, null, extensionSettings, pendingExtensionUninstallTargets, loadInstalledHosts)
    {
    }

    internal ExtensionService(
        FileLogger logger,
        AppSettings? appSettings,
        Dictionary<string, Dictionary<string, object?>> extensionSettings,
        HashSet<string> pendingExtensionUninstallTargets,
        bool loadInstalledHosts)
    {
        _appSettings = appSettings;
        _extensionSettings = extensionSettings;
        _pendingExtensionUninstallTargets = pendingExtensionUninstallTargets;
        if (!loadInstalledHosts) return;

        var failedTargets = PluginEngine.CleanPendingUninstalls([.. pendingExtensionUninstallTargets], AppInfo.ExtensionsFolder);
        pendingExtensionUninstallTargets.Clear();
        foreach (var failed in failedTargets) _pendingExtensionUninstallTargets.Add(failed);
        AppInfo.SaveAppSettings(_appSettings);

        foreach (var host in EnumerateLocalExtensionHosts(AppInfo.ExtensionsFolder))
        {
            var result = TryLoadHostWithResult(host.LibraryPath, logger, out _, out _, host.UninstallTargetRelativePath);
            if (result is ExtensionHostLoadResult.OutdatedSdk)
                _ = _outdatedExtensionHostUninstallTargets.Add(host.UninstallTargetRelativePath);
        }

        HostModels.CollectionChanged += (s, _) =>
        {
            if (s is ObservableCollection<ExtensionsHostModel> { Count: var c } o)
                for (var i = 0; i < c; i++) o[i].Priority = i;
        };
    }

    public static IEnumerable<LocalExtensionHost> EnumerateLocalExtensionHosts(string directory) =>
        Directory.Exists(directory) ? new PluginHostEngine(CurrentVersion).EnumerateExtensionHosts(directory) : [];

    public void UnloadHost(ExtensionsHostModel model)
    {
        try
        {
            model.PropertyChanged -= OnHostModelPropertyChanged;
            _ = HostModels.Remove(model);
            if (_settingsGroups.FirstOrDefault(t => t.Model == model) is { } group) _ = _settingsGroups.Remove(group);
            foreach (var extension in model.Extensions) extension.OnExtensionUnloaded();
            try { _ = PluginEngine.UnloadPlugin(model.Name); } catch { /* ignored */ }
            model.Dispose();
        }
        catch { /* ignored */ }
    }

    public bool ScheduleHostUninstall(ExtensionsHostModel model)
    {
        if (model.UninstallTargetRelativePath.Length is 0) return false;
        _ = _pendingExtensionUninstallTargets.Add(model.UninstallTargetRelativePath);
        model.IsPendingUninstall = true;
        AppInfo.SaveAppSettings(_appSettings);
        return true;
    }

    public bool CancelHostUninstall(ExtensionsHostModel model)
    {
        if (model.UninstallTargetRelativePath.Length is 0) return false;
        _ = _pendingExtensionUninstallTargets.Remove(model.UninstallTargetRelativePath);
        model.IsPendingUninstall = false;
        AppInfo.SaveAppSettings(_appSettings);
        return true;
    }

    public int ScheduleAllHostUninstalls() =>
        ScheduleUninstallTargets(EnumerateLocalExtensionHosts(AppInfo.ExtensionsFolder).Select(static h => h.UninstallTargetRelativePath));

    public int ScheduleOutdatedHostUninstalls() => ScheduleUninstallTargets(_outdatedExtensionHostUninstallTargets);

    private int ScheduleUninstallTargets(IEnumerable<string> relativeTargets)
    {
        var count = 0;
        foreach (var target in relativeTargets)
        {
            if (target.Length is 0) continue;
            _ = _pendingExtensionUninstallTargets.Add(target);
            count++;
        }
        foreach (var model in HostModels)
            model.IsPendingUninstall = _pendingExtensionUninstallTargets.Contains(model.UninstallTargetRelativePath);
        AppInfo.SaveAppSettings(_appSettings);
        return count;
    }

    private void OnHostModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ExtensionsHostModel model && e.PropertyName == nameof(ExtensionsHostModel.IsActive))
        {
            try { _ = PluginEngine.SetPluginActive(model.Name, model.IsActive); } catch { /* ignored */ }
        }
    }

    public void Dispose()
    {
        while (HostModels is [var model, ..]) UnloadHost(model);
        PluginEngine.Dispose();
    }
}
