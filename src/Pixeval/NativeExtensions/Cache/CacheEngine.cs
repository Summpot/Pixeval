// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.IO;

namespace Pixeval.Native.Cache;

public partial class CacheEngine
{
    public bool TryCache(string key, Stream stream)
    {
        try
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return TryCache(key, ms.ToArray());
        }
        catch
        {
            return false;
        }
    }

    public bool TryCache(string key, byte[] data)
    {
        try
        {
            Put(key, data);
            return true;
        }
        catch (CacheError.OutOfMemory)
        {
            try
            {
                _ = PurgeCompact();
                Put(key, data);
                return true;
            }
            catch
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    public bool TryReadCache(string key, out Stream? readonlyStream)
    {
        try
        {
            var bytes = Get(key);
            if (bytes is null)
            {
                readonlyStream = null;
                return false;
            }

            readonlyStream = new MemoryStream(bytes, writable: false);
            return true;
        }
        catch
        {
            readonlyStream = null;
            return false;
        }
    }
}
