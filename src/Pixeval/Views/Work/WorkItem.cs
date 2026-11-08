// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Rendering.Composition;
using Mako.Global.Enum;
using Mako.Model;
using Pixeval.Controls;
using Pixeval.Utilities;
using Pixeval.ViewModels;
using Pixeval.Views.Entry;
using Pixeval.Views.Viewers;

namespace Pixeval.Views.Work;

public class WorkItem : EntryItem, IWorkAnimatable
{
    // Button.Click covers pointer, Enter and Space. Preserve Shift selection when
    // the list handles that activation, without interpreting child-button clicks.
    internal KeyModifiers ActivationModifiers { get; private set; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ActivationModifiers = e.KeyModifiers;
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ActivationModifiers = e.KeyModifiers;
        base.OnKeyDown(e);
    }

    protected override void OnClick()
    {
        try
        {
            base.OnClick();
        }
        finally
        {
            ActivationModifiers = KeyModifiers.None;
        }
    }

    protected void BookmarkButtonHost_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // A command disables its button while awaiting the service. Its surrounding
        // hit target must not turn that second click into a click on the artwork.
        if (sender is Border { Child: HeartButton { IsEffectivelyEnabled: false } })
            e.Handled = true;
    }

    protected void BookmarkButtonHost_OnTapped(object? sender, RoutedEventArgs e) => e.Handled = true;

    internal virtual void Recycle()
    {
        ActivationModifiers = KeyModifiers.None;
        ContextMenu?.Close();
        foreach (var descendant in this.GetLogicalDescendants())
            if (descendant is Control { ContextMenu: { } menu })
                menu.Close();

        if (ElementComposition.GetElementVisual(this) is { } visual)
        {
            visual.StopAnimation(nameof(CompositionVisual.Opacity));
            visual.Opacity = 1;
        }

        DataContext = null;
    }

    public event EventHandler<Control, IWorkViewModel>? RequestOpenUserInfoPage;

    public event EventHandler<Control, IWorkViewModel>? RequestAddToBookmark;

    public event EventHandler<Control, IWorkViewModel>? RequestBlockAuthor;

    protected void AddToBookmark_OnRightClick(object sender, ContextRequestedEventArgs e)
    {
        e.Handled = true;
        ContextMenu?.Close();
        RequestAddToBookmark?.Invoke(this, (IWorkViewModel) DataContext!);
    }

    protected void OpenUserInfoPage_OnClicked(object sender, RoutedEventArgs e)
    {
        RequestOpenUserInfoPage?.Invoke(this, (IWorkViewModel) DataContext!);
    }

    protected void BlockAuthor_OnClicked(object sender, RoutedEventArgs e)
    {
        RequestBlockAuthor?.Invoke(this, (IWorkViewModel) DataContext!);
    }

    protected void OpenSeriesPage_OnClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: IWorkViewModel { Entry: WorkBase { Series: { } series } work } }
            || TopLevel.GetTopLevel(this)?.ViewContainer is not { } viewContainer)
            return;

        viewContainer.CreateSeriesPage(
            work is Novel ? SimpleWorkType.Novel : SimpleWorkType.Illustration,
            series.Id);
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
