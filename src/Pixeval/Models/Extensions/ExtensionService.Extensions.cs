// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.Commands.Transformers;
using Pixeval.Extensions.Common.Downloaders;
using Pixeval.Extensions.Common.FormatProviders;

namespace Pixeval.Models.Extensions;

public sealed partial class ExtensionService
{
    public IEnumerable<IExtension> Extensions => HostModels.SelectMany(t => t.Extensions);

    public IEnumerable<IExtension> ActiveExtensions => ActiveModels.SelectMany(t => t.Extensions);

    public IEnumerable<IImageTransformerCommandExtension> ActiveImageTransformerCommands =>
        ActiveExtensions.OfType<IImageTransformerCommandExtension>();

    public IEnumerable<ITextTransformerCommandExtension> ActiveTextTransformerCommands =>
        ActiveExtensions.OfType<ITextTransformerCommandExtension>();

    public IEnumerable<IDownloaderExtension> ActiveDownloaders =>
        ActiveExtensions.OfType<IDownloaderExtension>();

    public IEnumerable<IStaticImageFormatProviderExtension> ActiveStaticImageFormatProviders =>
        ActiveExtensions.OfType<IStaticImageFormatProviderExtension>();

    public IEnumerable<IAnimatedImageFormatProviderExtension> ActiveAnimatedImageFormatProviders =>
        ActiveExtensions.OfType<IAnimatedImageFormatProviderExtension>();

    public IEnumerable<INovelFormatProviderExtension> ActiveNovelFormatProviders =>
        ActiveExtensions.OfType<INovelFormatProviderExtension>();

    public IStaticImageFormatProviderExtension? GetStaticImageFormatProvider(string extension) =>
        ActiveStaticImageFormatProviders.FirstOrDefault(t =>
            string.Equals(t.FormatExtension, extension, StringComparison.OrdinalIgnoreCase));

    public IAnimatedImageFormatProviderExtension? GetAnimatedImageFormatProvider(string extension) =>
        ActiveAnimatedImageFormatProviders.FirstOrDefault(t =>
            string.Equals(t.FormatExtension, extension, StringComparison.OrdinalIgnoreCase));

    public INovelFormatProviderExtension? GetNovelFormatProvider(string extension) =>
        ActiveNovelFormatProviders.FirstOrDefault(t =>
            string.Equals(t.FormatExtension, extension, StringComparison.OrdinalIgnoreCase));
}
