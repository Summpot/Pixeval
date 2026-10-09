// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.AppManagement.Settings;
using Pixeval.Models.Options;
using Pixeval.Utilities;
using Pixeval.Utilities.Network;

namespace Pixeval.Native.Update;

public partial class UpdateEngine
{
    public static UpdateEngine CreateFromSettings(NetworkSettingsGroup networkSettings)
    {
        var domainFronting = networkSettings.GitHubDomainFronting.EnableGitHubDomainFronting;
        string? proxyUrl = networkSettings.ProxySettings.ProxyType switch
        {
            ProxyType.Custom => ProxyHelper.NormalizeProxyUri(networkSettings.ProxySettings.Proxy),
            _ => null
        };

        var dnsMappings = new Dictionary<string, List<string>>();
        if (domainFronting)
        {
            dnsMappings["github.com"] = [.. networkSettings.GitHubDomainFronting.GitHubNameResolver];
            dnsMappings["api.github.com"] = [.. networkSettings.GitHubDomainFronting.GitHubApiNameResolver];
            dnsMappings["avatars.githubusercontent.com"] = [.. networkSettings.GitHubDomainFronting.GitHubAvatarNameResolver];
            dnsMappings["objects.githubusercontent.com"] = [.. networkSettings.GitHubDomainFronting.GitHubUserContentNameResolver];
            dnsMappings["github.githubassets.com"] = [.. networkSettings.GitHubDomainFronting.GitHubAssetsNameResolver];
            dnsMappings["codeload.github.com"] = [.. networkSettings.GitHubDomainFronting.GitHubCodeloadNameResolver];
        }

        var options = new UpdateNetworkOptions(
            EnableDomainFronting: domainFronting,
            ProxyUrl: proxyUrl,
            CustomDnsMappings: dnsMappings);

        return new UpdateEngine(options);
    }

    public async Task<string> DownloadAssetWithProgressAsync(
        ReleaseAsset asset,
        string destinationPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var token = cancellationToken.CanBeCanceled ? new UpdateCancellationToken() : null;
        await using var registration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(() => token?.Cancel())
            : default;

        var callback = progress != null ? new ProgressCallbackAdapter(progress) : null;
        return await DownloadAssetAsync(asset, destinationPath, callback, token).ConfigureAwait(false);
    }

    public async Task<string> DownloadFileWithProgressAsync(
        string url,
        string destinationPath,
        string? expectedSha256 = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var token = cancellationToken.CanBeCanceled ? new UpdateCancellationToken() : null;
        await using var registration = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(() => token?.Cancel())
            : default;

        var callback = progress != null ? new ProgressCallbackAdapter(progress) : null;
        return await DownloadFileAsync(url, destinationPath, expectedSha256, callback, token).ConfigureAwait(false);
    }

    private sealed class ProgressCallbackAdapter(IProgress<int> progress) : IUpdateProgressCallback
    {
        public void OnProgress(ulong downloadedBytes, ulong totalBytes, double percentage)
        {
            progress.Report((int) Math.Clamp(percentage, 0.0, 100.0));
        }
    }
}
