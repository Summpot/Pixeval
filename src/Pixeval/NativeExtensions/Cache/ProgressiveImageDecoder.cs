// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;

namespace Pixeval.Native.Cache;

public partial class ProgressiveImageDecoder
{
    public const long MaximumPixelCount = 4 * 1024 * 1024;
    public const int PreviewDimension = 1024;

    private Stream? _source;

    public ProgressiveImageDecoder() : this(null)
    {
    }

    public DecodedPreviewFrame? Decode(Stream source)
    {
        if (!ReferenceEquals(source, _source))
        {
            Reset();
            _source = source;
        }

        if (source.Length <= 0)
            return null;

        byte[] data;
        if (source is MemoryStream ms && ms.TryGetBuffer(out var segment))
        {
            data = segment.AsSpan(0, (int) ms.Length).ToArray();
        }
        else
        {
            var pos = source.Position;
            try
            {
                source.Position = 0;
                data = new byte[source.Length];
                source.ReadExactly(data);
            }
            finally
            {
                source.Position = pos;
            }
        }

        return Decode(data);
    }
}
