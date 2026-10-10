// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.ViewModels.Viewers;

public interface IIllustrationAutoPlayTarget
{
    bool IsAutoPlaying { get; }

    int AutoPlayInterval { get; }

    void MoveAutoPlayNext();
}
