using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pixeval.Utilities;

namespace Pixeval.Tests;

[TestClass]
public sealed class StreamsArchiveTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ArchiveExtractionRetainsSourceOwnership(bool seekable, bool dispose)
    {
        using var source = new ArchiveSource(CreateArchive(), seekable);
        var frames = await Streams.ReadZipAsync(source, dispose);
        try
        {
            Assert.AreEqual(dispose, source.IsDisposed);
            if (!dispose)
            {
                Assert.IsTrue(source.CanRead);
                if (seekable)
                {
                    source.Position = 0;
                    Assert.AreEqual(0x50, source.ReadByte(), "The caller can reread its retained ZIP source.");
                }
                else
                    Assert.AreEqual(-1, source.ReadByte(), "A retained nonseekable source remains readable after its bytes have been consumed.");
            }
            Assert.HasCount(2, frames);
            Assert.AreEqual(0, frames[0].Position);
            Assert.AreEqual(0, frames[1].Position);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, frames[0].ToArray());
            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, frames[1].ToArray());
        }
        finally
        {
            foreach (var frame in frames)
                await frame.DisposeAsync();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InvalidArchiveIsRejectedWithoutChangingSourceOwnership(bool dispose)
    {
        using var source = new ArchiveSource([1, 2, 3, 4], true);
        await Assert.ThrowsAsync<InvalidDataException>(() => Streams.ReadZipAsync(source, dispose));
        Assert.AreEqual(dispose, source.IsDisposed);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancellationDuringEntryReadReleasesOwnedSource(bool dispose)
    {
        using var cancellation = new CancellationTokenSource();
        using var source = new CancelDuringReadStream(CreateArchive(), cancellation);
        await Assert.ThrowsAsync<OperationCanceledException>(() => Streams.ReadZipAsync(source, dispose, cancellation.Token));
        Assert.IsTrue(source.ReadObservedToken, "The entry read receives the caller's cancellation token.");
        Assert.AreEqual(!dispose, source.CanRead);
    }

    private static byte[] CreateArchive()
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            using (var first = archive.CreateEntry("first", CompressionLevel.NoCompression).Open())
                first.Write([1, 2, 3]);
            using (var second = archive.CreateEntry("second", CompressionLevel.NoCompression).Open())
                second.Write([4, 5, 6]);
        }
        return output.ToArray();
    }

    private sealed class CancelDuringReadStream(byte[] bytes, CancellationTokenSource cancellation) : MemoryStream(bytes)
    {
        internal bool ReadObservedToken { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadObservedToken = cancellationToken == cancellation.Token;
            cancellation.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class ArchiveSource(byte[] bytes, bool seekable) : Stream
    {
        private readonly MemoryStream _source = new(bytes);
        internal bool IsDisposed { get; private set; }
        public override bool CanRead => !IsDisposed;
        public override bool CanSeek => seekable;
        public override bool CanWrite => false;
        public override long Length => seekable ? _source.Length : throw new NotSupportedException();
        public override long Position
        {
            get => seekable ? _source.Position : throw new NotSupportedException();
            set { if (seekable) _source.Position = value; else throw new NotSupportedException(); }
        }
        public override int Read(byte[] buffer, int offset, int count) => _source.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _source.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _source.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => seekable ? _source.Seek(offset, origin) : throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            if (disposing) _source.Dispose();
            base.Dispose(disposing);
        }
    }
}
