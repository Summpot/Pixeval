// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Threading.Tasks;

namespace Pixeval.Services.Core;

public sealed class LauncherService : ILauncherService
{
    private readonly IUiHost _host;

    public LauncherService(IUiHost host) => _host = host;

    public Task LaunchUriAsync(Uri uri) =>
        _host.TopLevel?.Launcher?.LaunchUriAsync(uri) ?? Task.CompletedTask;

    public Task LaunchFileInfoAsync(FileInfo file) =>
        _host.TopLevel?.Launcher?.LaunchFileInfoAsync(file) ?? Task.CompletedTask;

    public Task LaunchDirectoryInfoAsync(DirectoryInfo directory) =>
        _host.TopLevel?.Launcher?.LaunchDirectoryInfoAsync(directory) ?? Task.CompletedTask;
}

public sealed class NullLauncherService : ILauncherService
{
    public static NullLauncherService Instance { get; } = new();

    public Task LaunchUriAsync(Uri uri) => Task.CompletedTask;

    public Task LaunchFileInfoAsync(FileInfo file) => Task.CompletedTask;

    public Task LaunchDirectoryInfoAsync(DirectoryInfo directory) => Task.CompletedTask;
}
