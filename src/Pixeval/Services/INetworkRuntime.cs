// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Services;

public interface INetworkRuntime
{
    void AttachNameResolverHooks();

    void UpdateNetworkOptions();
}
