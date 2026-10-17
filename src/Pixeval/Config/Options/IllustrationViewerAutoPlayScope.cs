// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using FluentIcons.Common;
using Pixeval.Attributes;

namespace Pixeval.Config;

[LocalizationMetadata]
public enum IllustrationViewerAutoPlayScope
{
    [LocalizedResource(Symbol.Image, nameof(Strings.EnumIllustrationViewerAutoPlayScopeCurrentWork))]
    CurrentWork,

    [LocalizedResource(Symbol.ImageMultiple, nameof(Strings.EnumIllustrationViewerAutoPlayScopeAllWorks))]
    AllWorks
}
