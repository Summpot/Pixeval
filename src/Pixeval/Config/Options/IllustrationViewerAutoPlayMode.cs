// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using FluentIcons.Common;
using Pixeval.Attributes;

namespace Pixeval.Config;

[LocalizationMetadata]
public enum IllustrationViewerAutoPlayMode
{
    [LocalizedResource(Symbol.ArrowRight, nameof(Strings.EnumIllustrationViewerAutoPlayModeSequential))]
    Sequential,

    [LocalizedResource(Symbol.ArrowRepeatAll, nameof(Strings.EnumIllustrationViewerAutoPlayModeLoop))]
    Loop
}
