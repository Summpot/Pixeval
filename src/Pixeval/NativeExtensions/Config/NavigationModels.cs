// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

namespace Pixeval.Native.Config;

public partial record NavigationYamlSettings
{
    public NavigationYamlSettings() : this(null, null, null)
    {
    }
}

public partial record NavigationYamlItem
{
    public NavigationYamlItem() : this(null, null, null, null, null)
    {
    }
}
