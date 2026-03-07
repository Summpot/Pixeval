// Copyright (c) Pixeval.Caching.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Pixeval.Utilities.Memory;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Logger;

namespace Pixeval.Caching;

public sealed class CacheTable<TKey, THeader, TProtocol> : IDisposable
    where THeader : unmanaged
    where TKey : IEquatable<TKey>
    where TProtocol : ICacheProtocol<TKey, THeader>
{
    private readonly object _syncLock = new();

    private readonly string _cacheDirectory;

    private readonly int _maxCacheSizeInBytes;

    private readonly TProtocol _protocol;

    private IZoneTree<string, Memory<byte>>? _zoneTree;

    private IMaintainer? _maintainer;

    private bool _disposed;

    public int CacheLRUFactor { get; set; } = 2;

    public CacheTable(TProtocol protocol, CacheToken token)
    {
        _protocol = protocol;
        _cacheDirectory = token.CacheDirectory;
        _maxCacheSizeInBytes = token.MemoryMappedFileInitialSize;

        Directory.CreateDirectory(_cacheDirectory);
        (_zoneTree, _maintainer) = CreateZoneTree(_cacheDirectory);
    }

    public AllocatorState TryCache(TKey key, Stream stream)
    {
        return TryCache(key, stream.ReadEnd());
    }

    public AllocatorState TryCache(TKey key, Span<byte> span)
    {
        var cacheKey = _protocol.GetCacheKey(key);
        var header = _protocol.SerializeHeader(_protocol.GetHeader(key));
        var totalLength = header.Length + span.Length;

        if (_maxCacheSizeInBytes > 0 && totalLength > _maxCacheSizeInBytes)
            return AllocatorState.OutOfMemory;

        lock (_syncLock)
        {
            if (_disposed)
                return AllocatorState.AllocatorClosed;

            if (!EnsureCapacity(totalLength))
                return AllocatorState.OutOfMemory;

            var buffer = new byte[totalLength];
            header.CopyTo(buffer);
            span.CopyTo(buffer.AsSpan(header.Length));

            _zoneTree!.Upsert(cacheKey, buffer);
            _maintainer!.EvictToDisk();

            return AllocatorState.AllocationSuccess;
        }
    }

    public bool TryRemove(TKey key)
    {
        lock (_syncLock)
        {
            if (_disposed)
                return false;

            return _zoneTree!.TryDelete(_protocol.GetCacheKey(key), out _);
        }
    }

    public bool TryReadCache(TKey key, out Stream readonlyStream)
    {
        lock (_syncLock)
        {
            if (_disposed)
            {
                readonlyStream = null!;
                return false;
            }

            if (!TryGetPayload(_protocol.GetCacheKey(key), out var payload))
            {
                readonlyStream = null!;
                return false;
            }

            readonlyStream = CreateReadOnlyStream(payload);
            return true;
        }
    }

    public bool TryReadCache(TKey key, out Span<byte> span)
    {
        lock (_syncLock)
        {
            if (_disposed || !TryGetPayload(_protocol.GetCacheKey(key), out var payload))
            {
                span = Span<byte>.Empty;
                return false;
            }

            var copy = payload.ToArray();
            span = copy.AsSpan();
            return true;
        }
    }

    public void Clear()
    {
        lock (_syncLock)
        {
            if (_disposed)
                return;

            ResetStore(dropExisting: true);
        }
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            DisposeStore();
        }
    }

    private bool EnsureCapacity(int incomingBytes)
    {
        if (_maxCacheSizeInBytes <= 0)
            return true;

        var currentSize = CalculateDirectorySize(_cacheDirectory);
        if (currentSize + incomingBytes <= _maxCacheSizeInBytes)
            return true;

        ResetStore(dropExisting: true);
        return CalculateDirectorySize(_cacheDirectory) + incomingBytes <= _maxCacheSizeInBytes;
    }

    private bool TryGetPayload(string cacheKey, out ReadOnlyMemory<byte> payload)
    {
        if (!_zoneTree!.TryGet(cacheKey, out var cachedValue))
        {
            payload = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        var headerLength = Unsafe.SizeOf<THeader>();
        if (cachedValue.Length < headerLength)
        {
            payload = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        var header = _protocol.DeserializeHeader(cachedValue.Span[..headerLength]);
        var dataLength = _protocol.GetDataLength(header);
        if (dataLength < 0 || cachedValue.Length < headerLength + dataLength)
        {
            payload = ReadOnlyMemory<byte>.Empty;
            return false;
        }

        payload = cachedValue[headerLength..(headerLength + dataLength)];
        return true;
    }

    private static Stream CreateReadOnlyStream(ReadOnlyMemory<byte> payload)
    {
        if (MemoryMarshal.TryGetArray(payload, out ArraySegment<byte> segment) && segment.Array is not null)
            return new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false, publiclyVisible: true);

        return new MemoryStream(payload.ToArray(), writable: false);
    }

    private void ResetStore(bool dropExisting)
    {
        DisposeStore(dropExisting);
        Directory.CreateDirectory(_cacheDirectory);
        (_zoneTree, _maintainer) = CreateZoneTree(_cacheDirectory);
    }

    private void DisposeStore(bool dropExisting = false)
    {
        if (_maintainer is not null)
        {
            _maintainer.TryCancelBackgroundThreads();
            _maintainer.WaitForBackgroundThreads();
        }

        try
        {
            if (dropExisting && _zoneTree is not null && Directory.Exists(_cacheDirectory))
            {
                _zoneTree.Maintenance.Drop();
                _zoneTree = null;
            }
        }
        finally
        {
            _maintainer?.Dispose();
            _zoneTree?.Dispose();
            _maintainer = null;
            _zoneTree = null;
        }
    }

    private static long CalculateDirectorySize(string directory)
    {
        if (!Directory.Exists(directory))
            return 0;

        long total = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            total += new FileInfo(file).Length;
        }

        return total;
    }

    private static (IZoneTree<string, Memory<byte>> Tree, IMaintainer Maintainer) CreateZoneTree(string cacheDirectory)
    {
        var tree = new ZoneTreeFactory<string, Memory<byte>>()
            .SetComparer(new StringOrdinalComparerAscending())
            .SetDataDirectory(cacheDirectory)
            .SetWriteAheadLogDirectory(cacheDirectory)
            .SetLogLevel(LogLevel.Error)
            .OpenOrCreate();

        var maintainer = tree.CreateMaintainer();
        maintainer.ThresholdForMergeOperationStart = 1;
        maintainer.MaximumReadOnlySegmentCount = 2;
        maintainer.EnableJobForCleaningInactiveCaches = true;

        return (tree, maintainer);
    }
}
