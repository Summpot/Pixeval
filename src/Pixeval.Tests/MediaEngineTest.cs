// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Native.Media;

namespace Pixeval.Tests;

[TestClass]
public class MediaEngineTest
{
    private string _testDir = null!;

    [TestInitialize]
    public void Setup()
    {
        var targetTmp = Path.Combine(AppContext.BaseDirectory, "test_tmp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(targetTmp);
        _testDir = targetTmp;
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch
            {
                // ignore cleanup errors on open handles
            }
        }
    }

    private static byte[] CreateSampleJpeg()
    {
        using var bitmap = new SkiaSharp.SKBitmap(16, 16);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.Red);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    [TestMethod]
    public void MediaPingShouldReturnPong()
    {
        var ping = PixevalMediaMethods.MediaPing();
        Assert.AreEqual("media_pong", ping);
    }

    [TestMethod]
    public void TranscodeBytesShouldConvertJpegToPngAndWebp()
    {
        var jpeg = CreateSampleJpeg();
        var png = MediaEngine.Shared.TranscodeBytes(jpeg, ImageCodecFormat.Png, null);
        Assert.IsNotNull(png);
        Assert.IsTrue(png.Length > 0);
        Assert.AreEqual((byte)0x89, png[0]); // PNG magic header

        var webp = MediaEngine.Shared.TranscodeBytes(jpeg, ImageCodecFormat.Webp, null);
        Assert.IsNotNull(webp);
        Assert.IsTrue(webp.Length > 0);
        Assert.AreEqual((byte)'R', webp[0]); // RIFF magic header
    }

    [TestMethod]
    public async Task PackMangaAsyncShouldCreateValidCbz()
    {
        var jpeg = CreateSampleJpeg();
        var page0 = Path.Combine(_testDir, "p0.jpg");
        var page1 = Path.Combine(_testDir, "p1.jpg");
        await File.WriteAllBytesAsync(page0, jpeg);
        await File.WriteAllBytesAsync(page1, jpeg);

        var cbzPath = Path.Combine(_testDir, "manga.cbz");
        await MediaEngine.Shared.PackMangaAsync([page0, page1], cbzPath, MangaArchiveFormat.Cbz);

        Assert.IsTrue(File.Exists(cbzPath));
        using var archive = ZipFile.OpenRead(cbzPath);
        Assert.AreEqual(2, archive.Entries.Count);
        Assert.AreEqual("0000.jpg", archive.Entries[0].FullName);
        Assert.AreEqual("0001.jpg", archive.Entries[1].FullName);
    }

    [TestMethod]
    public async Task SynthesizeUgoiraAsyncShouldCreateGifAndOriginal()
    {
        var jpeg = CreateSampleJpeg();
        var zipPath = Path.Combine(_testDir, "ugoira.zip");

        // Create ugoira zip archive
        using (var zipStream = new FileStream(zipPath, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            var e0 = archive.CreateEntry("000000.jpg");
            using (var s = e0.Open())
                s.Write(jpeg);

            var e1 = archive.CreateEntry("000001.jpg");
            using (var s = e1.Open())
                s.Write(jpeg);
        }

        // Synthesize GIF
        var gifPath = Path.Combine(_testDir, "anim.gif");
        await MediaEngine.Shared.SynthesizeUgoiraAsync(zipPath, gifPath, UgoiraFormat.Gif, [100, 100]);
        Assert.IsTrue(File.Exists(gifPath));
        var gifBytes = await File.ReadAllBytesAsync(gifPath);
        Assert.AreEqual((byte)'G', gifBytes[0]);
        Assert.AreEqual((byte)'I', gifBytes[1]);
        Assert.AreEqual((byte)'F', gifBytes[2]);

        // Synthesize Original (loose extraction)
        var looseDir = Path.Combine(_testDir, "loose");
        await MediaEngine.Shared.SynthesizeUgoiraAsync(zipPath, looseDir, UgoiraFormat.Original, [100, 120]);
        Assert.IsTrue(Directory.Exists(looseDir));
        Assert.IsTrue(File.Exists(Path.Combine(looseDir, "000000.jpg")));
        Assert.IsTrue(File.Exists(Path.Combine(looseDir, "000001.jpg")));
        Assert.IsTrue(File.Exists(Path.Combine(looseDir, "intervals in milliseconds.csv")));
        var csv = await File.ReadAllTextAsync(Path.Combine(looseDir, "intervals in milliseconds.csv"));
        Assert.AreEqual("100,120", csv);
    }
}
