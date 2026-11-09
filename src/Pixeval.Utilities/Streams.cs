// Copyright (c) Pixeval.Utilities.
// Licensed under the GPL v3 License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IO;

namespace Pixeval.Utilities;

public static class Streams
{
    private const int BlockSizeInBytes = 1024; // 1KB

    private const int LargeBufferMultipleInBytes = 1024 * BlockSizeInBytes; // 1MB

    private const int MaxBufferSizeInBytes = 16 * 1024 * BlockSizeInBytes; // 16MB

    private const int MaximumLargeBufferPoolSizeInBytes = 24 * 1024 * BlockSizeInBytes; // 24MB

    private const int MaximumSmallBufferPoolSizeInBytes = 24 * 1024 * BlockSizeInBytes; // 24MB

    private static readonly RecyclableMemoryStreamManager _RecyclableMemoryStreamManager =
        new(new RecyclableMemoryStreamManager.Options(
            BlockSizeInBytes,
            LargeBufferMultipleInBytes,
            MaxBufferSizeInBytes,
            MaximumSmallBufferPoolSizeInBytes,
            MaximumLargeBufferPoolSizeInBytes));

    /// <inheritdoc cref="RecyclableMemoryStreamManager.GetStream()" />
    public static MemoryStream RentStream() => _RecyclableMemoryStreamManager.GetStream();

    /// <remarks><paramref name="span"/>仅用作初始化写入，不会随着Stream更改</remarks>
    /// <inheritdoc cref="RecyclableMemoryStreamManager.GetStream(ReadOnlySpan{byte})" />
    public static MemoryStream RentStream(ReadOnlySpan<byte> span) => _RecyclableMemoryStreamManager.GetStream(span);

    public static async Task<MemoryStream> CopyToMemoryStreamAsync(this Stream source, bool dispose)
    {
        var s = _RecyclableMemoryStreamManager.GetStream();
        await source.CopyToAsync(s);
        s.Position = 0;
        if (dispose)
            await source.DisposeAsync();
        return s;
    }

    public static async Task<IReadOnlyList<MemoryStream>> ReadZipAsync(Stream zipStream, bool dispose, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        List<MemoryStream> frames = [];
        try
        {
            token.ThrowIfCancellationRequested();
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, true);
            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested();
                var frame = RentStream();
                // Own each output before opening or copying its entry. A later
                // malformed entry must not orphan already extracted frames.
                frames.Add(frame);
                await using var input = await entry.OpenAsync(token);
                await input.CopyToAsync(frame, token);
                frame.Position = 0;
            }
            token.ThrowIfCancellationRequested();
            return frames;
        }
        catch
        {
            foreach (var frame in frames)
                await frame.DisposeAsync();
            throw;
        }
        finally
        {
            if (dispose)
                await zipStream.DisposeAsync();
        }
    }

    public static async Task<MemoryStream> WriteZipAsync(IReadOnlyList<string> names, IReadOnlyList<Stream> streams, bool dispose)
    {
        var zipStream = _RecyclableMemoryStreamManager.GetStream();

        var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create, true);

        for (var i = 0; i < streams.Count; ++i)
        {
            // ReSharper disable once AccessToDisposedClosure
            var entry = zipArchive.CreateEntry(names[i]);
            await using var entryStream = await entry.OpenAsync();
            await streams[i].CopyToAsync(entryStream);
            if (dispose)
                await streams[i].DisposeAsync();
        }

        await zipArchive.DisposeAsync();
        // see-also https://stackoverflow.com/questions/47707862/ziparchive-gives-unexpected-end-of-data-corrupted-error/47707973
        // 在flush前释放ZipArchive
        zipStream.Position = 0;
        await zipStream.FlushAsync();

        return zipStream;
    }
}
