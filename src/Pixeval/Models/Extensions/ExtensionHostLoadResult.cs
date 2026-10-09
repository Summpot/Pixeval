// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Models.Extensions;

public enum ExtensionHostLoadResult
{
    Loaded,
    NativeLibraryLoadFailed,
    MissingEntryPoint,
    EntryPointInvocationFailed,
    OutdatedSdk,
    InitializationFailed,
    ExtensionLoadFailed
}
