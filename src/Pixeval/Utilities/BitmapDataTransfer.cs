// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using System.Threading;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace Pixeval.Utilities;

/// <summary>A clipboard-owned snapshot, independent of the viewer's decoded frames.</summary>
internal sealed class BitmapDataTransfer : IAsyncDataTransfer
{
    private readonly DataTransfer _data = new();
    private Bitmap? _bitmap;

    internal BitmapDataTransfer(Bitmap source)
    {
        using var stream = Streams.RentStream();
        source.Save(stream, new PngBitmapEncoderOptions());
        stream.Position = 0;
        _bitmap = new Bitmap(stream);
        _data.Add(DataTransferItem.Create(DataFormat.Bitmap, _bitmap));
    }

    public IReadOnlyList<DataFormat> Formats => _data.Formats;

    public IReadOnlyList<IAsyncDataTransferItem> Items => ((IAsyncDataTransfer) _data).Items;

    public void Dispose() => Interlocked.Exchange(ref _bitmap, null)?.Dispose();
}
