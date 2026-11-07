// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.VisualTree;

namespace Pixeval.Controls;

public sealed class Skeleton : TemplatedControl
{
    private const double ShimmerWidthRatio = 0.75;
    private const double MinShimmerWidth = 120;

    private Border? _shimmer;
    private CompositionVisual? _animatedVisual;
    private readonly List<Visual> _visibilityAncestors = [];

    public static readonly StyledProperty<TimeSpan> ShimmerPeriodProperty =
        AvaloniaProperty.Register<Skeleton, TimeSpan>(nameof(ShimmerPeriod), TimeSpan.FromSeconds(1.2), coerce: (_, value) => value > TimeSpan.Zero ? value : TimeSpan.FromSeconds(1.2));

    public TimeSpan ShimmerPeriod
    {
        get => GetValue(ShimmerPeriodProperty);
        set => SetValue(ShimmerPeriodProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _visibilityAncestors.Add(ancestor);
            ancestor.PropertyChanged += OnAncestorPropertyChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        foreach (var ancestor in _visibilityAncestors)
            ancestor.PropertyChanged -= OnAncestorPropertyChanged;
        _visibilityAncestors.Clear();
        StopShimmerAnimation();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnAncestorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty)
            UpdateShimmerAnimation();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        StopShimmerAnimation();
        if (_shimmer is not null)
            _shimmer.Loaded -= OnShimmerLoaded;
        base.OnApplyTemplate(e);

        _shimmer = e.NameScope.Find<Border>("PART_Shimmer");
        if (_shimmer is not null)
            _shimmer.Loaded += OnShimmerLoaded;
        UpdateShimmerAnimation();
    }

    private void OnShimmerLoaded(object? sender, RoutedEventArgs e) => UpdateShimmerAnimation();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UpdateShimmerAnimation();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        StopShimmerAnimation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == BoundsProperty || e.Property == ShimmerPeriodProperty || e.Property == IsVisibleProperty)
            UpdateShimmerAnimation();
    }

    private void UpdateShimmerAnimation()
    {
        if (!IsLoaded || !IsEffectivelyVisible || _shimmer is null || Bounds is not { Width: > 0, Height: > 0 })
        {
            StopShimmerAnimation();
            return;
        }

        var shimmerWidth = double.Max(MinShimmerWidth, Bounds.Width * ShimmerWidthRatio);
        _shimmer.Width = shimmerWidth;
        _shimmer.Height = double.Max(Bounds.Height, 1);

        if (ElementComposition.GetElementVisual(_shimmer) is not { } visual)
            return;

        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        animation.InsertKeyFrame(0f, new((float) -shimmerWidth, 0, 0), new LinearEasing());
        animation.InsertKeyFrame(1f, new((float) (Bounds.Width + shimmerWidth), 0, 0), new LinearEasing());
        animation.Duration = ShimmerPeriod;
        animation.IterationBehavior = AnimationIterationBehavior.Forever;

        visual.StartAnimation(nameof(CompositionVisual.Offset), animation);
        _animatedVisual = visual;
    }

    private void StopShimmerAnimation()
    {
        if (_animatedVisual is not { } visual)
            return;
        _animatedVisual = null;

        var shimmerWidth = double.Max(MinShimmerWidth, Bounds.Width * ShimmerWidthRatio);
        var parked = new Vector3D(-shimmerWidth, 0, 0);
        var animation = visual.Compositor.CreateVector3DKeyFrameAnimation();
        animation.InsertKeyFrame(0f, parked);
        animation.InsertKeyFrame(1f, parked);
        animation.Duration = TimeSpan.FromMilliseconds(1);
        animation.IterationBehavior = AnimationIterationBehavior.Count;
        animation.IterationCount = 1;

        // Replace the shimmer through a composition batch; StopAnimation bypasses that path.
        // The finite animation parks the shimmer offscreen and then leaves the animation clock.
        visual.StartAnimation(nameof(CompositionVisual.Offset), animation);
    }
}
