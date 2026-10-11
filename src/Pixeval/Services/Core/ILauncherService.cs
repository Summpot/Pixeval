// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Threading.Tasks;

namespace Pixeval.Services.Core;

public interface ILauncherService
{
    Task LaunchUriAsync(Uri uri);

    Task LaunchFileInfoAsync(FileInfo file);

    Task LaunchDirectoryInfoAsync(DirectoryInfo directory);
}
