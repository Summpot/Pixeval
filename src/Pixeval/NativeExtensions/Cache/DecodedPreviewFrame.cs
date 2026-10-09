// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pixeval.Native.Cache;

public readonly record struct PreviewPixel(byte Blue, byte Green, byte Red, byte Alpha)
{
    public static implicit operator SkiaSharp.SKColor(PreviewPixel p) => new(p.Red, p.Green, p.Blue, p.Alpha);
    public static bool operator ==(PreviewPixel p, SkiaSharp.SKColor c) =>
        p.Red == c.Red && p.Green == c.Green && p.Blue == c.Blue && p.Alpha == c.Alpha;
    public static bool operator !=(PreviewPixel p, SkiaSharp.SKColor c) => !(p == c);
    public static bool operator ==(SkiaSharp.SKColor c, PreviewPixel p) => p == c;
    public static bool operator !=(SkiaSharp.SKColor c, PreviewPixel p) => !(p == c);
}

public partial record DecodedPreviewFrame
{
    public byte[] Bytes => BgraData;

    public PreviewPixel GetPixel(long x, long y)
    {
        var offset = (int) ((y * Width + x) * 4);
        return new PreviewPixel(BgraData[offset], BgraData[offset + 1], BgraData[offset + 2], BgraData[offset + 3]);
    }

    public PreviewPixel GetPixel(int x, int y) => GetPixel((long) x, (long) y);
    public PreviewPixel GetPixel(uint x, uint y) => GetPixel((long) x, (long) y);

    public WriteableBitmap ToWriteableBitmap()
    {
        var wb = new WriteableBitmap(
            new PixelSize((int) Width, (int) Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using var frameBuffer = wb.Lock();
        Marshal.Copy(BgraData, 0, frameBuffer.Address, BgraData.Length);
        return wb;
    }
}
