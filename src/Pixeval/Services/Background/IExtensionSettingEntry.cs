// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System.Collections.Generic;
using AutoSettingsPage.Models;

namespace Pixeval.Services.Background;

public interface IExtensionSettingEntry : ISettingsEntry
{
    void ValueReset();

    void ValueSaving(Dictionary<string, object?> values);
}
