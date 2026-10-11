// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Pixeval.Views.Works;

public sealed class WorkListBox : ListBox
{
    protected override Type StyleKeyOverride => typeof(ListBox);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        var needsContainer = base.NeedsContainerOverride(item, index, out recycleKey);
        if (needsContainer)
            recycleKey = this.FindDataTemplate(item);
        return needsContainer;
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        if (ReferenceEquals(container, item))
        {
            base.PrepareContainerForItemOverride(container, item, index);
            return;
        }

        if (container is ContentControl contentControl)
        {
            if (contentControl.ContentTemplate is not ContainerRecyclingTemplate
                && this.FindDataTemplate(item) is { } template)
            {
                contentControl.ContentTemplate = new ContainerRecyclingTemplate(template);
            }

            contentControl.SetCurrentValue(ContentControl.ContentProperty, item);
        }

        base.PrepareContainerForItemOverride(container, item, index);
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        var card = container.GetLogicalDescendants().OfType<WorkItem>().FirstOrDefault()
            ?? container.GetVisualDescendants().OfType<WorkItem>().FirstOrDefault();
        card?.Recycle();
        container.DataContext = null;
    }

    private sealed class ContainerRecyclingTemplate(IDataTemplate inner) : IRecyclingDataTemplate
    {
        private WorkItem? _cachedWorkItem;

        public Control? Build(object? data) => Build(data, null);

        public Control? Build(object? data, Control? existing)
        {
            var target = existing as WorkItem ?? _cachedWorkItem;
            if (target is not null && inner.Match(data))
            {
                target.DataContext = data;
                return target;
            }

            var control = inner.Build(data);
            if (control is WorkItem workItem)
            {
                _cachedWorkItem = workItem;
                workItem.DataContext = data;
                return workItem;
            }

            if (control is not null)
                control.DataContext = data;
            return control;
        }

        public bool Match(object? data) => inner.Match(data);
    }
}
