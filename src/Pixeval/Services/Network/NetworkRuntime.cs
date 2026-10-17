// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Pixeval.Config;
using Pixeval.Maho;
using Pixeval.Mako;

namespace Pixeval.Services.Network;

public sealed class NetworkRuntime(
    AppSettings appSettings,
    MakoClient makoClient,
    MahoClient mahoClient,
    Func<MahoClientOptions> mahoOptions,
    IImageProviderService images) : INetworkRuntime
{
    public void AttachNameResolverHooks()
    {
        var fronting = appSettings.NetworkSettings.PixivDomainFronting;
        HookResolver(fronting.PixivAppApiNameResolver);
        HookResolver(fronting.PixivImageNameResolver);
        HookResolver(fronting.PixivImageNameResolver2);
        HookResolver(fronting.PixivOAuthNameResolver);
        HookResolver(fronting.PixivAccountNameResolver);
        HookResolver(fronting.PixivWebApiNameResolver);

        void HookResolver(ObservableCollection<string> ips) =>
            ips.CollectionChanged += OnResolverChanged;
    }

    public void UpdateNetworkOptions()
    {
        var config = appSettings.ToMakoConfiguration();
        makoClient.UpdateConfiguration(config);
        images.UpdateNetworkOptions(config);
        mahoClient.UpdateOptions(mahoOptions());
        AppInfo.AppVersion.ResetGithubClient();
    }

    private void OnResolverChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateNetworkOptions();
}
