// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.IO;
using System.Threading.Tasks;
using AnimatedControls.Avalonia;
using Avalonia.Headless;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Pixeval.Tests;

[TestClass]
[DoNotParallelize]
public sealed class AnimatedBitmapFrameTest
{
    [TestMethod]
    public async Task TwoFrameGifExposesEachFrame()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ImageViewerRenderingTest.ViewerTestApplication));
        await session.Dispatch(AssertTwoFrames, default);
    }

    private static void AssertTwoFrames()
    {
        using var stream = new MemoryStream(TwoFrameGif);
        using var bitmap = IAnimatedBitmap.Load(stream, disposeStream: false);
        bitmap.Init();

        Assert.AreEqual(2, bitmap.FrameCount);
        Assert.AreEqual(2, bitmap.Frames.Count);
        Assert.IsTrue(bitmap.Frames[0].Size.Width > 0);
        Assert.IsTrue(bitmap.Frames[0].Size.Height > 0);
        Assert.IsTrue(bitmap.Frames[1].Size.Width > 0);
        Assert.IsTrue(bitmap.Frames[1].Size.Height > 0);
        Assert.IsTrue(bitmap.Delays[0] > 0);
        Assert.IsTrue(bitmap.Delays[1] > 0);
    }

    /// <summary>
    /// 1×1 GIF89a with two frames, delays of 100ms, and a forever loop.
    /// </summary>
    private static readonly byte[] TwoFrameGif =
    [
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61,
        0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00,
        0xFF, 0x00, 0x00,
        0x00, 0x00, 0xFF,
        0x21, 0xFF, 0x0B,
        0x4E, 0x45, 0x54, 0x53, 0x43, 0x41, 0x50, 0x45, 0x32, 0x2E, 0x30,
        0x03, 0x01, 0x00, 0x00, 0x00,
        0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00,
        0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        0x02, 0x02, 0x44, 0x01, 0x00,
        0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00,
        0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
        0x02, 0x02, 0x4C, 0x01, 0x00,
        0x3B
    ];
}
