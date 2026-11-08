// Copyright (c) Pixeval.
// Licensed under the GPL-3.0 License.

using System;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace Pixeval.Controls;

internal sealed class PagePreviewSliderAutomationPeer(PagePreviewSlider owner) : ControlAutomationPeer(owner), IRangeValueProvider
{
    private int Count => owner.ItemsSource?.Count ?? 0;
    public bool IsReadOnly => !owner.IsEffectivelyEnabled || Count <= 1;
    public double Minimum => Count == 0 ? 0 : 1;
    public double Maximum => Count;
    public double Value => Count == 0 ? 0 : owner.SelectedIndex + 1;
    public double SmallChange => 1;
    public double LargeChange => 1;

    private double _lastMinimum = owner.ItemsSource?.Count > 0 ? 1 : 0;
    private double _lastMaximum = owner.ItemsSource?.Count ?? 0;
    private double _lastValue = owner.ItemsSource?.Count > 0 ? owner.SelectedIndex + 1 : 0;
    private bool _lastReadOnly = !owner.IsEffectivelyEnabled || owner.ItemsSource?.Count is not > 1;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;
    protected override string GetClassNameCore() => nameof(PagePreviewSlider);

    public void SetValue(double value)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("The page selector is read-only.");
        if (!double.IsFinite(value) || value < Minimum || value > Maximum || value != Math.Truncate(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        owner.SelectFromAutomation((int) value - 1);
    }

    internal void NotifyState()
    {
        if (_lastMinimum != Minimum)
            RaisePropertyChangedEvent(RangeValuePatternIdentifiers.MinimumProperty, _lastMinimum, Minimum);
        if (_lastMaximum != Maximum)
            RaisePropertyChangedEvent(RangeValuePatternIdentifiers.MaximumProperty, _lastMaximum, Maximum);
        if (_lastValue != Value)
            RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, _lastValue, Value);
        if (_lastReadOnly != IsReadOnly)
            RaisePropertyChangedEvent(RangeValuePatternIdentifiers.IsReadOnlyProperty, _lastReadOnly, IsReadOnly);
        _lastMinimum = Minimum;
        _lastMaximum = Maximum;
        _lastValue = Value;
        _lastReadOnly = IsReadOnly;
    }
}
