// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Pixeval.Views.Works;

public sealed class WorkListBox : ListBox
{
    protected override Type StyleKeyOverride => typeof(ListBox);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        if (item is Control)
        {
            recycleKey = null;
            return false;
        }

        recycleKey = item?.GetType() ?? DefaultRecycleKey;
        return true;
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        var container = base.CreateContainerForItemOverride(item, index, recycleKey);
        if (container is ContentControl contentControl
            && item is not Control
            && this.FindDataTemplate(item) is { } template)
        {
            contentControl.Content = template.Build(item);
        }

        return container;
    }

    protected override void ClearContainerForItemOverride(Control container) =>
        container.DataContext = null;
}
