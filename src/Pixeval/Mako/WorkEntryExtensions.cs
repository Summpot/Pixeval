using System;
using Pixeval.Pixiv;
using Pixeval.Filters;

namespace Pixeval.Mako;

public static class WorkEntryExtensions
{
    extension(WorkEntry entry)
    {
        public IWorkEntry AsWorkEntry => entry switch
        {
            WorkEntry.Illust illustration => illustration.Illustration,
            WorkEntry.NovelWork novel => novel.Novel,
            _ => throw new InvalidOperationException("Unsupported work entry type")
        };

        public ArtworkMetadata ToArtworkMetadata() => entry.AsWorkEntry switch
        {
            Illustration illustration => illustration.ToArtworkMetadata(),
            Novel novel => novel.ToArtworkMetadata(),
            _ => throw new InvalidOperationException("Unsupported work entry type")
        };
    }
}
