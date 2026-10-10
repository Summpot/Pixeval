// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using Microsoft.Extensions.DependencyInjection;

namespace Pixeval.Native.Booru;

public static class BooruServiceExtensions
{
    public static IServiceCollection AddBooruServices(this IServiceCollection services)
    {
        var booruClient = new BooruClient(null);
        services.AddSingleton(booruClient);
        return services;
    }
}
