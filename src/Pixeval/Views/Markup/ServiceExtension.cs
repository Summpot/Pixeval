// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace Pixeval.Views.Markup;

public class ServiceExtension : MarkupExtension
{
    public Type? Type { get; set; }

    public ServiceExtension()
    {
    }

    public ServiceExtension(Type type)
    {
        Type = type;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (Type is null)
            throw new InvalidOperationException("Type must be specified for ServiceExtension.");

        var targetProvider = App.Services
            ?? serviceProvider.GetService<IServiceProvider>()
            ?? throw new InvalidOperationException("Dependency injection service provider is not initialized.");

        return targetProvider.GetRequiredService(Type);
    }
}
