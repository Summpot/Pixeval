// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Services.Background;

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
