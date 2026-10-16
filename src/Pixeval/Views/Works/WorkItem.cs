// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Rendering.Composition;
using Pixeval.Pixiv;
using Pixeval.Mako;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Works;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Works;

public class WorkItem : EntryItem, IWorkAnimatable
{
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        Recycle();
        base.OnUnloaded(e);
    }

    internal virtual void Recycle()
    {
        foreach (var descendant in this.GetLogicalDescendants())
            if (descendant is Control { ContextMenu: { } menu })
                menu.Close();

        if (ElementComposition.GetElementVisual(this) is { } visual)
        {
            visual.StopAnimation(nameof(CompositionVisual.Opacity));
            visual.Opacity = 1;
        }

        ClearValue(DataContextProperty);
    }

    public void StartAnimation()
    {
        if (ElementComposition.GetElementVisual(this) is not { } visual)
            return;
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0f, 0, new LinearEasing());
        animation.InsertKeyFrame(1f, 1, new LinearEasing());
        animation.Duration = TimeSpan.FromSeconds(0.3);
        visual.StartAnimation(nameof(CompositionVisual.Opacity), animation);
    }
}
