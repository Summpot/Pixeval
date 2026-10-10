// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Native.Storage;

public interface IArtworkSerializable
{
    string SerializeKey { get; }
    string Serialize();
}
