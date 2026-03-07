// Copyright (c) Pixeval.Caching.
// Licensed under the GPL-3.0 License.

using System;

namespace Pixeval.Caching;

public interface ICacheProtocol<in TKey, THeader> where THeader : unmanaged
{
	string GetCacheKey(TKey key);

    THeader GetHeader(TKey key);

    Span<byte> SerializeHeader(THeader header);

    THeader DeserializeHeader(Span<byte> span);

    int GetDataLength(THeader header);
}
