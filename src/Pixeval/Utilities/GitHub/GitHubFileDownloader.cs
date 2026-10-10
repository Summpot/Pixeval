// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Native.Update;
using Velopack.Sources;

namespace Pixeval.Utilities.GitHub;

/// <summary>
/// Forwards Velopack update downloads to <see cref="UpdateEngine"/>.
/// </summary>
internal sealed class GitHubFileDownloader(Func<UpdateEngine> engineProvider) : IFileDownloader
{
    public Task<string> DownloadString(
        string url,
        IDictionary<string, string>? headers,
        double timeout)
        => engineProvider().DownloadUpdateTextAsync(url, headers, NormalizeTimeout(timeout));

    public Task<byte[]> DownloadBytes(
        string url,
        IDictionary<string, string>? headers,
        double timeout)
        => engineProvider().DownloadUpdateBytesAsync(url, headers, NormalizeTimeout(timeout));

    public Task DownloadFile(
        string url,
        string targetFile,
        Action<int> progress,
        IDictionary<string, string>? headers,
        double timeout,
        CancellationToken cancelToken)
        => engineProvider().DownloadFileWithProgressAsync(
            url,
            targetFile,
            expectedSha256: null,
            progress: progress is null ? null : new Progress<int>(progress),
            cancellationToken: cancelToken,
            headers: headers,
            timeoutMinutes: NormalizeTimeout(timeout));

    private static double? NormalizeTimeout(double timeout) =>
        timeout is > 0 and < double.PositiveInfinity ? timeout : null;
}
