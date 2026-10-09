// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Pixeval.Extensions.Common;
using Pixeval.Extensions.Common.Settings;
using Pixeval.Utilities;

namespace Pixeval.Models.Extensions;

public sealed partial class ExtensionService
{
    private void LoadSettingsExtension(ExtensionsHostModel model, IEnumerable<IExtension> extensions)
    {
        var extensionSettingsGroup = new ExtensionSettingsGroup(model);
        var values = model.Values;
        var settingsExtensions = extensions.OfType<ISettingsExtension>();
        foreach (var settingsExtension in settingsExtensions)
        {
            var token = settingsExtension.Token;
            switch (settingsExtension)
            {
                case IStringSettingsExtension i:
                {
                    var value = values.TryGetTargetOrAddDefault(token, i.DefaultValue);
                    extensionSettingsGroup.Add(
                        new ExtensionSettingsEntry<IStringSettingsExtension, string>(i, value, t => t.DefaultValue,
                            i.OnValueChanged));
                    break;
                }
                case IIntOrEnumSettingsExtension i:
                {
                    var value = values.TryGetTargetOrAddDefault(token, i.DefaultValue);
                    switch (i)
                    {
                        case IIntSettingsExtension a:
                            extensionSettingsGroup.Add(new ExtensionIntSettingsEntry(a, value, t => t.DefaultValue,
                                a.OnValueChanged));
                            break;
                        case IEnumSettingsExtension b:
                            extensionSettingsGroup.Add(new ExtensionEnumSettingsEntry(b, value, t => t.DefaultValue,
                                i.OnValueChanged));
                            break;
                    }

                    break;
                }
                case IColorSettingsExtension i:
                {
                    var value = values.TryGetTargetOrAddDefault(token, i.DefaultValue);
                    extensionSettingsGroup.Add(
                        new ExtensionSettingsEntry<IColorSettingsExtension, uint>(i, value, t => t.DefaultValue,
                            i.OnValueChanged));
                    break;
                }
                case IStringsArraySettingsExtension i:
                {
                    var value = values.TryGetTargetOrAddDefault(token, i.DefaultValue);

                    extensionSettingsGroup.Add(
                        new ExtensionSettingsEntry<IStringsArraySettingsExtension, ObservableCollection<string>>(i,
                            [.. value], t => [.. t.DefaultValue], t => i.OnValueChanged([.. t])));
                    break;
                }
                case IDateTimeOffsetSettingsExtension i:
                {
                    var value = values.TryGetTargetOrAddDefault(token, i.DefaultValue);
                    extensionSettingsGroup.Add(
                        new ExtensionSettingsEntry<IDateTimeOffsetSettingsExtension, DateTimeOffset>(i, value,
                            t => t.DefaultValue, i.OnValueChanged));
                    break;
                }
                case IBoolSettingsExtension i:
                {
                    var value = values.TryGetTargetOrAddDefault(token, i.DefaultValue);
                    extensionSettingsGroup.Add(
                        new ExtensionSettingsEntry<IBoolSettingsExtension, bool>(i, value, t => t.DefaultValue,
                            i.OnValueChanged));
                    break;
                }
                case IDoubleSettingsExtension i:
                {
                    var value = values.TryGetTargetOrAddDefault(token, i.DefaultValue);
                    extensionSettingsGroup.Add(new ExtensionDoubleSettingsEntry(i, value, t => t.DefaultValue,
                        i.OnValueChanged));
                    break;
                }
                default:
                    break;
            }
        }

        if (extensionSettingsGroup.Count is not 0)
            _settingsGroups.Add(extensionSettingsGroup);
    }
}
