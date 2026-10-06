// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Pixeval.Native.Maho;

namespace Pixeval.Utilities.Network;

/// <summary>
/// 包装底层流（如 NetworkStream），并在初次 TLS 握手时利用 Rust MahoClientHelloProcessor 执行 SNI 定位与分片。
/// </summary>
public sealed class MahoTlsFragmentedStream(Stream innerStream, int splitDelayMs = 100) : Stream
{
    private readonly Stream _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
    private readonly MahoClientHelloProcessor _processor = new();
    private readonly int _splitDelayMs = Math.Max(0, splitDelayMs);
    private bool _disposed;

    public Stream InnerStream => _innerStream;

    public bool IsHandshakeCompleted => _processor.IsCompleted();

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;
    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }

    public override void Flush() => _innerStream.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _innerStream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) =>
        _innerStream.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) =>
        _innerStream.Read(buffer);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _innerStream.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _innerStream.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) =>
        _innerStream.Seek(offset, origin);

    public override void SetLength(long value) =>
        _innerStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Write(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
            return;

        if (_processor.IsCompleted())
        {
            _innerStream.Write(buffer);
            return;
        }

        WriteAsync(buffer.ToArray().AsMemory()).AsTask().GetAwaiter().GetResult();
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
            return;

        if (_processor.IsCompleted())
        {
            await _innerStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            return;
        }

        var result = _processor.ProcessChunk([.. buffer.Span]);

        if (!result.IsHandshake)
        {
            await _innerStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (result.Fragments.Count > 0)
        {
            for (var i = 0; i < result.Fragments.Count; i++)
            {
                if (i > 0 && _splitDelayMs > 0)
                {
                    await Task.Delay(_splitDelayMs, cancellationToken).ConfigureAwait(false);
                }

                var frag = result.Fragments[i];
                await _innerStream.WriteAsync(frag.AsMemory(), cancellationToken).ConfigureAwait(false);
                await _innerStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (result.RemainingBytes.Length > 0)
            {
                await _innerStream.WriteAsync(result.RemainingBytes.AsMemory(), cancellationToken).ConfigureAwait(false);
                await _innerStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;
        if (disposing)
        {
            _processor.Dispose();
            _innerStream.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _processor.Dispose();
        await _innerStream.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
